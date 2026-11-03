using ACEnergo.Web.Domain;
using ACEnergo.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Globalization;
namespace ACEnergo.Web.Pages;
[Authorize(Policy = "ElekView")]
public class ElectricityPriceYearModel(IElectricityPriceStore store, ILogger<ElectricityPriceYearModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true)] public int MeterId { get; set; }
    [BindProperty(SupportsGet = true)] public string? Period { get; set; }
    public PriceSnapshot[] Months { get; private set; } = [];
    public IActionResult OnGet()
    {
        if (Period is null && !Request.Query.ContainsKey(nameof(Period)))
        { Period = DateTime.Today.ToString("yyyy-MM", CultureInfo.InvariantCulture); ModelState.Remove(nameof(Period)); }
        if (!ElectricityTariffs.TryMonth(Period, out var month)) return BadRequest("Neplatné období; použijte RRRR-MM.");
        try { Months = Enumerable.Range(1, 12).Select(m => store.Load(MeterId, new DateOnly(month.Year, m, 1))).ToArray(); return Page(); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException or PlatformNotSupportedException or System.Data.OleDb.OleDbException or InvalidOperationException or FormatException or InvalidCastException)
        {
            logger.LogError(ex, "Načtení cen selhalo pro měření {MeterId}, období {Period}", MeterId, Period);
            Response.StatusCode = 503;
            ModelState.AddModelError("", "Ceník není dostupný nebo obsahuje neplatná data. Podrobnosti jsou ve výstupu aplikace.");
            return Page();
        }
    }
}
