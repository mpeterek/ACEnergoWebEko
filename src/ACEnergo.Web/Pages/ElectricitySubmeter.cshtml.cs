using ACEnergo.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace ACEnergo.Web.Pages;
[Authorize(Policy="ElekView")]
public class ElectricitySubmeterModel(IElectricitySubmeterStore store,ILogger<ElectricitySubmeterModel> logger):PageModel
{
    [BindProperty(SupportsGet=true)]public int Id{get;set;}
    public SubmeterDefinition? Data{get;private set;}
    public IActionResult OnGet(){
        try{Data=store.Load(Id);ViewData["Submeter"]=Data;return Page();}
        catch(KeyNotFoundException){Response.StatusCode=404;ModelState.AddModelError("","Archivovaný elektrický vstup nebyl nalezen.");return Page();}
        catch(Exception ex)when(ex is InvalidDataException or IOException or InvalidOperationException or UnauthorizedAccessException or PlatformNotSupportedException or System.Data.OleDb.OleDbException){logger.LogError(ex,"Podružné měření {Id} není dostupné",Id);Response.StatusCode=503;ModelState.AddModelError("","Konfigurace vstupu není dostupná. Podrobnosti jsou ve výstupu aplikace.");return Page();}
    }
}
