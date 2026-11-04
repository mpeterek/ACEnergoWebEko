using ACEnergo.Web.Services;
using Microsoft.AspNetCore.Mvc;
namespace ACEnergo.Web.ViewComponents;
public sealed record SubmeterTreeModel(SubmeterList List,SubmeterDefinition? Selected,int? Station,string? Type,string? Search,string? Error);
public class SubmeterTreeViewComponent(IElectricitySubmeterStore store,ILogger<SubmeterTreeViewComponent> logger):ViewComponent
{
    public IViewComponentResult Invoke(SubmeterDefinition? selected,bool selectedOnly=false,int? station=null,string? inputType=null,string? search=null,int afterId=0)
    {
        if(selectedOnly)return View(new SubmeterTreeModel(new(selected is null?[]:[selected.Input],false,[],[]),selected,station,inputType,search,null));
        station??=selected?.Input.Station;
        try{return View(new SubmeterTreeModel(store.Browse(station,inputType,search,afterId),selected,station,inputType,search,null));}
        catch(Exception ex)when(ex is InvalidDataException or IOException or InvalidOperationException or UnauthorizedAccessException or PlatformNotSupportedException or System.Data.OleDb.OleDbException or ArgumentException){
            logger.LogError(ex,"Seznam podružných měření není dostupný");
            return View(new SubmeterTreeModel(new([],false,[],[]),selected,station,inputType,search,"Seznam měření není dostupný. Podrobnosti jsou ve výstupu aplikace."));
        }
    }
}
