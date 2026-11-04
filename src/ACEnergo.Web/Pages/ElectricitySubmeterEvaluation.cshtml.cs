using System.Globalization;
using System.Text;
using ACEnergo.Web.Domain;
using ACEnergo.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace ACEnergo.Web.Pages;
[Authorize(Policy="ElekView")]
public class ElectricitySubmeterEvaluationModel(IElectricitySubmeterEvaluation evaluation,IElectricityGroupConfiguration groups,ILogger<ElectricitySubmeterEvaluationModel> logger):PageModel
{
    [BindProperty(SupportsGet=true)]public int Id{get;set;}
    [BindProperty(SupportsGet=true)]public string? Mode{get;set;}
    [BindProperty(SupportsGet=true)]public string? Date{get;set;}
    [BindProperty(SupportsGet=true)]public string? Interval{get;set;}
    [BindProperty(SupportsGet=true)]public int? MainMeterId{get;set;}
    public SubmeterEvaluation? Data{get;private set;}
    public MainMeterChoice[] MainMeters{get;private set;}=[];
    public string Title=>Mode=="day"?"Denní vyhodnocení":Mode=="year"?"Roční vyhodnocení":"Měsíční vyhodnocení";
    public static string Number(decimal? value)=>value?.ToString("0.#####",CultureInfo.GetCultureInfo("cs-CZ"))??"—";
    public string Label(ConsumptionBucket row)=>Mode=="day"?row.Window.Until.ToString("dd.MM. HH:mm"):Mode=="year"?row.Window.From.AddHours(-Data!.Shift).ToString("MM/yyyy"):row.Window.From.AddHours(-Data!.Shift).ToString("dd.MM.yyyy");
    public IActionResult OnGet()=>Load(false);
    public IActionResult OnGetExport()=>Load(true);
    private IActionResult Load(bool export){
        Mode??="month";Interval??="quarter";
        if(Date is null&&!Request.Query.ContainsKey(nameof(Date)))Date=DateTime.Today.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
        if(!ModelState.IsValid||!DateOnly.TryParseExact(Date,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var date))return BadRequest("Neplatné datum vyhodnocení; použijte RRRR-MM-DD.");
        try{
            ElectricityConsumption.Validate(Mode,date,Interval);
            if(MainMeterId is <=0)throw new ArgumentException("Neplatné hlavní měření.");
            Data=evaluation.Load(Id,Mode,date,Interval,MainMeterId,HttpContext.RequestAborted);
            ViewData["Submeter"]=Data.Definition;
            if(export)return File(Csv(Data),"text/csv; charset=utf-8",$"elektrina-{Id}-{Mode}-{date:yyyyMMdd}.csv");
            MainMeters=groups.MainMeters();return Page();
        }catch(KeyNotFoundException){Response.StatusCode=404;ModelState.AddModelError("","Podružné nebo zvolené hlavní měření nebylo nalezeno.");return Page();}
        catch(ArgumentException ex){Response.StatusCode=400;ModelState.AddModelError("",ex.Message);return Page();}
        catch(Exception ex)when(ex is InvalidDataException or IOException or InvalidOperationException or UnauthorizedAccessException or PlatformNotSupportedException or System.Data.OleDb.OleDbException or OverflowException or FormatException or InvalidCastException){
            logger.LogError(ex,"Vyhodnocení podružného měření {Id}, {Mode}, {Date} selhalo",Id,Mode,Date);Response.StatusCode=503;ModelState.AddModelError("","Vyhodnocení není dostupné nebo archiv obsahuje neplatná data. Podrobnosti jsou ve výstupu aplikace.");return Page();
        }
    }
    private byte[] Csv(SubmeterEvaluation data){
        static string Text(string? text){var value=text??"";if(value.Length>0&&"=+-@\t\r\n".Contains(value[0]))value="'"+value;return "\""+value.Replace("\"","\"\"")+"\"";}
        var csv=new StringBuilder();csv.AppendLine("ID;Od;Do;Spotřeba;Jednotka práce;Maximum 1/4 h;Jednotka výkonu;Platné hodnoty;Očekávané hodnoty;Náklady Kč;Stav");
        foreach(var row in data.Rows)csv.AppendLine(string.Join(";",Id.ToString(CultureInfo.InvariantCulture),Text(row.Window.From.ToString("yyyy-MM-dd HH:mm")),Text(row.Window.Until.ToString("yyyy-MM-dd HH:mm")),row.Energy is null?"":Number(row.Energy),Text(data.Definition.Input.WorkUnit),row.PeakPower is null?"":Number(row.PeakPower),Text(data.Definition.Input.PowerUnit),row.Samples.ToString(CultureInfo.InvariantCulture),row.Expected.ToString(CultureInfo.InvariantCulture),row.Cost is null?"":Number(row.Cost),Text(row.Status)));
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
    }
}
