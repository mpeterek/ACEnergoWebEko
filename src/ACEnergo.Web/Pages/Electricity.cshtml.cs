using ACEnergo.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace ACEnergo.Web.Pages;
[Authorize(Policy="ElekView")]
public class ElectricityModel(AccessStore store, ILogger<ElectricityModel> logger) : PageModel {
 public IReadOnlyList<AccessStore.ElectricityMeter> Meters {get;private set;} = [];
 public string? Error {get;private set;}
 public void OnGet() {
  try { Meters=store.ElectricityMeters(); }
  catch(Exception ex) when(ex is PlatformNotSupportedException or System.Data.OleDb.OleDbException or IOException or UnauthorizedAccessException or InvalidOperationException) {
   logger.LogError("Čtení hlavních měření selhalo: {Type}",ex.GetType().Name);
   Response.StatusCode=503; Error="Konfigurační databáze není dostupná. Ověřte Windows, nastavení MDB a ovladač Access.";
  }
 }
}
