using System.Security.Claims;
using ACEnergo.Web.Domain;
using ACEnergo.Web.Pages;
using ACEnergo.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;

internal static class TariffChecks
{
    public static void Run()
    {
        var count = 0;
        void Check(bool condition, string name) { if (!condition) throw new Exception(name); count++; }
        void Invalid(Action action) { try { action(); } catch (ArgumentException) { count++; return; } throw new Exception("Invalid tariff accepted"); }
        var month = new DateOnly(2026, 10, 1);
        var cells = ElectricityTariffs.Defaults();
        Check(cells.Length == 168, "168 cells");
        for (var day = 0; day < 7; day++)
            for (var hour = 0; hour < 24; hour++)
            {
                var expected = day >= 5 ? 2 : (hour >= 5 && hour <= 9) || (hour >= 16 && hour <= 19) ? 1 : 0;
                Check(cells[day * 24 + hour] == expected, $"Legacy default {day}/{hour}");
            }
        Check(ElectricityTariffs.WriteMonths(month).SequenceEqual(new[] { month, month.AddMonths(1), month.AddMonths(2) }), "October to December only");
        Check(ElectricityTariffs.WriteMonths(new(2026, 1, 15)).Length == 12, "January twelve months");
        Check(ElectricityTariffs.WriteMonths(new(2026, 12, 1)).Length == 1, "December one month");
        Check(ElectricityTariffs.TryMonth("2026-02", out var february) && february == new DateOnly(2026, 2, 1), "Parse month");
        Check(!ElectricityTariffs.TryMonth("2026-13", out _) && !ElectricityTariffs.TryMonth("../../db", out _) && !ElectricityTariffs.TryMonth("9999-12", out _), "Reject invalid month");
        Invalid(() => ElectricityTariffs.Validate([]));
        Invalid(() => ElectricityTariffs.Validate(null));
        Invalid(() => ElectricityTariffs.Validate(Enumerable.Repeat(3, 168).ToArray()));
        Invalid(() => ElectricityTariffs.Validate(Enumerable.Repeat(-1, 168).ToArray()));
        var defaulted = ElectricityTariffs.ReadMonth([], month);
        Check(defaulted.IsDefault && defaulted.Cells.SequenceEqual(cells), "Missing month explicit defaults");
        var rows = Enumerable.Range(1, 7).Select(day => new TariffRecord(month.ToDateTime(TimeOnly.MinValue), day, ElectricityTariffs.Days[day - 1], Enumerable.Repeat(day % 3, 24).ToArray())).ToArray();
        var read = ElectricityTariffs.ReadMonth(rows.Reverse(), month);
        Check(!read.IsDefault && read.Cells[0] == 1 && read.Cells[6 * 24] == 1, "Day order respected");
        try { ElectricityTariffs.ReadMonth(rows.Take(6), month); throw new Exception("Partial data accepted"); } catch (InvalidDataException) { count++; }
        try { ElectricityTariffs.ReadMonth(rows.Concat([rows[0]]), month); throw new Exception("Duplicate day accepted"); } catch (InvalidDataException) { count++; }
        Check(ElectricityTariffs.Version(rows) == ElectricityTariffs.Version(rows.Reverse()), "Version independent of row order");
        Check(ElectricityTariffs.Version(rows) != ElectricityTariffs.Version(rows.Concat([rows[0] with { EffectiveAt = month.AddMonths(1).ToDateTime(TimeOnly.MinValue) }])), "Future month part of version");
        Check(ElectricityTariffs.ReadMonth(rows, month.AddMonths(1)).IsDefault, "No inheritance from previous month");

        var store = new FakeStore(new(5, "Test", month, cells, false, ElectricityTariffs.Version(rows)));
        ElectricityTariffsModel Page(bool edit)
        {
            var claims = new List<Claim> { new(ClaimTypes.Name, "test"), new("permission", "ElekView") };
            if (edit) claims.Add(new("permission", "ElekConfig"));
            var http = new DefaultHttpContext { User = new(new ClaimsIdentity(claims, "test")) };
            return new(store, NullLogger<ElectricityTariffsModel>.Instance)
            {
                PageContext = new() { HttpContext = http, ViewData = new(new EmptyModelMetadataProvider(), new ModelStateDictionary()) },
                MeterId = 5, Period = "2026-10", Cells = cells.ToArray(), Version = store.Snapshot.Version, ConfirmOverwrite = true
            };
        }
        var viewer = Page(false);
        Check(viewer.OnPost() is ForbidResult && store.Writes == 0 && store.Reads == 0, "View-only cannot POST");
        var editor = Page(true);
        Check(editor.OnGet() is PageResult && editor.Cells.Length == 168, "Editor loads full grid");
        editor = Page(true); editor.ConfirmOverwrite = false;
        Check(editor.OnPost() is PageResult && editor.Response.StatusCode == 400 && store.Writes == 0, "Server requires overwrite confirmation");
        editor = Page(true); editor.Cells = [0];
        Check(editor.OnPost() is PageResult && editor.Response.StatusCode == 400 && editor.Cells.Length == 168 && store.Writes == 0, "Reject and safely re-render partial input");
        editor = Page(true); editor.Period = "2026-13";
        Check(editor.OnPost() is BadRequestObjectResult && store.Writes == 0, "Invalid period cannot write");
        editor = Page(true); editor.Version = "";
        Check(editor.OnPost() is PageResult && editor.Response.StatusCode == 400 && store.Writes == 0, "Missing version cannot write");
        editor = Page(true);
        Check(editor.OnPost() is RedirectToPageResult && store.Writes == 1, "Valid save uses POST redirect GET");
        store.Conflict = true; editor = Page(true);
        Check(editor.OnPost() is PageResult && editor.Response.StatusCode == 409, "Concurrent modification produces conflict");
        store.Conflict = false; store.ReadFailure = new InvalidDataException("PRIVATE-DATA"); editor = Page(true);
        Check(editor.OnGet() is PageResult && editor.Response.StatusCode == 503 && !editor.ModelState.Values.SelectMany(value => value.Errors).Any(error => error.ErrorMessage.Contains("PRIVATE-DATA")), "Invalid tariff rows produce controlled failure");
        Console.WriteLine($"PASS: {count} tariff defaults, dates, version and handler checks.");
    }

    private sealed class FakeStore(TariffSnapshot snapshot) : IElectricityTariffStore
    {
        public TariffSnapshot Snapshot => snapshot;
        public int Reads { get; private set; }
        public int Writes { get; private set; }
        public bool Conflict { get; set; }
        public Exception? ReadFailure { get; set; }
        public TariffSnapshot Load(int meterId, DateOnly month) { Reads++; if (ReadFailure is not null) throw ReadFailure; return snapshot; }
        public void Save(int meterId, DateOnly month, IReadOnlyList<int> cells, string expectedVersion)
        {
            if (Conflict) throw new TariffConflictException();
            if (meterId != snapshot.MeterId || month != snapshot.Month || !cells.SequenceEqual(snapshot.Cells) || expectedVersion != snapshot.Version) throw new Exception("Wrong write parameters");
            Writes++;
        }
    }
}
