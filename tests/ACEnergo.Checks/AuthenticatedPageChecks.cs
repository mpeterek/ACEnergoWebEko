using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using ACEnergo.Web.Domain;
using ACEnergo.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

internal static class AuthenticatedPageChecks
{
    public static async Task RunAsync()
    {
        using var factory = new Factory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var count = 0;
        void Check(bool condition, string name) { if (!condition) throw new Exception("HTTP: " + name); count++; }
        void Role(string value) { client.DefaultRequestHeaders.Remove("X-Test-Role"); client.DefaultRequestHeaders.Add("X-Test-Role", value); }
        static string Token(string html)
        {
            var match = Regex.Match(html, "<input[^>]*name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
            if (!match.Success) throw new Exception("Missing antiforgery token");
            return WebUtility.HtmlDecode(match.Groups[1].Value);
        }
        async Task<(HttpStatusCode Code, string Html)> Get(string url)
        {
            using var response = await client.GetAsync(url);
            return (response.StatusCode, await response.Content.ReadAsStringAsync());
        }
        async Task<HttpStatusCode> Post(string handler, string version, string token, params (string Key, string Value)[] extra)
        {
            var fields = new List<KeyValuePair<string, string>> { new("Version", version), new("Period", "2026-10") };
            if (token.Length > 0) fields.Add(new("__RequestVerificationToken", token));
            fields.AddRange(extra.Select(pair => new KeyValuePair<string, string>(pair.Key, pair.Value)));
            using var response = await client.PostAsync("/ElectricityHolidays/5?handler=" + handler, new FormUrlEncodedContent(fields));
            return response.StatusCode;
        }

        Check((await Get("/ElectricityHolidays/5")).Code == HttpStatusCode.Unauthorized, "anonymous cannot load holidays");
        Role("none");
        Check((await Get("/ElectricityHolidays/5")).Code == HttpStatusCode.Forbidden, "missing view permission denied");
        Role("view");
        var view = await Get("/ElectricityHolidays/5?Period=2026-10");
        Check(view.Code == HttpStatusCode.OK && view.Html.Contains("2026-11-17"), "viewer loads actual Razor table");
        Check(!view.Html.Contains("data-holiday-select") && !view.Html.Contains("data-holiday-remove"), "viewer has no edit controls");
        Check(view.Html.Contains("&lt;test&gt;") && !view.Html.Contains("<test>"), "database names HTML encoded");
        var before = factory.Holidays.Writes;
        Check(await Post("Add", factory.Holidays.CurrentVersion, Token(view.Html), ("NewDate", "2026-11-18")) == HttpStatusCode.Forbidden && factory.Holidays.Writes == before, "viewer cannot forge add POST");
        Check(await Post("Remove", factory.Holidays.CurrentVersion, Token(view.Html), ("SelectedDates", "2026-11-17")) == HttpStatusCode.Forbidden && factory.Holidays.Writes == before, "viewer cannot forge delete POST");

        Role("config");
        var edit = await Get("/ElectricityHolidays/5?Period=2026-10");
        Check(edit.Code == HttpStatusCode.OK && edit.Html.Contains("data-holiday-select") && edit.Html.Contains("name=\"NewDate\""), "editor gets date and selection controls");
        Check(edit.Html.Contains("/css/pages/holidays.css?v=") && edit.Html.Contains("/js/pages/holidays.js?v="), "external versioned assets");
        Check(!Regex.IsMatch(edit.Html, "\\sstyle\\s*=|<style\\b|\\son[a-z]+\\s*=", RegexOptions.IgnoreCase) &&
              !Regex.IsMatch(edit.Html, "<script(?![^>]*\\bsrc=)[^>]*>", RegexOptions.IgnoreCase), "no inline styles or scripts");
        Check(await Post("Add", factory.Holidays.CurrentVersion, "", ("NewDate", "2026-11-18")) == HttpStatusCode.BadRequest && factory.Holidays.Writes == before, "CSRF missing token rejects actual POST");
        var originalVersion = factory.Holidays.CurrentVersion;
        Check(await Post("Remove", originalVersion, Token(edit.Html)) == HttpStatusCode.BadRequest && factory.Holidays.Writes == before, "empty selection rejects actual POST");
        Check(await Post("Add", originalVersion, Token(edit.Html), ("NewDate", "2026-02-29")) == HttpStatusCode.BadRequest && factory.Holidays.Writes == before, "invalid leap date rejects POST");
        Check(await Post("Add", originalVersion, Token(edit.Html), ("NewDate", "2026-11-18")) == HttpStatusCode.Found && factory.Holidays.Writes == before + 1, "valid add redirects after write");
        var refreshed = await Get("/ElectricityHolidays/5?Period=2026-10");
        Check(refreshed.Html.Contains("datetime=\"2026-11-18\""), "new date visible after redirect");
        Check(await Post("Remove", originalVersion, Token(refreshed.Html), ("SelectedDates", "2026-11-18")) == HttpStatusCode.Conflict && factory.Holidays.Writes == before + 1, "stale deletion returns HTTP 409");
        Check(await Post("Remove", factory.Holidays.CurrentVersion, Token(refreshed.Html), ("SelectedDates", "2026-11-18")) == HttpStatusCode.Found && factory.Holidays.Writes == before + 2, "valid removal redirects after write");
        Check(factory.Holidays.CurrentVersion == originalVersion, "add/remove restore initial list");
        Check(await Post("Add", factory.Holidays.CurrentVersion, Token(refreshed.Html), ("NewDate", "2026-11-18")) == HttpStatusCode.Found, "prepare two-date selection");
        Check(await Post("Remove", factory.Holidays.CurrentVersion, Token(refreshed.Html), ("SelectedDates", "2026-11-17"), ("SelectedDates", "2026-11-18")) == HttpStatusCode.Found && factory.Holidays.Load(5).Dates.Length == 0, "repeated checkbox names bind and remove both dates");
        var empty = await Get("/ElectricityHolidays/5");
        Check(empty.Code == HttpStatusCode.OK && !empty.Html.Contains("data-holiday-remove"), "empty list renders without delete controls");
        Check(await Post("Add", factory.Holidays.CurrentVersion, Token(empty.Html), ("NewDate", "2026-11-17")) == HttpStatusCode.Found && factory.Holidays.CurrentVersion == originalVersion, "add from empty list restores data");
        var writes = factory.Holidays.Writes;
        Check(await Post("Add", factory.Holidays.CurrentVersion, Token(empty.Html), ("NewDate", "2026-11-17")) == HttpStatusCode.Found && factory.Holidays.Writes == writes && factory.Holidays.CurrentVersion == originalVersion, "duplicate date POST does not rewrite stored state");
        var tariff = await Get("/ElectricityTariffs/5?Period=2026-10");
        Check(tariff.Code == HttpStatusCode.OK && Regex.Matches(tariff.Html, "name=\"Cells\\[\\d+\\]\"").Count == 168, "tariff Razor renders 168 editable cells");
        Check(tariff.Html.Contains("/ElectricityHolidays/5") && tariff.Html.Contains("Period=2026-10"), "tariffs link preserves meter and period");
        var defaultTariff = await Get("/ElectricityTariffs/5");
        Check(defaultTariff.Code == HttpStatusCode.OK && !WebUtility.HtmlDecode(defaultTariff.Html).Contains("The Period field is required."), "tariffs open without explicit period with valid default");
        var hiddenPeriod = Regex.Match(defaultTariff.Html, "<input[^>]*name=\"Period\"[^>]*value=\"([^\"]*)\"");
        var defaultPeriod = WebUtility.HtmlDecode(hiddenPeriod.Groups[1].Value);
        Check(hiddenPeriod.Success && ElectricityTariffs.TryMonth(defaultPeriod, out _), "default month is present in actual hidden POST input");
        async Task<HttpStatusCode> PostTariff(string? period)
        {
            var fields = new List<KeyValuePair<string, string>>
            {
                new("Version", new string('A', 64)), new("ConfirmOverwrite", "true"),
                new("__RequestVerificationToken", Token(defaultTariff.Html))
            };
            if (period is not null) fields.Add(new("Period", period));
            var cells = ElectricityTariffs.Defaults(); cells[0] = 2;
            fields.AddRange(cells.Select((value, index) => new KeyValuePair<string, string>($"Cells[{index}]", value.ToString())));
            using var response = await client.PostAsync("/ElectricityTariffs/5", new FormUrlEncodedContent(fields));
            return response.StatusCode;
        }
        Check(await PostTariff(defaultPeriod) == HttpStatusCode.Found && factory.Tariffs.Writes == 1 &&
            factory.Tariffs.LastMonth.ToString("yyyy-MM") == defaultPeriod && factory.Tariffs.FirstCell == 2,
            "save default month without relying on query string");
        Check(await PostTariff(null) == HttpStatusCode.BadRequest && factory.Tariffs.Writes == 1, "missing POST period does not fall back or write");
        Check(await PostTariff("") == HttpStatusCode.BadRequest && factory.Tariffs.Writes == 1, "empty POST period does not write");
        Check(await PostTariff("2026-13") == HttpStatusCode.BadRequest && factory.Tariffs.Writes == 1, "invalid POST period does not write");
        Check((await Get("/ElectricityTariffs/5?Period=")).Code == HttpStatusCode.BadRequest, "explicit empty GET period rejected");
        Role("view"); tariff = await Get("/ElectricityTariffs/5?Period=2026-10");
        Check(tariff.Code == HttpStatusCode.OK && !tariff.Html.Contains("data-tariff-cell"), "viewer tariff grid is read-only");
        var prices = await Get("/ElectricityPrices/5");
        Check(prices.Code == HttpStatusCode.OK && prices.Html.Contains("Cena test") && !WebUtility.HtmlDecode(prices.Html).Contains("The Period field is required."), "prices load without period");
        Check((await Get("/ElectricityPrices/5?Period=2026-10")).Code == HttpStatusCode.OK, "prices exact month");
        Check((await Get("/ElectricityPrices/5?Period=bad")).Code == HttpStatusCode.BadRequest, "prices reject invalid month");
        Check((await Get("/ElectricityPrices/999")).Code == HttpStatusCode.NotFound, "prices unknown meter");
        var yearPrices = await Get("/ElectricityPriceYear/5?Period=2026-10");
        Check(yearPrices.Code == HttpStatusCode.OK && yearPrices.Html.Contains("01/2026") && yearPrices.Html.Contains("12/2026"), "annual prices cover January through December");
        Check((await Get("/ElectricityPriceYear/5?Period=bad")).Code == HttpStatusCode.BadRequest, "annual prices reject invalid period");
        Role("none");
        Check((await Get("/ElectricityPriceYear/5")).Code == HttpStatusCode.Forbidden, "annual prices require view permission");
        Check((await Get("/ElectricityPrices/5")).Code == HttpStatusCode.Forbidden, "prices require view permission");
        Check((await Get("/ElectricityPriceEdit/5?Period=2026-10")).Code == HttpStatusCode.Forbidden, "price edit needs configuration permission");
        Role("config");
        var priceEdit = await Get("/ElectricityPriceEdit/5?Period=2026-10");
        Check(priceEdit.Code == HttpStatusCode.OK && Regex.Matches(priceEdit.Html,"name=\"Values\\[\\d+\\]\"").Count == 28, "all price editor fields render");
        Role("admin");
        Check((await Get("/ElectricityHolidays/5")).Html.Contains("data-holiday-remove"), "administrator has configuration controls");
        Check(!WebUtility.HtmlDecode((await Get("/")).Html).Contains("Analogová"), "analog module excluded even for admin");
        Check((await Get("/ElectricityHolidays/999")).Code == HttpStatusCode.NotFound, "unknown meter 404");
        Console.WriteLine($"PASS: {count} authenticated Razor/HTTP, CSRF and authorization checks (test stores only).");
    }

    // Test-only DI and authentication; no test account or authentication switch is added to the web project.
    private sealed class Factory : WebApplicationFactory<AccessStore>
    {
        public FakeHolidays Holidays { get; } = new();
        public FakeTariffs Tariffs { get; } = new();
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root is not null && !File.Exists(Path.Combine(root.FullName, "ACEnergoWebEko.sln"))) root = root.Parent;
            if (root is null) throw new Exception("Solution root not found");
            builder.UseContentRoot(Path.Combine(root.FullName, "src", "ACEnergo.Web"));
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> { ["AllowedHosts"] = "localhost" }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IElectricityHolidayStore>();
                services.AddSingleton<IElectricityHolidayStore>(Holidays);
                services.RemoveAll<IElectricityPriceEditor>();
                services.AddSingleton<IElectricityPriceEditor, FakePriceEditor>();
                services.RemoveAll<IElectricityPriceStore>();
                services.AddSingleton<IElectricityPriceStore, FakePrices>();
                services.RemoveAll<IElectricityTariffStore>();
                services.AddSingleton<IElectricityTariffStore>(Tariffs);
                services.AddDataProtection().UseEphemeralDataProtectionProvider();
                services.AddLogging(logging => logging.ClearProviders());
                services.AddAuthentication(options =>
                {
                    options.DefaultScheme = "Test";
                    options.DefaultAuthenticateScheme = "Test";
                    options.DefaultChallengeScheme = "Test";
                    options.DefaultForbidScheme = "Test";
                }).AddScheme<AuthenticationSchemeOptions, TestAuth>("Test", _ => { });
            });
        }
    }
    private sealed class TestAuth(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-Test-Role", out var role)) return Task.FromResult(AuthenticateResult.NoResult());
            var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "web-test"), new(ClaimTypes.Name, "web-test") };
            if (role == "view" || role == "config") claims.Add(new("permission", "ElekView"));
            if (role == "config") claims.Add(new("permission", "ElekConfig"));
            if (role == "admin") claims.Add(new(ClaimTypes.Role, "Administrator"));
            return Task.FromResult(AuthenticateResult.Success(new(new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")), "Test")));
        }
    }
    private sealed class FakeHolidays : IElectricityHolidayStore
    {
        private readonly List<HolidayRecord> rows = [new(new DateTime(2026, 11, 17), "_")];
        public int Writes { get; private set; }
        public string CurrentVersion => ElectricityHolidays.Version(rows);
        public HolidaySnapshot Load(int meterId) => meterId == 5 ? new(5, "Měření <test>", rows.ToArray(), CurrentVersion) : throw new KeyNotFoundException();
        private void Validate(int meterId, string version)
        { _ = Load(meterId); if (version != CurrentVersion) throw new HolidayConflictException(); }
        public bool Add(int meterId, DateOnly date, string expectedVersion)
        {
            Validate(meterId, expectedVersion);
            if (rows.Any(row => DateOnly.FromDateTime(row.At) == date)) return false;
            rows.Add(new(date.ToDateTime(TimeOnly.MinValue), "_")); Writes++; return true;
        }
        public int Remove(int meterId, IReadOnlyList<DateOnly> dates, string expectedVersion)
        {
            Validate(meterId, expectedVersion);
            var removed = rows.RemoveAll(row => dates.Contains(DateOnly.FromDateTime(row.At)));
            Writes++; return removed;
        }
    }
    private sealed class FakePriceEditor : IElectricityPriceEditor {
        public EditablePrices Load(int id,DateOnly month)=>new("Test",Enumerable.Repeat("0",28).ToArray(),new string('A',64));
        public void Save(int id,DateOnly month,string[] values,string version)=>ElectricityPriceEditor.Parse(values);
    }
    private sealed class FakePrices : IElectricityPriceStore
    {
        public PriceSnapshot Load(int meterId, DateOnly month) => meterId == 5
            ? new("Test", month, false, [new("Cena test", "123,45")]) : throw new KeyNotFoundException();
    }
    private sealed class FakeTariffs : IElectricityTariffStore
    {
        public TariffSnapshot Load(int meterId, DateOnly month) => new(meterId, "Test", month, ElectricityTariffs.Defaults(), false, new string('A', 64));
        public int Writes { get; private set; }
        public DateOnly LastMonth { get; private set; }
        public int FirstCell { get; private set; }
        public void Save(int meterId, DateOnly month, IReadOnlyList<int> cells, string expectedVersion)
        {
            if (meterId != 5 || expectedVersion != new string('A', 64)) throw new Exception("Wrong tariff target/version");
            ElectricityTariffs.Validate(cells);
            Writes++; LastMonth = month; FirstCell = cells[0];
        }
    }
}
