using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

namespace ACEnergo.Web.Domain;

public sealed record TariffRecord(DateTime EffectiveAt, int Day, string Name, int[] Hours);
public sealed record TariffSnapshot(int MeterId, string MeterName, DateOnly Month, int[] Cells, bool IsDefault, string Version);
public sealed class TariffConflictException() : Exception("Tarify mezitím změnil jiný uživatel. Znovu načtěte období a porovnejte změny.");

public static class ElectricityTariffs
{
    public const int CellCount = 7 * 24;
    public static readonly string[] Days = ["Po", "Út", "St", "Čt", "Pá", "So", "Ne"];
    public static readonly string[] Names = ["Nízký", "Vysoký", "Víkend"];

    public static bool TryMonth(string? value, out DateOnly month)
    {
        month = default;
        return DateOnly.TryParseExact(value + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out month) && month.Year is >= 2000 and < 9999;
    }

    public static DateOnly MonthStart(DateOnly date) => new(date.Year, date.Month, 1);

    // Přesný převod původní podmínky numHodina+1: vysoký tarif 05–10 a 16–20.
    public static int[] Defaults() => Enumerable.Range(0, CellCount).Select(index =>
        index / 24 >= 5 ? 2 : index % 24 is >= 5 and <= 9 or >= 16 and <= 19 ? 1 : 0).ToArray();

    public static void Validate(IReadOnlyList<int>? cells)
    {
        ArgumentNullException.ThrowIfNull(cells);
        if (cells.Count != CellCount || cells.Any(value => value is < 0 or > 2))
            throw new ArgumentException("Tarif musí obsahovat přesně 168 hodnot v rozsahu 0 až 2.");
    }

    public static DateOnly[] WriteMonths(DateOnly from)
    {
        if (from.Year is < 2000 or >= 9999) throw new ArgumentOutOfRangeException(nameof(from));
        return Enumerable.Range(from.Month, 13 - from.Month).Select(month => new DateOnly(from.Year, month, 1)).ToArray();
    }

    public static (int[] Cells, bool IsDefault) ReadMonth(IEnumerable<TariffRecord> records, DateOnly month)
    {
        var rows = records.Where(row => row.EffectiveAt == MonthStart(month).ToDateTime(TimeOnly.MinValue)).OrderBy(row => row.Day).ToArray();
        if (rows.Length == 0) return (Defaults(), true);
        if (rows.Length != 7 || !rows.Select(row => row.Day).SequenceEqual(Enumerable.Range(1, 7)) ||
            rows.Any(row => row.Hours.Length != 24 || row.Hours.Any(value => value is < 0 or > 2)))
            throw new InvalidDataException("Uložený tarif nemá sedm platných denních řádků. Nebyl nahrazen výchozími hodnotami.");
        return (rows.SelectMany(row => row.Hours).ToArray(), false);
    }

    public static string Version(IEnumerable<TariffRecord> records)
    {
        // Zahrnuje celé zapisované období do prosince, aby se odhalily i změny budoucích měsíců.
        var canonical = records.Select(row => JsonSerializer.Serialize(row)).Order(StringComparer.Ordinal).ToArray();
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(canonical)));
    }
}
