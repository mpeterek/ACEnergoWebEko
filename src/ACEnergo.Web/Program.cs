using ACEnergo.Web.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages(options => options.Conventions.AuthorizeFolder("/"));
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options => {
    options.LoginPath = "/Login"; options.AccessDeniedPath = "/Denied";
    options.Cookie.HttpOnly = true; options.Cookie.SameSite = SameSiteMode.Lax;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
});
builder.Services.AddAuthorization(options => options.AddPolicy("ElekView", policy => policy.RequireAssertion(context => context.User.IsInRole("Administrator") || context.User.HasClaim("permission", "ElekView"))));
builder.Services.AddRateLimiter(options => {
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", context => !HttpMethods.IsPost(context.Request.Method)
        ? System.Threading.RateLimiting.RateLimitPartition.GetNoLimiter("read")
        : System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "local", _ => new() { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
builder.Services.AddSingleton<DatabaseCatalog>();
builder.Services.AddScoped<AccessStore>();
builder.Services.AddScoped<IElectricityTariffStore, ElectricityTariffStore>();
builder.Services.AddScoped<IElectricityHolidayStore, ElectricityHolidayStore>();
builder.Services.AddScoped<IElectricityPriceStore, ElectricityPriceStore>();
builder.Services.AddScoped<IElectricityPriceEditor, ElectricityPriceEditor>();
builder.Services.AddScoped<IElectricityMeterConfiguration, ElectricityMeterConfiguration>();
builder.Services.AddScoped<IElectricityGroupCatalog, ElectricityGroupCatalog>();
builder.Services.AddAuthorization(options => options.AddPolicy("ElekConfig", policy => policy.RequireAuthenticatedUser().RequireAssertion(context => context.User.IsInRole("Administrator") || context.User.HasClaim("permission", "ElekConfig"))));
var app = builder.Build();
if (!app.Environment.IsDevelopment()) { app.UseExceptionHandler("/Error"); app.UseHsts(); app.UseHttpsRedirection(); }
app.UseStaticFiles(); app.UseRouting(); app.UseRateLimiter(); app.UseAuthentication(); app.UseAuthorization();
app.MapGet("/health/live", () => Results.Ok(new { status = "running" })).AllowAnonymous();
app.MapGet("/Default.asp", () => Results.Redirect("/"));
app.MapGet("/Servis/Logon/LogonPage.asp", () => Results.Redirect("/Login")).AllowAnonymous();
app.MapRazorPages(); app.Run();
public partial class Program { }
