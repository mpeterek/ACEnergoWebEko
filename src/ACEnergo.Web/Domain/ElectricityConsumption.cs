namespace ACEnergo.Web.Domain;

public sealed record PulseSample(DateTime End, decimal? Energy);
public sealed record ConsumptionWindow(DateTime From, DateTime Until)
{
    public int Expected => checked((int)((Until - From).Ticks / TimeSpan.FromMinutes(15).Ticks));
}
public sealed record ConsumptionBucket(ConsumptionWindow Window, decimal? Energy, decimal? PeakPower, int Samples, int Expected, bool ArchivePresent, decimal? Cost, decimal?[] Bands)
{
    public bool Complete => ArchivePresent && Samples == Expected;
    public string Status => !ArchivePresent ? "Chybí měsíční MDB" : Samples == 0 ? "Bez hodnot" : Complete ? "Úplné" : "Neúplné";
}
public sealed record ConsumptionTariffs(int[] Cells, HashSet<DateOnly> Holidays)
{
    // Čas označuje konec čtvrthodiny. Půlnoc patří do předchozího dne a pásma 24.
    public int Band(DateTime end)
    {
        var start = end.AddMinutes(-15);
        return Holidays.Contains(DateOnly.FromDateTime(start)) ? 2 : Cells[(((int)start.DayOfWeek + 6) % 7) * 24 + start.Hour];
    }
}
public static class ElectricityConsumption
{
    public static void Validate(string mode, DateOnly date, string interval)
    {
        if (mode is not ("day" or "month" or "year") || interval is not ("quarter" or "hour") || date.Year is < 2000 or > 9998)
            throw new ArgumentException("Neplatný rozsah vyhodnocení. Vyberte den, měsíc nebo rok od roku 2000.");
    }
    public static ConsumptionWindow Window(string mode, DateOnly date, int shift)
    {
        if (shift is < -23 or > 23) throw new InvalidDataException("Časový posun typu musí být v rozsahu −23 až 23 hodin.");
        var first = mode == "day" ? date : new DateOnly(date.Year, date.Month, 1);
        var from = first.ToDateTime(TimeOnly.MinValue).AddHours(shift);
        return new(from, mode == "day" ? from.AddDays(1) : first.AddMonths(1).ToDateTime(TimeOnly.MinValue).AddHours(shift));
    }
    public static void ValidateSamples(ConsumptionWindow window, IReadOnlyList<PulseSample> samples)
    {
        var times = new HashSet<DateTime>();
        foreach (var sample in samples)
            if (sample.End <= window.From || sample.End > window.Until || sample.End.Ticks % TimeSpan.FromMinutes(15).Ticks != 0 || !times.Add(sample.End))
                throw new InvalidDataException("Archiv obsahuje duplicitní čas, neplatnou čtvrthodinu nebo hodnotu mimo zvolené období.");
    }
    public static ConsumptionBucket Aggregate(ConsumptionWindow window, IReadOnlyList<PulseSample> samples, bool present, TypePriceRecord? price = null, ConsumptionTariffs? tariffs = null)
    {
        ValidateSamples(window, samples);
        if (!present && samples.Count != 0) throw new InvalidDataException("Hodnoty bez zdrojové databáze.");
        var values = samples.Where(s => s.Energy.HasValue).ToArray();
        decimal? energy = values.Length == 0 ? null : values.Sum(s => s.Energy!.Value);
        decimal? peak = values.Length == 0 ? null : values.Max(s => s.Energy!.Value * 4);
        var complete = present && values.Length == window.Expected;
        var bands = new decimal?[3];
        decimal? cost = null;
        if (price?.Tariff == false && complete && price.Values[0].HasValue) cost = energy * price.Values[0];
        if (price?.Tariff == true && tariffs is not null)
        {
            ElectricityTariffs.Validate(tariffs.Cells);
            for (var band = 0; band < 3; band++) bands[band] = values.Length == 0 ? null : values.Where(s => tariffs.Band(s.End) == band).Sum(s => s.Energy!.Value);
            // Databázové ceny: 1 = vysoký, 2 = nízký, 3 = víkend; matice: 0 = nízký.
            int[] indices = [2, 1, 3];
            if (complete && values.All(s => price.Values[indices[tariffs.Band(s.End)]].HasValue))
                cost = values.Sum(s => s.Energy!.Value * price.Values[indices[tariffs.Band(s.End)]]!.Value);
        }
        return new(window, energy, peak, values.Length, window.Expected, present, cost, bands);
    }
}
