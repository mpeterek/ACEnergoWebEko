using ACEnergo.Web.Domain;
using ACEnergo.Web.Services;
internal sealed class SubmeterFixture : IElectricitySubmeterStore
{
    public SubmeterList Browse(int? station,string? inputType,string? search,int afterId)
    {
        ElectricitySubmeterStore.Validate(station,inputType,search,afterId);
        var all=Enumerable.Range(1,130).Select(i=>new SubmeterItem(10000+i,i==3?"Test <submeter>":"Vstup "+i,i==4?"EU":"EP","kWh","kW")).Concat([new(20003,"Other station","EP","kWh","kW")]);
        var rows=all.Where(r=>r.Id>afterId&&(station is null||r.Station==station)&&(string.IsNullOrEmpty(inputType)||r.Type==inputType)&&(string.IsNullOrEmpty(search)||r.Id.ToString()==search||r.Name.Contains(search,StringComparison.OrdinalIgnoreCase))).OrderBy(r=>r.Id).Take(101).ToArray();
        return new(rows.Take(100).ToArray(),rows.Length>100,["EP","EU"],[1,2]);
    }
    public SubmeterDefinition Load(int id)
    {
        if(id!=20003&&(id<10001||id>10130))throw new KeyNotFoundException();
        var tariff=id==10004;
        return new(new(id,id==10003?"Test <submeter>":id==20003?"Other station":"Vstup "+(id-10000),tariff?"EU":"EP","kWh","kW"),tariff?0:6,2,-1,
            [new(new DateTime(2025,1,1),[10m,2m,3m,4m,null,null],tariff),new(new DateTime(2026,10,15),[20m,2m,3m,4m,null,null],tariff)]);
    }
}
internal sealed class PulseArchiveFixture : IElectricityPulseArchive
{
    public bool Missing {get;set;}
    public bool Partial {get;set;}
    public bool Duplicate {get;set;}
    public int Reads{get;private set;}
    public PulseArchive Read(int id,DateOnly logicalMonth,ConsumptionWindow window)
    {
        Reads++;
        if(Missing||logicalMonth.Month==11)return new(false,[]);
        var rows=Enumerable.Range(1,window.Expected).Select(i=>new PulseSample(window.From.AddMinutes(i*15),1m)).ToArray();
        if(Partial){rows[0]=rows[0] with{Energy=null};rows=rows.Skip(1).Take(rows.Length-2).ToArray();}
        if(Duplicate&&rows.Length>1)rows[1]=rows[0];
        return new(true,rows);
    }
}
