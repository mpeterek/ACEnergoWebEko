using System.Data.OleDb;
using System.Runtime.Versioning;
using ACEnergo.Web.Domain;
namespace ACEnergo.Web.Services;
public sealed record PulseArchive(bool Present, PulseSample[] Samples);
public interface IElectricityPulseArchive
{
    PulseArchive Read(int id, DateOnly logicalMonth, ConsumptionWindow window);
}
public sealed class ElectricityPulseArchive(DatabaseCatalog catalog,IConfiguration config) : IElectricityPulseArchive
{
    public PulseArchive Read(int id, DateOnly logicalMonth, ConsumptionWindow window)
    {
        if(id<=0||id>=50000000||window.Expected is <=0 or >2976)throw new ArgumentException("Neplatný rozsah archivních dat.");
        if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException();
        return ReadWindows(id,logicalMonth,window);
    }
    [SupportedOSPlatform("windows")]
    private PulseArchive ReadWindows(int id,DateOnly month,ConsumptionWindow window)
    {
        var path=catalog.Resolve(DatabaseCatalog.MonthlyName(month.Year,month.Month));
        try {using var file=File.Open(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);}
        catch(FileNotFoundException){return new(false,[]);}
        catch(DirectoryNotFoundException){return new(false,[]);}
        var builder=new OleDbConnectionStringBuilder{Provider=config["Access:Provider"]??"Microsoft.ACE.OLEDB.16.0",DataSource=path,OleDbServices=-2};builder["Mode"]="Read";
        using var connection=new OleDbConnection(builder.ConnectionString);connection.Open();
        // Stejně jako ASP čteme logickou měsíční MDB včetně jejího případného přesahu posunu.
        using var command=new OleDbCommand("SELECT [dtDateTime],[numHodnota] FROM [tblDataImpuls] WHERE [numCislo]=? AND [dtDateTime]>? AND [dtDateTime]<=? ORDER BY [dtDateTime]",connection);
        command.Parameters.Add("id",OleDbType.Integer).Value=id;
        command.Parameters.Add("from",OleDbType.Date).Value=window.From;
        command.Parameters.Add("until",OleDbType.Date).Value=window.Until;
        using var reader=command.ExecuteReader();var rows=new List<PulseSample>();
        while(reader is not null&&reader.Read()){
            if(rows.Count>=window.Expected)throw new InvalidDataException("Archiv obsahuje více čtvrthodin, než dovoluje období.");
            rows.Add(new(Convert.ToDateTime(reader[0]),reader.IsDBNull(1)?null:Convert.ToDecimal(reader[1])));
        }
        var result=rows.ToArray();ElectricityConsumption.ValidateSamples(window,result);return new(true,result);
    }
}
