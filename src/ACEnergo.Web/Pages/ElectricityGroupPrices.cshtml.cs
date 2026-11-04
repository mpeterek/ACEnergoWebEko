using ACEnergo.Web.Domain;
using ACEnergo.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace ACEnergo.Web.Pages;
[Authorize(Policy = "ElekView")]
public class ElectricityGroupPricesModel(IElectricityTypePriceStore prices, IElectricityGroupConfiguration groups, ILogger<ElectricityGroupPricesModel> logger) : ElectricityTypePricePage(prices, groups, logger)
{
    [BindProperty] public string[] Values { get; set; } = Enumerable.Repeat("", 6).ToArray();
    [BindProperty] public bool? Tariff { get; set; }
    [BindProperty] public string Version { get; set; } = "";
    public IActionResult OnGet() => Load(() => {
        Version = Data!.Version;
        Values = Selection.Price?.Values.Select(v => v?.ToString(System.Globalization.CultureInfo.GetCultureInfo("cs-CZ")) ?? "").ToArray() ?? Enumerable.Repeat("", 6).ToArray();
        Tariff = Selection.Price?.Tariff;
    });
    public IActionResult OnPost()
    {
        if (!CanEdit) return Forbid();
        var result = Load(() => {
            if (!ModelState.IsValid) { Response.StatusCode = 400; return; }
            Prices.Add(Kind!, Id, Month, Values, Tariff, Version);
            TempData["TypePriceSaved"] = "Ceník byl přidán. Platí pro všechny elektrické vstupy tohoto typu.";
        });
        return Response.StatusCode == 200 && ModelState.IsValid && Data is not null ? RedirectToPage(new { Kind, Id, Period }) : result;
    }
}
