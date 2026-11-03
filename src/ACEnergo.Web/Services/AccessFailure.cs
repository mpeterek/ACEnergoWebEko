namespace ACEnergo.Web.Services;

// Diagnostika neobsahuje původní text výjimky, SQL ani přihlašovací údaje.
public static class AccessFailure
{
    public static string Describe(Exception exception) => exception switch
    {
        FileNotFoundException or DirectoryNotFoundException => "MDB-FILE: Soubor nebo adresář databáze nebyl nalezen. Ověřte nastavení Access:Directory a přesný název ACWebEkoLogin.mdb.",
        UnauthorizedAccessException => "MDB-PERMISSION: Proces aplikace nemá oprávnění k databázovému souboru nebo jeho adresáři.",
        PlatformNotSupportedException => "MDB-PLATFORM: OLE DB vyžaduje Windows a kompatibilní Access provider.",
        System.Data.OleDb.OleDbException => "MDB-OLEDB: Access provider odmítl otevření databáze nebo provedení dotazu. Podrobnosti jsou ve výstupu aplikace.",
        InvalidOperationException => "MDB-INIT: Připojení OLE DB se nepodařilo inicializovat. Ověřte registraci a bitovost Access provideru a konfiguraci aplikace.",
        IOException => "MDB-IO: Chyba při přístupu k souboru databáze. Ověřte dostupnost, zamčení a práva.",
        _ => "MDB-UNKNOWN: Připojení k databázi selhalo. Podrobnosti jsou ve výstupu aplikace."
    };
}
