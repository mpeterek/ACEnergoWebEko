using ACEnergo.Web.Services;
using ACEnergo.Web.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace ACEnergo.Web.Pages;
[Authorize(Policy="ElekView")]
public class ElectricityMeterModel(IElectricityMeterConfiguration store,ILogger<ElectricityMeterModel> logger):PageModel {
 [BindProperty(SupportsGet=true)]public int MeterId{get;set;}
 [BindProperty]public string MeterName{get;set;}="";
 [BindProperty]public string Coefficient{get;set;}="";
 [BindProperty]public string ReactiveCoefficient{get;set;}="";
 [BindProperty]public string Version{get;set;}="";
 [BindProperty]public bool DisconnectReactive{get;set;}
 public MeterConfiguration? Data{get;private set;}
 public bool CanEdit=>User.IsInRole("Administrator")||User.HasClaim("permission","ElekConfig");
 public IActionResult OnGet()=>Run(false);
 public IActionResult OnPost(){if(!CanEdit)return Forbid();return Run(true);}
 private IActionResult Run(bool save){
  try{
   Data=store.Load(MeterId);
   if(!save){MeterName=Data.Name;Coefficient=Data.Coefficient;ReactiveCoefficient=Data.ReactiveCoefficient;Version=Data.Version;return Page();}
   if(!ModelState.IsValid){Response.StatusCode=400;return Page();}
   store.Save(MeterId,MeterName,Coefficient,ReactiveCoefficient,DisconnectReactive,Version);
   return RedirectToPage(new{MeterId});
  }catch(ArgumentException ex){Response.StatusCode=400;ModelState.AddModelError("",ex.Message);return Page();}
  catch(TariffConflictException){Response.StatusCode=409;ModelState.AddModelError("","Konfigurace se mezitím změnila. Znovu načtěte stránku.");return Page();}
  catch(KeyNotFoundException){return NotFound();}
  catch(Exception ex)when(ex is InvalidDataException or IOException or System.Data.OleDb.OleDbException or InvalidOperationException or UnauthorizedAccessException or PlatformNotSupportedException or OverflowException){logger.LogError(ex,"Konfigurace měření {Id} selhala",MeterId);Response.StatusCode=503;ModelState.AddModelError("","Konfigurace není dostupná. Podrobnosti jsou ve výstupu aplikace.");return Page();}
 }
}
