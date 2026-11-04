# Migrace ACEnergoWebEko – stav implementace

Projekt obsahuje první etapu a navazující převod tarifů a svátků; úplný převod aplikace není dokončen.
Původní archiv: příloha `ACEnergoWebEko.-.kopie.zip` vydání `v.1.0` na GitHubu.
Archiv prošel kontrolou CRC. Originály i MDB zůstaly nezměněné mimo nový projekt.

## Ověřený rozsah

1 088 skutečných ASP souborů, 164 100 řádků včetně HTML a JavaScriptu.
Inventář `legacy-inventory.json` eviduje cesty, include závislosti, COM objekty,
odkazy na tabulky a výskyty ConnectDBase. Jde o statickou analýzu, nikoli důkaz
úplnosti dynamicky sestavovaných SQL dotazů. Metadata FrontPage jsou vyloučena.

| Oblast | ASP souborů | Stav |
| --- | ---: | --- |
| Elektřina | 319 | Převeden seznam, týdenní tarify a svátky; nativní integrační testy čekají na Windows |
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
  transakčně zapisovat do konfigurační MDB s oprávněním ElekConfig. Stejně
  nyní zapisuje také správa svátků.
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
- Ceny a vyhodnocení spotřeby do této etapy ještě nepatří. Svátky navazují
  v níže popsané třetí etapě.

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

## Třetí etapa – ručně zadané svátky hlavního měření

Převedeny všechny ASP soubory Elektrina/HlavniMereni/Svatky včetně hlavní
stránky a Sub_SvatkyElHl.asp. Rámce, ručně skládaný kalendář a klientský
VBScript nahrazuje jediná Razor stránka, nativní HTML výběr data a externí
CSS/JS. Odkazy jsou v seznamu hlavních měření a na stránce tarifů.

Zachované chování:

- tblSvatky je v konfigurační ACEnergoWebEko.mdb. Všechny dotazy jsou omezeny
  explicitním ID hlavního měření, nikoli globální session vybraného měření.
- Seznam zahrnuje všechny roky a je seřazený podle data. Nejde o automaticky
  generovaný kalendář státních svátků a nesouvisí s měsíčními MDB.
- Přidání existujícího data je beze změn. Nový záznam má strTarif = "_".
- Odebrat lze více vybraných dat najednou. Smažou se všechny řádky stejného
  data a měření, stejně jako v původní smyčce rs.Delete. Žádné jiné měření
  ani tabulka tarifů se touto operací nemění. Limit formuláře je 1000 dat.
- Date input přenáší ISO datum, server ho striktně validuje včetně přestupných
  let. Podporovaný rozsah odpovídá Access Date/Time: roky 0100 až 9999.
- Bez ElekConfig nebo administrátora je stránka pouze čtecí. POST má také
  serverovou kontrolu oprávnění, CSRF a token verze celého seznamu.
- Zápisy používají ReadCommitted transakci, rollback a stejný pomocný zámek
  jako tarify. Obě služby nyní sdílejí ElectricityConfigurationDatabase; název
  souboru .tariffs.lock zůstal kvůli kompatibilitě již nasazených instancí.
- Návrat do tarifů zachovává ID měření i vybraný měsíc. Kalendář samotný
  nepoužívá předchozí session ani rámce pro ukládání zvoleného data.

V originální MDB byly čtecím parserem ověřeny sloupce dtDatum, numIDHlavni,
strTarif a čtyři existující řádky: bez duplicit, s půlnocí a značkou _.
Parser nepoužívá Windows OLE DB a nemění databázi.

### Windows integrační test svátků

Zastavte web a jiné zapisovače do zdrojové MDB. Z kořene řešení spusťte
(číslo 1 nahraďte skutečným ID hlavního měření):

```powershell
dotnet run --project tests/ACEnergo.Checks -- --holiday-mdb "C:\ACEnergoData\ACEnergoWebEko.mdb" 1
```

Test vytvoří dočasnou kopii konfigurace. Pouze v ní ověří vložení se značkou _,
opakované vložení bez duplicity, konflikt staré verze, rollback po prvním
DELETE ve dvoupoložkové dávce a hromadné odebrání. Kontroluje také nezměněné
tarify a, pokud existuje další hlavní měření, jeho seznam svátků. Při jediném
měření je tento dílčí test explicitně označen jako SKIP. Dočasnou kopii poté
smaže a ověří SHA-256 původního souboru.

