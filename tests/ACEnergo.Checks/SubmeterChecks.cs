using ACEnergo.Web.Domain;
using ACEnergo.Web.Services;
internal static class SubmeterChecks
{
    public static void Run()
    {
        var count=0;
        void Check(bool pass,string name){if(!pass)throw new Exception("CONSUMPTION: "+name);count++;}
        void Reject<T>(Action action)where T:Exception{try{action();}catch(T){count++;return;}throw new Exception("CONSUMPTION: expected "+typeof(T).Name);}
        var date=new DateOnly(2026,10,1);var store=new SubmeterFixture();var archive=new PulseArchiveFixture();var tariff=new Tariffs();var holiday=new Holidays();
        var service=new ElectricitySubmeterEvaluation(store,archive,tariff,holiday);
        var day=service.Load(10003,"day",date,"quarter",null);
        Check(day.Rows.Length==96&&day.Energy==96m&&day.Cost==960m&&day.Complete&&day.PeakPower==4m,"96 intervals, sum, power and unit cost");
        Check(day.Rows[0].Window.From.Hour==6&&day.Rows[^1].Window.Until==new DateTime(2026,10,2,6,0,0),"day offset and midnight boundary");
        var hours=service.Load(10003,"day",date,"hour",null);
        Check(hours.Rows.Length==24&&hours.Energy==day.Energy&&hours.Rows.All(r=>r.Energy==4&&r.PeakPower==4),"hourly aggregation preserves peak and energy");
        var month=service.Load(10003,"month",date,"quarter",null);
        Check(month.Rows.Length==31&&month.Energy==2976m&&month.Cost==29760m&&month.Shift==2,"monthly distinct shift and first-month price");
        Check(service.Load(10003,"day",new(2026,10,16),"quarter",null).Cost==1920m,"day uses price effective at selected day");
        Check(ElectricityConsumption.Window("month",new(2026,3,1),-1).Expected==31*96,"negative shift across short February keeps whole March");
        Check(ElectricityConsumption.Window("month",new(2024,2,1),-1).Expected==29*96,"leap February with negative shift");
        var year=service.Load(10003,"year",date,"quarter",null);
        Check(year.Rows.Length==12&&year.Rows[0].Window.From==new DateTime(2025,12,31,23,0,0)&&year.Shift==-1,"year shift and month boundaries");
        Check(!year.Complete&&year.Rows[10].Energy is null&&year.Rows[10].Status.Contains("MDB")&&year.Cost is null,"missing November and no annual price calculation");
        Check(year.Energy==(365-30)*96m,"known annual subtotal excludes missing month");
        archive.Partial=true;
        var partial=service.Load(10003,"day",date,"hour",null);
        Check(!partial.Complete&&partial.Cost is null&&partial.Energy==94m&&partial.Warnings.Length>0,"missing quarter does not become zero or complete cost");
        archive.Partial=false;archive.Missing=true;
        var missing=service.Load(10003,"day",date,"quarter",null);
        Check(missing.Energy is null&&missing.PeakPower is null&&missing.Cost is null&&missing.Rows.All(r=>!r.ArchivePresent),"missing archive not zero");
        archive.Missing=false;archive.Duplicate=true;
        Reject<InvalidDataException>(()=>service.Load(10003,"day",date,"quarter",null));archive.Duplicate=false;
        var window=ElectricityConsumption.Window("day",date,0);
        Reject<InvalidDataException>(()=>ElectricityConsumption.Aggregate(window,[new(window.From,1)],true));
        Reject<InvalidDataException>(()=>ElectricityConsumption.Aggregate(window,[new(window.From.AddMinutes(16),1)],true));
        Reject<InvalidDataException>(()=>ElectricityConsumption.Aggregate(window,[new(window.Until.AddMinutes(15),1)],true));
        Reject<InvalidDataException>(()=>ElectricityConsumption.Window("day",date,24));
        var nul=ElectricityConsumption.Aggregate(window,[new(window.From.AddMinutes(15),null)],true);
        Check(nul.Energy is null&&!nul.Complete&&nul.Samples==0,"NULL sample is missing");
        Check(ElectricityConsumption.Aggregate(window,[new(window.From.AddMinutes(15),0m)],true).Energy==0,"measured zero distinct from missing");
        Check(ElectricityConsumption.Aggregate(window,[new(window.From.AddMinutes(15),-1m)],true).Energy==-1,"negative archive value preserved");
        Check(service.Load(10004,"day",date,"quarter",null).Cost is null,"tariff price needs chosen main meter");
        holiday.Dates=[date];
        var festive=service.Load(10004,"day",date,"quarter",5);
        Check(festive.Cost==384m&&festive.Rows.Sum(r=>r.Bands[2]??0)==96,"holiday includes midnight end and weekend price");
        holiday.Dates=[];
        var monday=service.Load(10004,"day",new(2026,10,5),"quarter",5);
        Check(monday.Cost==284m&&monday.Rows.Sum(r=>r.Bands[1]??0)==4,"low/high prices match legacy mapping");
        tariff.IsDefault=true;
        var fallback=service.Load(10004,"day",date,"quarter",5);
        Check(fallback.Cost is null&&fallback.Rows.All(r=>r.Bands.All(v=>v is null)),"unsaved default matrix not used for billing");
        Reject<ArgumentException>(()=>service.Load(10003,"invalid",date,"quarter",null));
        Reject<ArgumentException>(()=>service.Load(10003,"day",date,"minute",null));
        Reject<ArgumentException>(()=>service.Load(10003,"day",new(1999,1,1),"quarter",null));
        Reject<ArgumentException>(()=>service.Load(10003,"day",date,"quarter",-1));
        Reject<KeyNotFoundException>(()=>service.Load(50000001,"day",date,"quarter",null));
        Reject<ArgumentException>(()=>ElectricitySubmeterStore.Validate(5000,null,null,0));
        Reject<ArgumentException>(()=>ElectricitySubmeterStore.Validate(null,"bad",null,0));
        Reject<ArgumentException>(()=>ElectricitySubmeterStore.Validate(null,null,new string('x',81),0));
        Reject<ArgumentException>(()=>ElectricitySubmeterStore.Validate(null,null,null,-1));
        using var cancelled=new CancellationTokenSource();cancelled.Cancel();var before=archive.Reads;
        Reject<OperationCanceledException>(()=>service.Load(10003,"year",date,"quarter",null,cancelled.Token));
        Check(archive.Reads==before,"cancelled year query does not open archives");
        Console.WriteLine($"PASS: {count} submeter aggregation, offsets, archive integrity, missing data, pricing, tariffs and cancellation checks (synthetic archive).");
    }
    private sealed class Tariffs:IElectricityTariffStore
    {
        public bool IsDefault{get;set;}
        public TariffSnapshot Load(int meterId,DateOnly month){var cells=new int[168];cells[0]=1;return new(meterId,"Test",month,cells,IsDefault,new string('A',64));}
        public void Save(int meterId,DateOnly month,IReadOnlyList<int> cells,string expectedVersion)=>throw new NotSupportedException();
    }
    private sealed class Holidays:IElectricityHolidayStore
    {
        public DateOnly[] Dates{get;set;}=[];
        public HolidaySnapshot Load(int meterId)=>new(meterId,"Test",Dates.Select(d=>new HolidayRecord(d.ToDateTime(TimeOnly.MinValue),"_")).ToArray(),new string('A',64));
        public bool Add(int meterId,DateOnly date,string expectedVersion)=>throw new NotSupportedException();
        public int Remove(int meterId,IReadOnlyList<DateOnly> dates,string expectedVersion)=>throw new NotSupportedException();
    }
}
