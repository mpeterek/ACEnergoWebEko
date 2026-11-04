using System.Data.OleDb;
using System.Globalization;
using System.Runtime.Versioning;
using ACEnergo.Web.Domain;
using ACEnergo.Web.Services;
using Microsoft.Extensions.Configuration;
internal static class WindowsSubmeterReadCheck
{
    public static void Run(string[] args)
    {
        if(args.Length is <3 or >4||!ElectricityTariffs.TryMonth(args[2],out var month))throw new ArgumentException("Použití: --submeter-mdb C:\\ACEnergoData\\ACEnergoWebEko.mdb RRRR-MM [ID]");
        if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException("Vyžaduje Windows a ACE provider bitovosti procesu.");
        Read(Path.GetFullPath(args[1]),month,args.Length==4?int.Parse(args[3],CultureInfo.InvariantCulture):null);
    }
    [SupportedOSPlatform("windows")]
    private static void Read(string source,DateOnly month,int? input)
    {
        if(!File.Exists(source)||!string.Equals(Path.GetFileName(source),"ACEnergoWebEko.mdb",StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Zadejte konfigurační MDB.");
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"Access:Directory",Path.GetDirectoryName(source)},{"Access:Provider","Microsoft.ACE.OLEDB.16.0"}}).Build();
        var catalog=new DatabaseCatalog(config);var meters=new ElectricitySubmeterStore(catalog,config);
        var id=input??meters.Browse(null,null,null,0).Rows.FirstOrDefault()?.Id??throw new Exception("Chybí archivované elektrické vstupy.");
        var definition=meters.Load(id);var station=meters.Browse(id/10000,null,null,0);
        if(station.Rows.Any(r=>r.Station!=id/10000))throw new Exception("Filtr stanice neodpovídá ID.");
        var evaluation=new ElectricitySubmeterEvaluation(meters,new ElectricityPulseArchive(catalog,config),new ElectricityTariffStore(catalog,config),new ElectricityHolidayStore(catalog,config));
        var result=evaluation.Load(id,"month",month,"quarter",null);
        var path=catalog.Resolve(DatabaseCatalog.MonthlyName(month.Year,month.Month));
        if(!File.Exists(path))throw new Exception("Pro ověření součtu zvolte existující měsíční MDB.");
        var window=ElectricityConsumption.Window("month",month,definition.MonthShift??throw new Exception("Chybí posun typu."));
        var cs=new OleDbConnectionStringBuilder{Provider="Microsoft.ACE.OLEDB.16.0",DataSource=path};cs["Mode"]="Read";
        using var connection=new OleDbConnection(cs.ConnectionString);connection.Open();
        // Nezávislá kontrola agregace přímým SQL SUM, nikoli opakováním C# smyčky.
        using var command=new OleDbCommand("SELECT Sum([numHodnota]),Count([numHodnota]) FROM [tblDataImpuls] WHERE [numCislo]=? AND [dtDateTime]>? AND [dtDateTime]<=?",connection);
        command.Parameters.Add("id",OleDbType.Integer).Value=id;command.Parameters.Add("from",OleDbType.Date).Value=window.From;command.Parameters.Add("until",OleDbType.Date).Value=window.Until;
        using var reader=command.ExecuteReader();if(reader is null||!reader.Read())throw new Exception("Součet SQL není dostupný.");
        decimal? sum=reader.IsDBNull(0)?null:Convert.ToDecimal(reader[0]);var samples=Convert.ToInt32(reader[1]);
        if(result.Energy!=sum||result.Rows.Sum(r=>r.Samples)!=samples)throw new Exception("Výsledek nesouhlasí s přímým SQL SUM/COUNT.");
        Console.WriteLine($"PASS: nativní čtení podružných měření, filtr stanice, detail, cenová historie a měsíční agregace proti SQL SUM/COUNT ({samples}/{window.Expected} hodnot). Pouze čtení, žádné zápisy.");
    }
}
