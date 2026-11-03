using ACEnergo.Web.Services;
using System.Diagnostics;
using System.Reflection;

internal static class TariffWriteLockChecks
{
    public static void Run()
    {
        var folder = Directory.CreateTempSubdirectory("acenergo-lock-check-");
        var path = Path.Combine(folder.FullName, "config.mdb");
        int Probe()
        {
            var executable = Environment.ProcessPath ?? throw new Exception("Chybí cesta testovacího procesu.");
            var start = new ProcessStartInfo(executable) { UseShellExecute = false };
            if (string.Equals(Path.GetFileNameWithoutExtension(executable), "dotnet", StringComparison.OrdinalIgnoreCase))
                start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
            start.ArgumentList.Add("--tariff-lock-probe");
            start.ArgumentList.Add(path);
            using var child = Process.Start(start) ?? throw new Exception("Nelze spustit kontrolu druhého procesu.");
            if (!child.WaitForExit(10000)) { child.Kill(true); throw new Exception("Timeout kontroly souběhu."); }
            return child.ExitCode;
        }
        try
        {
            using (TariffWriteLock.Acquire(path))
            {
                var blocked = false;
                try { using var second = TariffWriteLock.Acquire(path); }
                catch (IOException) { blocked = true; }
                if (!blocked) throw new Exception("Druhý zapisovač získal již držený zámek.");
                if (Probe() != 23) throw new Exception("Druhý proces nebyl blokován.");
            }
            // Soubor zůstává na disku, ale zavřený handle nesmí blokovat další zápis.
            if (!File.Exists(path + ".tariffs.lock")) throw new Exception("Soubor zámku se neočekávaně smazal.");
            using (TariffWriteLock.Acquire(path)) { }
            if (Probe() != 0) throw new Exception("Druhý proces nezískal uvolněný zámek.");
            try
            {
                using var held = TariffWriteLock.Acquire(path);
                throw new InvalidOperationException("Simulated failure");
            }
            catch (InvalidOperationException ex) when (ex.Message == "Simulated failure") { }
            using (TariffWriteLock.Acquire(path)) { }
            if (Probe() != 0) throw new Exception("Výjimka neodblokovala druhý proces.");
            Console.WriteLine("PASS: cross-process tariff write lock, reacquisition and release after exception.");
        }
        finally { folder.Delete(true); }
    }
}
