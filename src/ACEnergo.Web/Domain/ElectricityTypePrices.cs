using System.Globalization;
namespace ACEnergo.Web.Domain;

public sealed record TypePriceRecord(DateTime? EffectiveAt, decimal?[] Values, bool? Tariff);
public sealed record TypePriceSelection(TypePriceRecord? Price, bool Ambiguous);
public static class ElectricityTypePrices
{
    public static readonly string[] Labels = ["Jednotná cena", "Vysoký tarif", "Nízký tarif", "Víkendový tarif", "Další cena 4", "Další cena 5"];
    // tblCenyPodruzne: DECIMAL(18,5). Prázdná hodnota zachovává NULL.
    public static decimal?[] Parse(string[] values)
    {
        if (values.Length != 6) throw new ArgumentException("Ceník musí obsahovat šest cen.");
        return values.Select(value => {
            if (string.IsNullOrWhiteSpace(value)) return (decimal?)null;
            var normalized = value.Replace(',', '.');
            var point = normalized.IndexOf('.');
            if ((point >= 0 && normalized[(point + 1)..].TrimEnd('0').Length > 5)
                || !decimal.TryParse(normalized, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number)
                || number < 0 || number >= 10000000000000m || decimal.Round(number, 5) != number)
                throw new ArgumentException("Cena musí být nezáporné číslo, nejvýše 13 míst před a 5 míst za desetinnou čárkou.");
            return (decimal?)number;
        }).ToArray();
    }
    public static TypePriceSelection Select(IEnumerable<TypePriceRecord> history, DateOnly month)
    {
        var candidates = history.Where(row => row.EffectiveAt.HasValue && row.EffectiveAt.Value <= month.ToDateTime(TimeOnly.MinValue)).OrderByDescending(row => row.EffectiveAt).ToArray();
        if (candidates.Length == 0) return new(null, false);
        var latest = candidates.Where(row => row.EffectiveAt == candidates[0].EffectiveAt).ToArray();
        if (latest.Any(row => row.Tariff != latest[0].Tariff || !row.Values.SequenceEqual(latest[0].Values))) return new(null, true);
        return new(latest[0], false);
    }
    public static void ValidateMonth(DateOnly month)
    {
        if (month.Year is < 2000 or >= 9999 || month.Day != 1) throw new ArgumentException("Platnost musí být první den měsíce od roku 2000.");
    }
    public static void ValidateNewMonth(IEnumerable<TypePriceRecord> history, DateOnly month)
    {
        ValidateMonth(month);
        if (history.Any(row => row.EffectiveAt == month.ToDateTime(TimeOnly.MinValue)))
            throw new ArgumentException("Pro tento měsíc již existuje ceník. Historie se nepřepisuje; zvolte jiný měsíc platnosti.");
    }
    public static string Display(decimal? value) => value?.ToString("0.#####", CultureInfo.GetCultureInfo("cs-CZ")) ?? "Neuvedeno";
}
