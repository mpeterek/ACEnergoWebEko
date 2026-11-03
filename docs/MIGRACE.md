# Migrace ACEnergoWebEko – stav implementace

Projekt obsahuje první etapu a navazující převod tarifů; úplný převod aplikace není dokončen.
Původní archiv: příloha `ACEnergoWebEko.-.kopie.zip` vydání `v.1.0` na GitHubu.
Archiv prošel kontrolou CRC. Originály i MDB zůstaly nezměněné mimo nový projekt.

## Ověřený rozsah

1 088 skutečných ASP souborů, 164 100 řádků včetně HTML a JavaScriptu.
Inventář `legacy-inventory.json` eviduje cesty, include závislosti, COM objekty,
odkazy na tabulky a výskyty ConnectDBase. Jde o statickou analýzu, nikoli důkaz
úplnosti dynamicky sestavovaných SQL dotazů. Metadata FrontPage jsou vyloučena.

| Oblast | ASP souborů | Stav |
| --- | ---: | --- |
| Elektřina | 319 | Převeden seznam a týdenní tarify; OLE DB zápisy čekají na ověření na Windows |
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
- MDB nejsou ve wwwroot. Přihlášení a seznamy jsou čtecí; modul tarifů může
  transakčně zapisovat do konfigurační MDB s oprávněním ElekConfig.
- Výběr období je validován proti dostupným souborům; vyhodnocení spotřeby
  nad tímto obdobím dosud není implementováno. Nezobrazuje se umělé měření.

## Dosud chybí pro úplnou migraci

Schéma a integrita MDB ověřené na Access Engine; úspěšné přihlášení se skutečným
účtem; test oprávnění uživatelů; ostatní konfigurace a zápisy; Windows ověření transakcí tarifů; výpočty spotřeb,
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

## Druhá etapa – týdenní tarify hlavního měření elektřiny

Převedeny TarifyElHl.asp a Sub_TarifyHlavniElHl.asp; rámce a hlavička jsou
sloučeny do Razor stránky s oddělenými CSS/JS. Nový odkaz **Tarify** je v
seznamu hlavních měření. Rozsah:

- 7 dní × 24 hodin; hodnoty 0 nízký, 1 vysoký, 2 víkend.
- Čtení přesného vybraného měsíce; předchozí tarif se automaticky nedědí.
- Chybějící měsíc ukáže výchozí rozvrh s výslovnou informací, že není uložený.
- Původní vysoký tarif má indexy hodin 5–9 a 16–19, tedy intervaly
  05:00–10:00 a 16:00–20:00; sobota a neděle používají víkendový tarif.
- Uložení přepíše vybraný měsíc a zbytek stejného roku až do prosince.
  Tato původní vlastnost je uvedena u tlačítka a vyžaduje potvrzení ve formuláři.
- Zápis chrání oprávnění ElekConfig nebo administrátor, CSRF, validace všech
  168 hodnot, kontrola načtené verze celého zapisovaného rozsahu a transakce.
- Existující denní řádky se aktualizují; chybějící se doplní bez mazání tabulky.
  Chyba nebo zjištěné duplicity přeruší transakci. Používá se ReadCommitted,
  protože místní ACE nepodporuje Serializable. Zápisy jednotlivých instancí
  této aplikace koordinuje výhradní souborový zámek MDB.tariffs.lock.
  Po získání zámku se znovu porovná verze tarifů v transakci. Zápis jinými
  nástroji (Classic ASP, samotný Access) tento pomocný zámek nerespektuje;
  během migrace je proto nepouštějte současně jako zapisovače stejné MDB.
  Pomocný soubor je prázdný, po ukončení zápisu zůstává na disku a jeho
  existence sama o sobě neznamená držený zámek. Za běhu ho nemažte.
- Svátky, ceny a vyhodnocení spotřeby do této etapy ještě nepatří.

Čtecím parserem Access Parser 0.0.6 byla v originální konfiguraci ověřena
existence všech použitých sloupců, 2 940 řádků, 420 úplných měsíčních skupin,
žádné duplicitní denní klíče a hodnoty tarifů pouze 0/1/2. Data byla pouze čtena.
To nenahrazuje testy nativních OLE DB příkazů a transakcí na Windows.

### Ověření nativního zápisu před používáním

Zastavte web i další aplikace zapisující do testované MDB. V terminálu VS
spusťte z kořene řešení (za poslední argument dosaďte skutečné ID hlavního
měření, které vidíte v seznamu):

```powershell
dotnet run --project tests/ACEnergo.Checks -- --tariff-mdb "C:\ACEnergoData\ACEnergoWebEko.mdb" 1
```

Test si vytvoří **dočasnou kopii** konfigurace a zapisuje pouze do této kopie
pro říjen–prosinec 2098. Ověřuje načtení, opakovaný zápis bez duplicit,
neovlivnění sousedních období, odmítnutí zastaralé verze a rollback po záměrně
vyvolané chybě uprostřed SQL zápisů. Potom kopii odstraní a porovná SHA-256
zdrojového souboru. Vyžaduje funkční Microsoft.ACE.OLEDB.16.0, Windows a
x86 runtime podle již ověřeného lokálního nastavení.

Tento nativní test se v cloudovém Linuxu nespouští a zatím není označen jako
prošlý. Pro první ruční použití nové editace použijte také testovací kopie MDB.
