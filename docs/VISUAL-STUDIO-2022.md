# Otevření ve Visual Studiu 2022

## Příprava na Windows 11

- Visual Studio **2022**, doporučeno **17.14 s aktuálními aktualizacemi**.
- Ve Visual Studio Installeru zvolte workload **ASP.NET and web development**
  (Vývoj pro ASP.NET a web).
- Je potřeba **.NET 8 SDK nebo novější stabilní SDK podporované vaším Visual
  Studiem**. `global.json` preferuje .NET 8 a při jeho absenci umožní novější
  hlavní verzi (například nainstalované SDK 9). Projekty stále cílí na `net8.0`.
  Doporučená verze Visual Studia výše je vhodná i pro SDK pásma 9.0.3xx.
  Visual Studio 2019 není podporováno.
- Ke spuštění aplikace nainstalujte **ASP.NET Core Runtime 8** (nebo .NET 8 SDK,
  které jej obsahuje). Samotné novější SDK nezaručuje přítomnost runtime 8.
- Pro skutečné čtení MDB je navíc potřeba kompatibilní Windows Access OLE DB
  provider. Sestavení řešení ani zobrazení přihlášení MDB nevyžadují.

## Otevření a spuštění

1. Rozbalte **celý** archiv do zapisovatelné složky, například
   `C:\Projekty\ACEnergoWebEko`. Neotevírejte řešení přímo uvnitř ZIP.
2. Otevřete **ACEnergoWebEko.sln**. Obsahuje web `ACEnergo.Web`, konzolové
   kontroly `ACEnergo.Checks` a složku dokumentace.
3. Nechte obnovit NuGet balíčky. Pokud obnova neproběhne, pravé tlačítko na
   řešení → **Restore NuGet Packages**. Je potřeba přístup k nuget.org.
4. Pravé tlačítko na **ACEnergo.Web** → **Set as Startup Project**.
5. Vyberte profil **ACEnergo.Web (x86 HTTPS)** a spusťte **F5** (ladění) nebo
   **Ctrl+F5** (bez ladění). Visual Studio otevře `/Login`.
6. Při prvním HTTPS spuštění potvrďte důvěru vývojovému certifikátu. Pokud
   není připravený, spusťte v terminálu `dotnet dev-certs https --trust`.
   Alternativně pro místní vývoj vyberte profil **ACEnergo.Web (x86 HTTP)**.

HTTPS používá port 7080 a HTTP port 5080. Profily běží přímo přes Kestrel;
IIS Express pro otevření a ladění projektu není potřeba. Je-li port obsazený,
upravte `Properties/launchSettings.json` nebo ukončete jinou vlastní instanci.

## Nastavení vlastních MDB bez změny sdíleného projektu

1. Vytvořte kopie původních MDB mimo web, například v `C:\ACEnergoData`.
2. Pravé tlačítko na web → **Manage User Secrets** (Spravovat tajné kódy
   uživatele). Pro lokální vývoj vložte vlastní nastavení:

```json
{
  "Access": {
    "Directory": "C:\\ACEnergoData",
    "Provider": "Microsoft.ACE.OLEDB.16.0"
  }
}
```

Tento soubor je uložený ve vašem profilu mimo repozitář. Není součástí
publikace a User Secrets nejsou šifrovaný produkční trezor. Nevkládejte do
repozitáře hesla ani skutečné MDB. Přihlašovací údaje zůstávají ve stávající
`ACWebEkoLogin.mdb`; aplikace nemá výchozí náhradní účet.

Na Windows je nyní výchozí běh win-x86 kvůli potvrzenému 32bitovému Office.
Directory.Build.props nastavuje RuntimeIdentifier=win-x86 a UseAppHost=true
pro web i kontrolní program. Takto vytvořený EXE běží 32bitově i při sestavení
64bitovým SDK; vyžaduje však odpovídající 32bitový .NET runtime.
Při chybě registrace provideru nejprve ověřte nainstalovaný provider a jeho
bitovost. Samotná změna textu connection stringu ovladač nenainstaluje.

