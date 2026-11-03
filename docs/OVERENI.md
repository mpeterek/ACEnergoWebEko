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
