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

## Regrese: chybějící Period při otevření tarifů

HTTP test reprodukoval anglickou validační chybu při GET /ElectricityTariffs/5
bez query parametru. Ne-nullable bindovaná vlastnost měla implicitní Required,
i když inicializátor nabízel aktuální měsíc. Period je nyní nullable a výchozí
měsíc se explicitně nastavuje pouze při GET bez parametru; odpovídající záznam
ModelState se odstraní, aby skryté pole nepřevzalo prázdnou původní hodnotu.

Prošlo všech 35 autentizovaných HTTP kontrol, včetně otevření bez query,
platného skutečně vykresleného hidden Period a uložení upravených 168 hodnot
bez query parametru. POST bez období, s prázdným nebo neplatným obdobím
vrací 400 bez zápisu; explicitně prázdný GET parametr je také odmítnut.
Základních 18, tarifních 194 a svátečních 46 kontrol i procesový zámek prošly.

## Čtecí přehled cen a vyřazení modulu Analog

Ve skutečné konfigurační MDB byla čtecím parserem ověřena existence všech
28 použitých polí ceníku (444 řádků). Nové testy kontrolují české desetinné
hodnoty, NULL a přepínače jednotek; HTTP testy zobrazení bez explicitního
období, vybraný měsíc, neplatný měsíc, neexistující měření a oprávnění.
Nabídka administrátora již neobsahuje analogová měření. Nativní načtení
přehledu přes OLE DB je třeba ověřit na Windows; HTTP test používá testovací
úložiště. Přehled neobsahuje žádnou zapisovací operaci.

## Jednotky a DataTables v cenících

Ceník i editor uvádějí jednotky: MW, Kč/MW, Kč/MWh, Kč/MVArh, Kč a %.
Meze nedočerpání/přečerpání rozlišují % a MWh podle příslušného přepínače;
NULL přepínač nezavádí domnělou jednotku. Kapacitní sazby Kč/MW jsou ověřené
proti násobení kapacity v BilanceMesic.asp a BilanceDen.asp, nikoli proti
nepřesnému skupinovému nadpisu původního formuláře.

DataTables 2.3.4 a jQuery 3.7.1 jsou lokální s přiloženými licencemi.
V Chromium byl na skutečně vykresleném Razor HTML s 28 testovacími položkami
ověřen start DataTables, hledání DPH a všech 14 sloupců (položka, jednotka,
12 měsíců). Při 1920/1440/1280 px nebyl vodorovný přesah; při 768 px tabulka
používá horizontální posouvání. Žádný měsíc se neskrývá, stránkování ani
vnitřní svislé posouvání nejsou zapnuté. Výška celého dokumentu zůstává závislá
na počtu řádků a velikosti okna. Hodnoty se nekrátí ani nově nezaokrouhlují.

## Oprava záhlaví měsíců a oříznutého hledání

DataTables má vlastní padding a flex obal záhlaví. Hlavičky měsíců i jejich
hodnoty nyní používají shodné pravé zarovnání a padding. Jednotka má větší
šířku a její záhlaví se neláme. Posouvání je pouze na řádku tabulky DataTables;
vyhledávání zůstává mimo ořezávající kontejner, s místem pro focus outline.

Chromium: při 1920/1440/1280/768/390 px rozdíl pravých okrajů měsíčního
záhlaví a hodnoty 0 px; celé hledací pole včetně 6px focus rezervy zůstalo
v obrazovce a celý dokument neměl vodorovný přesah. Na úzkých obrazovkách
se posouvá pouze tabulka. Volitelný test je tools/check-price-layout.cjs.

Úvodní přehled a tři katalogy elektřiny: regresní HTTP testy ověřily odstranění
sloganu, přítomnost nové navigace, vykreslení každé kategorie s jednotkami,
odmítnutí neznámého/analogového oddílu a zákaz bez ElekView. Celkem 56
HTTP kontrol prošlo, ostatní existující kontroly také. Původní MDB čtecím
parserem potvrzuje numIDEnergie, numCislo, strNazev, strRozmerP a strRozmerV
ve všech třech tabulkách. Žádné zápisy nejsou v nových katalozích implementovány.

