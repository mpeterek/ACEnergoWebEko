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

internal static class HolidayChecks
{
    public static void Run()
    {
        var count = 0;
        void Check(bool condition, string name) { if (!condition) throw new Exception(name); count++; }
        void Invalid(Action action) { try { action(); } catch (ArgumentException) { count++; return; } throw new Exception("Invalid holiday accepted"); }
        Check(ElectricityHolidays.TryDate("2024-02-29", out var leap) && leap == new DateOnly(2024, 2, 29), "Leap date");
        Check(ElectricityHolidays.TryDate("2000-02-29", out _), "Leap century");
        foreach (var text in new[] { "1900-02-29", "2026-02-29", "2026-13-01", "2026-04-31", "0099-01-01", "1.1.2026", "2026-10-01' OR 1=1", "" })
            Check(!ElectricityHolidays.TryDate(text, out _), "Reject malformed date");
        Check(!ElectricityHolidays.TryDate(null, out _), "Reject missing date");
        Check(ElectricityHolidays.TryDate("0100-01-01", out _) && ElectricityHolidays.TryDate("9999-12-31", out _), "Access date boundaries");
        Invalid(() => ElectricityHolidays.Selection(null));
        Invalid(() => ElectricityHolidays.Selection([]));
        Invalid(() => ElectricityHolidays.Selection(["invalid"]));
        Invalid(() => ElectricityHolidays.Selection(Enumerable.Repeat("2026-01-01", 1001).ToArray()));
        Invalid(() => ElectricityHolidays.ValidateDates(null));
        Invalid(() => ElectricityHolidays.ValidateDates([]));
        Invalid(() => ElectricityHolidays.ValidateDates([new DateOnly(99, 1, 1)]));
        var dates = ElectricityHolidays.Selection(["2026-12-25", "2026-11-17", "2026-12-25"]);
        Check(dates.SequenceEqual(new[] { new DateOnly(2026, 11, 17), new DateOnly(2026, 12, 25) }), "Selected dates sorted and unique");
        var rows = dates.Select(date => new HolidayRecord(date.ToDateTime(TimeOnly.MinValue), "_")).ToArray();
        Check(ElectricityHolidays.Dates(rows.Reverse().Concat([rows[0]])).SequenceEqual(dates), "Display dates sorted and unique");
        Check(ElectricityHolidays.Dates([]).Length == 0, "Empty list");
        try { ElectricityHolidays.Dates([rows[0] with { At = rows[0].At.AddHours(1) }]); throw new Exception("Time component ignored"); } catch (InvalidDataException) { count++; }
        var version = ElectricityHolidays.Version(rows);
        ElectricityHolidays.ValidateVersion(version);
        Check(version == ElectricityHolidays.Version(rows.Reverse()), "Version ignores row order");
        Check(version != ElectricityHolidays.Version(rows.Concat([rows[0]])), "Version detects duplicates");
        Check(version != ElectricityHolidays.Version([rows[0] with { Marker = "other" }, rows[1]]), "Version includes marker");
        Invalid(() => ElectricityHolidays.ValidateVersion(null));
        Invalid(() => ElectricityHolidays.ValidateVersion(""));
        Invalid(() => ElectricityHolidays.ValidateVersion(new string('G', 64)));
        Check(ElectricityHolidays.NewMarker == "_", "Legacy insert marker");

        var store = new FakeStore(new(5, "Test", rows, version));
        ElectricityHolidaysModel Page(bool edit)
        {
            var claims = new List<Claim> { new(ClaimTypes.Name, "test"), new("permission", "ElekView") };
            if (edit) claims.Add(new("permission", "ElekConfig"));
            return new(store, NullLogger<ElectricityHolidaysModel>.Instance)
            {
                PageContext = new()
                {
                    HttpContext = new DefaultHttpContext { User = new(new ClaimsIdentity(claims, "test")) },
                    ViewData = new(new EmptyModelMetadataProvider(), new ModelStateDictionary())
                },
                MeterId = 5, Version = version, NewDate = "2026-11-18", Period = "2026-10", SelectedDates = ["2026-11-17"]
            };
        }
        var viewer = Page(false);
        Check(viewer.OnPostAdd() is ForbidResult && store.AddCalls == 0 && store.Reads == 0, "Viewer cannot add");
        Check(viewer.OnPostRemove() is ForbidResult && store.RemoveCalls == 0 && store.Reads == 0, "Viewer cannot remove");
        Check(viewer.OnGet() is PageResult && viewer.Snapshot!.Dates.Length == 2 && !viewer.CanEdit, "Viewer sees all dates");
        var editor = Page(true); editor.NewDate = "2026-02-29";
        Check(editor.OnPostAdd() is PageResult && editor.Response.StatusCode == 400 && store.AddCalls == 0, "Invalid date cannot write");
        editor = Page(true); editor.Version = "";
        Check(editor.OnPostAdd() is PageResult && editor.Response.StatusCode == 400 && store.AddCalls == 0, "Missing version cannot add");
        editor = Page(true); editor.Period = "invalid";
        Check(editor.OnGet() is BadRequestObjectResult && editor.OnPostAdd() is BadRequestObjectResult && editor.OnPostRemove() is BadRequestObjectResult, "Reject invalid return context");
        editor = Page(true); editor.SelectedDates = [];
        Check(editor.OnPostRemove() is PageResult && editor.Response.StatusCode == 400 && store.RemoveCalls == 0, "Empty selection cannot delete");
        editor = Page(true); editor.SelectedDates = ["invalid"];
        Check(editor.OnPostRemove() is PageResult && editor.Response.StatusCode == 400 && store.RemoveCalls == 0, "Malformed selection cannot delete");
        editor = Page(true); editor.ModelState.AddModelError("", "binding error");
        Check(editor.OnPostRemove() is PageResult && editor.Response.StatusCode == 400 && store.RemoveCalls == 0, "Binding failure cannot delete");
        editor = Page(true);
        Check(editor.OnPostAdd() is RedirectToPageResult && store.AddCalls == 1 && store.LastAdded == new DateOnly(2026, 11, 18), "Add correct meter/date and redirect");
        store.AddedResult = false; editor = Page(true); editor.NewDate = "2026-11-17";
        Check(editor.OnPostAdd() is RedirectToPageResult && editor.Notice!.Contains("už"), "Duplicate add is explicit no-op");
        editor = Page(true); editor.SelectedDates = ["2026-12-25", "2026-11-17", "2026-11-17"];
        Check(editor.OnPostRemove() is RedirectToPageResult && store.RemoveCalls == 1 && store.LastRemoved!.SequenceEqual(dates), "Bulk delete uses selected unique dates");
        store.Conflict = true; editor = Page(true);
        Check(editor.OnPostAdd() is PageResult && editor.Response.StatusCode == 409, "Stale add conflicts");
        editor = Page(true);
        Check(editor.OnPostRemove() is PageResult && editor.Response.StatusCode == 409, "Stale removal conflicts");
        store.Conflict = false; store.Missing = true; editor = Page(true);
        Check(editor.OnGet() is NotFoundResult && editor.OnPostAdd() is NotFoundResult && editor.OnPostRemove() is NotFoundResult, "Missing meter returns 404");
        store.Missing = false; store.ReadFailure = new InvalidDataException("PRIVATE-DATA"); editor = Page(true);
        Check(editor.OnGet() is PageResult && editor.Response.StatusCode == 503 && !editor.ModelState.Values.SelectMany(value => value.Errors).Any(error => error.ErrorMessage.Contains("PRIVATE-DATA")), "Data failure does not expose exception details");
        Console.WriteLine($"PASS: {count} holiday dates, version, authorization and handler checks.");
    }

