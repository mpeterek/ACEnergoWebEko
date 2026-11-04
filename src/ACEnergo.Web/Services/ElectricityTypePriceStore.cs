using System.Data;
using System.Data.OleDb;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text.Json;
using ACEnergo.Web.Domain;
namespace ACEnergo.Web.Services;

public sealed record TypePriceSnapshot(int EnergyId, string Type, string? WorkUnit, string? DayShift, string? MonthShift, string? YearShift, TypePriceRecord[] History, string Version);
public interface IElectricityTypePriceStore
{
    TypePriceSnapshot Load(string kind, int id);
    void Add(string kind, int id, DateOnly month, string[] values, bool? tariff, string version);
}
public sealed class ElectricityTypePriceStore(DatabaseCatalog catalog, IConfiguration config) : IElectricityTypePriceStore
{
    private readonly ElectricityConfigurationDatabase db = new(catalog, config);
    public TypePriceSnapshot Load(string kind, int id)
    {
        ElectricityGroupCatalog.Definition(kind);
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        using var connection = db.Open(false);
        return Read(connection, kind, id);
    }
    public void Add(string kind, int id, DateOnly month, string[] values, bool? tariff, string version)
    {
        ElectricityGroupCatalog.Definition(kind);
        ElectricityTypePrices.ValidateMonth(month);
        var parsed = ElectricityTypePrices.Parse(values);
        ElectricityHolidays.ValidateVersion(version);
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        lock (ElectricityConfigurationDatabase.WriteGate) Write(kind, id, month, parsed, tariff, version);
    }
    [SupportedOSPlatform("windows")]
    private static TypePriceSnapshot Read(OleDbConnection connection, string kind, int id, OleDbTransaction? tx = null)
    {
        var table = ElectricityGroupCatalog.Definition(kind).Table;
        using var command = new OleDbCommand($"SELECT e.[numKlic],e.[strTypEnergie],p.[strRozmerP],e.[numHodinaDen],e.[numHodinaMesic],e.[numHodinaRok] FROM [{table}] AS p INNER JOIN [tblKonfigEnergie] AS e ON p.[numIDEnergie]=e.[numKlic] WHERE p.[numCislo]=? AND e.[strNazevEnergie]='Elektřina'", connection, tx);
        command.Parameters.Add("id", OleDbType.Integer).Value = id;
        object[] metadata;
        using (var reader = command.ExecuteReader())
        {
            if (reader is null || !reader.Read()) throw new KeyNotFoundException();
            metadata = Enumerable.Range(0, reader.FieldCount).Select(reader.GetValue).ToArray();
            if (reader.Read()) throw new InvalidDataException("Duplicitní definice typu vstupu.");
        }
        var energyId = Convert.ToInt32(metadata[0]);
        var ordered = ReadHistory(connection, energyId, tx);
        var version = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { metadata, ordered })));
        string? Text(int i) => metadata[i] is DBNull ? null : Convert.ToString(metadata[i]);
        return new(energyId, Text(1) ?? "", Text(2), Text(3), Text(4), Text(5), ordered, version);
    }
    [SupportedOSPlatform("windows")]
    internal static TypePriceRecord[] ReadHistory(OleDbConnection connection, int energyId, OleDbTransaction? tx = null)
    {
        using var prices = new OleDbCommand("SELECT [dtDatum],[numCena],[numCena1],[numCena2],[numCena3],[numCena4],[numCena5],[bTarif] FROM [tblCenyPodruzne] WHERE [numKlic]=?", connection, tx);
        prices.Parameters.Add("energy", OleDbType.Integer).Value = energyId;
        var history = new List<TypePriceRecord>();
        using (var reader = prices.ExecuteReader())
        {
            while (reader is not null && reader.Read())
            {
                var values = Enumerable.Range(1, 6).Select(i => reader.IsDBNull(i) ? (decimal?)null : Convert.ToDecimal(reader[i])).ToArray();
                history.Add(new(reader.IsDBNull(0) ? null : Convert.ToDateTime(reader[0]), values, reader.IsDBNull(7) ? null : Convert.ToBoolean(reader[7])));
            }
        }
        // Pořadí řádků ACE není stabilní, včetně více záznamů stejného data.
        var ordered = history.OrderByDescending(row => row.EffectiveAt).ThenBy(row => JsonSerializer.Serialize(row), StringComparer.Ordinal).ToArray();
        return ordered;
    }
    [SupportedOSPlatform("windows")]
    private void Write(string kind, int id, DateOnly month, decimal?[] values, bool? tariff, string version)
    {
        using var held = db.AcquireWriteLock();
        using var connection = db.Open(true);
        using var tx = connection.BeginTransaction(IsolationLevel.ReadCommitted);
        var current = Read(connection, kind, id, tx);
        if (current.Version != version) throw new GroupConfigurationConflictException();
        ElectricityTypePrices.ValidateNewMonth(current.History, month);
        using (var unique = new OleDbCommand("SELECT COUNT(*) FROM [tblKonfigEnergie] WHERE [strNazevEnergie]='Elektřina' AND [strTypEnergie]=?", connection, tx))
        {
            unique.Parameters.Add("type", OleDbType.VarWChar, 2).Value = current.Type;
            if (Convert.ToInt32(unique.ExecuteScalar()) != 1) throw new InvalidDataException("Typ elektřiny není jednoznačný; ceník nebyl zapsán.");
        }
        // Stejně jako ZapisCenTypu v ASP přidáváme historii; starší ceny zůstávají zachovány.
        using var command = new OleDbCommand("INSERT INTO [tblCenyPodruzne] ([numKlic],[dtDatum],[numCena],[numCena1],[numCena2],[numCena3],[numCena4],[numCena5],[bTarif]) VALUES (?,?,?,?,?,?,?,?,?)", connection, tx);
        command.Parameters.Add("energy", OleDbType.Integer).Value = current.EnergyId;
        command.Parameters.Add("date", OleDbType.Date).Value = month.ToDateTime(TimeOnly.MinValue);
        for (var i = 0; i < 6; i++)
        {
            var parameter = command.Parameters.Add("price" + i, OleDbType.Decimal);
            parameter.Precision = 18; parameter.Scale = 5; parameter.Value = (object?)values[i] ?? DBNull.Value;
        }
        command.Parameters.Add("tariff", OleDbType.Boolean).Value = (object?)tariff ?? DBNull.Value;
        if (command.ExecuteNonQuery() != 1) throw new InvalidDataException("Změněn neočekávaný počet ceníků.");
        tx.Commit();
    }
}
