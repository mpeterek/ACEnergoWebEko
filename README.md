# ACEnergoWebEko

Probíhající migrace Classic ASP do ASP.NET Core 8 / C# s Razor Pages.
**Úplná funkční migrace dosud není dokončena.** Přesný rozsah je v
[docs/MIGRACE.md](docs/MIGRACE.md).

## Visual Studio 2022

Otevřete **ACEnergoWebEko.sln** a jako spouštěcí projekt zvolte **ACEnergo.Web**.
Podrobný postup, požadavky, lokální MDB a publikace jsou v
[docs/VISUAL-STUDIO-2022.md](docs/VISUAL-STUDIO-2022.md).

## Vývoj

Projekt cílí na .NET 8. Lze použít .NET 8 SDK nebo kompatibilní novější SDK.
V cloudovém prostředí je instalován v `/workspace/dotnet`.
Používejte existující checkout; cloudový úkol je již izolovaný, další worktree není potřeba.

```sh
dotnet build src/ACEnergo.Web
dotnet run --project tests/ACEnergo.Checks
ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/ACEnergo.Web --urls http://localhost:5080
# V druhém terminálu; tento smoke test je určen pro Linux bez OLE DB:
python3 tools/smoke.py
```

Na Linuxu funguje rozhraní a testy výběru souborů; skutečné OLE DB připojení
vyžaduje Windows. Není zde testovací účet ani náhradní úložiště skutečných dat.

## Windows 11 / IIS

1. Nainstalujte .NET 8 Hosting Bundle a kompatibilní podporovaný Access OLE DB
   provider. Architektura procesu a provideru musí souhlasit. Pro zde potvrzený 32bitový
   Office je výchozí Windows běh x86; instalujte také ASP.NET Core Runtime 8 x86.
   Konkrétní provider ověřte na cílovém počítači; výchozí je `Microsoft.ACE.OLEDB.16.0`.
2. Umístěte **kopie** MDB do `C:\ACEnergoData`, mimo webový kořen. Pro začátek
   `ACEnergoWebEko.mdb`, `ACWebEkoLogin.mdb`, `ACEnergoWebEko2610.mdb`.
3. Nastavte `Access__Directory`, případně `Access__Provider` a `AllowedHosts`
   na skutečný hostname. Nekopírujte MDB do publikačního adresáře.
4. `dotnet publish src/ACEnergo.Web -c Release -o artifacts/publish`.
   Publikovaný adresář nastavte jako IIS web/aplikaci. Pool: No Managed Code,
   správná bitovost, samostatná identita. Nastavte HTTPS binding.
5. Identitě poolu přidělte nejmenší potřebná práva na složku MDB včetně práv
   potřebných pro zámkové soubory Access. Nepřidělujte anonymní veřejný přístup.
   Pro stabilní cookies zapněte načítání uživatelského profilu poolu a ověřte
   trvalé uložení a ochranu ASP.NET Data Protection klíčů mezi restarty.
6. Ověřte /Login, přihlášení skutečným účtem, práva a /Electricity. Přihlášení
   se starým heslem používá existující strukturu MDB; změna úložiště hesel
   vyžaduje samostatný migrační krok. Web nikdy hesla nezobrazuje.

`/health/live` potvrzuje pouze běh webu, nikoli dostupnost MDB.
Novou verzi zatím nepoužívejte jako náhradu produkčního Classic ASP:
vyhodnocení a většina modulů ještě nejsou převedeny. Zápisy týdenních tarifů
jsou implementované; před používáním je ověřte na testovacích MDB podle
`docs/MIGRACE.md`.

## Opakování inventáře

```sh
python3 tools/inventory.py '/cesta/k/puvodnimu/ASP' docs/legacy-inventory.json
```

Inventář neexportuje obsah MDB ani hesla. Původní archiv není součástí nového
zdrojového projektu; zůstává v přílohách vydání v1.0.

## HTML, CSS a JavaScript

Stránky sdílejí `Pages/Shared/_Layout.cshtml` a dílčí šablony `_Header.cshtml`
a `_Footer.cshtml`. V jednotlivých `.cshtml` ponechávejte HTML, Razor binding
a jednoduché podmínky/cykly pro vykreslení. Databázová a výpočetní logika patří
do služeb; zpracování požadavku do `.cshtml.cs`.

