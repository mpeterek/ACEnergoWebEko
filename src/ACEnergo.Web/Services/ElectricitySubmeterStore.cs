using System.Data.OleDb;
using System.Runtime.Versioning;
using ACEnergo.Web.Domain;
namespace ACEnergo.Web.Services;

public sealed record SubmeterItem(int Id, string Name, string Type, string? WorkUnit, string? PowerUnit)
{
    public int Station => Id / 10000;
    public int Input => Id % 10000;
}
public sealed record SubmeterDefinition(SubmeterItem Input, int? DayShift, int? MonthShift, int? YearShift, TypePriceRecord[] Prices);
public sealed record SubmeterList(SubmeterItem[] Rows, bool HasMore, string[] Types, int[] Stations);
public interface IElectricitySubmeterStore
{
    SubmeterList Browse(int? station, string? inputType, string? search, int afterId);
    SubmeterDefinition Load(int id);
}
public sealed class ElectricitySubmeterStore(DatabaseCatalog catalog, IConfiguration config) : IElectricitySubmeterStore
{
    private readonly ElectricityConfigurationDatabase db = new(catalog, config);
    private const string Filter = "p.[boolArchiv]<>0 AND p.[numCislo]>0 AND p.[numCislo]<50000000 AND p.[strTyp] IN (SELECT [strTypEnergie] FROM [tblKonfigEnergie] WHERE [strNazevEnergie]='Elektřina')";
    public static void Validate(int? station, string? inputType, string? search, int afterId)
    {
        if(station is < 0 or >= 5000 || afterId is < 0 or >= 50000000 || inputType?.Length > 2 || search?.Length > 80)
            throw new ArgumentException("Neplatný filtr podružných měření.");
    }
    public SubmeterList Browse(int? station, string? inputType, string? search, int afterId)
    {
        Validate(station, inputType, search, afterId);
        if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException();
        return ReadList(station, inputType?.Trim(), search?.Trim(), afterId);
    }
    public SubmeterDefinition Load(int id)
    {
        if(id<=0 || id>=50000000)throw new KeyNotFoundException();
        if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException();
        return ReadDefinition(id);
    }
    [SupportedOSPlatform("windows")]
    private SubmeterList ReadList(int? station, string? type, string? search, int afterId)
    {
        using var connection=db.Open(false);
        using var command=new OleDbCommand("SELECT TOP 101 p.[numCislo],p.[strNazev],p.[strTyp],p.[strRozmerP],p.[strRozmerV] FROM [tblKonfigImpuls] AS p WHERE "+Filter+" AND p.[numCislo]>?"+
            (station is not null?" AND p.[numCislo]>=? AND p.[numCislo]<?":"")+
            (!string.IsNullOrEmpty(type)?" AND p.[strTyp]=?":"")+
            (!string.IsNullOrEmpty(search)?" AND (p.[numCislo]=? OR InStr(1,p.[strNazev],?)>0)":"")+" ORDER BY p.[numCislo]",connection);
        command.Parameters.Add("after",OleDbType.Integer).Value=afterId;
        if(station is not null){command.Parameters.Add("from",OleDbType.Integer).Value=station.Value*10000;command.Parameters.Add("until",OleDbType.Integer).Value=(station.Value+1)*10000;}
        if(!string.IsNullOrEmpty(type))command.Parameters.Add("type",OleDbType.VarWChar,2).Value=type;
        if(!string.IsNullOrEmpty(search)){command.Parameters.Add("id",OleDbType.Integer).Value=int.TryParse(search,out var id)?id:-1;command.Parameters.Add("search",OleDbType.VarWChar,80).Value=search;}
        var rows=new List<SubmeterItem>();
        using(var reader=command.ExecuteReader()){while(reader is not null&&reader.Read())rows.Add(Item(reader));}
        using var types=new OleDbCommand("SELECT DISTINCT [strTypEnergie] FROM [tblKonfigEnergie] WHERE [strNazevEnergie]='Elektřina' ORDER BY [strTypEnergie]",connection);
        var typeList=new List<string>();using(var reader=types.ExecuteReader()){while(reader is not null&&reader.Read())typeList.Add(Convert.ToString(reader[0])??"");}
        using var stations=new OleDbCommand("SELECT DISTINCT Fix(p.[numCislo]/10000) AS [StationNumber] FROM [tblKonfigImpuls] AS p WHERE "+Filter+" ORDER BY Fix(p.[numCislo]/10000)",connection);
        var stationList=new List<int>();using(var reader=stations.ExecuteReader()){while(reader is not null&&reader.Read())stationList.Add(Convert.ToInt32(reader[0]));}
        return new(rows.Take(100).ToArray(),rows.Count>100,typeList.ToArray(),stationList.ToArray());
    }
    [SupportedOSPlatform("windows")]
    private static SubmeterItem Item(OleDbDataReader reader)=>new(Convert.ToInt32(reader[0]),Convert.ToString(reader[1])??"",Convert.ToString(reader[2])??"",reader.IsDBNull(3)?null:Convert.ToString(reader[3]),reader.IsDBNull(4)?null:Convert.ToString(reader[4]));
    [SupportedOSPlatform("windows")]
    private SubmeterDefinition ReadDefinition(int id)
    {
        using var connection=db.Open(false);
        using var command=new OleDbCommand("SELECT p.[numCislo],p.[strNazev],p.[strTyp],p.[strRozmerP],p.[strRozmerV],e.[numKlic],e.[numHodinaDen],e.[numHodinaMesic],e.[numHodinaRok] FROM [tblKonfigImpuls] AS p INNER JOIN [tblKonfigEnergie] AS e ON p.[strTyp]=e.[strTypEnergie] WHERE p.[numCislo]=? AND p.[boolArchiv]<>0 AND p.[numCislo]>0 AND p.[numCislo]<50000000 AND e.[strNazevEnergie]='Elektřina'",connection);
        command.Parameters.Add("id",OleDbType.Integer).Value=id;
        SubmeterItem item;int key;int? day,month,year;
        using(var reader=command.ExecuteReader()){
            if(reader is null||!reader.Read())throw new KeyNotFoundException();
            item=Item(reader);key=Convert.ToInt32(reader[5]);
            day=reader.IsDBNull(6)?null:Convert.ToInt32(reader[6]);month=reader.IsDBNull(7)?null:Convert.ToInt32(reader[7]);year=reader.IsDBNull(8)?null:Convert.ToInt32(reader[8]);
            if(reader.Read())throw new InvalidDataException("Typ elektrického vstupu není jednoznačný.");
        }
        return new(item,day,month,year,ElectricityTypePriceStore.ReadHistory(connection,key));
    }
}
