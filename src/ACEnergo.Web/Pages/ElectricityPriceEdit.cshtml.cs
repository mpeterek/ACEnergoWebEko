using ACEnergo.Web.Services;
using ACEnergo.Web.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace ACEnergo.Web.Pages;
[Authorize(Policy="ElekConfig")]
public class ElectricityPriceEditModel(IElectricityPriceEditor store, ILogger<ElectricityPriceEditModel> logger):PageModel {
 [BindProperty(SupportsGet=true)] public int MeterId{get;set;}
 [BindProperty(SupportsGet=true)] public string? Period{get;set;}
 [BindProperty] public string[] Values{get;set;}=[];
 [BindProperty] public string Version{get;set;}="";
 [BindProperty] public bool Confirm{get;set;}
 public EditablePrices? Data{get;private set;}
 public IActionResult OnGet(){
  if(Period is null && !Request.Query.ContainsKey(nameof(Period))) {Period=DateTime.Today.ToString("yyyy-MM",System.Globalization.CultureInfo.InvariantCulture);ModelState.Remove(nameof(Period));}
  return Run(false);
 }
 public IActionResult OnPost()=>Run(true);
 private IActionResult Run(bool save){
  if(!ElectricityTariffs.TryMonth(Period,out var month)) return BadRequest("Neplatné období.");
  try {
   Data=store.Load(MeterId,month);
   if(!save){Values=Data.Values;Version=Data.Version;return Page();}
   if(!ModelState.IsValid || !Confirm){Response.StatusCode=400;ModelState.AddModelError("","Potvrďte přepsání cen do konce roku.");return Page();}
   store.Save(MeterId,month,Values,Version);
   return RedirectToPage("/ElectricityPrices",new{MeterId,Period});
  } catch(ArgumentException ex){Response.StatusCode=400;ModelState.AddModelError("",ex.Message);return Page();}
  catch(TariffConflictException){Response.StatusCode=409;ModelState.AddModelError("","Ceník se mezitím změnil. Znovu načtěte stránku.");return Page();}
  catch(KeyNotFoundException){return NotFound();}
  catch(Exception ex) when(ex is System.Data.OleDb.OleDbException or IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException or PlatformNotSupportedException){logger.LogError(ex,"Editace cen selhala");Response.StatusCode=503;ModelState.AddModelError("","Ceník není dostupný; podrobnosti jsou ve výstupu aplikace.");return Page();}
 }
}
