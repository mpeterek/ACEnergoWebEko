# Ověření první implementační etapy

Provedeno v cloudovém Linux prostředí s .NET SDK 8.0.427.

- Debug build: úspěch, 0 varování, 0 chyb.
- Release publish: úspěch, výstup artifacts/publish-net8.
- 12 kontrol pravidel názvu MDB, hranic roku/měsíce, odmítnutí cesty mimo
  katalog, seznamu dostupných měsíců a počtu původních práv: úspěch.
- HTTP: /health/live a /Login 200; / a /Electricity bez přihlášení
  přesměrují na přihlášení; POST bez antiforgery tokenu vrací 400;
  POST s tokenem na Linuxu vrací informaci o nedostupném MDB runtime
  a neudělí přístup: úspěch.
- git diff --check: úspěch.

Nebylo ověřeno: úspěšné přihlášení přes Access Engine, data seznamu měření,
IIS, skutečná uživatelská oprávnění, souběh, výpočty, importy, exporty,
konfigurační zápisy. Tyto výsledky neprokazují funkční ekvivalenci celé aplikace.

## Oddělení vzhledu a chování

Ověřeno vykreslené HTML přihlašovací stránky: bez inline style atributů,
style bloků, inline skriptů a obsluh událostí. Ověřeny verzované odkazy na
externí CSS/JS a odpovědi 200 se správnými MIME typy pro všechny čtyři
soubory. JavaScript prošel node --check. Kontrola provedena po spuštění
aplikace cílené na net8.0. OLE DB závislost je System.Data.OleDb 8.0.0.

## Visual Studio 2022

- Řešení ACEnergoWebEko.sln obsahuje oba projekty a složku dokumentace.
- Sestavení celého řešení pod SDK 8.0.427: úspěch, 0 chyb a varování
  (v cloudovém sandboxu použito -m:1).
- Spuštění přes launch profil ACEnergo.Web (HTTP) a následné HTTP kontroly:
  úspěch.
- Publikace přes FolderProfile: úspěch.
- Kontrolní konzolový program: všech 12 kontrol prošlo.

Grafické Visual Studio 2022, F5 debugger, důvěra certifikátu na Windows a
IIS nebyly v Linux prostředí spuštěny; jejich postup je popsán v
docs/VISUAL-STUDIO-2022.md.

## Výběr SDK bez NETSDK1141

Global.json nyní dovoluje roll-forward od SDK 8.0.100 na vyšší hlavní verzi,
pokud SDK 8 není nainstalováno. Sestavení obou projektů prošlo bez chyb a
varování se SDK 8.0.427 i v izolovaném výběru, kde bylo dostupné pouze SDK
10.0.403. Cíl zůstává net8.0; všech 12 kontrol prošlo pod SDK 8.
SDK 9.0.320 ani Visual Studio uživatele nejsou v cloudu instalovány.

## Diagnostika připojení MDB

Sestavení obou projektů bez chyb a varování. Prošlo 18 kontrol včetně rozlišení
chybějícího souboru, oprávnění, platformy a inicializace; veřejné diagnostické
texty nepropouštějí původní text výjimky. HTTP test na Linuxu ověřil neúspěšné
přihlášení, bezpečný vývojový diagnostický blok a zachování ochrany CSRF.
Přístupová práva a registraci ACE na lokálním Windows uživatele nelze v cloudu
ověřit; jejich konkrétní stav musí určit vývojová diagnostika a výstup aplikace.

## Windows x86 pro potvrzený 32bitový Office

- Vyhodnocení MSBuild s OS=Windows_NT pro oba projekty: RuntimeIdentifier
  win-x86, UseAppHost=true, SelfContained=false.
- Linux sestavení a všech 18 kontrol: úspěch.
- WinX86Folder publish: úspěch. Nativní ACEnergo.Web.exe má ověřenou PE
  architekturu I386 (0x014c); aplikace nadále cílí na net8.0.
- Generovaný IIS web.config spouští ACEnergo.Web.exe, nikoli x64 dotnet s DLL.
- Windows x86 se sestavuje po jednotlivých projektech; přepínač -r nelze
  předat celému řešení (NETSDK1134). Na Windows získávají projekty RID
  automaticky z Directory.Build.props, nikoli z přepínače řešení.

