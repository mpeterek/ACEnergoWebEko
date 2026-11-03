using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

namespace ACEnergo.Web.Domain;

public sealed record HolidayRecord(DateTime At, string Marker);
public sealed record HolidaySnapshot(int MeterId, string MeterName, HolidayRecord[] Records, string Version)
{
    public DateOnly[] Dates => ElectricityHolidays.Dates(Records);
}
public sealed class HolidayConflictException() : Exception("Seznam svátků mezitím změnil jiný uživatel. Znovu načtěte seznam a vyberte požadovanou změnu.");

public static class ElectricityHolidays
{
    public const string NewMarker = "_";
    public const int MaxSelection = 1000;
    public static bool TryDate(string? value, out DateOnly date) => DateOnly.TryParseExact(value,
        "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date) && date.Year >= 100;

    public static DateOnly[] Selection(IReadOnlyList<string>? values)
    {
        if (values is null || values.Count == 0) throw new ArgumentException("Vyberte alespoň jeden svátek k odebrání.");
        if (values.Count > MaxSelection) throw new ArgumentException("Najednou lze odebrat nejvýše 1000 dat.");
        var dates = new List<DateOnly>();
        foreach (var value in values)
        {
            if (!TryDate(value, out var date)) throw new ArgumentException("Vybrané datum není platné; použijte RRRR-MM-DD.");
            dates.Add(date);
        }
        return dates.Distinct().Order().ToArray();
    }

    public static void ValidateDates(IReadOnlyList<DateOnly>? dates)
    {
        if (dates is null || dates.Count is < 1 or > MaxSelection || dates.Any(date => date.Year < 100))
            throw new ArgumentException("Vyberte 1 až 1000 platných dat od roku 0100.");
    }

    public static void ValidateVersion(string? version)
    {
        if (string.IsNullOrEmpty(version) || version.Length != 64 || !version.All(Uri.IsHexDigit))
            throw new ArgumentException("Chybí platná verze seznamu. Znovu načtěte stránku.");
    }

    public static DateOnly[] Dates(IEnumerable<HolidayRecord> rows)
    {
        var records = rows.ToArray();
        if (records.Any(row => row.At.TimeOfDay != TimeSpan.Zero || row.At.Year < 100))
            throw new InvalidDataException("Svátky obsahují neplatné datum nebo časovou složku; seznam nebyl zkrácen ani upraven.");
        return records.Select(row => DateOnly.FromDateTime(row.At)).Distinct().Order().ToArray();
    }

    public static string Version(IEnumerable<HolidayRecord> rows) => Convert.ToHexString(SHA256.HashData(
        JsonSerializer.SerializeToUtf8Bytes(rows.Select(row => JsonSerializer.Serialize(row)).Order(StringComparer.Ordinal).ToArray())));
}
