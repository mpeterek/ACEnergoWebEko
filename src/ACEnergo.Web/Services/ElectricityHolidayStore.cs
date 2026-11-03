using System.Data;
using System.Data.OleDb;
using System.Runtime.Versioning;
using ACEnergo.Web.Domain;

namespace ACEnergo.Web.Services;

public interface IElectricityHolidayStore
{
    HolidaySnapshot Load(int meterId);
    bool Add(int meterId, DateOnly date, string expectedVersion);
    int Remove(int meterId, IReadOnlyList<DateOnly> dates, string expectedVersion);
}

public sealed class ElectricityHolidayStore(DatabaseCatalog catalog, IConfiguration configuration) : IElectricityHolidayStore
{
    private readonly ElectricityConfigurationDatabase database = new(catalog, configuration);

    public HolidaySnapshot Load(int meterId)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Připojení MDB vyžaduje Windows a Access Database Engine.");
        return LoadWindows(meterId);
    }

    public bool Add(int meterId, DateOnly date, string expectedVersion)
    {
        ElectricityHolidays.ValidateDates([date]);
        ElectricityHolidays.ValidateVersion(expectedVersion);
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Připojení MDB vyžaduje Windows a Access Database Engine.");
        lock (ElectricityConfigurationDatabase.WriteGate) return AddWindows(meterId, date, expectedVersion);
    }

    public int Remove(int meterId, IReadOnlyList<DateOnly> dates, string expectedVersion)
    {
        ElectricityHolidays.ValidateDates(dates);
        ElectricityHolidays.ValidateVersion(expectedVersion);
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Připojení MDB vyžaduje Windows a Access Database Engine.");
        lock (ElectricityConfigurationDatabase.WriteGate) return RemoveWindows(meterId, dates, expectedVersion);
    }

    [SupportedOSPlatform("windows")]
    private static List<HolidayRecord> Read(OleDbConnection connection, int meterId, OleDbTransaction? transaction = null)
    {
        using var command = new OleDbCommand("SELECT [dtDatum], [strTarif] FROM [tblSvatky] WHERE [numIDHlavni] = ? ORDER BY [dtDatum]", connection, transaction);
        command.Parameters.Add("meter", OleDbType.Integer).Value = meterId;
        using var reader = command.ExecuteReader();
        var rows = new List<HolidayRecord>();
        while (reader is not null && reader.Read())
        {
            if (reader["dtDatum"] is DBNull) throw new InvalidDataException("Svátek obsahuje prázdné datum.");
            rows.Add(new(Convert.ToDateTime(reader["dtDatum"]), Convert.ToString(reader["strTarif"]) ?? ""));
        }
        _ = ElectricityHolidays.Dates(rows);
        return rows;
    }

    [SupportedOSPlatform("windows")]
    private HolidaySnapshot LoadWindows(int meterId)
    {
        using var connection = database.Open(false);
        var name = ElectricityConfigurationDatabase.MeterName(connection, meterId);
        var rows = Read(connection, meterId).ToArray();
        return new(meterId, name, rows, ElectricityHolidays.Version(rows));
    }

    [SupportedOSPlatform("windows")]
    private T ChangeWindows<T>(int meterId, string expectedVersion, Func<OleDbConnection, OleDbTransaction, List<HolidayRecord>, T> change)
    {
        using var writeLock = database.AcquireWriteLock();
        using var connection = database.Open(true);
        using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);
        _ = ElectricityConfigurationDatabase.MeterName(connection, meterId, transaction);
        var rows = Read(connection, meterId, transaction);
        if (!string.Equals(ElectricityHolidays.Version(rows), expectedVersion, StringComparison.Ordinal)) throw new HolidayConflictException();
        var result = change(connection, transaction, rows);
        transaction.Commit();
        return result;
    }

    [SupportedOSPlatform("windows")]
    private bool AddWindows(int meterId, DateOnly date, string expectedVersion) => ChangeWindows(meterId, expectedVersion, (connection, transaction, rows) =>
    {
        var timestamp = date.ToDateTime(TimeOnly.MinValue);
        if (rows.Any(row => row.At == timestamp)) return false;
        using var command = new OleDbCommand("INSERT INTO [tblSvatky] ([numIDHlavni], [dtDatum], [strTarif]) VALUES (?, ?, ?)", connection, transaction);
        command.Parameters.Add("meter", OleDbType.Integer).Value = meterId;
        command.Parameters.Add("date", OleDbType.Date).Value = timestamp;
        command.Parameters.Add("marker", OleDbType.VarWChar, 1).Value = ElectricityHolidays.NewMarker;
        if (command.ExecuteNonQuery() != 1) throw new InvalidDataException("Vložení svátku nevrátilo očekávaný počet záznamů.");
        return true;
    });

    [SupportedOSPlatform("windows")]
    private int RemoveWindows(int meterId, IReadOnlyList<DateOnly> dates, string expectedVersion) => ChangeWindows(meterId, expectedVersion, (connection, transaction, _) =>
    {
        var removed = 0;
        foreach (var date in dates)
        {
            using var command = new OleDbCommand("DELETE FROM [tblSvatky] WHERE [numIDHlavni] = ? AND [dtDatum] = ?", connection, transaction);
            command.Parameters.Add("meter", OleDbType.Integer).Value = meterId;
            command.Parameters.Add("date", OleDbType.Date).Value = date.ToDateTime(TimeOnly.MinValue);
            var affected = command.ExecuteNonQuery();
            if (affected < 0) throw new InvalidDataException("Odebrání svátku nevrátilo platný počet záznamů.");
            removed += affected;
        }
        return removed;
    });
}
