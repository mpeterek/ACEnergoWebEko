using System.Collections;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using ACEnergo.Web.Domain;
using ACEnergo.Web.Services;
using Microsoft.Extensions.Configuration;

internal static class WindowsTariffCheck
{
    public static void Run(string[] args)
    {
        if (args.Length != 3 || !int.TryParse(args[2], out var meterId)) throw new ArgumentException("Použití: --tariff-mdb C:\\ACEnergoData\\ACEnergoWebEko.mdb ID_MERENI");
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Integrační test Accessu musí běžet na Windows se správným providerem a bitovostí.");
        RunWindows(Path.GetFullPath(args[1]), meterId);
    }

    [SupportedOSPlatform("windows")]
    private static void RunWindows(string source, int meterId)
    {
        static string Hash(string path) { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)); }
        var before = Hash(source);
        var folder = Directory.CreateTempSubdirectory("acenergo-tariff-check-");
        try
        {
            File.Copy(source, Path.Combine(folder.FullName, "ACEnergoWebEko.mdb"));
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["Access:Directory"] = folder.FullName, ["Access:Provider"] = "Microsoft.ACE.OLEDB.16.0" }).Build();
            var store = new ElectricityTariffStore(new DatabaseCatalog(config), config);
            var month = new DateOnly(2098, 10, 1);
            var initial = store.Load(meterId, month);
            var previous = store.Load(meterId, month.AddMonths(-1));
            var nextYear = store.Load(meterId, new(2099, 1, 1));
            var changed = initial.Cells.ToArray(); changed[0] = (changed[0] + 1) % 3;
            store.Save(meterId, month, changed, initial.Version);
            foreach (var effective in ElectricityTariffs.WriteMonths(month))
                if (!store.Load(meterId, effective).Cells.SequenceEqual(changed)) throw new Exception("Nesouhlasí zpětně načtený tarif.");
            var previousAfter = store.Load(meterId, month.AddMonths(-1));
            if (!previous.Cells.SequenceEqual(previousAfter.Cells) || previous.IsDefault != previousAfter.IsDefault ||
                nextYear.Version != store.Load(meterId, new(2099, 1, 1)).Version) throw new Exception("Zápis změnil období mimo vybraný rozsah.");
            Console.WriteLine("PASS Access copy: round-trip and October–December scope");

            if (initial.Version != store.Load(meterId, month).Version)
            {
                var conflict = false;
                try { store.Save(meterId, month, changed, initial.Version); }
                catch (TariffConflictException) { conflict = true; }
                if (!conflict) throw new Exception("Zastaralá verze nebyla odmítnuta.");
                Console.WriteLine("PASS Access copy: stale version rejected");
            }
            var saved = store.Load(meterId, month);
            store.Save(meterId, month, changed, saved.Version);
            if (!store.Load(meterId, month).Cells.SequenceEqual(changed)) throw new Exception("Opakovaný update způsobil neplatná data.");
            Console.WriteLine("PASS Access copy: repeat update without duplicate daily rows");

            var rollbackBefore = store.Load(meterId, month);
            var replacement = Enumerable.Repeat(2, 168).ToArray();
            var failed = false;
            try { store.Save(meterId, month, new FailDuringWrite(replacement), rollbackBefore.Version); }
            catch (InvalidOperationException ex) when (ex.Message == "Simulated write failure") { failed = true; }
            if (!failed || store.Load(meterId, month).Version != rollbackBefore.Version) throw new Exception("Rollback neobnovil původní stav tarifů.");
            Console.WriteLine("PASS Access copy: rollback after partial SQL updates");
        }
        finally
        {
            folder.Delete(true);
            if (Hash(source) != before) throw new Exception("Zdrojová MDB se během testu změnila; test zapisuje pouze do dočasné kopie. Ověřte jiné běžící aplikace.");
        }
        Console.WriteLine("PASS original MDB unchanged; temporary test copy deleted");
    }

    // Enumerace pro validaci proběhne, ale po dvou zapsaných dnech selže indexové čtení pro SQL.
    private sealed class FailDuringWrite(int[] values) : IReadOnlyList<int>
    {
        private int reads;
        public int Count => values.Length;
        public int this[int index] => ++reads == 50 ? throw new InvalidOperationException("Simulated write failure") : values[index];
        public IEnumerator<int> GetEnumerator() => ((IEnumerable<int>)values).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
