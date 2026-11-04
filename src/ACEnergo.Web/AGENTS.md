# AGENTS.md

Tento soubor definuje pravidla pro AI coding agenty (Codex, Copilot, Cursor, apod.) v tomto repozitáři.

## 1) Cíl projektu
- ASP.NET Core Web aplikace na `.NET 8`.
- Backend obsahuje MVC/Razor, EF Core migrace, autentizaci/autorizaci a práci s WebSocket/telemetrickými daty.

## 2) Základní workflow agenta
1. Nejdřív si udělej rychlý průzkum (`git status`, relevantní soubory, závislosti).
2. Navrhni minimální změnu, která řeší požadavek bez side‑effectů.
3. Udržuj změny malé a tematicky sjednocené.
4. Po úpravách spusť validaci (build + testy/lint pokud existují).
5. V odpovědi vždy stručně popiš co se změnilo, proč, a jak ověřit.

## 3) Build / test / lokální ověření
Používej příkazy z rootu repozitáře:

Poznámky:
- Pokud testy nejsou definované, explicitně to uveď.
- U rozsáhlejších zásahů preferuj i `dotnet build -c Release`.

## 4) Konvence kódu
- C# s `nullable` zapnutým: nepřidávej nové nullable warningy.
- Preferuj asynchronní API (`async/await`) tam, kde už je asynchronní tok.
- Dodržuj existující naming a strukturu složek/souborů.
- Nepřejmenovávej veřejná API/DTO/DB entity bez jasného důvodu.
- Neprováděj „cleanup refactor“ mimo scope zadání.

## 6) Konfigurace a bezpečnost
- Nikdy necommituj tajné údaje (hesla, tokeny, connection stringy s citlivými daty).
- Preferuj konfiguraci přes `appsettings.*` a environment variables.
- Nevypínej bezpečnostní mechanismy (auth, cert validace, CORS pravidla) bez explicitního požadavku.

## 7) Frontend assets / minifikace
- požívej syntaxi ES5 
- V `Debug` konfiguraci nemá běžet minifikace; v `Release` ano (viz `.csproj`).
- U změn JS/CSS ověř, že nedojde k rozbití bundling/minify pipeline.
- prověř, že JS/CSS jsou navrženy tak, aby byly kompatibilní pro konstrukci s minifikátorem používaným ve Visual studiu a projektu

## 8) Co agent nesmí dělat bez explicitního zadání
- Hromadně přepisovat velké části projektu.
- Měnit architekturu, vrstvy nebo datový model „preventivně“.
- Zavádět nové velké závislosti (NuGet balíčky) bez jasné potřeby.
- Měnit deployment/IIS chování bez důvodu a verifikace.

## 9) Očekávaný formát výstupu agenta
- **Summary**: seznam konkrétních změn podle souborů.
- **Testing**: přesné příkazy a jejich výsledek (pass/fail/warning).
- **Risk/Notes**: případné dopady, kompatibilita, co zůstává otevřené.

## 10) Preferovaná rozhodovací pravidla
- Když existuje více řešení, preferuj to:
  1. jednodušší,
  2. konzistentní s existujícím kódem,
  3. snadněji testovatelné,
  4. s nejmenším rizikem regrese.

---

## 11) Pravidla pro redesign na moderní styl
- Neměň business logiku bez výslovného souhlasu.
- Neměň databázové modely a migrace.
- Zachovej routování, autorizaci a model binding.
- Každou fázi udržuj sestavitelnou.
- Po každé změně spusť dotnet build.
- Velké změny rozděluj do samostatných commitů.
- Neodstraňuj existující funkce pouze kvůli redesignu.
- Zachovej názvy ViewModelů.
- Zachovej API.
- Neměň autorizaci.
- Neměň routování.

## 12) UI redesign

- Modernizuj pouze vzhled.
- Zachovej HTML strukturu, pokud není změna nutná.
- Preferuj Bootstrap 5.
- Nepřidávej nové JavaScript frameworky.
- Vytvářej znovupoužitelné komponenty.
- CSS organizuj přehledně
- kde je používán AdminLTE, tak ho zachovat
- importuj a využívej AdminLTE
- využívej font-awsome
- využívej tabulky DataTable.js
- využívej javascript a jquery



Pokud se instrukce v tomto souboru dostanou do konfliktu s výslovným zadáním od uživatele/developera, má přednost explicitní zadání.