Čtecí parser má omezení u komprimovaného Unicode písmene ř v názvu energie;
pro kontrolní počty byly identifikované odpovídající klíče podle štítku Elekt…:
44 fiktivních měření, 44 středisek, 1 závod. Produkční SQL nepoužívá tento
pomocný prefix: zachovává přesný parametr Elektřina z původního ASP a spoléhá
na dekódování OLE DB providerem. Kontrolu nativního filtru je nutno dokončit
na Windows. Publikace x86 prošla bez chyb.

## Cyklické přepínání tarifů

JavaScript mění každý editovatelný select na barevné tlačítko. Původní
select zůstává skrytý a nese hodnotu formuláře; bez JS zůstává použitelný
původní výběr. Pořadí je Nízký → Vysoký → Víkend → Nízký. Read-only zobrazení
se nemění. Chromium ověřilo 168 tlačítek, čtyři kliknutí s přetočením,
barvy, mezerník a Enter, neodeslání formuláře kliknutím, upozornění na změny
a všech 168 položek FormData. Volitelný test: tools/check-tariff-cycle.cjs.
Regresní konzolové a HTTP kontroly prošly. Ukládací backend se neměnil.

Login: doplněno tlačítko Zobrazit heslo / Skrýt heslo, type=button,
aria-controls a aria-pressed. Chromium ověřilo myš, mezerník, zachování hodnoty,
neodeslání formuláře a šířku 390 px bez přesahu. Výchozí stav je password;
bez JavaScriptu se nefunkční přepínač nezobrazí. Backend přihlášení se neměnil.

Login – ikona oka: textový přepínač nahrazen ikonou uvnitř pole vpravo.
Skryté heslo má přeškrtnuté oko, zobrazené běžné oko. Přístupný popisek,
aria-pressed a ovládání klávesnicí zůstávají. Chromium při šířce 390 px
ověřilo umístění uvnitř pole, načtení SVG, přepnutí myší a mezerníkem i
zachování hodnoty. Backend přihlášení se neměnil.

Společná konfigurace tří oddílů elektřiny: sestavení bez varování a chyb;
regresní konzolové kontroly a 91 autentizovaných Razor/HTTP kontrol prošly.
Nové kontroly pro každý oddíl pokrývají čtení, HTML kódování, editační prvky,
POST, CSRF, podvržený zápis čtenářem, chybný název, konflikt verze a 404.
Ověřen je odkaz závod → středisko, whitelist oddílů a oprávnění. Čisté kontroly
validují hranice názvu 30 znaků a nepovinných jednotek 15 znaků.
Schéma a délky polí byly ověřené čtecím parserem dodané MDB. HTTP používá
výhradně testovací úložiště; neprokazuje nativní OLE DB operace. Ty vyžadují
Windows s odpovídajícím ACE providerem a kopii MDB pro první zápis.

Správa složení fiktivních měření, středisek a závodů:

- Regresní konzolové kontroly a 140 autentizovaných Razor/HTTP kontrol prošly.
  Nové testy pokrývají hledání, čtecí režim, přidání / změnu / odebrání,
  skutečně navázané hodnoty koeficientu i checkboxu, potvrzení odebrání,
  duplicitní položku, neplatný koeficient, konflikt verze, chybějící CSRF,
  podvržené zápisy čtenářem, 404 a vyloučení analogového oddílu.
- Čisté kontroly pokrývají koeficienty s čárkou i tečkou, zápornou hodnotu,
  nulu, odmítnutí NaN / Infinity / přetečení / neplatných vstupů,
  popisky virtuálních ID a whitelist tabulek.
- Chromium ověřilo skutečně vykreslené Razor HTML při 1440, 768 a 390 px:
  bez horizontálního přesahu stránky, povinné potvrzení před odebráním,
  správnou hodnotu koeficientu, ChildId a Version ve FormData.