## Kontroly

**Build → Build Solution** sestaví oba projekty v konfiguraci Debug nebo
Release. Ve vestavěném terminálu v kořenu řešení spusťte:

```powershell
dotnet run --project tests/ACEnergo.Checks
```

Jde o konzolový kontrolní program se základními kontrolami, testy tarifů a svátků a autentizovanými HTTP testy, nikoli testovací projekt
pro Test Explorer. HTTP smoke test `tools/smoke.py` je určen pro cloudový
Linux bez OLE DB; na Windows ověřte přihlášení skutečným účtem a měření.

## Publikace pro IIS

Pravé tlačítko na web → **Publish** → zvolte připravený profil **FolderProfile**.
Pokud ho UI samo nenabídne, použijte **Import Profile** a vyberte
`Properties/PublishProfiles/FolderProfile.pubxml`.
Profil vytvoří Release výstup v `artifacts/publish-net8` a neodesílá nic na server.
Je ekvivalentní příkazu:

```powershell
dotnet publish src/ACEnergo.Web -p:PublishProfile=FolderProfile
```

Na IIS je potřeba **.NET 8 Hosting Bundle**, správná bitovost Access Engine,
práva identity poolu, hostname a HTTPS. Postup je v README. Vývojová nastavení
User Secrets se na IIS nepřenášejí; použijte konfiguraci prostředí cílového webu.

Projekt obsahuje dosud migrovanou první etapu. Zbývající moduly a ověření MDB
jsou uvedeny v `docs/MIGRACE.md`; nastavení Visual Studia jejich převod nenahrazuje.

## Chyba NETSDK1141 při načtení řešení

Původní global.json vyžadoval přesně pásmo SDK 8.0.427. Nová verze souboru
používá minimum 8.0.100 a `rollForward: major`, takže umožní i nainstalované
SDK 9, pokud SDK 8 není dostupné. Aktualizujte větev nebo nahraďte obsah
kořenového global.json tímto nastavením a řešení znovu otevřete:

```json
{
  "sdk": {
    "version": "8.0.100",
    "rollForward": "major",
    "allowPrerelease": false
  }
}
```

V terminálu ve složce řešení ověřte `dotnet --version` a `dotnet --list-runtimes`.
Pokud sestavení projde a spuštění požaduje Microsoft.AspNetCore.App 8,
doinstalujte ASP.NET Core Runtime 8 nebo .NET 8 SDK.

## Přihlášení hlásí nedostupnou MDB

Po aktualizaci větve a novém sestavení spusťte vývojový HTTP/HTTPS profil
a zkuste přihlášení znovu. V prostředí Development se pod formulářem zobrazí
blok diagnostiky s použitou cestou, providerem, architekturou procesu a typem
výjimky. V produkci se cesty ani tato diagnostika nezobrazují.

- MDB-FILE: ověřte skutečnou cestu a název ACWebEkoLogin.mdb. Lokální User
  Secrets a proměnné Access__Directory mohou přepsat appsettings.json.
- MDB-PERMISSION: ověřte přístup identity procesu k souboru a adresáři.
- MDB-INIT: zkontrolujte ve Výstupu konkrétní chybu registrace provideru.
  Proces X64 potřebuje provider X64; běžná instalace 32bit Office sama o sobě
  nezaručuje dostupnost 64bit ACE. Neinstalujte naslepo ovladač jiné bitovosti.
- MDB-OLEDB: ve Výstupu zkontrolujte, zda provider odmítl soubor, zamčení,
  přístupová práva, formát databáze nebo dotaz nad tblLog.

Výchozí provider je Microsoft.ACE.OLEDB.16.0. Pokud je na počítači skutečně
zaregistrovaný jiný kompatibilní provider, zadejte jej v lokálním Access:Provider.
Pouhé přejmenování provideru bez jeho instalace nepomůže. Pro doplnění ACE
použijte podporovaný Microsoft Access Runtime odpovídající bitovosti procesu
a kompatibilní s instalací Office.

