using ACEnergo.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace ACEnergo.Web.Pages;
[Authorize(Policy="ElekView")]
public class ElectricityGroupsModel(IElectricityGroupCatalog store,ILogger<ElectricityGroupsModel> logger):PageModel {
 [BindProperty(SupportsGet=true)]public string? Kind{get;set;}
 public string Heading{get;private set;}="Elektřina";
 public ElectricityGroup[] Rows{get;private set;}=[];
 public bool Loaded{get;private set;}
 public IActionResult OnGet(){
  if(Kind is null)return NotFound();
  try{Heading=ElectricityGroupCatalog.Definition(Kind).Title;}catch(ArgumentException){return NotFound();}
  try{Rows=store.Load(Kind);Loaded=true;return Page();}
  catch(Exception ex)when(ex is System.Data.OleDb.OleDbException or IOException or UnauthorizedAccessException or InvalidOperationException or PlatformNotSupportedException or FormatException or InvalidCastException){logger.LogError(ex,"Přehled elektřiny {Kind} nelze načíst",Kind);Response.StatusCode=503;ModelState.AddModelError("","Přehled nelze načíst. Podrobnosti jsou ve výstupu aplikace.");return Page();}
 }
}
