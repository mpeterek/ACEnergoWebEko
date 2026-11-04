# Pracovní plocha elektřiny

Tato etapa přepracovává existující obrazovky fiktivních měření, středisek a
závodů podle uživatelského návodu „ACEnergoWebEko – modul Elektřina“, zejména
kapitol 3.3, 3.4 a 3.4.1. Databázová schémata, výpočty, existující adresy,
autorizace, názvy modelů a ukládací služby zůstávají zachované.

## Rozložení

Společný `_GroupLayout` obsahuje strom skupin vlevo a pracovní plochu vpravo.
Strom dovoluje hledat podle názvu a ID. U vybrané skupiny ukazuje její položky;
kliknutí na položku otevře její konfiguraci na příslušném místě. Ostatní větve
nabídnou otevření příslušného vstupu. Na telefonu se strom přesune nad detail.
Pravá plocha používá tlačítka Konfigurace vstupu a Položky vstupu / závodu.
Přechody zachovávají stejnou pracovní plochu a stávající URL.

Podle PDF znamená fiktivní vstup matematické složení reálných vstupů pomocí
koeficientů (včetně záporného P pro odčítání a zlomku pro poměrný podíl).
Podružná měření jsou samostatný oddíl. Ceny podružných, fiktivních měření,
středisek a závodů jsou podle návodu sdílené podle typu/signatury vstupu;
nejde o měsíční ceník hlavního měření. Vyhodnocení používá den, měsíc a rok,
případně tarifní pásma přiřazeného hlavního měření. Tyto výpočty se nesmějí
nahrazovat součtem zobrazených koeficientů ani ceníkem hlavního měření.

## Použité knihovny

Lokální soubory, bez CDN a bez nového klientského frameworku:

- Bootstrap 5.3.8, MIT.
- AdminLTE 4.0.0, MIT, řada kompatibilní s Bootstrapem 5.
- Font Awesome Free 6.7.2, licence zahrnutá ve složce knihovny.
- Stávající jQuery a DataTables zůstávají zachované.

Balíčky byly stažené z npm se shodou kontrolního součtu integrity z metadat.
Licence jsou vedle distribučních souborů ve wwwroot/lib. Vlastní nový JS je
ES5; již minifikované cizí knihovny se znovu nepředávají staršímu minifikátoru.

BuildBundlerMinifier 3.2.449 je pouze build dependency (PrivateAssets=all),
nikoli nový runtime framework. bundleconfig.json minifikuje nové soubory
measurement-workspace.css/js pouze při Release. Debug má RunBundleMinify=False.
Development načítá čitelné originály, ostatní prostředí vygenerované minifikované
soubory. Vygenerované soubory jsou v repozitáři i publikaci. Dříve existující
stránkové skripty nebyly v této etapě hromadně přepsány ani nově minifikovány.

## Co tato etapa nedokončuje

Tlačítka Vyhodnocení a Konfigurace ceny u skupin jsou výslovně označená jako
nepřevedená. Podružná měření jsou na Přehledu označená jako připravovaný oddíl;
jejich vyhodnocení stále chybí. Samotný redesign je nenahrazuje. Zakládání
samotných skupin, kompletní informační tabulka typu/ceny/časových posunů,
bilanční výpočty, grafy a exporty z návodu vyžadují následnou samostatnou migraci.
Analogy se nemigrují a neprovádí se živá komunikace s Energomatem.

## Chyba 404

Chyba na uživatelově Windows nebyla zatím reprodukována. HTTP testy ověřují
odkazy ze skutečného HTML katalogu na obě stránky střediska 16 nad testovacími
úložišti. Nativní OLE DB tím ověřené není. Diagnostika PAGE-NOT-FOUND znamená
chybějící stránku běžícího procesu; GROUP-NOT-FOUND znamená nenalezenou
konfiguraci v používané MDB. Druhý záznam uvádí oddíl, ID i cestu k MDB pouze
v serverovém logu. Pro určení konkrétní příčiny je potřeba tento výstup z PC.

## Responzivní velikosti společné pro stránky

