using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace ACEnergo.Web.Pages;
[AllowAnonymous]
public class NotFoundModel(ILogger<NotFoundModel> logger):PageModel {
 public void OnGet(){
  Response.StatusCode=404;
  logger.LogWarning("PAGE-NOT-FOUND: běžící aplikace nemá stránku pro cestu {Path}",Request.Path);
 }
}