- Původní ASP ověřuje editovaná pole, filtr archivovaných elektrických vstupů,
  offset fiktivních ID a účinek odebrání střediska ze závodu. Schéma dodané
  MDB potvrzuje všechny použité tabulky a sloupce.

HTTP kontroly používají testovací úložiště. Nativní OLE DB dotazy, transakce
ani vazby přes IIF / LEFT JOIN nebyly spuštěné proti ACE na Windows.

## Diagnostika 404 u střediska 16

Trasa `/ElectricityGroup/centres/16` je definována v Razor Pages. Dodaná
původní MDB obsahuje středisko 16 s numIDEnergie=2; tento klíč patří mezi
klíče Elektřiny. To neprokazuje obsah MDB ani verzi procesu na místním PC.
Příčina uživatelem hlášené 404 zatím nebyla reprodukována v jeho prostředí.

Rozšířené HTTP kontroly nyní načítají skutečné HTML seznamu středisek,
vyhledají oba generované odkazy pro ID 16 a projdou na jejich cíle. Obě
stránky vracejí 200 nad testovacími úložišti. Celkem prošlo 146 HTTP kontrol.
Kontroly navíc rozlišují 404 chybějící konfigurace s vysvětlením v těle
stránky a 404 chybějícího endpointu s vlastní stránkou aplikace.

V serverovém výstupu `GROUP-NOT-FOUND` znamená, že handler existuje, ale
příslušný záznam nebyl načten z používané konfigurační MDB; záznam obsahuje
oddíl, ID a cestu k souboru. `PAGE-NOT-FOUND` znamená, že pro danou cestu
neexistuje stránka v běžícím procesu. Hodnoty ani cesty z těchto diagnostik
nejsou zobrazovány anonymně v prohlížeči. Při přidání nových Razor Pages
je vhodné zastavit aplikaci a znovu ji sestavit a spustit; samotné obnovení
prohlížeče nebo běžící Hot Reload nemusí načíst novou trasu.

## Redesign pracovní plochy podle AGENTS.md a návodu

- `dotnet build ACEnergoWebEko.sln --no-restore -m:1 --nologo`: PASS,
  bez chyb a varování; Debug nespouští minifikátor.
- `dotnet build ACEnergoWebEko.sln -c Release --no-restore -m:1 --nologo`:
  PASS, bez chyb a varování, včetně vytvoření minifikovaného CSS a JS.
- `dotnet run --project tests/ACEnergo.Checks --no-restore`: PASS včetně
  148 Razor/HTTP kontrol, oprávnění, CSRF a existujících ukládacích handlerů.
- Chromium: pracovní plocha 1440/1024/768/390 px bez vodorovného přesahu,
  strom vlevo na desktopu, hledání a jeho prázdný stav, obnovení seznamu,
  odeslané hodnoty koeficientu a ID beze změn. Žádná JS chyba při načtení.
  Vlastní nový skript prošel parserem Acorn s ecmaVersion=5.
- `tools/check-price-layout.cjs`: PASS při 1920/1440/1280/768/390 px
  nad nově vykresleným HTML se společným Bootstrapem a AdminLTE.
- `tools/check-tariff-cycle.cjs`: PASS nad novým HTML, včetně 168 hodnot,
  kliknutí a klávesnice.
- `dotnet publish src/ACEnergo.Web -p:PublishProfile=WinX86Folder --nologo`:
  PASS. Po přidání build dependency bylo nutné znovu obnovit závislosti pro
  win-x86; první pokus s --no-restore skončil NETSDK1047, opakování s restore
  úspěšně vytvořilo publikaci.

Použitý SDK v prostředí: /workspace/dotnet/dotnet. Nativní ACE na Windows
není k dispozici; funkčnost jeho dotazů ani odstranění lokální chyby 404
nejsou touto validací potvrzené. Podrobnosti rozsahu: UI-ELEKTRINA.md.

