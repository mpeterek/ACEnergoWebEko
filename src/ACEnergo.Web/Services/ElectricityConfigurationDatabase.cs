using System.Data.OleDb;
using System.Runtime.Versioning;

namespace ACEnergo.Web.Services;

// Společné připojení pouze ke konfigurační MDB, nikdy k souboru zadanému z URL.
public sealed class ElectricityConfigurationDatabase(DatabaseCatalog catalog, IConfiguration configuration)
{
    internal static readonly object WriteGate = new();
    public IDisposable AcquireWriteLock() => TariffWriteLock.Acquire(catalog.Resolve("ACEnergoWebEko.mdb"));

    [SupportedOSPlatform("windows")]
    public OleDbConnection Open(bool write)
    {
        var path = catalog.Resolve("ACEnergoWebEko.mdb");
        using (File.Open(path, FileMode.Open, write ? FileAccess.ReadWrite : FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)) { }
        var builder = new OleDbConnectionStringBuilder
        {
            Provider = configuration["Access:Provider"] ?? "Microsoft.ACE.OLEDB.16.0",
            DataSource = path,
            OleDbServices = -2
        };
        builder["Mode"] = write ? "ReadWrite" : "Read";
        var connection = new OleDbConnection(builder.ConnectionString);
        try { connection.Open(); return connection; }
        catch { connection.Dispose(); throw; }
    }

    [SupportedOSPlatform("windows")]
    public static string MeterName(OleDbConnection connection, int meterId, OleDbTransaction? transaction = null)
    {
        using var command = new OleDbCommand("SELECT [strNazev] FROM [tblKonfigHlavniElektrina] WHERE [numID] = ?", connection, transaction);
        command.Parameters.Add("meter", OleDbType.Integer).Value = meterId;
        var result = command.ExecuteScalar();
        if (result is null) throw new KeyNotFoundException("Hlavní měření neexistuje.");
        return Convert.ToString(result) ?? "";
    }
}