- `wwwroot/css/site.css`: proměnné vzhledu, základ a společné rozvržení.
- `wwwroot/css/components.css`: sdílené panely, formuláře, tabulky a tlačítka.
- `wwwroot/css/pages/login.css`: styly specifické pro přihlášení.
- `wwwroot/js/site.js`: společné chování, načítané s `defer`.

Používejte pojmenované CSS třídy. Nevkládejte `style`, `<style>`, inline
JavaScript ani atributy `onclick`/`onload` do stránek. Další stránkové CSS
připojte přes sekci `Styles`; JavaScript přes sekci `Scripts`, vždy jako externí
soubor s `asp-append-version="true"`. Události připojujte v JS přes
`addEventListener` a `data-*` atributy. Šablony nepřevádíme na statické HTML:
Razor zůstává pro bezpečné vykreslování dat a ochranu formulářů.

Soubor `global.json` preferuje dostupné stabilní .NET 8 SDK od verze 8.0.100;
pokud není dostupné, povoluje přechod na novější hlavní verzi SDK. Cílový
framework obou projektů zůstává `net8.0`. Použité SDK musí podporovat i vaše
verze Visual Studia. Pro spuštění je potřeba runtime ASP.NET Core 8; na IIS
instalujte .NET 8 Hosting Bundle.

## Windows s 32bitovým Office

Na Windows Directory.Build.props nastavuje oba projekty na `win-x86` s
nativním apphostem. Vyberte spouštěcí profil **ACEnergo.Web (x86 HTTP)**.
K běhu je potřeba ASP.NET Core Runtime 8 **x86** a existující 32bitový Access
provider. Publikace pro tento režim: **WinX86Folder**. Úplný postup je v
[průvodci Visual Studiem](docs/VISUAL-STUDIO-2022.md#potvrzený-32bitový-office-a-ace--aktuální-nastavení).

## Týdenní tarify elektřiny

Po přihlášení otevřete **Elektřina → Seznam hlavních měření → Tarify**.
Načtěte měsíc a rok; editace je dostupná s oprávněním ElekConfig nebo
administrátora. Uložení se podle původní logiky týká vybraného měsíce
a všech následujících měsíců do prosince stejného roku.

Před prvním používáním zápisu na Windows proveďte integrační test na dočasné
kopii, popsaný v [migračním přehledu](docs/MIGRACE.md#ověření-nativního-zápisu-před-používáním).

## Svátky hlavního měření

Otevřete **Elektřina → Seznam hlavních měření → Svátky**, případně odkaz
**Svátky tohoto měření** přímo u tarifů. Zobrazí se ručně zadaná data všech
roků pro dané měření. S oprávněním ElekConfig nebo administrátora můžete
přidat datum a odebrat označená data. Existující datum se nevkládá podruhé.

Postup nativního testu nad dočasnou kopií MDB je v
[průvodci migrací](docs/MIGRACE.md#windows-integrační-test-svátků).
Běžný konzolový test nyní ověřuje i autentizované HTTP formuláře pomocí
testovacích úložišť; přihlášení a zápisy do skutečných MDB v tomto režimu
neprovádí. Testovací autentizace je pouze v testovacím projektu, nikoli ve webu.

## Rozsah: analogová měření se nepřevádějí

Na výslovný pokyn uživatele je modul Analog vyřazen z migrace a z nabídky.
Původní soubory a databáze se nemažou. Přenos starých příznaků Analog při
přihlášení zůstává kompatibilní s existující tabulkou uživatelů; žádné
analogové obrazovky, výpočty ani zápisy se neimplementují.

## Přehled cen hlavního měření elektřiny

Nový odkaz **Ceny** v seznamu hlavních měření načítá všech 28 položek
z tblCenyHlavniElektrina pro přesné ID měření a první den zvoleného měsíce.
Zachovává původní nulové výchozí hodnoty při chybějícím záznamu, s viditelným
upozorněním. Existující NULL se zobrazuje jako Neuvedeno. Přepínače jednotek
rozlišují % a MWh podle původních hodnot 0/1. Čísla se zobrazují česky.

Přehled je pouze čtecí; editace cen a navazující výpočty ještě nejsou převedeny.
Původní UpdateCenyRezKapHlMereni přepisuje vybraný měsíc až do konce roku;
implementace tohoto zápisu bude samostatným navazujícím krokem. Žádná sazba
ani DPH se v této etapě nepřepočítává.