    private sealed class FakeStore(HolidaySnapshot snapshot) : IElectricityHolidayStore
    {
        public int Reads { get; private set; }
        public int AddCalls { get; private set; }
        public int RemoveCalls { get; private set; }
        public DateOnly? LastAdded { get; private set; }
        public DateOnly[]? LastRemoved { get; private set; }
        public bool AddedResult { get; set; } = true;
        public bool Conflict { get; set; }
        public bool Missing { get; set; }
        public Exception? ReadFailure { get; set; }
        public HolidaySnapshot Load(int meterId)
        {
            Reads++;
            if (ReadFailure is not null) throw ReadFailure;
            if (Missing) throw new KeyNotFoundException();
            if (meterId != snapshot.MeterId) throw new Exception("Wrong read meter");
            return snapshot;
        }
        private void CheckTarget(int meterId, string version)
        {
            if (meterId != snapshot.MeterId || version != snapshot.Version) throw new Exception("Wrong write meter/version");
            if (Conflict) throw new HolidayConflictException();
        }
        public bool Add(int meterId, DateOnly date, string expectedVersion)
        { CheckTarget(meterId, expectedVersion); LastAdded = date; AddCalls++; return AddedResult; }
        public int Remove(int meterId, IReadOnlyList<DateOnly> dates, string expectedVersion)
        { CheckTarget(meterId, expectedVersion); LastRemoved = dates.ToArray(); RemoveCalls++; return dates.Count; }
    }
}
