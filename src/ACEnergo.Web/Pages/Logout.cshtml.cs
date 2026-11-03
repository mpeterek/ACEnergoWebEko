using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace ACEnergo.Web.Pages;
public class LogoutModel : PageModel { public async Task<IActionResult> OnPostAsync() { await HttpContext.SignOutAsync("Cookies"); return RedirectToPage("/Login"); } }
