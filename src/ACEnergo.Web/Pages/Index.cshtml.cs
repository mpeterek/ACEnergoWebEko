using ACEnergo.Web.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace ACEnergo.Web.Pages;
public class IndexModel(DatabaseCatalog catalog) : PageModel {
 public string[] Months { get; private set; } = [];
 public string? Period { get; private set; }
 public string? Error { get; private set; }
 public (string Name,string Right)[] Modules => [("Elektřina","ElekView"),("Plyn","PlynView"),("Voda","VodaView"),("Teplo","TeploView"),("Ostatní","OstatniView")];
 public void OnGet(string? period) { Months=catalog.AvailableMonths(); if(period is not null && !Months.Contains(period,StringComparer.Ordinal)){Error="Vybraná měsíční databáze není dostupná."; Response.StatusCode=400;} else Period=period ?? Months.FirstOrDefault(); }
}
