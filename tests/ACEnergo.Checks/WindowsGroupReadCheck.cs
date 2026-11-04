using System.Runtime.Versioning;
using ACEnergo.Web.Services;
using Microsoft.Extensions.Configuration;

internal static class WindowsGroupReadCheck
{
    public static void Run(string[] args)
    {
        if (args.Length != 2) throw new ArgumentException("Použití: --group-mdb C:\\ACEnergoData\\ACEnergoWebEko.mdb");
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Tato čtecí kontrola vyžaduje Windows a odpovídající ACE provider.");
        ReadWindows(Path.GetFullPath(args[1]));
    }

    [SupportedOSPlatform("windows")]
    private static void ReadWindows(string path)
    {
        if (!File.Exists(path) || !string.Equals(Path.GetFileName(path), "ACEnergoWebEko.mdb", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Zadejte existující konfigurační soubor ACEnergoWebEko.mdb.");
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Access:Directory"] = Path.GetDirectoryName(path),
            ["Access:Provider"] = "Microsoft.ACE.OLEDB.16.0"
        }).Build();
        var database = new DatabaseCatalog(config);
        var catalog = new ElectricityGroupCatalog(database, config);
        var details = new ElectricityGroupConfiguration(database, config);
        var composition = new ElectricityGroupCompositionEditor(database, config);
        var legacy = new ElectricityFictitiousComposition(database, config);
        var prices = new ElectricityTypePriceStore(database, config);
        var passed = 0;
        var failed = 0;
        foreach (var kind in new[] { "fictitious", "centres", "plants" })
        {
            var groups = catalog.Load(kind);
            if (groups.Length == 0) { Console.WriteLine($"SKIP: {kind} nemá elektrické záznamy."); continue; }
            foreach (var category in kind == "centres" ? new[] { "physical", "fictitious" } : new[] { kind == "plants" ? "centres" : "physical" })
            {
                try
                {
                    var afterId = 0;
                    CompositionPicker batch;
                    do
                    {
                        batch = composition.Browse(kind, groups[0].Id, category, null, null, afterId);
                        if (batch.Rows.Any(row => row.Id <= afterId || !batch.Types.Contains(row.Type, StringComparer.OrdinalIgnoreCase)))
                            throw new Exception("Kandidát neodpovídá kurzoru nebo elektrickým typům.");
                        if (batch.Rows.Length > 0)
                        {
                            if (category == "physical" && batch.Rows.Any(row => row.Id >= ElectricityGroupCompositionEditor.FictitiousOffset)) throw new Exception("Fiktivní ID mezi podružnými vstupy.");
                            if (category == "fictitious" && batch.Rows.Any(row => row.Id < ElectricityGroupCompositionEditor.FictitiousOffset)) throw new Exception("Reálné ID mezi fiktivními vstupy.");
                            afterId = batch.Rows[^1].Id;
                        }
                        else if (batch.HasMore) throw new Exception("Prázdná dávka s pokračováním.");
                    } while (batch.HasMore);
                    var first = composition.Browse(kind, groups[0].Id, category, null, null, 0).Rows.FirstOrDefault();
                    if (first is not null && composition.Browse(kind, groups[0].Id, category, null, first.Type, 0).Rows.Any(row => !string.Equals(row.Type, first.Type, StringComparison.OrdinalIgnoreCase)))
                        throw new Exception("Filtr typu neodpovídá výběru.");
                    if (first is not null && category != "centres")
                    {
                        var station=first.Id/10000;
                        var stationRows=composition.Browse(kind,groups[0].Id,category,null,null,0,station);
                        if(stationRows.Rows.Any(row=>row.Id/10000!=station)||stationRows.Stations is null||!stationRows.Stations.Contains(station))
                            throw new Exception("Filtr nebo seznam stanic neodpovídá číslování vstupů.");
                    }
                    Console.WriteLine($"PASS: nativní výběr {kind}/{category}, konečný kurzor {afterId}.");
                }
                catch (Exception ex) { failed++; Console.WriteLine($"FAIL: výběr {kind}/{category}: {ex}"); }
            }
            foreach (var group in groups)
            {
                try
                {
                    // Záměrně nativní služby: HTTP fakes chybu pozičních parametrů neodhalí.
                    var detail = details.Load(kind, group.Id);
                    var items = composition.Load(kind, group.Id);
                    var typePrices = prices.Load(kind, group.Id);
                    if(typePrices.History.Any(row=>row.Values.Length!=6))throw new Exception("Ceník typu nemá šest hodnot.");
                    if (detail.Name != group.Name || items.Name != group.Name)
                        throw new Exception("Název detailu neodpovídá řádku katalogu.");
                    if (!detail.Components.Select(row => row.Input).Order().SequenceEqual(items.Rows.Select(row => row.Id).Order()))
                        throw new Exception("Složení detailu neodpovídá editoru.");
                    if (kind == "fictitious" && legacy.Load(group.Id).Name != group.Name)
                        throw new Exception("Původní čtecí stránka neodpovídá katalogu.");
                    passed++;
                }
                catch (Exception ex)
                {
                    failed++;
                    Console.WriteLine($"FAIL: {kind}/{group.Id}: {ex}");
                }
            }
        }
        Console.WriteLine($"Nativní MDB: {passed} ověřených detailů, {failed} chyb. Pouze čtení; žádné ukládací metody nebyly volány.");
        if (failed != 0 || passed == 0) Environment.ExitCode = 1;
    }
}
