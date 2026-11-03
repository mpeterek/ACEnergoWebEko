using ACEnergo.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
namespace ACEnergo.Web.Pages;
[AllowAnonymous, EnableRateLimiting("login")]
public class LoginModel(AccessStore store, ILogger<LoginModel> logger, IWebHostEnvironment environment, DatabaseCatalog catalog, IConfiguration configuration) : PageModel
{
    [BindProperty] public string Username { get; set; } = "";
    [BindProperty] public string Password { get; set; } = "";
    public string? DiagnosticPath { get; private set; }
    public string? DiagnosticProvider { get; private set; }
    public string? DiagnosticProcess { get; private set; }
    public string? DiagnosticException { get; private set; }
    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(Username) || Username.Length > 255 || (Password?.Length ?? 0) > 255) { ModelState.AddModelError("", "Zkontrolujte přihlašovací údaje."); return Page(); }
        try {
            var principal = store.Authenticate(Username, Password ?? "");
            if (principal is null) { ModelState.AddModelError("", "Nesprávné jméno nebo heslo."); return Page(); }
            await HttpContext.SignInAsync("Cookies", principal);
            return LocalRedirect("~/");
        } catch (Exception ex) when (ex is PlatformNotSupportedException or System.Data.OleDb.OleDbException or IOException or UnauthorizedAccessException or InvalidOperationException) {
            logger.LogError(ex, "Přihlášení MDB selhalo. Proces: {Architecture}; provider: {Provider}; reference: {Reference}",
                System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture,
                configuration["Access:Provider"] ?? "Microsoft.ACE.OLEDB.16.0", HttpContext.TraceIdentifier);
            ModelState.AddModelError("", AccessFailure.Describe(ex));
            if (environment.IsDevelopment())
            {
                try { DiagnosticPath = catalog.Resolve("ACWebEkoLogin.mdb"); }
                catch (Exception pathError) when (pathError is ArgumentException or InvalidOperationException or NotSupportedException)
                { DiagnosticPath = "Neplatná konfigurace Access:Directory."; }
                DiagnosticProvider = configuration["Access:Provider"] ?? "Microsoft.ACE.OLEDB.16.0";
                DiagnosticProcess = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString();
                DiagnosticException = $"{ex.GetType().Name} (0x{ex.HResult:X8})";
            }
            ModelState.AddModelError("", "Přihlašovací databáze není dostupná. Správce musí ověřit nastavení MDB a ovladače na Windows.");
            return Page();
        }
    }
}
