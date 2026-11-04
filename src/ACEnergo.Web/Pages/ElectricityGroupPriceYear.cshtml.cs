using ACEnergo.Web.Domain;
using ACEnergo.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace ACEnergo.Web.Pages;
[Authorize(Policy = "ElekView")]
public class ElectricityGroupPriceYearModel(IElectricityTypePriceStore prices, IElectricityGroupConfiguration groups, ILogger<ElectricityGroupPriceYearModel> logger) : ElectricityTypePricePage(prices, groups, logger)
{
    public TypePriceSelection[] Months => Enumerable.Range(1, 12).Select(m => ElectricityTypePrices.Select(Data?.History ?? [], new DateOnly(Month.Year, m, 1))).ToArray();
    public IActionResult OnGet() => Load();
}
