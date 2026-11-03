using ACEnergo.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
namespace ACEnergo.Web.Pages;
[AllowAnonymous, EnableRateLimiting("login")]
public class LoginModel(AccessStore store, ILogger<LoginModel> logger) : PageModel
{
    [BindProperty] public string Username { get; set; } = "";
    [BindProperty] public string Password { get; set; } = "";
    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(Username) || Username.Length > 255 || (Password?.Length ?? 0) > 255) { ModelState.AddModelError("", "Zkontrolujte přihlašovací údaje."); return Page(); }
        try {
            var principal = store.Authenticate(Username, Password ?? "");
            if (principal is null) { ModelState.AddModelError("", "Nesprávné jméno nebo heslo."); return Page(); }
            await HttpContext.SignInAsync("Cookies", principal);
            return LocalRedirect("~/");
        } catch (Exception ex) when (ex is PlatformNotSupportedException or System.Data.OleDb.OleDbException or IOException or InvalidOperationException) {
            logger.LogError("Přihlášení selhalo: {Type}", ex.GetType().Name);
            ModelState.AddModelError("", "Přihlašovací databáze není dostupná. Správce musí ověřit nastavení MDB a ovladače na Windows.");
            return Page();
        }
    }
}