`wwwroot/css/responsive.css` se načítá jako poslední společný styl. Zmenšuje
písmo, nadpisy, formuláře, tabulky, hlavičku a mezery také na stránkách mimo
pracovní plochu. Používá rem a respektuje výchozí velikost písma prohlížeče.
Při běžném základu 16 px má text 14 px, od šířky 1800 px 15 px; nadpisy se
plynule mění přibližně mezi 20 a 29 px. Nízké notebooky (výška do 700 px)
šetří místo hlavně zmenšením odsazení. Není zakázáno přiblížení prohlížeče.

Ovládací prvky pro myš mají běžně výšku 34 px, dotykové nebo mobilní alespoň
44 px. Textová pole na dotykových zařízeních mají 16 px, aby zůstala čitelná
a zamezila automatickému přiblížení při editaci na mobilu. Pole hesla zachovává
prostor pro ikonu oka. Tablet od šířky 768 px ponechává oba sloupce; mobil
nebo nízké okno do 1023×500 px skládá strom nad pracovní obsah a omezuje jeho
výšku. Široké tabulky posouvají pouze svůj kontejner. Skryté popisky buněk
jsou ukotvené v něm a nerozšiřují celou stránku.

Nový styl je zahrnutý v bundleconfig.json: Debug používá originál, Release
vytváří responsive.min.css a produkční prostředí jej načítá. Pravidla
routování, model binding a databázová logika se v této úpravě nemění.

## Výběr měření a kompaktní konfigurace

Úvod fiktivních měření, středisek a závodů už neopakuje katalog v pracovní
ploše. Jediný výběr je ve stromu vlevo; vpravo je před výběrem stručná výzva.
Akce vybraného vstupu se objeví až po jeho výběru. Společný návrat na Přehled
je umístěn pod pracovní plochou.

Strom má širší výchozí panel a vlastní ES5 skript měří šířku názvů. Šířku
nastaví CSS proměnnou, nejvýše na 480 px a v mezích podílu šířky okna.
Názvy se nezalamují; při nedostatku místa lze posunout pouze obsah stromu.
Na mobilu zůstává celý panel nad pracovní plochou. Nové konfigurace používají
dvousloupcová pole a běžně široké tlačítko Uložit změny vpravo dole; na mobilu
jsou pole pod sebou. Stejný formát dostala konfigurace hlavního měření a
editor cen. Navigace tarifů, svátků a cen používá kompaktní tlačítka.

Hlavní měření střediska a závodu je nově editovatelné výběrem názvu a ID
z tblKonfigHlavniElektrina. Lze zvolit Bez hlavního měření. U střediska je
editovatelný také uložený bPrepocet (Neuvedeno / Zapnutý / Vypnutý). Jde o
příznak střediska, nikoli nahrazení příznaků jednotlivých vazeb závodu.
Fiktivní vstupy tato pole v konfigurační tabulce nemají a formulář je nenabízí.

Vazba, příznak, název i jednotky se ukládají atomicky s dosavadní verzí,
ReadCommitted a společným zámkem. Nové hlavní měření se ověřuje proti MDB;
neexistující nebo záporné ID se odmítne. Nezměněnou starší nedostupnou vazbu
lze zachovat při editaci názvu, aniž by byla skrytě odstraněna. Původní NULL
vazba zůstane NULL, původní odpojení 0 zůstane 0. Hodnota příznaku Neuvedeno
znamená NULL. Původní Save API je zachované a bez nových polí vazby nemění.

Podle UpDateZavodyH.asp odpojení hlavního měření závodu také nastaví bPrepocet
na False na jeho položkách, ve stejné transakci; formulář tento účinek uvádí.
Databázové schéma, oprávnění a adresy se nemění. První nativní ověření nových
zápisů proveďte na kopii MDB; zde jsou k dispozici pouze HTTP testovací úložiště.

## Modální výběr přiřazených položek

Návrat Zpět na přehled je nyní nahoře v liště oddílu. Přidávací akce
Vybrat ze seznamu je u nadpisu přiřazených položek, bez nutnosti procházet
celý jejich výpis. Otevírá Bootstrap modal s tabulkou DataTables.

U středisek jsou dvě záložky: Podružná měření a Fiktivní měření. U fiktivního
vstupu se vybírají podružné reálné vstupy; u závodu střediska. Seznam se načte
hned po otevření. Nabízí hledání názvu či úplného ID a filtr skutečné signatury
strTyp / strTypEnergie, nikoli jen obecný popisek druhu vstupu.

