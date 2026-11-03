using System.Data.OleDb;
using System.Globalization;
using ACEnergo.Web.Domain;
using ACEnergo.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ACEnergo.Web.Pages;

[Authorize(Policy = "ElekView")]
public class ElectricityHolidaysModel(IElectricityHolidayStore store, ILogger<ElectricityHolidaysModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true)] public int MeterId { get; set; }
    [BindProperty(SupportsGet = true)] public string? Period { get; set; }
    [BindProperty(SupportsGet = true)] public string? NewDate { get; set; } = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    [BindProperty] public string[] SelectedDates { get; set; } = [];
    [BindProperty] public string Version { get; set; } = "";
    [TempData] public string? Notice { get; set; }
    public HolidaySnapshot? Snapshot { get; private set; }
    public bool CanEdit => User.Identity?.IsAuthenticated == true && (User.IsInRole("Administrator") || User.HasClaim("permission", "ElekConfig"));

    private bool ValidContext() => string.IsNullOrEmpty(Period) || ElectricityTariffs.TryMonth(Period, out _);
    public IActionResult OnGet()
    {
        if (!ValidContext()) return BadRequest("Neplatné období pro návrat do tarifů.");
        try
        {
            Snapshot = store.Load(MeterId);
            Version = Snapshot.Version;
            return Page();
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (Exception ex) when (IsDataError(ex)) { Report(ex); return Page(); }
    }

    public IActionResult OnPostAdd()
    {
        if (!CanEdit) return Forbid();
        if (!ValidContext()) return BadRequest("Neplatné období pro návrat do tarifů.");
        try
        {
            Snapshot = store.Load(MeterId);
            if (!ModelState.IsValid) return InvalidForm();
            if (!ElectricityHolidays.TryDate(NewDate, out var date))
            { ModelState.AddModelError("", "Zadejte platné datum svátku."); return InvalidForm(); }
            try { ElectricityHolidays.ValidateVersion(Version); }
            catch (ArgumentException ex) { ModelState.AddModelError("", ex.Message); return InvalidForm(); }
            var added = store.Add(MeterId, date, Version);
            Notice = added ? $"Svátek {date:dd.MM.yyyy} byl přidán." : $"Datum {date:dd.MM.yyyy} už mezi svátky existuje. Nebylo změněno.";
            return RedirectToPage(new { MeterId, Period, NewDate });
        }
        catch (HolidayConflictException ex) { return ConflictPage(ex); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (Exception ex) when (IsDataError(ex)) { Report(ex); return Page(); }
    }

    public IActionResult OnPostRemove()
    {
        if (!CanEdit) return Forbid();
        if (!ValidContext()) return BadRequest("Neplatné období pro návrat do tarifů.");
        try
        {
            Snapshot = store.Load(MeterId);
            if (!ModelState.IsValid) return InvalidForm();
            DateOnly[] dates;
            try
            {
                ElectricityHolidays.ValidateVersion(Version);
                dates = ElectricityHolidays.Selection(SelectedDates);
            }
            catch (ArgumentException ex) { ModelState.AddModelError("", ex.Message); return InvalidForm(); }
            var removed = store.Remove(MeterId, dates, Version);
            Notice = removed == 0 ? "Vybraná data nebyla v seznamu nalezena." : $"Odstraněno záznamů svátků: {removed}.";
            return RedirectToPage(new { MeterId, Period, NewDate });
        }
        catch (HolidayConflictException ex) { return ConflictPage(ex); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (Exception ex) when (IsDataError(ex)) { Report(ex); return Page(); }
    }

    private IActionResult InvalidForm() { Response.StatusCode = 400; return Page(); }
    private IActionResult ConflictPage(HolidayConflictException exception)
    { Response.StatusCode = 409; ModelState.AddModelError("", exception.Message); return Page(); }
    private static bool IsDataError(Exception exception) => exception is InvalidDataException or PlatformNotSupportedException or OleDbException or IOException or UnauthorizedAccessException or InvalidOperationException or FormatException or OverflowException;
    private void Report(Exception exception)
    {
        logger.LogError(exception, "Svátky: databázová operace selhala pro měření {MeterId}", MeterId);
        Response.StatusCode = 503;
        ModelState.AddModelError("", exception is InvalidDataException
            ? "Seznam svátků obsahuje neplatná data. Operace byla zrušena; podrobnosti jsou ve výstupu aplikace."
            : AccessFailure.Describe(exception));
    }
}
