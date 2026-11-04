using ACEnergo.Web.Services;
using Microsoft.AspNetCore.Mvc;
namespace ACEnergo.Web.ViewComponents;
public sealed record GroupTreeModel(string Kind,int SelectedId,ElectricityGroup[] Groups,GroupConfiguration? Selected,string? Error);
public class GroupTreeViewComponent(IElectricityGroupCatalog catalog,ILogger<GroupTreeViewComponent> logger):ViewComponent {
 public IViewComponentResult Invoke(string kind,int selectedId,GroupConfiguration? selected){
  try{return View(new GroupTreeModel(kind,selectedId,catalog.Load(kind),selected,null));}
  catch(Exception ex)when(ex is IOException or UnauthorizedAccessException or InvalidOperationException or PlatformNotSupportedException or System.Data.OleDb.OleDbException){
   logger.LogError(ex,"Strom měření {Kind} není dostupný",kind);
   return View(new GroupTreeModel(kind,selectedId,[],selected,"Seznam měření není dostupný. Podrobnosti jsou ve výstupu aplikace."));
  }
 }
}
