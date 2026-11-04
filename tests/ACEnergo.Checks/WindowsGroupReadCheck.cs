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
        var passed = 0;
        var failed = 0;
        foreach (var kind in new[] { "fictitious", "centres", "plants" })
        {
            var groups = catalog.Load(kind);
            if (groups.Length == 0) { Console.WriteLine($"SKIP: {kind} nemá elektrické záznamy."); continue; }
            foreach (var group in groups)
            {
                try
                {
                    // Záměrně nativní služby: HTTP fakes chybu pozičních parametrů neodhalí.
                    var detail = details.Load(kind, group.Id);
                    var items = composition.Load(kind, group.Id);
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