## Nula řádků detailu při funkčním katalogu (GROUP-NOT-FOUND)

Uživatelský výstup lokalizoval chybu do ElectricityGroupConfiguration.Read:
ExecuteReader/Read nevrátil záznam pro fictitious/50000001. Trasa i ID jsou
správné; nešlo o chybějící Razor Page. Katalog takový záznam zobrazuje.
Rizikovým rozdílem je kombinace pozičního parametru ID ve vnějším dotazu
s parametrem energie ve vnořeném dotazu ACE/OLE DB.

Dotazy detailu, editoru složení, validace nového vstupu a původní čtecí
stránky nyní používají pro energii pevný SQL literál 'Elektřina'. Hodnota
nikdy nepochází od uživatele. ID, názvy, jednotky, hledaný text i koeficienty
zůstávají parametrizované. Filtr energií ani ochrana zápisů nebyly odebrány.
Tím se odstranilo riziko pořadí parametrů mezi vnitřním a vnějším dotazem.
Konkrétní chování uživatelova ACE není možné v Linuxu reprodukovat;
úspěšné nativní načtení po této změně musí potvrdit Windows.

Debug build a stávajících 148 HTTP kontrol prošly. HTTP fakes tuto nativní
chybu neodhalují. Doplněna proto volitelná čtecí kontrola pro Windows,
která pro každý záznam elektrického katalogu skutečně načte nativní detail,
editor složení a u fiktivních měření i původní čtecí stránku:

```powershell
dotnet run --project tests/ACEnergo.Checks -- --group-mdb "C:\ACEnergoData\ACEnergoWebEko.mdb"
```

Kontrola nevolá žádnou ukládací metodu. Porovnává názvy a ID položek mezi
katalogem a detaily; vypíše selhávající kind/ID. Při chybě nebo nulovém počtu
ověřených záznamů končí nenulovým kódem. Je sestavená, ale zde nebyla spuštěná
proti ACE. Vyžaduje stejnou bitovost a provider jako aplikace.

## Kompaktní responzivní velikosti

Debug i Release sestavení bez chyb a varování, 148 HTTP kontrol a publikace
WinX86Folder prošly. Release vytvořil responsive.min.css. Pro kompatibilitu
s používaným minifikátorem je aritmetika uvnitř clamp zapsaná přes calc.

Nový `tools/check-responsive-layout.cjs` ověřil osm skutečně vykreslených
stránek (přehled, login, hlavní konfigurace, katalog skupin, detail skupiny,
editor položek, roční ceny a tarify) při 1920×1080, 1440×900, 1366×600,
1024×768, 768×1024, 844×390, 390×844 a 320×568. Posledních pět profilů
simuluje dotyk. Všech 64 kombinací prošlo pro zdrojový i minifikovaný styl:
žádný vodorovný přesah celé stránky, odpovídající umístění stromu, text 14–15 px,
nadpisy 20–29 px, dotyková pole 16 px a tlačítka alespoň 44 px. Kontroluje se
také přepínání hesla a nepřítomnost JavaScriptových chyb.

Samostatné kontroly check-price-layout.cjs a check-tariff-cycle.cjs prošly
s novými styly. Zarovnání měsíců, hledání DataTables i 168 hodnot tarifů se
zachovaly. Opraven byl přesah stránky u skrytých popisků tarifních buněk;
ukotvení scrollovacího kontejneru zachovává popisky pro asistivní technologie.

Reprodukce přípravy HTML (Bash, adresář vytvořit předem):

```bash
mkdir -p /tmp/acenergo-responsive
ACE_RESPONSIVE_DIR=/tmp/acenergo-responsive \
ACE_COMPOSITION_HTML=/tmp/acenergo-responsive/composition.html \
ACE_PRICE_HTML=/tmp/acenergo-responsive/prices.html \
ACE_TARIFF_HTML=/tmp/acenergo-responsive/tariffs.html \
dotnet run --project tests/ACEnergo.Checks --no-restore
node tools/check-responsive-layout.cjs /tmp/acenergo-responsive
ACE_RESPONSIVE_MINIFIED=1 node tools/check-responsive-layout.cjs /tmp/acenergo-responsive
```

