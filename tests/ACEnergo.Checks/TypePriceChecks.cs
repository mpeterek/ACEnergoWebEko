using ACEnergo.Web.Domain;
internal static class TypePriceChecks
{
    public static void Run()
    {
        var count = 0;
        void Check(bool pass, string name) { if (!pass) throw new Exception("TYPE PRICE: " + name); count++; }
        void Reject(Action action) { try { action(); } catch (ArgumentException) { count++; return; } throw new Exception("TYPE PRICE: invalid input accepted"); }
        TypePriceRecord Row(int year, int month, decimal? price, bool? tariff = false) => new(new DateTime(year, month, 1), Enumerable.Repeat(price, 6).ToArray(), tariff);
        var history = new[] { Row(2025, 1, 1.25m), Row(2026, 10, 2m), new TypePriceRecord(null, new decimal?[6], null) };
        Check(ElectricityTypePrices.Select(history, new(2026, 1, 1)).Price?.Values[0] == 1.25m, "carry across years");
        Check(ElectricityTypePrices.Select(history, new(2026, 10, 1)).Price?.Values[0] == 2m, "effective month inclusive");
        Check(ElectricityTypePrices.Select(history, new(2024, 12, 1)).Price is null, "missing not zero");
        Check(ElectricityTypePrices.Select(history, new(2026, 9, 1)).Price?.Values[0] == 1.25m, "future not used");
        var ambiguous = history.Append(Row(2026, 10, 3m)).ToArray();
        Check(ElectricityTypePrices.Select(ambiguous, new(2026, 11, 1)).Ambiguous, "different same date ambiguous");
        Check(ElectricityTypePrices.Select(history.Append(Row(2026, 10, 2m)), new(2026, 11, 1)).Price is not null, "identical duplicates consistent");
        Check(ElectricityTypePrices.Select(history.Append(Row(2026, 10, 2m, true)), new(2026, 11, 1)).Ambiguous, "tariff disagreement ambiguous");
        Check(ElectricityTypePrices.Select(ambiguous.Append(Row(2026, 12, 4m)), new(2026, 12, 1)).Price?.Values[0] == 4m, "new date supersedes ambiguous old date");
        Check(ElectricityTypePrices.Select([Row(2026, 1, null)], new(2026, 1, 1)).Price?.Values[0] is null, "NULL remains NULL");
        foreach (var value in new[]{"-1","1e3","1,2.3","10000000000000","0.000001","NaN","0."+new string('0',40)+"1"}) Reject(() => ElectricityTypePrices.Parse(Enumerable.Repeat(value,6).ToArray()));
        Check(ElectricityTypePrices.Parse(["1,23456","1.23456","","0","9999999999999.99999", "0.00001"])[0] == 1.23456m, "Czech decimal and schema limits");
        Reject(() => ElectricityTypePrices.Parse(["1"]));
        Reject(() => ElectricityTypePrices.ValidateNewMonth(history, new(2025,1,1)));
        Reject(() => ElectricityTypePrices.ValidateMonth(new(2026,1,2)));
        Reject(() => ElectricityTypePrices.ValidateMonth(new(1999,1,1)));
        Console.WriteLine($"PASS: {count} type-price effective-date, ambiguity, NULL and decimal-schema checks.");
    }
}