Podrobná výjimka se zapisuje do Visual Studio → Zobrazení → Výstup →
ASP.NET Core Web Server, případně Debug. Aplikace neloguje zadané heslo ani
obsah MDB; neposílejte své přihlašovací údaje při hlášení chyby.

## Potvrzený 32bitový Office a ACE – aktuální nastavení

Pro místní prostředí s existujícím ACEOLEDB.DLL v Program Files (x86)
neinstalujte Access Runtime x64 a nepřepisujte registry. Použijte 32bitovou
aplikaci proti existujícímu 32bitovému provideru.

1. Zastavte aplikaci, proveďte Git → Pull na větvi migration/aspnet-core8-vs2022.
2. Nainstalujte **ASP.NET Core Runtime 8 – Windows x86** z oficiální stránky
   https://dotnet.microsoft.com/en-us/download/dotnet/8.0. Nezaměňujte x86
   za x64 ani .NET runtime za Access Runtime. Ověřte přítomnost runtime:

   ```powershell
   & "C:\Program Files (x86)\dotnet\dotnet.exe" --list-runtimes
   ```

   Musí být dostupné Microsoft.NETCore.App 8.0.x a Microsoft.AspNetCore.App
   8.0.x. Alternativou je instalace .NET 8 SDK pro Windows x86, které zahrnuje
   i potřebné runtimy. Existující x64 SDK můžete ponechat pro sestavení.
3. Ve Visual Studiu spusťte **Build → Rebuild Solution**. Samotné přepnutí
   názvu platformy na x86 není potřeba: architekturu nativního hostitele určuje
   RuntimeIdentifier v Directory.Build.props.
4. Vyberte **ACEnergo.Web (x86 HTTP)** a F5. Pokud VS drží starý profil,
   řešení zavřete a znovu otevřete. Názvy profilů mají x86 význam na Windows;
   na Linuxu zůstává běh přenositelný a Access není dostupný.
5. Po přihlášení by provider již měl být načtený; pokud připojení znovu
   selže, v diagnostice ověřte **X86** a pošlete nový typ chyby. Tím se
   odliší další případný problém souboru, oprávnění nebo tabulky tblLog.

Při spuštění z terminálu používejte `dotnet run --project src/ACEnergo.Web
--launch-profile "ACEnergo.Web (x86 HTTP)"`, který zvolí apphost, nebo přímo
EXE z bin\Debug\net8.0\win-x86. Nespouštějte DLL přes 64bitové dotnet.exe: tím
byste znovu vynutili proces X64 a obešli x86 apphost.

### IIS s 32bitovým providerem

Publikujte profilem **WinX86Folder** do artifacts/publish-win-x86. Na IIS
nainstalujte .NET 8 Hosting Bundle včetně x86 komponent. Pro samostatný
aplikační pool nastavte **Enable 32-Bit Applications = True** a
**.NET CLR Version = No Managed Code**. Nastavte práva identity poolu k MDB,
produkční Access:Directory a hostname podle README. Vývojové User Secrets se
na IIS nepřenášejí.

Pro jiné prostředí s 64bitovým ACE lze výchozí RID explicitně přepsat
parametrem `-r win-x64`; musí však souhlasit runtime, provider i nastavení IIS.

## Nová správa svátků

Po Pull a sestavení zvolte **Elektřina → Seznam hlavních měření → Svátky**.
Přidávání a hromadné odebrání vyžaduje konfiguraci elektřiny nebo
administrátora. Pouze pro čtení stačí ElekView. Pro nativní zápisový test
na dočasné kopii použijte postup --holiday-mdb v docs/MIGRACE.md.

Testovací projekt nyní obnovuje Microsoft.AspNetCore.Mvc.Testing 8.0.33
z NuGet pro HTTP testy. Webový projekt tuto závislost neobsahuje.
