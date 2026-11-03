# Migrace ACEnergoWebEko – stav implementace

Toto je první implementační etapa, nikoli dokončený převod aplikace.
Původní archiv: příloha `ACEnergoWebEko.-.kopie.zip` vydání `v.1.0` na GitHubu.
Archiv prošel kontrolou CRC. Originály i MDB zůstaly nezměněné mimo nový projekt.

## Ověřený rozsah

1 088 skutečných ASP souborů, 164 100 řádků včetně HTML a JavaScriptu.
Inventář `legacy-inventory.json` eviduje cesty, include závislosti, COM objekty,
odkazy na tabulky a výskyty ConnectDBase. Jde o statickou analýzu, nikoli důkaz
úplnosti dynamicky sestavovaných SQL dotazů. Metadata FrontPage jsou vyloučena.

| Oblast | ASP souborů | Stav |
| --- | ---: | --- |
| Elektřina | 319 | Převeden čtecí seznam hlavních měření; ostatní čeká |
| Plyn | 215 | Čeká |
| Teplo | 174 | Čeká |
| Voda | 170 | Čeká |
| Ostatní | 118 | Čeká |
| Analog | 29 | Čeká |
| Knihovna | 35 | Převeden výpočet názvu měsíční MDB |
| Servis | 20 | Implementována autentizace; správa uživatelů čeká |
| Kořen a Path | 8 | Nové rozvržení a přesměrování vstupní URL |

## Implementované smlouvy

- `Knihovna/Utility.asp: ConnectDBase`: název obsahuje rok minus 2000, doplněný
  nejméně na dvě číslice, a dvoumístný měsíc. Rok 2100 tedy dává `100`, nikoli `00`.
  Podporovaný rozsah nového rozhraní je 2000–9999; historické roky před 2000
  vyžadují samostatné ověření skutečných souborů.
- Konfigurace `ACEnergoWebEko.mdb`, autentizace `ACWebEkoLogin.mdb`.
- `Servis/Logon/Logon.asp`: parametrizovaný dotaz nad `tblLog`, Trim na jménu
  a hesle, stejné MDB porovnání textu, všech 13 příznaků oprávnění v claims.
  Nová aplikace nepřenáší heslo do cookie ani logu a databázi nemění.
- `Elektrina/HlavniMereni/SeznamHlavniElHl.asp`: čtení `strNazev`, `numID`
  z `tblKonfigHlavniElektrina`, řazení numID ASC. Nová stránka vynucuje ElekView
  nebo administrátora; tuto správu administrátorského přístupu ověřit proti menu.
- MDB nejsou ve wwwroot. Aplikace nyní otevírá databáze pouze ke čtení.
- Výběr období je validován proti dostupným souborům; vyhodnocení spotřeby
  nad tímto obdobím dosud není implementováno. Nezobrazuje se umělé měření.

## Dosud chybí pro úplnou migraci

Schéma a integrita MDB ověřené na Access Engine; úspěšné přihlášení se skutečným
účtem; test oprávnění uživatelů; konfigurace, zápisy a transakce; výpočty spotřeb,
tarify, účiník, rezervovaná kapacita, směny, ztráty, DPH, zaokrouhlování; roční
sestavy a čtení více MDB; všechny grafy, exporty a importy používané bez Energomatu;
správa uživatelů; úplné mapování starých URL; regresní srovnání s Classic ASP.
Chilkat/Energomat se podle zadání nepřevádí. Ostatní použití WScript.Shell a
FileSystemObject je třeba posoudit jednotlivě. Staré klientské VBScript/IE
skripty budou nahrazeny JavaScriptem podle konkrétních funkcí.

## Akceptace dalších etap

Každou funkci porovnat na stejných kopiích MDB a stejných vstupech, včetně
Null/Empty, české lokalizace, hranic měsíců/roků, letního času a zaokrouhlování.
Zápisy porovnat na oddělených kopiích včetně chování při souběhu a zamčení.
Teprve poté označit příslušný záznam inventáře jako ověřeně migrovaný.
