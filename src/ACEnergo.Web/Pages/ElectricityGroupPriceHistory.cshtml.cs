using ACEnergo.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace ACEnergo.Web.Pages;
[Authorize(Policy = "ElekView")]
public class ElectricityGroupPriceHistoryModel(IElectricityTypePriceStore prices, IElectricityGroupConfiguration groups, ILogger<ElectricityGroupPriceHistoryModel> logger) : ElectricityTypePricePage(prices, groups, logger)
{
    public IActionResult OnGet() => Load();
}
