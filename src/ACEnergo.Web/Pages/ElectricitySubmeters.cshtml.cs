using ACEnergo.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace ACEnergo.Web.Pages;
[Authorize(Policy="ElekView")]
public class ElectricitySubmetersModel(IElectricitySubmeterStore store,ILogger<ElectricitySubmetersModel> logger):PageModel
{
    [BindProperty(SupportsGet=true)]public int? Station{get;set;}
    [BindProperty(SupportsGet=true)]public string? InputType{get;set;}
    [BindProperty(SupportsGet=true)]public string? Search{get;set;}
    [BindProperty(SupportsGet=true)]public int AfterId{get;set;}
    public IActionResult OnGet(){
        if(!ModelState.IsValid)return BadRequest("Neplatný filtr měření.");
        try{ElectricitySubmeterStore.Validate(Station,InputType,Search,AfterId);}catch(ArgumentException ex){return BadRequest(ex.Message);}
        ViewData["Station"]=Station;ViewData["InputType"]=InputType;ViewData["Search"]=Search;ViewData["AfterId"]=AfterId;
        return Page();
    }
    public IActionResult OnGetTree(){
        Response.Headers["Cache-Control"]="no-store";
        if(!ModelState.IsValid)return BadRequest("Neplatný filtr měření.");
        try{return new JsonResult(store.Browse(Station,InputType,Search,AfterId));}
        catch(ArgumentException ex){return BadRequest(ex.Message);}
        catch(Exception ex)when(ex is InvalidDataException or IOException or UnauthorizedAccessException or InvalidOperationException or PlatformNotSupportedException or System.Data.OleDb.OleDbException){logger.LogError(ex,"Výběr podružných měření selhal");return StatusCode(503,"Seznam měření není dostupný. Zkuste načtení znovu.");}
    }
}
