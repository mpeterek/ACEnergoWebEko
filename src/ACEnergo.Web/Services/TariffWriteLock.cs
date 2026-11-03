namespace ACEnergo.Web.Services;

public static class TariffWriteLock
{
    // Prázdný pomocný soubor se nemaže: smazání by umožnilo otevřít jiný inode
    // pod stejným názvem a obejít zámek. Uvolňuje se pouze otevřený handle.
    // Stejný soubor používají všechny instance této aplikace nad stejnou MDB.
    public static IDisposable Acquire(string databasePath) => File.Open(
        databasePath + ".tariffs.lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
}