Data vždy spadají do Elektřiny podle tblKonfigEnergie. Reálné vstupy navíc
zachovávají původní filtr boolArchiv. Fiktivní vstupy a střediska se spojují
s energií přes numIDEnergie. Kategorie se ověřuje podle druhu rodiče i při
přímém požadavku na server. Typy dropdownu jsou definované elektrické typy.

GET handler Candidates na existující stránce čte nejvýše 100 položek a příznak
pokračování. Další dávka používá kurzor numCislo, takže dlouhé seznamy nejsou
načítané celé do modálu a fiktivní vstupy nejsou vytlačené reálnými položkami
ze společného limitu. Filtrování probíhá na serveru přes celou kategorii;
DataTables vykresluje dávky bez druhého klientského filtru či stránkování.

Již přiřazené položky mají označení a nemají tlačítko Přidat. Tlačítko konkrétní
položky odesílá normální POST Add se stávajícím tokenem CSRF, verzí a ChildId.
Ukládací metody, kontrola duplicit, oprávnění i transakce zůstávají zachované.
Bez JavaScriptu zůstává dostupné původní textové hledání v noscript části.

Na mobilu je modal přes celou obrazovku, tabulka se posouvá ve vlastním
kontejneru a tělo okna svisle. Záložky podporují šipky/Home/End, Escape zavírá
modal a vrací fokus na spouštěcí tlačítko. Fokus po otevření jde do hledání;
po načtení další dávky přejde na první novou dostupnou položku. Tím zůstává
klávesnice uvnitř okna i po zmizení tlačítka pro pokračování. Zohledněna je
vlastní obsluha fokusu AdminLTE. Vlastní JS je ES5 a nové soubory se minifikují
v Release přes stávající bundleconfig.json.

## Adresy vstupů, stanice a hustší řádky

Modální DataTables má hned za ID sloupce Adresa stanice a Č. vstupu. Podle
Knihovna/Utility.asp / StrFormatStaniceVstup se číslo stanice získá celočíselným
dělením ID číslem 10000 a číslo vstupu zbytkem po dělení. Obě části se doplní
na čtyři místa: 10003 → 0001 / 0003; 50000001 → 5000 / 0001. U fiktivních
vstupů zůstává původní ID nezměněné. Původní strom středisek pro ně používal
značku (Fikt); samostatné číslování skupin zůstává v hlavním stromu zachované.
Střediska vybíraná do závodu nemají hardwarovou stanici: zobrazí se — a jejich
vlastní číslo střediska, jak odpovídá původním SeznamStrediskaPolozky.asp a
SeznamZavodyPolozky.asp.

Filtr Číslo stanice obsahuje stanice dostupné v celé elektrické kategorii,
nejen v prvních 100 řádcích. Server používá rozsah ID od stanice×10000 do
(stanice+1)×10000, s horní hranicí vyloučenou. Parametry rozsahu jsou Double,
aby bezpečně pokryly hranice celého rozsahu Int32 a nevyžadovaly typ BigInt
v MDB. Metadata stanic používají DISTINCT Fix(ID/10000) nad stejným filtrem
energie a archivace. Při přepnutí podružné/fiktivní záložky se stanice resetuje.
U výběru středisek je tento filtr skrytý a na serveru odmítnutý.

DataTables nyní umožňuje řazení sloupců; akční sloupec řazení nemá. Řadí se
načtené dávky; filtr stanice, typu i názvu zůstává serverový nad celou kategorií.
Aktivní záložka má tmavomodré pozadí, bílý tučný text a výraznou dolní hranu.
Modál má menší písmo, odsazení buněk a filtrů. Na dotykových zařízeních se
zachovává minimální výška ovládání a čitelnost textových polí.

Přiřazené položky všech tří oddílů používají společné kompaktní řádky: název
s jednotkami, editace koeficientu nebo příznaku a akce odebrání. Na menších
oknech se rozložení skládá. Potvrzení odebrání i všechny formulářové hodnoty
zůstávají zachované; upozornění na chybějící hlavní měření závodu je jediné
nad seznamem místo opakování v každé položce.
