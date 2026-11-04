using ACEnergo.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace ACEnergo.Web.Pages;
[Authorize(Policy="ElekView")]
public class ElectricityGroupModel(IElectricityGroupConfiguration store,ILogger<ElectricityGroupModel> logger,DatabaseCatalog catalog):PageModel {
 [BindProperty(SupportsGet=true)]public string? Kind{get;set;}
 [BindProperty(SupportsGet=true)]public int Id{get;set;}
 [BindProperty]public string GroupName{get;set;}="";
 [BindProperty]public string? PowerUnit{get;set;}
 [BindProperty]public string? EnergyUnit{get;set;}
 [BindProperty]public int? MainMeterId{get;set;}
 [BindProperty]public bool? Recalculate{get;set;}
 public MainMeterChoice[] MainMeters{get;private set;}=[];
 [BindProperty]public string Version{get;set;}="";
 public string Heading{get;private set;}="Elektřina";
 public GroupConfiguration? Data{get;private set;}
 public bool CanEdit=>User.IsInRole("Administrator")||User.HasClaim("permission","ElekConfig");
 public IActionResult OnGet()=>Run(false);
 public IActionResult OnPost(){if(!CanEdit)return Forbid();return Run(true);}
 private IActionResult Run(bool save){
  if(Kind is null)return NotFound();
  try{Heading=ElectricityGroupCatalog.Definition(Kind).Title;}catch(ArgumentException){return NotFound();}
  try{
   Data=store.Load(Kind,Id);
   if(Kind!="fictitious")MainMeters=store.MainMeters();
   if(!save){GroupName=Data.Name;PowerUnit=Data.PowerUnit;EnergyUnit=Data.EnergyUnit;Version=Data.Version;MainMeterId=int.TryParse(Data.MainMeter,out var meter)&&meter>0?meter:null;Recalculate=bool.TryParse(Data.Recalculation,out var recalculate)?recalculate:null;return Page();}
   if(!ModelState.IsValid){Response.StatusCode=400;return Page();}
   if(Request.Form.ContainsKey(nameof(MainMeterId))||Request.Form.ContainsKey(nameof(Recalculate)))store.SaveSettings(Kind,Id,GroupName,PowerUnit,EnergyUnit,Version,MainMeterId,Recalculate);
   else store.Save(Kind,Id,GroupName,PowerUnit,EnergyUnit,Version);
   TempData["GroupSaved"]="Konfigurace byla uložena.";
   return RedirectToPage(new{Kind,Id});
  }catch(KeyNotFoundException ex){
   logger.LogWarning(ex,"GROUP-NOT-FOUND: stránka byla nalezena, ale elektrická konfigurace {Kind}/{Id} není v použité MDB {Database}",Kind,Id,catalog.Resolve("ACEnergoWebEko.mdb"));
   Response.StatusCode=404;
   ModelState.AddModelError("",$"Záznam {Heading}, ID {Id}, nebyl v používané databázi elektřiny nalezen. Vraťte se do seznamu a obnovte jej.");
   return Page();
  }
  catch(GroupConfigurationConflictException){Response.StatusCode=409;ModelState.AddModelError("","Konfigurace se mezitím změnila. Znovu načtěte stránku a zopakujte úpravy.");return Page();}
  catch(ArgumentException ex){Response.StatusCode=400;ModelState.AddModelError("",ex.Message);return Page();}
  catch(Exception ex)when(ex is InvalidDataException or IOException or System.Data.OleDb.OleDbException or InvalidOperationException or UnauthorizedAccessException or PlatformNotSupportedException or OverflowException or FormatException or InvalidCastException){logger.LogError(ex,"Konfigurace {Kind}/{Id} selhala",Kind,Id);Response.StatusCode=503;ModelState.AddModelError("","Konfigurace není dostupná. Podrobnosti jsou ve výstupu aplikace.");return Page();}
 }
}
