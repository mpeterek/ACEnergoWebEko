using ACEnergo.Web.Domain;
namespace ACEnergo.Web.Services;
public sealed record SubmeterEvaluation(SubmeterDefinition Definition,string Mode,DateOnly Date,string Interval,int Shift,ConsumptionBucket[] Rows,string[] Warnings)
{
    public bool Complete=>Rows.All(r=>r.Complete);
    public decimal? Energy=>Rows.Any(r=>r.Energy.HasValue)?Rows.Sum(r=>r.Energy??0):null;
    public decimal? Cost=>Mode!="year"&&Rows.All(r=>r.Cost.HasValue)?Rows.Sum(r=>r.Cost!.Value):null;
    public decimal? PeakPower=>Rows.Max(r=>r.PeakPower);
}
public interface IElectricitySubmeterEvaluation
{
    SubmeterEvaluation Load(int id,string mode,DateOnly date,string interval,int? mainMeterId,CancellationToken cancellationToken=default);
}
public sealed class ElectricitySubmeterEvaluation(IElectricitySubmeterStore meters,IElectricityPulseArchive archive,IElectricityTariffStore tariffs,IElectricityHolidayStore holidays) : IElectricitySubmeterEvaluation
{
    public SubmeterEvaluation Load(int id,string mode,DateOnly date,string interval,int? mainMeterId,CancellationToken cancellationToken=default)
    {
        ElectricityConsumption.Validate(mode,date,interval);
        if(mainMeterId is <=0)throw new ArgumentException("Neplatná volba hlavního měření pro tarify.");
        var meter=meters.Load(id);
        var shift=(mode=="day"?meter.DayShift:mode=="month"?meter.MonthShift:meter.YearShift)??throw new InvalidDataException("Časový posun typu není uveden.");
        var warnings=new List<string>();var rows=new List<ConsumptionBucket>();
        var price=mode=="year"?null:ElectricityTypePrices.Select(meter.Prices,mode=="day"?date:new(date.Year,date.Month,1));
        if(price?.Ambiguous==true)warnings.Add("Platná cena je nejednoznačná kvůli rozdílným záznamům stejného data. Náklady nejsou vypočteny.");
        else if(mode!="year"&&price?.Price is null)warnings.Add("Pro zvolené období chybí platná cena. Spotřeba se vyhodnotí bez nákladů.");
        else if(price?.Price?.Tariff is null&&mode!="year")warnings.Add("U ceníku není uveden způsob ocenění. Náklady nejsou vypočteny.");
        ConsumptionTariffs? tariff=null;
        if(price?.Price?.Tariff==true){
            if(mainMeterId is null)warnings.Add("Pro tarifní ceny zvolte hlavní měření, ze kterého se převezmou tarifní pásma.");
            else {
                var grid=tariffs.Load(mainMeterId.Value,new(date.Year,date.Month,1));
                if(grid.IsDefault)warnings.Add("Vybrané hlavní měření nemá uložené tarify pro tento měsíc. Výchozí matice se pro výpočet nákladů nepoužije.");
                else tariff=new(grid.Cells,holidays.Load(mainMeterId.Value).Dates.ToHashSet());
            }
        }
        foreach(var logical in mode=="year"?Enumerable.Range(1,12).Select(m=>new DateOnly(date.Year,m,1)):[date]){
            cancellationToken.ThrowIfCancellationRequested();
            var window=ElectricityConsumption.Window(mode=="day"?"day":"month",logical,shift);
            var data=archive.Read(id,logical,window);
            ElectricityConsumption.ValidateSamples(window,data.Samples);
            if(mode=="year")rows.Add(ElectricityConsumption.Aggregate(window,data.Samples,data.Present));
            else {
                var step=mode=="month"?TimeSpan.FromDays(1):interval=="hour"?TimeSpan.FromHours(1):TimeSpan.FromMinutes(15);
                for(var from=window.From;from<window.Until;from+=step){
                    var until=from+step;var samples=data.Samples.Where(s=>s.End>from&&s.End<=until).ToArray();
                    rows.Add(ElectricityConsumption.Aggregate(new(from,until),samples,data.Present,price?.Price,tariff));
                }
            }
        }
        if(mode!="year"&&rows.All(r=>r.Complete)&&rows.Any(r=>r.Cost is null)&&warnings.Count==0)warnings.Add("Ceník neobsahuje všechny potřebné ceny. Náklady nejsou vypočteny.");
        if(rows.Any(r=>!r.Complete))warnings.Add("Archiv je neúplný. Zobrazen je součet dostupných hodnot, nikoli úplná spotřeba. Chybějící čtvrthodiny nejsou nahrazeny nulou; náklady neúplného období se neuvádějí.");
        return new(meter,mode,date,interval,shift,rows.ToArray(),warnings.ToArray());
    }
}
