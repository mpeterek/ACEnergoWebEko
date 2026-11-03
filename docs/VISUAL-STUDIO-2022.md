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
5. Vyberte profil **ACEnergo.Web (HTTPS)** a spusťte **F5** (ladění) nebo
   **Ctrl+F5** (bez ladění). Visual Studio otevře `/Login`.
6. Při prvním HTTPS spuštění potvrďte důvěru vývojovému certifikátu. Pokud
   není připravený, spusťte v terminálu `dotnet dev-certs https --trust`.
   Alternativně pro místní vývoj vyberte profil **ACEnergo.Web (HTTP)**.

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

Ověřte shodnou bitovost Access provideru a procesu .NET. Standardní 64bit SDK
spouští 64bit proces; provider pouze pro 32bit proces nebude použitelný.
Při chybě registrace provideru nejprve ověřte nainstalovaný provider a jeho
bitovost. Samotná změna textu connection stringu ovladač nenainstaluje.

## Kontroly

**Build → Build Solution** sestaví oba projekty v konfiguraci Debug nebo
Release. Ve vestavěném terminálu v kořenu řešení spusťte:

```powershell
dotnet run --project tests/ACEnergo.Checks
```

Jde o konzolový kontrolní program s 12 kontrolami, nikoli testovací projekt
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
