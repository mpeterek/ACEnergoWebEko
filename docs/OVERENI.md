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