Windows EXE a Office ACE provider nelze spustit na cloudovém Linuxu. Skutečné
přihlášení je třeba ověřit na PC s existující ACE DLL a runtime ASP.NET Core 8
x86. Kontrola DLL/registrace uživatelem potvrzuje přítomnost x86 provideru,
nikoli zatím funkční přihlášení a správnost dotazu tblLog.

Sestavení obou jednotlivých projektů s -r win-x86 následně prošlo bez chyb
a varování. PE hlavičky obou vývojových apphostů potvrzují I386 (x86).

## Druhá etapa – tarify

- Debug sestavení řešení .NET 8: bez chyb a varování.
- Základní kontroly: 18 prošlo. Nové kontroly tarifů: 193 prošlo, včetně všech
  168 výchozích buněk, hranic měsíců, detekce neúplných dat, verzování a
  serverových handlerů (oprávnění, neplatný formulář, potvrzení, konflikt).
- HTTP: nová stránka tarifů bez přihlášení přesměruje na Login; původní
  přihlašovací a CSRF kontroly prošly. Externí CSS a JS tarifů vracejí 200
  se správným MIME typem; JavaScript prošel node --check.
- Originální tabulka tblTarifyElektrina byla čtecím parserem ověřena: všechna
  pole použitá novým kódem existují, 2 940 řádků v 420 úplných skupinách,
  0 duplicitních klíčů, pouze kódy 0/1/2 a první dny měsíců v půlnoci.
- Native Access test WindowsTariffCheck je připraven, ale nebyl na Linuxu
  spuštěn. OLE DB round-trip, SQL aktualizace/vkládání, transakce
  a rollback zůstávají k ověření na Windows.

## Oprava nepodporované úrovně izolace ACE

Uživatel na Windows doložil OleDbException 0x8004D008 při BeginTransaction
se Serializable. Tento pokus ještě nedošel k SQL aktualizacím. Implementace
byla změněna na ReadCommitted se zachováním jediné transakce, kontroly
načtené verze a rollbacku. Pro koordinaci více instancí webu používá
výhradní pomocný souborový zámek nad stejnou MDB. Externí zapisovače
bez tohoto protokolu zámek nerespektují.

Po opravě prošlo všech 211 stávajících kontrol a nový test výhradního zámku
mezi dvěma skutečnými procesy, opětovného získání po uzavření a uvolnění po
výjimce. Publikace Windows x86 prošla. Nativní Access round-trip a rollback
přes ReadCommitted je nadále potřeba spustit na cílovém Windows testem
WindowsTariffCheck podle docs/MIGRACE.md.

## Třetí etapa – svátky

- Základní kontroly: 18 prošlo. Tarify: 194 kontrol prošlo. Svátky: 46 kontrol
  dat, verzí, oprávnění a handlerů prošlo. Test souborového zámku mezi procesy
  zůstal úspěšný. Přibylo ošetření InvalidDataException i v tarifní stránce.
- Autentizované HTTP testy přes ASP.NET Core testovací host: 28 prošlo.
  Skutečně se vykreslí Razor a odešlou formuláře, ale používají se testovací
  úložiště bez MDB. Testy ověřují práva pro čtení/zápis/administrátora, CSRF,
  neplatné datum, konflikt verze, vykreslení a změnu seznamu, externí soubory,
  HTML kódování dat a regresně všech 168 editovatelných polí tarifů.
- Běžné HTTP smoke testy: bez přihlášení se obě stránky přesměrují na Login;
  přihlášení, CSRF a externí CSS/JS včetně svátků prošly. JS prošel node --check.
- Čtecí kontrola původní tblSvatky: očekávané tři sloupce, 4 řádky, žádné
  duplicitní klíče ani časy mimo půlnoc; pouze značka _.
- Windows x86 publikace: úspěch. EXE je I386 a produkční web neobsahuje
  testovací závislost Microsoft.AspNetCore.Mvc.Testing.
- WindowsHolidayCheck je připraven; nativní OLE DB vložení, odebrání,
  ReadCommitted transakce a rollback zde na Linuxu nebyly provedeny.

Kontrolní projekt včetně WindowsHolidayCheck a HTTP testů se také úspěšně
sestavil pro win-x86, bez chyb a varování.