Tento test vyžaduje Windows, x86 runtime a existující ACE provider. V cloudu
zatím nebyl spuštěn; nativní zápisy tímto nejsou označeny za ověřené.

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

## Roční přehled cen

Z měsíčního ceníku je dostupný roční přehled všech 12 měsíců zvoleného roku.
Používá stejná pravidla přesného měsíce a označuje neuložené ceníky.
Ceny nesčítá a nezaměňuje je za náklady. Je pouze čtecí; každý měsíc lze
otevřít samostatně. Přehled zatím načítá měsíce jednotlivě (12 připojení).
Není garantován společný snapshot při souběžné externí změně ceníků.

Úplná Elektřina není dokončena: zbývá editace cen, konfigurace vstupů,
podružná a fiktivní měření, střediska, závody, účiník, výpočty spotřeb a
nákladů, grafy a exporty. Analogová měření zůstávají výslovně vyloučena.

## Editace cen a kapacit

Z měsíčního ceníku vede odkaz Upravit ceny a kapacity, dostupný pouze pro
ElekConfig nebo administrátora. Edituje všech 28 položek. Zápis zachovává
původní rozsah: od vybraného měsíce do prosince stejného roku. Používá
ReadCommitted, společný zámek, token celého zapisovaného rozsahu a rollback.
Prázdné položky znamenají NULL; čísla přijímají desetinnou čárku nebo tečku,
nikoli oddělovače tisíců. Hodnoty se zapisují parametrizovaně, číselné položky
přes OleDb.Double podle původní číselné databázové vrstvy. Stávající řádky se
aktualizují, chybějící se doplní. Duplicity zápis přeruší.

Sestavení a testy formuláře/validace proběhly v Linuxu. Skutečný OLE DB zápis
cen a rollback této nové funkce dosud nebyly ověřeny na Windows. První
uložení a kontrolu měsíců před/za rozsahem proveďte pouze na kopii MDB.
Výpočty nákladů ani zbývající měřicí moduly tímto nejsou dokončeny.

## Konfigurace existujícího hlavního měření

Nový odkaz Konfigurace vstupů nabízí zobrazení činného/jalového identifikátoru,
editaci názvu (1–30 znaků) a nenulových přepočtových koeficientů. Záporný
nenulový koeficient je přípustný podle původního pravidla Not equal to 0.
Odpojení jalového vstupu nastaví ID 0 a koeficient 1 stejně jako původní ASP.
NULL u nepřiřazeného jalového vstupu se pouze pro zobrazení interpretuje jako
nepřiřazený vstup; token stále zahrnuje původní NULL hodnoty. Přiřazený vstup
s NULL koeficientem je odmítnut jako neplatná konfigurace.

Činná vazba se touto editací nemění. Výběr nového fyzického vstupu, metadata
jednotek a zakládání měření dosud nejsou převedené. Neobnovuje se komunikace
s Energomatem. Práva ElekView/ElekConfig, CSRF, token verze, společný zámek
a ReadCommitted transakce chrání změny. Zdrojová MDB obsahuje očekávaná pole;
při čtecí analýze byly nalezeny i nepřiřazené jalové vazby NULL.

HTTP testy používají testovací úložiště. Skutečný OLE DB zápis a odpojení vazby
je ještě potřeba ověřit na testovací kopii MDB ve Windows. Analogy se nemigrují.

## Úvodní přehled a tři čtecí kroky

Odstraněn slogan Vaše energie na jednom místě. Přehled má přímé odkazy
na hlavní měření, fiktivní měření, střediska a závody. Volba datového období
se nevydává za globální změnu období všech následujících obrazovek.

1. Fiktivní měření: čtecí seznam ID, názvu a jednotek z tblKonfigFiktivni.
2. Střediska: odpovídající čtecí seznam z tblKonfigStredisko.
3. Závody: odpovídající čtecí seznam z tblKonfigZavod.

