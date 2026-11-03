using System.Data;
using System.Data.OleDb;
using System.Runtime.Versioning;
using System.Security.Claims;
namespace ACEnergo.Web.Services;
public sealed class AccessStore(DatabaseCatalog catalog, IConfiguration config)
{
    public static readonly string[] Rights = ["ElekView","ElekConfig","ElekOdbDiagram","AnalogView","AnalogConfig","VodaView","VodaConfig","PlynView","PlynConfig","TeploView","TeploConfig","OstatniView","OstatniConfig"];
    public ClaimsPrincipal? Authenticate(string username, string password)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Připojení MDB vyžaduje Windows a Access Database Engine.");
        return AuthenticateWindows(username, password);
    }
    public sealed record ElectricityMeter(int Id, string Name);
    public IReadOnlyList<ElectricityMeter> ElectricityMeters()
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Čtení MDB vyžaduje Windows.");
        return ElectricityMetersWindows();
    }
    [SupportedOSPlatform("windows")]
    private IReadOnlyList<ElectricityMeter> ElectricityMetersWindows()
    {
        var path = catalog.Resolve("ACEnergoWebEko.mdb");
        if (!File.Exists(path)) throw new FileNotFoundException("Konfigurační MDB není dostupná.");
        var cs = new OleDbConnectionStringBuilder { Provider = config["Access:Provider"] ?? "Microsoft.ACE.OLEDB.16.0", DataSource = path };
        cs["Mode"] = "Read";
        using var connection = new OleDbConnection(cs.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT [strNazev], [numID] FROM [tblKonfigHlavniElektrina] ORDER BY [numID] ASC";
        using var reader = command.ExecuteReader();
        var meters = new List<ElectricityMeter>();
        while (reader is not null && reader.Read()) meters.Add(new(Convert.ToInt32(reader["numID"]), Convert.ToString(reader["strNazev"]) ?? ""));
        return meters;
    }
    [SupportedOSPlatform("windows")]
    private ClaimsPrincipal? AuthenticateWindows(string username, string password)
    {
        var path = catalog.Resolve("ACWebEkoLogin.mdb");
        if (!File.Exists(path)) throw new FileNotFoundException("Přihlašovací MDB není dostupná.");
        var cs = new OleDbConnectionStringBuilder { Provider = config["Access:Provider"] ?? "Microsoft.ACE.OLEDB.16.0", DataSource = path };
        cs["Mode"] = "Read";
        using var connection = new OleDbConnection(cs.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM [tblLog] WHERE [strLogon] = ? AND [strPassword] = ?";
        command.Parameters.Add("user", OleDbType.VarWChar, 255).Value = username.Trim();
        command.Parameters.Add("password", OleDbType.VarWChar, 255).Value = password.Trim();
        using var reader = command.ExecuteReader();
        if (reader is null || !reader.Read()) return null;
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, Convert.ToString(reader["ID"])!), new(ClaimTypes.Name, Convert.ToString(reader["strLogon"])!) };
        bool Flag(string field) => reader[field] != DBNull.Value && Convert.ToBoolean(reader[field]);
        if (Flag("bAdministrator")) claims.Add(new(ClaimTypes.Role, "Administrator"));
        foreach (var right in Rights) if (Flag("b" + right)) claims.Add(new("permission", right));
        return new(new ClaimsIdentity(claims, "Cookies"));
    }
}
