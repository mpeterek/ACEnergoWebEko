using System.Data.OleDb;
using System.Globalization;
using ACEnergo.Web.Domain;
using ACEnergo.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ACEnergo.Web.Pages;

[Authorize(Policy = "ElekView")]
public class ElectricityTariffsModel(IElectricityTariffStore store, ILogger<ElectricityTariffsModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true)] public int MeterId { get; set; }
    [BindProperty(SupportsGet = true)] public string? Period { get; set; }
    [BindProperty] public int[] Cells { get; set; } = [];
    [BindProperty] public bool ConfirmOverwrite { get; set; }
    [BindProperty] public string Version { get; set; } = "";
    [TempData] public string? Success { get; set; }
    public TariffSnapshot? Snapshot { get; private set; }
    public bool CanEdit => User.Identity?.IsAuthenticated == true && (User.IsInRole("Administrator") || User.HasClaim("permission", "ElekConfig"));

    public IActionResult OnGet()
    {
        // Chybějící query parametr je při otevření stránky očekávaný. Výchozí
        // období se používá pouze při GET; POST bez období se nesmí uložit.
        if (Period is null && !Request.Query.ContainsKey(nameof(Period)))
        {
            Period = DateTime.Today.ToString("yyyy-MM", CultureInfo.InvariantCulture);
            ModelState.Remove(nameof(Period));
        }
        if (!ElectricityTariffs.TryMonth(Period, out var month)) return BadRequest("Neplatné období; použijte RRRR-MM.");
        try
        {
            Snapshot = store.Load(MeterId, month);
            Cells = Snapshot.Cells.ToArray();
            Version = Snapshot.Version;
            return Page();
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (Exception ex) when (IsDataError(ex)) { Report(ex); return Page(); }
    }

    public IActionResult OnPost()
    {
        // Kontrola je na serveru; skrytí tlačítka samo o sobě není autorizace.
        if (!CanEdit) return Forbid();
        if (!ElectricityTariffs.TryMonth(Period, out var month)) return BadRequest("Neplatné období; použijte RRRR-MM.");
        try
        {
            Snapshot = store.Load(MeterId, month);
            if (!ModelState.IsValid) return InvalidForm();
            try { ElectricityTariffs.Validate(Cells); }
            catch (ArgumentException ex) { ModelState.AddModelError("", ex.Message); return InvalidForm(); }
            if (string.IsNullOrEmpty(Version) || Version.Length != 64 || !Version.All(Uri.IsHexDigit))
            { ModelState.AddModelError("", "Chybí platná verze tarifu. Znovu načtěte stránku."); return InvalidForm(); }
            if (!ConfirmOverwrite)
            { ModelState.AddModelError("", "Potvrďte přepsání tarifů až do konce roku."); return InvalidForm(); }
            store.Save(MeterId, month, Cells, Version);
            Success = $"Tarify měření {Snapshot.MeterName} byly uloženy od {month:MM/yyyy} do 12/{month.Year}.";
            return RedirectToPage(new { MeterId, Period });
        }
        catch (TariffConflictException ex) { Response.StatusCode = 409; ModelState.AddModelError("", ex.Message); return Page(); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (Exception ex) when (IsDataError(ex)) { Report(ex); return Page(); }
    }

    private IActionResult InvalidForm()
    {
        Response.StatusCode = 400;
        // Poškozený/neúplný POST nesmí shodit vykreslení 168 buněk.
        if (Cells is null || Cells.Length != ElectricityTariffs.CellCount || Cells.Any(value => value is < 0 or > 2)) Cells = Snapshot!.Cells.ToArray();
        return Page();
    }

    private static bool IsDataError(Exception exception) => exception is InvalidDataException or PlatformNotSupportedException or OleDbException or IOException or UnauthorizedAccessException or InvalidOperationException or FormatException or OverflowException;
    private void Report(Exception exception)
    {
        logger.LogError(exception, "Tarify: databázová operace selhala pro měření {MeterId}, období {Period}", MeterId, Period);
        Response.StatusCode = 503;
        ModelState.AddModelError("", exception is InvalidDataException
            ? "Uložené tarify jsou neúplné nebo neplatné. Operace byla zrušena; podrobnosti jsou ve výstupu aplikace."
            : AccessFailure.Describe(exception));
    }
}
