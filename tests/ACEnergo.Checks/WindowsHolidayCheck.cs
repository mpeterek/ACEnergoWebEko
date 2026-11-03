using System.Collections;
using System.Data.OleDb;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Globalization;
using System.Text.Json;
using ACEnergo.Web.Domain;
using ACEnergo.Web.Services;
using Microsoft.Extensions.Configuration;

internal static class WindowsHolidayCheck
{
    public static void Run(string[] args)
    {
        if (args.Length != 3 || !int.TryParse(args[2], out var meterId)) throw new ArgumentException("Použití: --holiday-mdb C:\\ACEnergoData\\ACEnergoWebEko.mdb ID_MERENI");
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Integrační test Accessu musí běžet na Windows s odpovídajícím providerem a bitovostí.");
        RunWindows(Path.GetFullPath(args[1]), meterId);
    }

    [SupportedOSPlatform("windows")]
    private static void RunWindows(string source, int meterId)
    {
        static string Hash(string path) { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)); }
        var before = Hash(source);
        var folder = Directory.CreateTempSubdirectory("acenergo-holiday-check-");
        try
        {
            File.Copy(source, Path.Combine(folder.FullName, "ACEnergoWebEko.mdb"));
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["Access:Directory"] = folder.FullName, ["Access:Provider"] = "Microsoft.ACE.OLEDB.16.0" }).Build();
            var catalog = new DatabaseCatalog(config);
            var database = new ElectricityConfigurationDatabase(catalog, config);
            var store = new ElectricityHolidayStore(catalog, config);
            var initial = store.Load(meterId);
            var tariffBefore = TariffFingerprint(database);
            int? otherId;
            using (var connection = database.Open(false))
            using (var command = new OleDbCommand("SELECT TOP 1 [numID] FROM [tblKonfigHlavniElektrina] WHERE [numID] <> ? ORDER BY [numID]", connection))
            {
                command.Parameters.Add("meter", OleDbType.Integer).Value = meterId;
                var value = command.ExecuteScalar();
                otherId = value is null ? null : Convert.ToInt32(value);
            }
            var otherBefore = otherId.HasValue ? store.Load(otherId.Value) : null;
            var additions = Enumerable.Range(0, 1095).Select(offset => new DateOnly(2098, 1, 1).AddDays(offset)).Except(initial.Dates).Take(2).ToArray();
            if (additions.Length != 2) throw new Exception("V testovaném období nelze zvolit dvě volná testovací data.");
            if (!store.Add(meterId, additions[0], initial.Version)) throw new Exception("První vložení selhalo.");
            var added = store.Load(meterId);
            var inserted = added.Records.Where(row => DateOnly.FromDateTime(row.At) == additions[0]).ToArray();
            if (inserted.Length != 1 || inserted[0].Marker != "_") throw new Exception("Svátek nemá správné datum nebo značku _.");
            if (store.Add(meterId, additions[0], added.Version) || store.Load(meterId).Version != added.Version) throw new Exception("Duplicitní vložení změnilo data.");
            Console.WriteLine("PASS Access copy: add, marker _ and duplicate add without changes");
            if (!store.Add(meterId, additions[1], added.Version)) throw new Exception("Druhé vložení selhalo.");
            var both = store.Load(meterId);
            var conflicted = false;
            try { store.Remove(meterId, additions, initial.Version); }
            catch (HolidayConflictException) { conflicted = true; }
            if (!conflicted || store.Load(meterId).Version != both.Version) throw new Exception("Zastaralá verze nebyla bezpečně odmítnuta.");
            Console.WriteLine("PASS Access copy: stale version rejected");
            var failed = false;
            try { store.Remove(meterId, new FailDuringRemove(additions), both.Version); }
            catch (InvalidOperationException ex) when (ex.Message == "Simulated holiday delete failure") { failed = true; }
            if (!failed || store.Load(meterId).Version != both.Version) throw new Exception("Rollback neobnovil oba svátky.");
            Console.WriteLine("PASS Access copy: rollback after first DELETE in a batch");
            if (store.Remove(meterId, additions, both.Version) != 2 || store.Load(meterId).Version != initial.Version) throw new Exception("Hromadné odebrání neobnovilo původní seznam.");
            if (TariffFingerprint(database) != tariffBefore) throw new Exception("Správa svátků změnila tarify.");
            if (otherBefore is not null)
            {
                if (store.Load(otherId!.Value).Version != otherBefore.Version) throw new Exception("Změnily se svátky jiného měření.");
                Console.WriteLine("PASS Access copy: another meter unchanged");
            }
            else Console.WriteLine("SKIP another-meter comparison: database contains only one main meter");
            Console.WriteLine("PASS Access copy: bulk remove and tariffs unchanged");
        }
        finally
        {
            folder.Delete(true);
            if (Hash(source) != before) throw new Exception("Zdrojová MDB se během testu změnila; test zapisuje pouze do dočasné kopie. Ověřte jiné běžící aplikace.");
        }
        Console.WriteLine("PASS original MDB unchanged; temporary test copy deleted");
    }

    [SupportedOSPlatform("windows")]
    private static string TariffFingerprint(ElectricityConfigurationDatabase database)
    {
        using var connection = database.Open(false);
        using var command = new OleDbCommand("SELECT * FROM [tblTarifyElektrina] ORDER BY [numIDHlavni], [dtDatumPlatnost], [numIndexDne]", connection);
        using var reader = command.ExecuteReader();
        if (reader is null) throw new Exception("Nelze načíst tabulku tarifů pro kontrolu.");
        var rows = new List<string?[]> { Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToArray() };
        while (reader.Read())
        {
            rows.Add(Enumerable.Range(0, reader.FieldCount).Select(index => reader.IsDBNull(index) ? null :
                reader[index] is DateTime date ? date.ToString("O", CultureInfo.InvariantCulture) :
                Convert.ToString(reader[index], CultureInfo.InvariantCulture)).ToArray());
        }
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(rows)));
    }

    // První enumerace slouží validaci. Druhá selže až po prvním provedeném DELETE.
    private sealed class FailDuringRemove(DateOnly[] values) : IReadOnlyList<DateOnly>
    {
        private int enumerations;
        public int Count => values.Length;
        public DateOnly this[int index] => values[index];
        public IEnumerator<DateOnly> GetEnumerator() => ++enumerations == 2 ? Fail().GetEnumerator() : ((IEnumerable<DateOnly>)values).GetEnumerator();
        private IEnumerable<DateOnly> Fail()
        {
            yield return values[0];
            throw new InvalidOperationException("Simulated holiday delete failure");
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
