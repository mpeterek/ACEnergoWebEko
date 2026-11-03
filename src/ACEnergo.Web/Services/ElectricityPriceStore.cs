using System.Data.OleDb;
using System.Globalization;
using System.Runtime.Versioning;
namespace ACEnergo.Web.Services;
public sealed record PriceField(string Column, string Label);
public sealed record PriceValue(string Label, string Value, string Unit = "");
public sealed record PriceSnapshot(string MeterName, DateOnly Month, bool IsDefault, PriceValue[] Values);
public interface IElectricityPriceStore { PriceSnapshot Load(int meterId, DateOnly month); }
public sealed class ElectricityPriceStore(DatabaseCatalog catalog, IConfiguration config) : IElectricityPriceStore
{
    public static readonly PriceField[] Fields = [
        new("numRezKapacitaRocni", "Roční rezervovaná kapacita"),
        new("numRezKapacitaMesicni", "Měsíční rezervovaná kapacita"),
        new("numCenaRocniRezKapacita", "Cena roční rezervované kapacity"),
        new("numCenaMesicniRezKapacita", "Cena měsíční rezervované kapacity"),
        new("numCenaPrekRezKapacity", "Cena překročení rezervované kapacity"),
        new("numTolerancePrekroceni", "Tolerance překročení"),
        new("numCenaPouzitiSiti", "Použití sítí"),
        new("numCenaOperatorTrhu", "Operátor trhu"),
        new("numCenaSystemoveSluzby", "Systémové služby"),
        new("numCenaDispecink", "Dispečink"),
        new("numCenaZpravaSite", "Správa sítě"),
        new("numCenaServisOpravy", "Servis a opravy"),
        new("numCenaOdpisy", "Odpisy"),
        new("numCenaTechPodpora", "Technická podpora"),
        new("numCenaTarifVysoky", "Vysoký tarif"),
        new("numCenaTarifNizky", "Nízký tarif"),
        new("numCenaTarifSvatek", "Víkendový / sváteční tarif"),
        new("bDPH", "DPH"),
        new("numCenaKrytiNakladu", "Krytí nákladů"),
        new("numCenaNedocerp", "Cena nedočerpání"),
        new("numCenaPrecerp", "Cena přečerpání"),
        new("numProcNedocerp", "Mez nedočerpání"),
        new("numProcPrecerp", "Mez přečerpání"),
        new("bHodnoceniPasmo", "Hodnocení pásma"),
        new("numCenaDodavkaJal", "Dodávka jalové energie"),
        new("bNedocerpMWhProc", "Jednotka nedočerpání"),
        new("bPrecerpMWhProc", "Jednotka přečerpání"),
        new("numCenaEkoDan", "Ekologická daň")];
    public static string Unit(string column, bool? shortfallMwh=false, bool? excessMwh=false) => column switch {
        "numRezKapacitaRocni" or "numRezKapacitaMesicni" => "MW",
        "numTolerancePrekroceni" => "%",
        "numProcNedocerp" => shortfallMwh is null ? "Neuvedeno" : shortfallMwh.Value ? "MWh" : "%",
        "numProcPrecerp" => excessMwh is null ? "Neuvedeno" : excessMwh.Value ? "MWh" : "%",
        "numCenaRocniRezKapacita" or "numCenaMesicniRezKapacita" or "numCenaPrekRezKapacity" => "Kč/MW",
        "numCenaDodavkaJal" => "Kč/MVArh",
        "numCenaDispecink" or "numCenaZpravaSite" or "numCenaServisOpravy" or "numCenaOdpisy" or "numCenaTechPodpora" => "Kč",
        _ when column.StartsWith("b", StringComparison.Ordinal) => "—",
        _ => "Kč/MWh"
    };
    public static string Display(string column, object? value)
    {
        if (value is null or DBNull) return "Neuvedeno";
        if (column is "bNedocerpMWhProc" or "bPrecerpMWhProc") return Convert.ToBoolean(value) ? "MWh" : "%";
        if (column.StartsWith("b", StringComparison.Ordinal)) return Convert.ToBoolean(value) ? "Ano" : "Ne";
        return Convert.ToString(value, CultureInfo.GetCultureInfo("cs-CZ")) ?? "Neuvedeno";
    }
    public PriceSnapshot Load(int meterId, DateOnly month)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Připojení MDB vyžaduje Windows.");
        return LoadWindows(meterId, new DateOnly(month.Year, month.Month, 1));
    }
    [SupportedOSPlatform("windows")]
    private PriceSnapshot LoadWindows(int meterId, DateOnly month)
    {
        var database = new ElectricityConfigurationDatabase(catalog, config);
        using var connection = database.Open(false);
        var name = ElectricityConfigurationDatabase.MeterName(connection, meterId);
        using var command = new OleDbCommand("SELECT " + string.Join(", ", Fields.Select(field => $"[{field.Column}]")) + " FROM [tblCenyHlavniElektrina] WHERE [numIDHlavni] = ? AND [dtDatumPlatnost] = ?", connection);
        command.Parameters.Add("meter", OleDbType.Integer).Value = meterId;
        command.Parameters.Add("date", OleDbType.Date).Value = month.ToDateTime(TimeOnly.MinValue);
        using var reader = command.ExecuteReader();
        var found = reader is not null && reader.Read();
        bool? shortfall = found ? reader!["bNedocerpMWhProc"] is DBNull ? null : Convert.ToBoolean(reader["bNedocerpMWhProc"]) : false;
        bool? excess = found ? reader!["bPrecerpMWhProc"] is DBNull ? null : Convert.ToBoolean(reader["bPrecerpMWhProc"]) : false;
        var values = Fields.Select(field => new PriceValue(field.Label, Display(field.Column,
            found ? reader![field.Column] : field.Column.StartsWith("b", StringComparison.Ordinal) ? false : 0),
            Unit(field.Column, shortfall, excess))).ToArray();
        if (found && reader!.Read()) throw new InvalidDataException("Duplicitní ceník stejného měření a měsíce.");
        return new(name, month, !found, values);
    }
}