Browser kontrola vyžaduje Playwright a Chromium (cestu lze nastavit přes
CHROMIUM_PATH); druhý běh vyžaduje předchozí Release build. Jde o emulované
profily Chromia, nikoli o test na fyzických tabletech či telefonech.

## Jediný strom, širší názvy a editace vazeb

Debug build a 167 HTTP kontrol prošly. Nové kontroly ověřují výběr hlavního
měření u středisek/závodů, možnost odpojení, příznak pouze u střediska,
read-only režim, podvržený zápis čtenářem, neexistující/záporné ID a konflikt
verze. Samostatná validace odmítá pole, která daný druh skupiny nemá.
Katalogová trasa vrací strom a výzvu k výběru, nikoli druhou tabulku všech
skupin. Testy sledují skutečný odkaz ze stromu a další akce až v detailu.

Rozšířená kontrola tools/check-responsive-layout.cjs zahrnuje 11 stránek:
k původním osmi přibyl editor cen, svátky a měsíční ceny. Všech osm rozměrů
1920×1080 až 320×568 prošlo se zdrojovými styly. Ověřuje se také kompaktní
šířka ukládacích tlačítek, nowrap názvů, dynamické rozšíření panelu při vložení
dlouhého názvu, bez přesahu dokumentu. Zdrojový i minifikovaný skript stromu
prošel Acorn ecmaVersion=5. Release build včetně minifikace a publikace x86
prošly; produkční varianty stylů byly také ověřené na všech rozměrech.

Nativní zápisy numIDHlavni / bPrepocet nebyly spuštěné proti Windows ACE.
Odpovídající tabulky a původní pravidlo rušení přepočtu položek při odpojení
hlavního měření závodu byly ověřené v původních ASP zdrojích.

## Výběr položek v modálu podle elektrických typů

Debug build a 196 HTTP kontrol prošly. Nové kontroly pokrývají modal a horní
návrat, počáteční seznam bez hledaného textu, oddělení fyzických a fiktivních
vstupů, filtr signatury typu, označení přiřazené položky, kurzor pokračování,
neplatný zdroj a rodiče a zákaz přístupu čtenářům. Čistá validace ověřuje také
omezení délky filtrů a záporný kurzor.

`tools/check-composition-picker.cjs` nad skutečným HTML a JSON exportovanými
HTTP testy prošel při 1440×900, 768×1024, 390×844 a 844×390, se zdrojovými
i produkčními minifikovanými soubory. Kontroluje otevření Bootstrap modálu,
DataTables řádky, záložky a typ, hledání a prázdný výsledek, chybu/retry,
syntetickou pokračovací dávku, zaměření hledání, klávesové záložky, Escape,
navrácení fokusu, rozměry okna i skutečné POST hodnoty ChildId/Version/CSRF.
Nový skript i minifikovaný výstup prošly parserem Acorn ecmaVersion=5.

Testovací HTML a JSON lze připravit zapnutím ACE_COMPOSITION_HTML a
ACE_PICKER_DIR při spuštění konzolových kontrol. Browser test vyžaduje
Playwright a Chromium:

```bash
node tools/check-composition-picker.cjs /tmp/acenergo-responsive/composition.html /tmp/acenergo-picker
ACE_PICKER_MINIFIED=1 node tools/check-composition-picker.cjs /tmp/acenergo-responsive/composition.html /tmp/acenergo-picker
```

Rozšířená Windows kontrola --group-mdb nyní čte také kandidáty všech kategorií
po dávkách a ověřuje elektrické signatury, ID a filtr typu. Zde není nativní
ACE k dispozici; tato část je sestavená, ale proti MDB nebyla spuštěná.
Browser test používá testovací JSON, nikoli živou MDB. Ukládací operace Add
se neměnila. Release build, minifikace a publikace Windows x86 prošly.