Všechny tři seznamy používají klíče z tblKonfigEnergie pro strNazevEnergie
Elektřina, čímž se vylučují jiné energie. Seznamy jsou řazené podle ID a
mají lokální DataTables pro hledání. Tabulky a všechny vybrané sloupce byly
ověřené čtecím parserem v originální MDB. Názvy tabulek jsou pevný whitelist;
URL nemůže zadat libovolnou tabulku. Přístup vyžaduje ElekView nebo admina.

Jde o tři čtecí části migrace, nikoli kompletní převod těchto oddílů. Složení
skupin, konfigurace, vazby, výpočty a grafy zůstávají další prací. Analogy
jsou nadále vyloučené. Nativní načtení je nutno ověřit na Windows; HTTP testy
používají testovací úložiště a prošly pro všechny tři trasy i oprávnění.

## Složení fiktivního měření elektřiny

Z názvu fiktivního měření v jeho čtecím seznamu vede odkaz na složení.
Parametrizovaný dotaz nejprve ověří příslušnost rodiče k elektřině, potom
načte tblKonfigFiktivniPolozky ve vazbě LEFT JOIN na tblKonfigImpuls, seřazené
podle numImpuls jako v původním menuFiktivniEl.asp. Chybějící definice vstupu
nezmizí z přehledu: zobrazí se jako Nedefinováno s původním ID/adresou.

Zobrazují se stanice/vstup (dělení a zbytek po 10000), název, jednotky výkonu
/ energie a uložené koeficienty A, B a P. NULL koeficient není nahrazován nulou;
je označen jako Neuvedeno. Prázdné složení je výslovně označené. Přístup
vyžaduje ElekView nebo administrátora; jiné energie a neexistující rodič vrací
404. Neprovádí se připojení k Energomatu ani migrace analogů.

Původní MDB potvrzuje tabulky a potřebné sloupce. HTTP testy s testovacím
úložištěm ověřují řádky, prázdné složení, chybějící definici, 404 a oprávnění.
Sestavení adresy má samostatné testy. Nativní OLE DB dotaz zůstává k ověření
na Windows. Jde pouze o čtecí složení: editace a výpočty z koeficientů nejsou
součástí této etapy.

## Společná konfigurace fiktivních měření, středisek a závodů

Nový detail `/ElectricityGroup/{kind}/{id}` propojuje všechny tři seznamy.
Pro `fictitious`, `centres` a `plants` obsahuje editaci názvu a jednotek výkonu
/ energie. Limity 30 a 15 znaků odpovídají schématu dodané MDB. Jednotky jsou
nepovinné; nezměněná prázdná hodnota zachovává původní NULL nebo prázdný řetězec.
Ukládání aktualizuje pouze tyto tři sloupce existujícího elektrického záznamu;
identifikátory, vazby a nastavení přepočtu nemění. Nezakládá ani nemaže záznamy.

Fiktivní měření a střediska zobrazují složení z příslušných tabulek Položky
s LEFT JOIN na tblKonfigImpuls. Závody zobrazují přiřazená střediska s odkazem
na jejich detail, koeficienty A/B/P a uložený příznak přepočtu. Chybějící
odkazované definice zůstávají viditelné. Střediska a závody zobrazují uložené
ID hlavního měření, střediska také příznak přepočtu. Původní URL složení
fiktivního měření zůstává funkční a odkazuje na novou konfiguraci.

Čtení vyžaduje ElekView, zápis navíc ElekConfig nebo administrátora.
POST používá antiforgery, SHA-256 verzi původních údajů, společný procesní
zamykací soubor a transakci ReadCommitted podporovanou ACE. Konflikt vrací
409 a vyžaduje nové načtení; neplatná data 400; chybějící záznam či neznámý
oddíl 404. Původní ASP a externí Access zamykací soubor nerespektují;
při editaci nepoužívejte souběžné externí zapisovače.

Tato etapa dokončuje základní editaci a čtecí složení tří oddílů společně.
Editace položek složení, nové záznamy, bilanční výpočty a grafy těchto oddílů
zůstávají další částí migrace. Analogová měření jsou vyloučená.
Nativní OLE DB čtení a zápis na Windows zatím nejsou touto etapou ověřené;
první ověření proveďte na kopii konfigurační MDB.
