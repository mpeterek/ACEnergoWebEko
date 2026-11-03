using System.Data;
using System.Data.OleDb;
using System.Runtime.Versioning;
using ACEnergo.Web.Domain;

namespace ACEnergo.Web.Services;

public interface IElectricityTariffStore
{
    TariffSnapshot Load(int meterId, DateOnly month);
    void Save(int meterId, DateOnly month, IReadOnlyList<int> cells, string expectedVersion);
}

public sealed class ElectricityTariffStore(DatabaseCatalog catalog, IConfiguration configuration) : IElectricityTariffStore
{
    // Zápisy z této instance webu se serializují; mezi procesy rozhoduje transakce Accessu.
    private static readonly object WriteGate = new();
    private static readonly string[] HourColumns = Enumerable.Range(1, 24).Select(hour => $"numHodina{hour:D2}").ToArray();
    private static readonly string SelectColumns = "[dtDatumPlatnost], [numIndexDne], [strNazevDne], " + string.Join(", ", HourColumns.Select(name => $"[{name}]"));

    public TariffSnapshot Load(int meterId, DateOnly month)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Připojení MDB vyžaduje Windows a Access Database Engine.");
        return LoadWindows(meterId, ElectricityTariffs.MonthStart(month));
    }

    public void Save(int meterId, DateOnly month, IReadOnlyList<int> cells, string expectedVersion)
    {
        ElectricityTariffs.Validate(cells);
        if (string.IsNullOrEmpty(expectedVersion) || expectedVersion.Length != 64 || !expectedVersion.All(Uri.IsHexDigit)) throw new ArgumentException("Chybí platná verze načteného tarifu.");
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Připojení MDB vyžaduje Windows a Access Database Engine.");
        lock (WriteGate) SaveWindows(meterId, ElectricityTariffs.MonthStart(month), cells, expectedVersion);
    }

    [SupportedOSPlatform("windows")]
    private OleDbConnection Open(bool write)
    {
        var path = catalog.Resolve("ACEnergoWebEko.mdb");
        using (File.Open(path, FileMode.Open, write ? FileAccess.ReadWrite : FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)) { }
        var builder = new OleDbConnectionStringBuilder
        {
            Provider = configuration["Access:Provider"] ?? "Microsoft.ACE.OLEDB.16.0",
            DataSource = path,
            OleDbServices = -2 // Bez resource poolingu: po Dispose se uvolní i souborové prostředky.
        };
        builder["Mode"] = write ? "ReadWrite" : "Read";
        var connection = new OleDbConnection(builder.ConnectionString);
        try { connection.Open(); return connection; }
        catch { connection.Dispose(); throw; }
    }

    [SupportedOSPlatform("windows")]
    private static string MeterName(OleDbConnection connection, int meterId, OleDbTransaction? transaction = null)
    {
        using var command = new OleDbCommand("SELECT [strNazev] FROM [tblKonfigHlavniElektrina] WHERE [numID] = ?", connection, transaction);
        command.Parameters.Add("meter", OleDbType.Integer).Value = meterId;
        var result = command.ExecuteScalar();
        if (result is null) throw new KeyNotFoundException("Hlavní měření neexistuje.");
        return Convert.ToString(result) ?? "";
    }

    [SupportedOSPlatform("windows")]
    private static List<TariffRecord> Read(OleDbConnection connection, int meterId, DateOnly month, OleDbTransaction? transaction = null)
    {
        using var command = new OleDbCommand($"SELECT {SelectColumns} FROM [tblTarifyElektrina] WHERE [numIDHlavni] = ? AND [dtDatumPlatnost] >= ? AND [dtDatumPlatnost] < ? ORDER BY [dtDatumPlatnost], [numIndexDne]", connection, transaction);
        command.Parameters.Add("meter", OleDbType.Integer).Value = meterId;
        command.Parameters.Add("from", OleDbType.Date).Value = month.ToDateTime(TimeOnly.MinValue);
        command.Parameters.Add("until", OleDbType.Date).Value = new DateTime(month.Year + 1, 1, 1);
        using var reader = command.ExecuteReader();
        var rows = new List<TariffRecord>();
        while (reader is not null && reader.Read())
        {
            if (reader["dtDatumPlatnost"] is DBNull || reader["numIndexDne"] is DBNull || HourColumns.Any(name => reader[name] is DBNull))
                throw new InvalidDataException("Tarif obsahuje prázdné datum, den nebo hodinovou hodnotu.");
            rows.Add(new(Convert.ToDateTime(reader["dtDatumPlatnost"]), Convert.ToInt32(reader["numIndexDne"]), Convert.ToString(reader["strNazevDne"]) ?? "",
                HourColumns.Select(name => Convert.ToInt32(reader[name])).ToArray()));
        }
        return rows;
    }

    [SupportedOSPlatform("windows")]
    private TariffSnapshot LoadWindows(int meterId, DateOnly month)
    {
        using var connection = Open(false);
        var name = MeterName(connection, meterId);
        var rows = Read(connection, meterId, month);
        var grid = ElectricityTariffs.ReadMonth(rows, month);
        return new(meterId, name, month, grid.Cells, grid.IsDefault, ElectricityTariffs.Version(rows));
    }

    [SupportedOSPlatform("windows")]
    private void SaveWindows(int meterId, DateOnly month, IReadOnlyList<int> cells, string expectedVersion)
    {
        using var connection = Open(true);
        using var transaction = connection.BeginTransaction(IsolationLevel.Serializable);
        // Dispose necommitnuté transakce provede rollback i při výjimce.
        _ = MeterName(connection, meterId, transaction);
        var rows = Read(connection, meterId, month, transaction);
        if (!string.Equals(ElectricityTariffs.Version(rows), expectedVersion, StringComparison.Ordinal)) throw new TariffConflictException();
        foreach (var effective in ElectricityTariffs.WriteMonths(month))
        {
            for (var day = 1; day <= 7; day++)
            {
                using var update = new OleDbCommand("UPDATE [tblTarifyElektrina] SET [strNazevDne] = ?, " + string.Join(", ", HourColumns.Select(name => $"[{name}] = ?")) + " WHERE [numIDHlavni] = ? AND [dtDatumPlatnost] = ? AND [numIndexDne] = ?", connection, transaction);
                AddDayValues(update, cells, day);
                AddKey(update, meterId, effective, day);
                var affected = update.ExecuteNonQuery();
                if (affected > 1) throw new InvalidDataException("Databáze obsahuje duplicitní tarif dne. Zápis byl zrušen.");
                if (affected == 0)
                {
                    var columns = new[] { "strNazevDne" }.Concat(HourColumns).Concat(["numIDHlavni", "dtDatumPlatnost", "numIndexDne"]).ToArray();
                    using var insert = new OleDbCommand($"INSERT INTO [tblTarifyElektrina] ({string.Join(", ", columns.Select(name => $"[{name}]"))}) VALUES ({string.Join(", ", columns.Select(_ => "?"))})", connection, transaction);
                    AddDayValues(insert, cells, day);
                    AddKey(insert, meterId, effective, day);
                    insert.ExecuteNonQuery();
                }
            }
        }
        transaction.Commit();
    }

    [SupportedOSPlatform("windows")]
    private static void AddDayValues(OleDbCommand command, IReadOnlyList<int> cells, int day)
    {
        command.Parameters.Add("name", OleDbType.VarWChar, 255).Value = ElectricityTariffs.Days[day - 1];
        for (var hour = 0; hour < 24; hour++) command.Parameters.Add($"hour{hour}", OleDbType.Integer).Value = cells[(day - 1) * 24 + hour];
    }

    [SupportedOSPlatform("windows")]
    private static void AddKey(OleDbCommand command, int meterId, DateOnly month, int day)
    {
        command.Parameters.Add("meter", OleDbType.Integer).Value = meterId;
        command.Parameters.Add("date", OleDbType.Date).Value = month.ToDateTime(TimeOnly.MinValue);
        command.Parameters.Add("day", OleDbType.Integer).Value = day;
    }
}
