using ACEnergo.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace ACEnergo.Web.Pages;
[Authorize(Policy="ElekView")]
public class ElectricityGroupCompositionModel(IElectricityGroupCompositionEditor editor,IElectricityGroupConfiguration configuration,ILogger<ElectricityGroupCompositionModel> logger):PageModel {
 [BindProperty(SupportsGet=true)]public string? Kind{get;set;}
 [BindProperty(SupportsGet=true)]public int Id{get;set;}
 [BindProperty(SupportsGet=true)]public string? Search{get;set;}
 [BindProperty]public string? Version{get;set;}
 [BindProperty]public int ChildId{get;set;}
 [BindProperty]public string? Coefficient{get;set;}
 [BindProperty]public bool Recalculate{get;set;}
 [BindProperty]public bool ConfirmRemoval{get;set;}
 public CompositionEditSnapshot? Snapshot{get;private set;}
 public GroupConfiguration? Data{get;private set;}
 public CompositionSearch Candidates{get;private set;}=new([],false);
 public string Heading{get;private set;}="Elektřina";
 public bool CanEdit=>User.IsInRole("Administrator")||User.HasClaim("permission","ElekConfig");
 public IActionResult OnGet()=>Run(null);
 public IActionResult OnPostAdd()=>Run("add");
 public IActionResult OnPostUpdate()=>Run("update");
 public IActionResult OnPostRemove()=>Run("remove");
 private IActionResult Run(string? action){
  if(action is not null&&!CanEdit)return Forbid();
  if(Kind is null)return NotFound();
  try{Heading=ElectricityGroupCatalog.Definition(Kind).Title;}catch(ArgumentException){return NotFound();}
  try{
   Snapshot=editor.Load(Kind,Id);Data=configuration.Load(Kind,Id);
   if(action is null){Version=Snapshot.Version;if(CanEdit&&!string.IsNullOrWhiteSpace(Search))Candidates=editor.Search(Kind,Id,Search);return Page();}
   if(!ModelState.IsValid){Response.StatusCode=400;return Page();}
   if(action=="add")editor.Add(Kind,Id,ChildId,Version??"");
   else if(action=="update")editor.Update(Kind,Id,ChildId,Coefficient,Recalculate,Version??"");
   else {if(!ConfirmRemoval)throw new ArgumentException("Pro odebrání položky zaškrtněte potvrzení.");editor.Remove(Kind,Id,ChildId,Version??"");}
   TempData["CompositionSaved"]="Složení bylo uloženo.";return RedirectToPage(new{Kind,Id});
  }catch(KeyNotFoundException){return NotFound();}
  catch(GroupConfigurationConflictException){Response.StatusCode=409;ModelState.AddModelError("","Konfigurace nebo složení se mezitím změnily. Znovu načtěte stránku.");return Page();}
  catch(ArgumentException ex){Response.StatusCode=400;ModelState.AddModelError("",ex.Message);return Page();}
  catch(Exception ex)when(ex is InvalidDataException or IOException or System.Data.OleDb.OleDbException or InvalidOperationException or UnauthorizedAccessException or PlatformNotSupportedException or OverflowException or FormatException or InvalidCastException){logger.LogError(ex,"Složení {Kind}/{Id} nelze upravit",Kind,Id);Response.StatusCode=503;ModelState.AddModelError("","Složení není dostupné. Podrobnosti jsou ve výstupu aplikace.");return Page();}
 }
}
