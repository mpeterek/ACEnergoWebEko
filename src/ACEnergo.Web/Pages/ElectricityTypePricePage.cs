using ACEnergo.Web.Domain;
using ACEnergo.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace ACEnergo.Web.Pages;

public abstract class ElectricityTypePricePage(IElectricityTypePriceStore prices, IElectricityGroupConfiguration groups, ILogger logger) : PageModel
{
    protected IElectricityTypePriceStore Prices => prices;
    [BindProperty(SupportsGet = true)] public string? Kind { get; set; }
    [BindProperty(SupportsGet = true)] public int Id { get; set; }
    [BindProperty(SupportsGet = true)] public string? Period { get; set; }
    public DateOnly Month { get; private set; }
    public GroupConfiguration? Group { get; private set; }
    public TypePriceSnapshot? Data { get; private set; }
    public TypePriceSelection Selection => ElectricityTypePrices.Select(Data?.History ?? [], Month);
    public bool CanEdit => User.IsInRole("Administrator") || User.HasClaim("permission", "ElekConfig");
    public string Unit => "Kč / " + (string.IsNullOrWhiteSpace(Data?.WorkUnit) ? "neuvedená jednotka" : Data.WorkUnit);
    protected IActionResult Load(Action? loaded = null)
    {
        if (Kind is null) return NotFound();
        try { ElectricityGroupCatalog.Definition(Kind); } catch (ArgumentException) { return NotFound(); }
        if (Period is null && !Request.Query.ContainsKey(nameof(Period)) && !Request.HasFormContentType)
        { Period = DateTime.Today.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture); ModelState.Remove(nameof(Period)); }
        if (!ElectricityTariffs.TryMonth(Period, out var month)) return BadRequest("Neplatné období; použijte RRRR-MM.");
        Month = month;
        try
        {
            Group = groups.Load(Kind, Id);
            ViewData["WorkspaceData"] = Group;
            Data = prices.Load(Kind, Id);
            loaded?.Invoke();
            return Page();
        }
        catch (KeyNotFoundException) { Response.StatusCode = 404; ModelState.AddModelError("", "Vybrané měření nebo jeho elektrický typ nebyly nalezeny."); return Page(); }
        catch (GroupConfigurationConflictException) { Response.StatusCode = 409; ModelState.AddModelError("", "Ceník nebo typ vstupu se mezitím změnil. Načtěte stránku znovu a zopakujte změnu."); return Page(); }
        catch (ArgumentException ex) { Response.StatusCode = 400; ModelState.AddModelError("", ex.Message); return Page(); }
        catch (Exception ex) when (ex is InvalidDataException or IOException or InvalidOperationException or UnauthorizedAccessException or PlatformNotSupportedException or System.Data.OleDb.OleDbException or FormatException or OverflowException or InvalidCastException)
        { logger.LogError(ex, "Ceny typu elektřiny {Kind}/{Id} nejsou dostupné", Kind, Id); Response.StatusCode = 503; ModelState.AddModelError("", "Ceník není dostupný nebo obsahuje neplatná data. Podrobnosti jsou ve výstupu aplikace."); return Page(); }
    }
}
