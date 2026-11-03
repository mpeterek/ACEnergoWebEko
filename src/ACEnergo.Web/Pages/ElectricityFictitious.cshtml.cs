using ACEnergo.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace ACEnergo.Web.Pages;
[Authorize(Policy="ElekView")]
public class ElectricityFictitiousModel(IElectricityFictitiousComposition store,ILogger<ElectricityFictitiousModel> logger):PageModel {
 [BindProperty(SupportsGet=true)]public int Id{get;set;}
 public FictitiousComposition? Data{get;private set;}
 public IActionResult OnGet(){
  try{Data=store.Load(Id);return Page();}
  catch(KeyNotFoundException){return NotFound();}
  catch(Exception ex)when(ex is InvalidDataException or InvalidCastException or IOException or System.Data.OleDb.OleDbException or InvalidOperationException or PlatformNotSupportedException or UnauthorizedAccessException or FormatException or OverflowException){logger.LogError(ex,"Složení fiktivního měření {Id} nelze načíst",Id);Response.StatusCode=503;ModelState.AddModelError("","Složení měření nelze načíst. Podrobnosti jsou ve výstupu aplikace.");return Page();}
 }
}
