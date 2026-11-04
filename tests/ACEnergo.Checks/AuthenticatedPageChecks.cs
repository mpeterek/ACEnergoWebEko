using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using ACEnergo.Web.Domain;
using ACEnergo.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

internal static class AuthenticatedPageChecks
{
    public static async Task RunAsync()
    {
        using var factory = new Factory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var count = 0;
        void Check(bool condition, string name) { if (!condition) throw new Exception("HTTP: " + name); count++; }
        void Role(string value) { client.DefaultRequestHeaders.Remove("X-Test-Role"); client.DefaultRequestHeaders.Add("X-Test-Role", value); }
        static string Token(string html)
        {
            var match = Regex.Match(html, "<input[^>]*name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
            if (!match.Success) throw new Exception("Missing antiforgery token");
            return WebUtility.HtmlDecode(match.Groups[1].Value);
        }
        async Task<(HttpStatusCode Code, string Html)> Get(string url)
        {
            using var response = await client.GetAsync(url);
            return (response.StatusCode, await response.Content.ReadAsStringAsync());
        }
        async Task<HttpStatusCode> Post(string handler, string version, string token, params (string Key, string Value)[] extra)
        {
            var fields = new List<KeyValuePair<string, string>> { new("Version", version), new("Period", "2026-10") };
            if (token.Length > 0) fields.Add(new("__RequestVerificationToken", token));
            fields.AddRange(extra.Select(pair => new KeyValuePair<string, string>(pair.Key, pair.Value)));
            using var response = await client.PostAsync("/ElectricityHolidays/5?handler=" + handler, new FormUrlEncodedContent(fields));
            return response.StatusCode;
        }

        Check((await Get("/ElectricityHolidays/5")).Code == HttpStatusCode.Unauthorized, "anonymous cannot load holidays");
        Role("none");
        Check((await Get("/ElectricityHolidays/5")).Code == HttpStatusCode.Forbidden, "missing view permission denied");
        Role("view");
        var view = await Get("/ElectricityHolidays/5?Period=2026-10");
        Check(view.Code == HttpStatusCode.OK && view.Html.Contains("2026-11-17"), "viewer loads actual Razor table");
        Check(!view.Html.Contains("data-holiday-select") && !view.Html.Contains("data-holiday-remove"), "viewer has no edit controls");
        Check(view.Html.Contains("&lt;test&gt;") && !view.Html.Contains("<test>"), "database names HTML encoded");
        var before = factory.Holidays.Writes;
        Check(await Post("Add", factory.Holidays.CurrentVersion, Token(view.Html), ("NewDate", "2026-11-18")) == HttpStatusCode.Forbidden && factory.Holidays.Writes == before, "viewer cannot forge add POST");
        Check(await Post("Remove", factory.Holidays.CurrentVersion, Token(view.Html), ("SelectedDates", "2026-11-17")) == HttpStatusCode.Forbidden && factory.Holidays.Writes == before, "viewer cannot forge delete POST");

        Role("config");
        var edit = await Get("/ElectricityHolidays/5?Period=2026-10");
        Check(edit.Code == HttpStatusCode.OK && edit.Html.Contains("data-holiday-select") && edit.Html.Contains("name=\"NewDate\""), "editor gets date and selection controls");
        Check(edit.Html.Contains("/css/pages/holidays.css?v=") && edit.Html.Contains("/js/pages/holidays.js?v="), "external versioned assets");
        Check(!Regex.IsMatch(edit.Html, "\\sstyle\\s*=|<style\\b|\\son[a-z]+\\s*=", RegexOptions.IgnoreCase) &&
              !Regex.IsMatch(edit.Html, "<script(?![^>]*\\bsrc=)[^>]*>", RegexOptions.IgnoreCase), "no inline styles or scripts");
        Check(await Post("Add", factory.Holidays.CurrentVersion, "", ("NewDate", "2026-11-18")) == HttpStatusCode.BadRequest && factory.Holidays.Writes == before, "CSRF missing token rejects actual POST");
        var originalVersion = factory.Holidays.CurrentVersion;
        Check(await Post("Remove", originalVersion, Token(edit.Html)) == HttpStatusCode.BadRequest && factory.Holidays.Writes == before, "empty selection rejects actual POST");
        Check(await Post("Add", originalVersion, Token(edit.Html), ("NewDate", "2026-02-29")) == HttpStatusCode.BadRequest && factory.Holidays.Writes == before, "invalid leap date rejects POST");
        Check(await Post("Add", originalVersion, Token(edit.Html), ("NewDate", "2026-11-18")) == HttpStatusCode.Found && factory.Holidays.Writes == before + 1, "valid add redirects after write");
        var refreshed = await Get("/ElectricityHolidays/5?Period=2026-10");
        Check(refreshed.Html.Contains("datetime=\"2026-11-18\""), "new date visible after redirect");
        Check(await Post("Remove", originalVersion, Token(refreshed.Html), ("SelectedDates", "2026-11-18")) == HttpStatusCode.Conflict && factory.Holidays.Writes == before + 1, "stale deletion returns HTTP 409");
        Check(await Post("Remove", factory.Holidays.CurrentVersion, Token(refreshed.Html), ("SelectedDates", "2026-11-18")) == HttpStatusCode.Found && factory.Holidays.Writes == before + 2, "valid removal redirects after write");
        Check(factory.Holidays.CurrentVersion == originalVersion, "add/remove restore initial list");
        Check(await Post("Add", factory.Holidays.CurrentVersion, Token(refreshed.Html), ("NewDate", "2026-11-18")) == HttpStatusCode.Found, "prepare two-date selection");
        Check(await Post("Remove", factory.Holidays.CurrentVersion, Token(refreshed.Html), ("SelectedDates", "2026-11-17"), ("SelectedDates", "2026-11-18")) == HttpStatusCode.Found && factory.Holidays.Load(5).Dates.Length == 0, "repeated checkbox names bind and remove both dates");
        var empty = await Get("/ElectricityHolidays/5");
        Check(empty.Code == HttpStatusCode.OK && !empty.Html.Contains("data-holiday-remove"), "empty list renders without delete controls");
        Check(await Post("Add", factory.Holidays.CurrentVersion, Token(empty.Html), ("NewDate", "2026-11-17")) == HttpStatusCode.Found && factory.Holidays.CurrentVersion == originalVersion, "add from empty list restores data");
        var writes = factory.Holidays.Writes;
        Check(await Post("Add", factory.Holidays.CurrentVersion, Token(empty.Html), ("NewDate", "2026-11-17")) == HttpStatusCode.Found && factory.Holidays.Writes == writes && factory.Holidays.CurrentVersion == originalVersion, "duplicate date POST does not rewrite stored state");
        var tariff = await Get("/ElectricityTariffs/5?Period=2026-10");
        if (Environment.GetEnvironmentVariable("ACE_TARIFF_HTML") is string tariffPath) await File.WriteAllTextAsync(tariffPath,tariff.Html);
        Check(tariff.Code == HttpStatusCode.OK && Regex.Matches(tariff.Html, "name=\"Cells\\[\\d+\\]\"").Count == 168, "tariff Razor renders 168 editable cells");
        Check(tariff.Html.Contains("/ElectricityHolidays/5") && tariff.Html.Contains("Period=2026-10"), "tariffs link preserves meter and period");
        var defaultTariff = await Get("/ElectricityTariffs/5");
        Check(defaultTariff.Code == HttpStatusCode.OK && !WebUtility.HtmlDecode(defaultTariff.Html).Contains("The Period field is required."), "tariffs open without explicit period with valid default");
        var hiddenPeriod = Regex.Match(defaultTariff.Html, "<input[^>]*name=\"Period\"[^>]*value=\"([^\"]*)\"");
        var defaultPeriod = WebUtility.HtmlDecode(hiddenPeriod.Groups[1].Value);
        Check(hiddenPeriod.Success && ElectricityTariffs.TryMonth(defaultPeriod, out _), "default month is present in actual hidden POST input");
        async Task<HttpStatusCode> PostTariff(string? period)
        {
            var fields = new List<KeyValuePair<string, string>>
            {
                new("Version", new string('A', 64)), new("ConfirmOverwrite", "true"),
                new("__RequestVerificationToken", Token(defaultTariff.Html))
            };
            if (period is not null) fields.Add(new("Period", period));
            var cells = ElectricityTariffs.Defaults(); cells[0] = 2;
            fields.AddRange(cells.Select((value, index) => new KeyValuePair<string, string>($"Cells[{index}]", value.ToString())));
            using var response = await client.PostAsync("/ElectricityTariffs/5", new FormUrlEncodedContent(fields));
            return response.StatusCode;
        }
        Check(await PostTariff(defaultPeriod) == HttpStatusCode.Found && factory.Tariffs.Writes == 1 &&
            factory.Tariffs.LastMonth.ToString("yyyy-MM") == defaultPeriod && factory.Tariffs.FirstCell == 2,
            "save default month without relying on query string");
        Check(await PostTariff(null) == HttpStatusCode.BadRequest && factory.Tariffs.Writes == 1, "missing POST period does not fall back or write");
        Check(await PostTariff("") == HttpStatusCode.BadRequest && factory.Tariffs.Writes == 1, "empty POST period does not write");
        Check(await PostTariff("2026-13") == HttpStatusCode.BadRequest && factory.Tariffs.Writes == 1, "invalid POST period does not write");
        Check((await Get("/ElectricityTariffs/5?Period=")).Code == HttpStatusCode.BadRequest, "explicit empty GET period rejected");
        Role("view"); tariff = await Get("/ElectricityTariffs/5?Period=2026-10");
        Check(tariff.Code == HttpStatusCode.OK && !tariff.Html.Contains("data-tariff-cell"), "viewer tariff grid is read-only");
        var prices = await Get("/ElectricityPrices/5");
        Check(prices.Code == HttpStatusCode.OK && prices.Html.Contains("Cena test") && !WebUtility.HtmlDecode(prices.Html).Contains("The Period field is required."), "prices load without period");
        Check((await Get("/ElectricityPrices/5?Period=2026-10")).Code == HttpStatusCode.OK, "prices exact month");
        Check((await Get("/ElectricityPrices/5?Period=bad")).Code == HttpStatusCode.BadRequest, "prices reject invalid month");
        Check((await Get("/ElectricityPrices/999")).Code == HttpStatusCode.NotFound, "prices unknown meter");
        var yearPrices = await Get("/ElectricityPriceYear/5?Period=2026-10");
        if (Environment.GetEnvironmentVariable("ACE_PRICE_HTML") is string htmlPath) await File.WriteAllTextAsync(htmlPath, yearPrices.Html);
        Check(yearPrices.Html.Contains("data-price-year") && yearPrices.Html.Contains("/lib/datatables/dataTables.min.js"), "year table loads local DataTables");
        Check(yearPrices.Code == HttpStatusCode.OK && yearPrices.Html.Contains("01/2026") && yearPrices.Html.Contains("12/2026"), "annual prices cover January through December");
        Check((await Get("/ElectricityPriceYear/5?Period=bad")).Code == HttpStatusCode.BadRequest, "annual prices reject invalid period");
        Role("none");
        Check((await Get("/ElectricityPriceYear/5")).Code == HttpStatusCode.Forbidden, "annual prices require view permission");
        Check((await Get("/ElectricityPrices/5")).Code == HttpStatusCode.Forbidden, "prices require view permission");
        Check((await Get("/ElectricityPriceEdit/5?Period=2026-10")).Code == HttpStatusCode.Forbidden, "price edit needs configuration permission");
        Role("config");
        var priceEdit = await Get("/ElectricityPriceEdit/5?Period=2026-10");
        Check(priceEdit.Code == HttpStatusCode.OK && Regex.Matches(priceEdit.Html,"name=\"Values\\[\\d+\\]\"").Count == 28, "all price editor fields render");
        Role("view");
        var meterConfig=await Get("/ElectricityMeter/5");
        Check(meterConfig.Code==HttpStatusCode.OK && !meterConfig.Html.Contains("name=\"MeterName\""), "meter configuration read-only for viewer");
        Role("config");
        meterConfig=await Get("/ElectricityMeter/5");
        Check(meterConfig.Code==HttpStatusCode.OK && meterConfig.Html.Contains("name=\"Coefficient\""), "meter configuration editable with permission");
        using(var saved=await client.PostAsync("/ElectricityMeter/5",new FormUrlEncodedContent(new Dictionary<string,string>{
            ["__RequestVerificationToken"]=Token(meterConfig.Html),["Version"]=new string('A',64),["MeterName"]="Edited",["Coefficient"]="2,5",["ReactiveCoefficient"]="1"})))
            Check(saved.StatusCode==HttpStatusCode.Found,"meter configuration POST accepts Czech coefficient");
        foreach(var kind in new[]{"fictitious","centres","plants"}) {
            var group=await Get("/ElectricityGroups/"+kind);
            Check(group.Code==HttpStatusCode.OK && group.Html.Contains("Test group") && group.Html.Contains("workspace-welcome") && !group.Html.Contains("data-price-table"), "group catalog renders "+kind);
        }
        foreach(var kind in new[]{"fictitious","centres","plants"}) {
            Role("view");
            var url="/ElectricityGroup/"+kind+"/1";
            var detail=await Get(url);
            Check(detail.Code==HttpStatusCode.OK && detail.Html.Contains("Koeficient A") && !detail.Html.Contains("name=\"GroupName\""),"group detail read-only "+kind);
            Check(detail.Html.Contains("&lt;group&gt;"),"group name HTML encoded "+kind);
            if(kind=="plants")Check(detail.Html.Contains("/ElectricityGroup/centres/10002"),"plant links to centre detail");
            Role("config");detail=await Get(url);
            Check(detail.Code==HttpStatusCode.OK && detail.Html.Contains("name=\"PowerUnit\""),"group edit controls "+kind);
            var token=Token(detail.Html);
            async Task<HttpStatusCode> Save(string name,string version,bool csrf=true) {
                var fields=new Dictionary<string,string>{["GroupName"]=name,["PowerUnit"]="MW",["EnergyUnit"]="MWh",["Version"]=version};
                if(csrf)fields["__RequestVerificationToken"]=token;
                using var response=await client.PostAsync(url,new FormUrlEncodedContent(fields));return response.StatusCode;
            }
            var groupWrites=factory.GroupConfig.Writes;
            Check(await Save("Edited",new string('A',64),false)==HttpStatusCode.BadRequest&&factory.GroupConfig.Writes==groupWrites,"group CSRF rejected "+kind);
            Role("view");
            Check(await Save("Edited",new string('A',64))==HttpStatusCode.Forbidden&&factory.GroupConfig.Writes==groupWrites,"group forged viewer POST rejected "+kind);
            Role("config");
            Check(await Save(new string('x',31),new string('A',64))==HttpStatusCode.BadRequest&&factory.GroupConfig.Writes==groupWrites,"group validates name "+kind);
            Check(await Save("Edited",new string('B',64))==HttpStatusCode.Conflict&&factory.GroupConfig.Writes==groupWrites,"group stale version rejected "+kind);
            Check(await Save("Edited",new string('A',64))==HttpStatusCode.Found&&factory.GroupConfig.Writes==groupWrites+1,"group configuration saves "+kind);
            Check((await Get("/ElectricityGroup/"+kind+"/999")).Code==HttpStatusCode.NotFound,"unknown group detail "+kind);
        }
        foreach(var kind in new[]{"centres","plants"}){
            Role("config");var url="/ElectricityGroup/"+kind+"/1";var form=await Get(url);
            Check(form.Html.Contains("name=\"MainMeterId\"")&&form.Html.Contains("Other meter"),"main meter selector "+kind);
            Check(form.Html.Contains("name=\"Recalculate\"")==(kind=="centres"),"recalculation editable only on centre "+kind);
            async Task<HttpStatusCode> Assign(string main,string flag,string version){
                var fields=new Dictionary<string,string>{["__RequestVerificationToken"]=Token(form.Html),["Version"]=version,["GroupName"]="Edited",["PowerUnit"]="MW",["EnergyUnit"]="MWh",["MainMeterId"]=main};
                if(kind=="centres")fields["Recalculate"]=flag;
                using var response=await client.PostAsync(url,new FormUrlEncodedContent(fields));return response.StatusCode;
            }
            var writesBefore=factory.GroupConfig.Writes;
            Role("view");Check(await Assign("6","false",new string('A',64))==HttpStatusCode.Forbidden&&factory.GroupConfig.Writes==writesBefore,"viewer cannot assign main meter "+kind);
            Check(!(await Get(url)).Html.Contains("name=\"MainMeterId\""),"viewer has read-only assignment "+kind);
            Role("config");Check(await Assign("999","false",new string('A',64))==HttpStatusCode.BadRequest&&factory.GroupConfig.Writes==writesBefore,"unknown main meter rejected "+kind);
            Check(await Assign("-1","false",new string('A',64))==HttpStatusCode.BadRequest&&factory.GroupConfig.Writes==writesBefore,"negative main meter rejected "+kind);
            Check(await Assign("6","false",new string('B',64))==HttpStatusCode.Conflict&&factory.GroupConfig.Writes==writesBefore,"assignment stale token rejected "+kind);
            Check(await Assign("6","false",new string('A',64))==HttpStatusCode.Found&&factory.GroupConfig.LastMainMeter==6,"assignment saved "+kind);
            if(kind=="centres")Check(factory.GroupConfig.LastRecalculate==false,"centre recalculation saved");
            Check(await Assign("","",new string('A',64))==HttpStatusCode.Found&&factory.GroupConfig.LastMainMeter is null,"main meter can be disconnected "+kind);
        }
        Role("config");
        var centreCatalog=await Get("/ElectricityGroups/centres");
        var centreLinks=Regex.Matches(centreCatalog.Html,"href=\"([^\"]*/ElectricityGroup(?:Composition)?/centres/16)\"").Select(match=>WebUtility.HtmlDecode(match.Groups[1].Value)).Distinct().ToArray();
        Check(centreCatalog.Html.Contains("data-measurement-tree")&&centreCatalog.Html.Contains("workspace-heading"),"shared tree and workspace toolbar render");
        Check(centreCatalog.Html.Contains("/lib/adminlte/adminlte.min.css")&&centreCatalog.Html.Contains("/lib/bootstrap/bootstrap.min.css")&&centreCatalog.Html.Contains("/lib/fontawesome/css/all.min.css"),"local requested UI assets render");
        Check(centreLinks.Length==1,"landing emits a single tree selection for centre 16");
        var centreDetail=await Get("/ElectricityGroup/centres/16");
        Check(centreDetail.Html.Contains("/ElectricityGroupComposition/centres/16"),"selected measurement offers its composition");
        foreach(var link in centreLinks){var linked=await Get(link);Check(linked.Code==HttpStatusCode.OK&&WebUtility.HtmlDecode(linked.Html).Contains("Středisko 16"),"follow rendered centre 16 link "+link);}
        foreach(var url in new[]{"/ElectricityGroup/centres/999","/ElectricityGroupComposition/centres/999"}){
            var missing=await Get(url);Check(missing.Code==HttpStatusCode.NotFound&&WebUtility.HtmlDecode(missing.Html).Contains("nebyl v používané databázi"),"missing group has explanatory body "+url);
        }
        foreach(var route in new[]{"/ElectricityGroup/centres/16","/ElectricityGroupComposition/centres/16"}){
            using var request=new HttpRequestMessage(HttpMethod.Get,route);request.Headers.Add("X-Workspace-Request","1");
            using var response=await client.SendAsync(request);var html=await response.Content.ReadAsStringAsync();
            Check(response.Headers.CacheControl?.NoStore==true&&response.Headers.Vary.Contains("X-Workspace-Request")&&response.StatusCode==HttpStatusCode.OK&&html.Contains("workspace-content")&&!html.Contains("class=\"site-header\"")&&!html.Contains("<script"),"workspace fragment excludes document shell and scripts "+route);
            Check(html.Contains("data-group-id=\"16\"")&&!html.Contains("data-group-id=\"1\""),"fragment contains only selected branch "+route);
            var compositionRoute=route.Contains("Composition");
            var fragmentFields=new Dictionary<string,string>{["__RequestVerificationToken"]=Token(html),["Version"]=compositionRoute?factory.CompositionEditor.Load("centres",16).Version:new string('A',64)};
            if(compositionRoute){fragmentFields["ChildId"]="10002";fragmentFields["Coefficient"]="1";}
            else{fragmentFields["GroupName"]="Fragment edited";fragmentFields["PowerUnit"]="MW";fragmentFields["EnergyUnit"]="MWh";}
            using(var fragmentSaved=await client.PostAsync(route+(compositionRoute?"?handler=Update":""),new FormUrlEncodedContent(fragmentFields)))
                Check(fragmentSaved.StatusCode==HttpStatusCode.Found,"CSRF token from fragment is valid on native POST "+route);

            if(Environment.GetEnvironmentVariable("ACE_RESPONSIVE_DIR") is string fragments){Directory.CreateDirectory(fragments);await File.WriteAllTextAsync(Path.Combine(fragments,route.Contains("Composition")?"fragment-composition.html":"fragment-configuration.html"),html);}
        }
        Role("view");
        using(var fragmentRequest=new HttpRequestMessage(HttpMethod.Get,"/ElectricityGroup/centres/16")){
            fragmentRequest.Headers.Add("X-Workspace-Request","1");using var fragmentResponse=await client.SendAsync(fragmentRequest);var fragmentHtml=await fragmentResponse.Content.ReadAsStringAsync();
            Check(fragmentResponse.StatusCode==HttpStatusCode.OK&&!fragmentHtml.Contains("name=\"GroupName\""),"fragment remains read-only for viewer");
        }
        Role("none");
        using(var fragmentRequest=new HttpRequestMessage(HttpMethod.Get,"/ElectricityGroup/centres/16")){
            fragmentRequest.Headers.Add("X-Workspace-Request","1");using var fragmentResponse=await client.SendAsync(fragmentRequest);
            Check(fragmentResponse.StatusCode==HttpStatusCode.Forbidden,"fragment header cannot bypass electricity authorization");
        }
        Role("config");
        var unknown=await Get("/unregistered-page/centres/16");
        Check(unknown.Code==HttpStatusCode.NotFound&&WebUtility.HtmlDecode(unknown.Html).Contains("Stránka nebyla nalezena"),"missing endpoint has distinct explanatory body");
        foreach(var kind in new[]{"fictitious","centres","plants"}) {
            var url="/ElectricityGroupComposition/"+kind+"/1";
            Role("view");var page=await Get(url);
            Check(page.Code==HttpStatusCode.OK&&!page.Html.Contains("name=\"ChildId\""),"composition viewer read-only "+kind);
            Role("config");page=await Get(url+"?Search=Test");
            Check(page.Code==HttpStatusCode.OK&&page.Html.Contains("name=\"ChildId\"")&&page.Html.Contains("Test candidate"),"composition editor searches "+kind);
            Check(page.Html.Contains("/css/pages/group-composition.css?v=")&&!Regex.IsMatch(page.Html,"\\sstyle\\s*=|\\son[a-z]+\\s*=",RegexOptions.IgnoreCase),"composition external styling "+kind);
            var token=Token(page.Html);
            async Task<HttpStatusCode> Change(string handler,int child,string version,string coefficient="1",bool confirm=false,bool csrf=true) {
                var fields=new Dictionary<string,string>{["ChildId"]=child.ToString(),["Version"]=version,["Coefficient"]=coefficient,["Recalculate"]="true",["ConfirmRemoval"]=confirm.ToString()};
                if(csrf)fields["__RequestVerificationToken"]=token;
                using var result=await client.PostAsync(url+"?handler="+handler,new FormUrlEncodedContent(fields));return result.StatusCode;
            }
            var store=factory.CompositionEditor;var initial=store.Writes;
            Check(await Change("Add",10003,store.Load(kind,1).Version,csrf:false)==HttpStatusCode.BadRequest&&store.Writes==initial,"composition CSRF rejected "+kind);
            Role("view");
            foreach(var handler in new[]{"Add","Update","Remove"})Check(await Change(handler,10002,store.Load(kind,1).Version,confirm:true)==HttpStatusCode.Forbidden&&store.Writes==initial,"composition viewer POST denied "+kind+handler);
            Role("config");
            Check(await Change("Add",10003,new string('B',64))==HttpStatusCode.Conflict&&store.Writes==initial,"composition stale version rejected "+kind);
            Check(await Change("Add",10002,store.Load(kind,1).Version)==HttpStatusCode.BadRequest,"composition duplicate rejected "+kind);
            Check(await Change("Add",10003,store.Load(kind,1).Version)==HttpStatusCode.Found,"composition add "+kind);
            if(kind!="plants")Check(await Change("Update",10003,store.Load(kind,1).Version,"bad")==HttpStatusCode.BadRequest,"composition invalid coefficient "+kind);
            Check(await Change("Update",10003,store.Load(kind,1).Version,"-1,25")==HttpStatusCode.Found,"composition update "+kind);
            Check(kind=="plants"?store.Load(kind,1).Rows.Single(r=>r.Id==10003).Recalculate==true:store.Load(kind,1).Rows.Single(r=>r.Id==10003).Coefficient=="-1,25","composition posted values bound "+kind);
            Check(await Change("Remove",10003,store.Load(kind,1).Version)==HttpStatusCode.BadRequest,"composition removal needs confirmation "+kind);
            Check(await Change("Remove",10003,store.Load(kind,1).Version,confirm:true)==HttpStatusCode.Found&&!store.Load(kind,1).Rows.Any(r=>r.Id==10003),"composition remove "+kind);
            Check((await Get("/ElectricityGroupComposition/"+kind+"/999")).Code==HttpStatusCode.NotFound,"composition missing parent "+kind);
            if(kind=="centres"&&Environment.GetEnvironmentVariable("ACE_COMPOSITION_HTML") is string output)File.WriteAllText(output,(await Get(url+"?Search=Test")).Html);
        }
        Role("config");
        foreach(var kind in new[]{"fictitious","centres","plants"}){
            var page=await Get("/ElectricityGroupComposition/"+kind+"/1");
            Check(page.Html.Contains("id=\"composition-picker\"")&&page.Html.Contains("id=\"picker-add-form\""),"modal add form renders "+kind);
            Check(page.Html.Contains("data-picker-tab=\"fictitious\"")==(kind=="centres"),"virtual tab only on centres "+kind);
            Check(page.Html.Contains("workspace-back")&&page.Html.IndexOf("workspace-back",StringComparison.Ordinal)<page.Html.IndexOf("workspace-toolbar",StringComparison.Ordinal)&&!page.Html.Contains("workspace-footer"),"back button at top "+kind);
            var category=kind=="plants"?"centres":"physical";
            var endpoint="/ElectricityGroupComposition/"+kind+"/1?handler=Candidates&category="+category;
            var result=await Get(endpoint);
            using var json=System.Text.Json.JsonDocument.Parse(result.Html);
            Check(result.Code==HttpStatusCode.OK&&json.RootElement.GetProperty("rows").GetArrayLength()==3,"picker initially lists items "+kind);
            Check(json.RootElement.GetProperty("rows")[0].GetProperty("assigned").GetBoolean(),"assigned picker entry flagged "+kind);
            Check((await Get(endpoint+"&inputType=E")).Html.Contains("Test candidate")&&!(await Get(endpoint+"&inputType=E")).Html.Contains("Assigned test"),"picker filters actual type "+kind);
            Role("view");Check((await Get(endpoint)).Code==HttpStatusCode.Forbidden,"viewer cannot query picker "+kind);Role("config");
            Check((await Get(endpoint+"&afterId=-1")).Code==HttpStatusCode.BadRequest,"negative picker cursor rejected "+kind);
        }
        var physicalAddresses=await Get("/ElectricityGroupComposition/centres/1?handler=Candidates&category=physical&station=1");
        using(var addressJson=System.Text.Json.JsonDocument.Parse(physicalAddresses.Html)){
            var rows=addressJson.RootElement.GetProperty("rows");
            Check(rows.GetArrayLength()==2&&rows[1].GetProperty("id").GetInt32()==10003&&rows[1].GetProperty("station").GetInt32()==1&&rows[1].GetProperty("input").GetInt32()==3,"physical address decomposition and station filter");
        }
        Check((await Get("/ElectricityGroupComposition/centres/1?handler=Candidates&category=physical&station=2")).Html.Contains("20003")&&!(await Get("/ElectricityGroupComposition/centres/1?handler=Candidates&category=physical&station=2")).Html.Contains("10003"),"second station isolated");
        Check((await Get("/ElectricityGroupComposition/centres/1?handler=Candidates&category=physical&station=-1")).Code==HttpStatusCode.BadRequest,"invalid station rejected");
        Check((await Get("/ElectricityGroupComposition/plants/1?handler=Candidates&category=centres&station=1")).Code==HttpStatusCode.BadRequest,"centres have no hardware station filter");
        var virtualPicker=await Get("/ElectricityGroupComposition/centres/1?handler=Candidates&category=fictitious");
        Check(virtualPicker.Code==HttpStatusCode.OK&&virtualPicker.Html.Contains("50000001")&&!virtualPicker.Html.Contains("10003"),"virtual tab isolated from physical rows");
        Check((await Get("/ElectricityGroupComposition/fictitious/1?handler=Candidates&category=fictitious")).Code==HttpStatusCode.BadRequest,"virtual source disallowed on fictitious group");
        Check((await Get("/ElectricityGroupComposition/centres/1?handler=Candidates&category=analog")).Code==HttpStatusCode.BadRequest,"analog source rejected");
        Check((await Get("/ElectricityGroupComposition/centres/999?handler=Candidates&category=physical")).Code==HttpStatusCode.NotFound,"picker unknown parent 404");
        Check((await Get("/ElectricityGroupComposition/centres/1?handler=Candidates&category=physical&afterId=20003")).Html.Contains("\"rows\":[]"),"picker keyset cursor applied");
        if(Environment.GetEnvironmentVariable("ACE_PICKER_DIR") is string pickerDirectory){
            Directory.CreateDirectory(pickerDirectory);
            foreach(var category in new[]{"physical","fictitious"})await File.WriteAllTextAsync(Path.Combine(pickerDirectory,category+".json"),(await Get("/ElectricityGroupComposition/centres/1?handler=Candidates&category="+category)).Html);
        }
        Check((await Get("/ElectricityGroupComposition/analog/1")).Code==HttpStatusCode.NotFound,"composition excludes analog");
        Role("none");Check((await Get("/ElectricityGroupComposition/plants/1")).Code==HttpStatusCode.Forbidden,"composition requires view permission");
        Role("config");
        Check((await Get("/ElectricityGroup/analog/1")).Code==HttpStatusCode.NotFound,"group detail rejects analog");
        Role("none");
        Check((await Get("/ElectricityGroup/centres/1")).Code==HttpStatusCode.Forbidden,"group detail requires view permission");
        Role("config");
        Check((await Get("/ElectricityGroups/analog")).Code==HttpStatusCode.NotFound,"group kind whitelist rejects analog");
        Role("none");
        Check((await Get("/ElectricityGroups/centres")).Code==HttpStatusCode.Forbidden,"group catalog requires electricity view permission");
        Role("view");
        var composition=await Get("/ElectricityFictitious/1");
        Check(composition.Code==HttpStatusCode.OK && composition.Html.Contains("0001/0002") && composition.Html.Contains("Koeficient A"),"fictitious composition renders address and coefficients");
        Check(WebUtility.HtmlDecode(composition.Html).Contains("Chybějící definice vstupu"),"orphan component is visible");
        Check((await Get("/ElectricityFictitious/2")).Html.Contains("nemá přiřazené"),"empty fictitious composition is explicit");
        Check((await Get("/ElectricityFictitious/999")).Code==HttpStatusCode.NotFound,"unknown fictitious measurement 404");
        Role("none");
        Check((await Get("/ElectricityFictitious/1")).Code==HttpStatusCode.Forbidden,"composition requires electricity permission");
        Role("admin");
        var home=WebUtility.HtmlDecode((await Get("/")).Html);
        Check(!home.Contains("Vaše energie na jednom místě") && home.Contains("/ElectricityGroups/plants"),"dashboard heading removed and group navigation present");
        Check((await Get("/ElectricityHolidays/5")).Html.Contains("data-holiday-remove"), "administrator has configuration controls");
        Check(!WebUtility.HtmlDecode((await Get("/")).Html).Contains("Analogová"), "analog module excluded even for admin");
        Check((await Get("/ElectricityHolidays/999")).Code == HttpStatusCode.NotFound, "unknown meter 404");
        if(Environment.GetEnvironmentVariable("ACE_RESPONSIVE_DIR") is string responsiveDirectory){
            Directory.CreateDirectory(responsiveDirectory);
            var pages=new[]{("home","/"),("login","/Login"),("meter","/ElectricityMeter/5"),("groups","/ElectricityGroups/centres"),("configuration","/ElectricityGroup/centres/16"),("configuration-one","/ElectricityGroup/centres/1"),("composition-sixteen","/ElectricityGroupComposition/centres/16"),("price-editor","/ElectricityPriceEdit/5?Period=2026-10"),("holidays","/ElectricityHolidays/5?Period=2026-10"),("monthly-prices","/ElectricityPrices/5?Period=2026-10")};
            foreach(var (name,url) in pages){var rendered=await Get(url);if(rendered.Code!=HttpStatusCode.OK)throw new Exception("Responsive fixture failed: "+url);await File.WriteAllTextAsync(Path.Combine(responsiveDirectory,name+".html"),rendered.Html);}
        }
        foreach(var kind in new[]{"fictitious","centres","plants"}) {
            var newMonth=kind=="fictitious"?10:kind=="centres"?11:12;
            var savePeriod="2026-"+newMonth;
            var url="/ElectricityGroupPrices/"+kind+"/1?Period=2026-09";
            Role("none");Check((await Get(url)).Code==HttpStatusCode.Forbidden,"shared prices require electricity permission "+kind);
            Role("view");var shared=await Get(url);
            Check(shared.Code==HttpStatusCode.OK&&!shared.Html.Contains("name=\"Values[0]\""),"shared prices viewer read-only "+kind);
            Check(WebUtility.HtmlDecode(shared.Html).Contains("Kč / kWh")&&shared.Html.Contains("1,25"),"shared price has work unit and effective older price "+kind);
            Role("config");shared=await Get(url);
            async Task<HttpStatusCode> AddTypePrice(string period,string version,bool csrf=true,string price="2,34567") {
                var fields=new List<KeyValuePair<string,string>>{new("Period",period),new("Version",version),new("Tariff","true")};
                for(var i=0;i<6;i++)fields.Add(new("Values["+i+"]",i==0?price:i==5?"":"3"));
                if(csrf)fields.Add(new("__RequestVerificationToken",Token(shared.Html)));
                using var response=await client.PostAsync("/ElectricityGroupPrices/"+kind+"/1",new FormUrlEncodedContent(fields));return response.StatusCode;
            }
            var typePriceWrites=factory.TypePrices.Writes;var version=factory.TypePrices.Load(kind,1).Version;
            Check(await AddTypePrice(savePeriod,version,false)==HttpStatusCode.BadRequest&&factory.TypePrices.Writes==typePriceWrites,"shared price CSRF "+kind);
            Role("view");Check(await AddTypePrice(savePeriod,version)==HttpStatusCode.Forbidden&&factory.TypePrices.Writes==typePriceWrites,"shared price forged viewer POST "+kind);
            Role("config");Check(await AddTypePrice("bad",version)==HttpStatusCode.BadRequest&&factory.TypePrices.Writes==typePriceWrites,"shared price month validation "+kind);
            Check(await AddTypePrice(savePeriod,version,true,"-1")==HttpStatusCode.BadRequest&&factory.TypePrices.Writes==typePriceWrites,"shared price negative rejected "+kind);
            Check(await AddTypePrice(savePeriod,new string('B',64))==HttpStatusCode.Conflict&&factory.TypePrices.Writes==typePriceWrites,"shared price stale history "+kind);
            Check(await AddTypePrice("2025-01",version)==HttpStatusCode.BadRequest&&factory.TypePrices.Writes==typePriceWrites,"shared price preserves existing month "+kind);
            Check(await AddTypePrice(savePeriod,version)==HttpStatusCode.Found&&factory.TypePrices.Writes==typePriceWrites+1,"shared price adds effective record "+kind);
            Check(factory.TypePrices.Load(kind,1).History.Single(r=>r.EffectiveAt==new DateTime(2026,newMonth,1)).Values[5] is null,"shared price empty preserves NULL "+kind);
            Check(factory.TypePrices.Load("plants",1).History.Length==factory.TypePrices.Load("fictitious",1).History.Length,"same type history shared across groups "+kind);
            var history=await Get("/ElectricityGroupPriceHistory/"+kind+"/1?Period=2026-10");
            Check(history.Code==HttpStatusCode.OK&&WebUtility.HtmlDecode(history.Html).Contains("Bez data")&&history.Html.Contains("2,34567"),"shared price history includes undated and new rows "+kind);
            var annual=await Get("/ElectricityGroupPriceYear/"+kind+"/1?Period=2026-10");
            Check(annual.Code==HttpStatusCode.OK&&annual.Html.Contains("01.01.2025")&&annual.Html.Contains("01.10.2026"),"shared price annual carries earlier year and changes in October "+kind);
            Check((await Get("/ElectricityGroupPrices/"+kind+"/999")).Code==HttpStatusCode.NotFound,"shared price missing input "+kind);
            using var request=new HttpRequestMessage(HttpMethod.Get,"/ElectricityGroupPriceYear/"+kind+"/1?Period=2026-10");request.Headers.Add("X-Workspace-Request","1");
            using var fragment=await client.SendAsync(request);var fragmentHtml=await fragment.Content.ReadAsStringAsync();
            Check(fragment.StatusCode==HttpStatusCode.OK&&fragmentHtml.Contains("workspace-content")&&!fragmentHtml.Contains("<script")&&fragment.Headers.CacheControl?.NoStore==true,"annual prices workspace fragment contract "+kind);
            if(Environment.GetEnvironmentVariable("ACE_RESPONSIVE_DIR") is string folder&&kind=="centres") {
                foreach(var (name,route) in new[]{("type-prices","ElectricityGroupPrices"),("type-history","ElectricityGroupPriceHistory"),("type-year","ElectricityGroupPriceYear")}) {
                    var html=(await Get("/"+route+"/centres/16?Period=2026-10")).Html;await File.WriteAllTextAsync(Path.Combine(folder,name+".html"),html);
                    using var req=new HttpRequestMessage(HttpMethod.Get,"/"+route+"/centres/16?Period=2026-10");req.Headers.Add("X-Workspace-Request","1");
                    using var frag=await client.SendAsync(req);await File.WriteAllTextAsync(Path.Combine(folder,"fragment-"+name+".html"),await frag.Content.ReadAsStringAsync());
                }
            }
        }
        Role("config");Check((await Get("/ElectricityGroupPrices/analog/1")).Code==HttpStatusCode.NotFound,"shared price whitelist");
        Check((await Get("/ElectricityGroupPriceYear/centres/1?Period=")).Code==HttpStatusCode.BadRequest,"shared price explicit empty period invalid");
        Role("none");
        foreach(var route in new[]{"/ElectricitySubmeters","/ElectricitySubmeters?handler=Tree","/ElectricitySubmeter/10003","/ElectricitySubmeterEvaluation/10003?Mode=day&Date=2026-10-01","/ElectricitySubmeterEvaluation/10003?handler=Export&Mode=day&Date=2026-10-01"})
            Check((await Get(route)).Code==HttpStatusCode.Forbidden,"submeter policy "+route);
        Role("view");
        var submeter=await Get("/ElectricitySubmeter/10003");
        Check(submeter.Code==HttpStatusCode.OK&&submeter.Html.Contains("&lt;submeter&gt;")&&!submeter.Html.Contains("<submeter>"),"physical name encoded");
        Check(submeter.Html.Contains("0001/0003")&&submeter.Html.Contains("kWh"),"physical address and work unit");
        Check(!submeter.Html.Contains("data-price-table"),"physical information separate from prices");
        var separatePrices=await Get("/ElectricitySubmeter/10003?Section=prices");Check(separatePrices.Code==HttpStatusCode.OK&&separatePrices.Html.Contains("data-price-table")&&!separatePrices.Html.Contains("<h2>Informace o vstupu"),"physical prices separate page state");
        Check((await Get("/ElectricitySubmeter/50000001")).Code==HttpStatusCode.NotFound,"virtual ID cannot be physical");
        Check((await Get("/ElectricitySubmeter/999")).Code==HttpStatusCode.NotFound,"unknown physical ID");
        var tree=await Get("/ElectricitySubmeters?handler=Tree&Station=1&InputType=EU");
        using(var parsed=System.Text.Json.JsonDocument.Parse(tree.Html)){var values=parsed.RootElement.GetProperty("rows");Check(tree.Code==HttpStatusCode.OK&&values.GetArrayLength()==1&&values[0].GetProperty("id").GetInt32()==10004,"physical tree station/type JSON filter");}
        Check((await Get("/ElectricitySubmeters?handler=Tree&Station=wrong")).Code==HttpStatusCode.BadRequest,"physical tree model binding");
        Check((await Get("/ElectricitySubmeters?handler=Tree&Station=5000")).Code==HttpStatusCode.BadRequest,"physical tree rejects virtual station");
        var first=await Get("/ElectricitySubmeters?handler=Tree");
        using(var parsed=System.Text.Json.JsonDocument.Parse(first.Html))Check(parsed.RootElement.GetProperty("hasMore").GetBoolean()&&parsed.RootElement.GetProperty("rows").GetArrayLength()==100,"physical list bounded first page");
        var next=await Get("/ElectricitySubmeters?handler=Tree&AfterId=10100");
        using(var parsed=System.Text.Json.JsonDocument.Parse(next.Html))Check(parsed.RootElement.GetProperty("rows")[0].GetProperty("id").GetInt32()==10101,"physical keyset continuation");
        foreach(var mode in new[]{"day","month","year"}){
            var route="/ElectricitySubmeterEvaluation/10003?Mode="+mode+"&Date=2026-10-01&Interval=hour";
            var result=await Get(route);Check(result.Code==HttpStatusCode.OK&&!result.Html.Contains("data-evaluation-table")&&result.Html.Contains("<svg"),"physical evaluation view "+mode);
            var expected=mode=="day"?"96":mode=="month"?"2976":"32160";
            Check(WebUtility.HtmlDecode(result.Html).Contains(expected),"physical known consumption "+mode);
            using var request=new HttpRequestMessage(HttpMethod.Get,route);request.Headers.Add("X-Workspace-Request","1");
            using var fragment=await client.SendAsync(request);var fragmentHtml=await fragment.Content.ReadAsStringAsync();
            Check(fragment.StatusCode==HttpStatusCode.OK&&fragmentHtml.Contains("workspace-content")&&!fragmentHtml.Contains("<script")&&fragment.Headers.CacheControl?.NoStore==true,"physical AJAX fragment "+mode);
            if(Environment.GetEnvironmentVariable("ACE_RESPONSIVE_DIR") is string folder){await File.WriteAllTextAsync(Path.Combine(folder,"submeter-"+mode+".html"),result.Html);await File.WriteAllTextAsync(Path.Combine(folder,"fragment-submeter-"+mode+".html"),fragmentHtml);}
            foreach(var section in mode=="year"?new[]{"values"}:new[]{"values","costs"}){
                var detail=await Get(route+"&View="+section);var html=WebUtility.HtmlDecode(detail.Html);
                Check(detail.Code==HttpStatusCode.OK&&html.Contains("data-evaluation-table")&&!html.Contains("<svg"),"separate evaluation result "+mode+"/"+section);
                Check(section=="costs"?html.Contains("<th scope=\"col\">Náklady (Kč)</th>"):!html.Contains("<th scope=\"col\">Náklady (Kč)</th>"),"measurement and cost columns separate "+mode+"/"+section);
                if(Environment.GetEnvironmentVariable("ACE_RESPONSIVE_DIR") is string detailFolder){
                    await File.WriteAllTextAsync(Path.Combine(detailFolder,"submeter-"+mode+"-"+section+".html"),detail.Html);
                    using var req=new HttpRequestMessage(HttpMethod.Get,route+"&View="+section);req.Headers.Add("X-Workspace-Request","1");using var resp=await client.SendAsync(req);await File.WriteAllTextAsync(Path.Combine(detailFolder,"fragment-submeter-"+mode+"-"+section+".html"),await resp.Content.ReadAsStringAsync());
                }
            }
        }
        foreach(var query in new[]{"Mode=bad","Date=2026-02-30","Date=","Interval=minute","MainMeterId=-1","View=bad"})Check((await Get("/ElectricitySubmeterEvaluation/10003?"+query)).Code==HttpStatusCode.BadRequest,"physical invalid evaluation "+query);
        var yearFromCosts=await Get("/ElectricitySubmeterEvaluation/10003?Mode=year&Date=2026-10-01&View=costs");
        Check(yearFromCosts.Code==HttpStatusCode.OK&&yearFromCosts.Html.Contains("<svg")&&!yearFromCosts.Html.Contains("data-evaluation-table"),"year from costs returns clear overview");
        Check((await Get("/ElectricitySubmeterEvaluation/10003")).Code==HttpStatusCode.OK,"physical default period valid");
        using(var csv=await client.GetAsync("/ElectricitySubmeterEvaluation/10003?handler=Export&Mode=day&Date=2026-10-01&Interval=hour")){
            var bytes=await csv.Content.ReadAsByteArrayAsync();var text=System.Text.Encoding.UTF8.GetString(bytes);
            Check(csv.StatusCode==HttpStatusCode.OK&&csv.Content.Headers.ContentType?.MediaType=="text/csv"&&csv.Content.Headers.ContentDisposition?.FileNameStar?.EndsWith(".csv")==true,"physical CSV download");
            Check(bytes.Take(3).SequenceEqual(new byte[]{239,187,191})&&text.Contains("2026-10-01 06:00")&&text.Contains("kWh")&&text.Split('\n',StringSplitOptions.RemoveEmptyEntries).Length==25,"physical CSV BOM, shift, units and hourly rows");
        }
        if(Environment.GetEnvironmentVariable("ACE_RESPONSIVE_DIR") is string submeterFolder){
            foreach(var (name,route) in new[]{("submeters","/ElectricitySubmeters"),("submeter","/ElectricitySubmeter/10003"),("submeter-prices","/ElectricitySubmeter/10003?Section=prices")}){var html=(await Get(route)).Html;await File.WriteAllTextAsync(Path.Combine(submeterFolder,name+".html"),html);}
            using var request=new HttpRequestMessage(HttpMethod.Get,"/ElectricitySubmeter/10003");request.Headers.Add("X-Workspace-Request","1");using var response=await client.SendAsync(request);await File.WriteAllTextAsync(Path.Combine(submeterFolder,"fragment-submeter.html"),await response.Content.ReadAsStringAsync());
            using(var req=new HttpRequestMessage(HttpMethod.Get,"/ElectricitySubmeter/10003?Section=prices")){req.Headers.Add("X-Workspace-Request","1");using var resp=await client.SendAsync(req);await File.WriteAllTextAsync(Path.Combine(submeterFolder,"fragment-submeter-prices.html"),await resp.Content.ReadAsStringAsync());}
            foreach(var (name,route) in new[]{("submeters-first","/ElectricitySubmeters?handler=Tree"),("submeters-next","/ElectricitySubmeters?handler=Tree&AfterId=10100"),("submeters-station","/ElectricitySubmeters?handler=Tree&Station=2"),("submeters-type","/ElectricitySubmeters?handler=Tree&InputType=EU"),("submeters-search","/ElectricitySubmeters?handler=Tree&Search=10003")})await File.WriteAllTextAsync(Path.Combine(submeterFolder,name+".json"),(await Get(route)).Html);
        }
        Console.WriteLine($"PASS: {count} authenticated Razor/HTTP, CSRF and authorization checks (test stores only).");
    }

    // Test-only DI and authentication; no test account or authentication switch is added to the web project.
    private sealed class Factory : WebApplicationFactory<AccessStore>
    {
        public FakeTypePrices TypePrices { get; } = new();
        public FakeHolidays Holidays { get; } = new();
        public FakeTariffs Tariffs { get; } = new();
        public FakeGroupConfiguration GroupConfig { get; } = new();
        public FakeCompositionEditor CompositionEditor { get; } = new();
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root is not null && !File.Exists(Path.Combine(root.FullName, "ACEnergoWebEko.sln"))) root = root.Parent;
            if (root is null) throw new Exception("Solution root not found");
            builder.UseContentRoot(Path.Combine(root.FullName, "src", "ACEnergo.Web"));
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> { ["AllowedHosts"] = "localhost" }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IElectricitySubmeterStore>();
                services.AddSingleton<IElectricitySubmeterStore, SubmeterFixture>();
                services.RemoveAll<IElectricityPulseArchive>();
                services.AddSingleton<IElectricityPulseArchive, PulseArchiveFixture>();
                services.RemoveAll<IElectricityHolidayStore>();
                services.AddSingleton<IElectricityHolidayStore>(Holidays);
                services.RemoveAll<IElectricityFictitiousComposition>();
                services.AddSingleton<IElectricityFictitiousComposition, FakeComposition>();
                services.RemoveAll<IElectricityGroupCompositionEditor>();
                services.AddSingleton<IElectricityGroupCompositionEditor>(CompositionEditor);
                services.RemoveAll<IElectricityGroupConfiguration>();
                services.AddSingleton<IElectricityGroupConfiguration>(GroupConfig);
                services.RemoveAll<IElectricityGroupCatalog>();
                services.AddSingleton<IElectricityGroupCatalog, FakeGroups>();
                services.RemoveAll<IElectricityMeterConfiguration>();
                services.AddSingleton<IElectricityMeterConfiguration, FakeMeterConfiguration>();
                services.RemoveAll<IElectricityPriceEditor>();
                services.AddSingleton<IElectricityPriceEditor, FakePriceEditor>();
                services.RemoveAll<IElectricityTypePriceStore>();
                services.AddSingleton<IElectricityTypePriceStore>(TypePrices);
                services.RemoveAll<IElectricityPriceStore>();
                services.AddSingleton<IElectricityPriceStore, FakePrices>();
                services.RemoveAll<IElectricityTariffStore>();
                services.AddSingleton<IElectricityTariffStore>(Tariffs);
                services.AddDataProtection().UseEphemeralDataProtectionProvider();
                services.AddLogging(logging => logging.ClearProviders());
                services.AddAuthentication(options =>
                {
                    options.DefaultScheme = "Test";
                    options.DefaultAuthenticateScheme = "Test";
                    options.DefaultChallengeScheme = "Test";
                    options.DefaultForbidScheme = "Test";
                }).AddScheme<AuthenticationSchemeOptions, TestAuth>("Test", _ => { });
            });
        }
    }
    private sealed class TestAuth(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-Test-Role", out var role)) return Task.FromResult(AuthenticateResult.NoResult());
            var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "web-test"), new(ClaimTypes.Name, "web-test") };
            if (role == "view" || role == "config") claims.Add(new("permission", "ElekView"));
            if (role == "config") claims.Add(new("permission", "ElekConfig"));
            if (role == "admin") claims.Add(new(ClaimTypes.Role, "Administrator"));
            return Task.FromResult(AuthenticateResult.Success(new(new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")), "Test")));
        }
    }
    private sealed class FakeHolidays : IElectricityHolidayStore
    {
        private readonly List<HolidayRecord> rows = [new(new DateTime(2026, 11, 17), "_")];
        public int Writes { get; private set; }
        public string CurrentVersion => ElectricityHolidays.Version(rows);
        public HolidaySnapshot Load(int meterId) => meterId == 5 ? new(5, "Měření <test>", rows.ToArray(), CurrentVersion) : throw new KeyNotFoundException();
        private void Validate(int meterId, string version)
        { _ = Load(meterId); if (version != CurrentVersion) throw new HolidayConflictException(); }
        public bool Add(int meterId, DateOnly date, string expectedVersion)
        {
            Validate(meterId, expectedVersion);
            if (rows.Any(row => DateOnly.FromDateTime(row.At) == date)) return false;
            rows.Add(new(date.ToDateTime(TimeOnly.MinValue), "_")); Writes++; return true;
        }
        public int Remove(int meterId, IReadOnlyList<DateOnly> dates, string expectedVersion)
        {
            Validate(meterId, expectedVersion);
            var removed = rows.RemoveAll(row => dates.Contains(DateOnly.FromDateTime(row.At)));
            Writes++; return removed;
        }
    }
    private sealed class FakeComposition : IElectricityFictitiousComposition {
        public FictitiousComposition Load(int id)=>id switch {
            1=>new("Test",[new(10002,"Test input","MW","MWh","1,5","0","1",true),new(20001,"Nedefinováno","—","—","1","0","1",false)]),
            2=>new("Empty",[]),_=>throw new KeyNotFoundException()};
    }
    private sealed class FakeCompositionEditor : IElectricityGroupCompositionEditor {
        private readonly Dictionary<string,List<CompositionEditRow>> data=new();
        public int Writes{get;private set;}
        private List<CompositionEditRow> Rows(string kind){ElectricityGroupCompositionEditor.Definition(kind);if(!data.TryGetValue(kind,out var rows)){rows=[new(10002,"1",false)];data.Add(kind,rows);}return rows;}
        public CompositionEditSnapshot Load(string kind,int id){
            if(id!=1&&!(kind=="centres"&&id==16))throw new KeyNotFoundException();var rows=Rows(kind);
            var version=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(rows)));
            return new(id==16?"Středisko 16":"Test <group>",version,rows.ToArray(),5);
        }
        public CompositionPicker Browse(string kind,int id,string category,string? search,string? inputType,int afterId,int? station=null){
            _=Load(kind,id);ElectricityGroupCompositionEditor.ValidatePicker(kind,category,search,inputType,afterId,station);
            var rows=category=="fictitious"?new[]{new CompositionCandidate(50000001,"Virtual test","kW","kWh","EP")}:new[]{new CompositionCandidate(10002,"Assigned test","MW","MWh","EP"),new CompositionCandidate(10003,"Test candidate","MW","MWh","E"),new CompositionCandidate(20003,"Other station","kW","kWh","E")};
            return new(rows.Where(row=>(station is null||row.Id/10000==station)&&row.Id>afterId&&(string.IsNullOrEmpty(inputType)||row.Type==inputType)&&(string.IsNullOrEmpty(search)||row.Name.Contains(search,StringComparison.OrdinalIgnoreCase))).ToArray(),false,["E","EP"],category=="centres"?[]:category=="fictitious"?[5000]:[1,2]);
        }
        public CompositionSearch Search(string kind,int id,string search){_=Load(kind,id);return new([new(10003,"Test candidate","MW","MWh","Vstup")],false);}
        private void Validate(string kind,int id,string version){if(Load(kind,id).Version!=version)throw new GroupConfigurationConflictException();}
        public void Add(string kind,int id,int child,string version){Validate(kind,id,version);if(Rows(kind).Any(r=>r.Id==child))throw new ArgumentException("Duplicate");Rows(kind).Add(new(child,"1",false));Writes++;}
        public void Update(string kind,int id,int child,string? coefficient,bool recalculate,string version){
            Validate(kind,id,version);var value=kind=="plants"?null:ElectricityGroupCompositionEditor.Coefficient(coefficient).ToString(System.Globalization.CultureInfo.GetCultureInfo("cs-CZ"));
            var index=Rows(kind).FindIndex(r=>r.Id==child);if(index<0)throw new ArgumentException("Missing");Rows(kind)[index]=new(child,value,recalculate);Writes++;
        }
        public void Remove(string kind,int id,int child,string version){Validate(kind,id,version);Rows(kind).RemoveAll(r=>r.Id==child);Writes++;}
    }
    private sealed class FakeGroupConfiguration : IElectricityGroupConfiguration {
        public int Writes {get;private set;}
        public int? LastMainMeter{get;private set;}
        public bool? LastRecalculate{get;private set;}
        public MainMeterChoice[] MainMeters()=>[new(5,"Main meter"),new(6,"Other meter")];
        public void SaveSettings(string kind,int id,string name,string? power,string? energy,string version,int? mainMeter,bool? recalculate){
            ElectricityGroupConfiguration.ValidateSettings(kind,mainMeter,recalculate);
            if(mainMeter is >0&&!MainMeters().Any(m=>m.Id==mainMeter))throw new ArgumentException("Unknown main meter");
            Save(kind,id,name,power,energy,version);LastMainMeter=mainMeter;LastRecalculate=recalculate;
        }
        public GroupConfiguration Load(string kind,int id){
            ElectricityGroupCatalog.Definition(kind);if(id!=1&&!(kind=="centres"&&id==16))throw new KeyNotFoundException();
            return new(id==16?"Středisko 16":"Test <group>","MW","MWh",new string('A',64),[new(10002,"Component","MW","MWh","1,5","0","1",true)],kind=="fictitious"?null:"5",kind=="centres"?"True":null);
        }
        public void Save(string kind,int id,string name,string? power,string? energy,string version){
            var current=Load(kind,id);ElectricityGroupConfiguration.Validate(name,power,energy);
            if(current.Version!=version)throw new GroupConfigurationConflictException();Writes++;
        }
    }
    private sealed class FakeGroups : IElectricityGroupCatalog {
        public ElectricityGroup[] Load(string kind){ElectricityGroupCatalog.Definition(kind);return kind=="centres"?[new(1,"Test group","MW","MWh"),new(16,"Středisko 16","MW","MWh")]:[new(1,"Test group","MW","MWh")];}
    }
    private sealed class FakeMeterConfiguration : IElectricityMeterConfiguration {
        public MeterConfiguration Load(int id)=>new("Test",101,"1",102,"1",new string('A',64));
        public void Save(int id,string name,string coefficient,string reactiveCoefficient,bool disconnect,string version){
            ElectricityMeterConfiguration.Name(name);ElectricityMeterConfiguration.Coefficient(coefficient);
        }
    }
    private sealed class FakeTypePrices : IElectricityTypePriceStore {
        private readonly List<TypePriceRecord> rows=[new(null,Enumerable.Repeat<decimal?>(9,6).ToArray(),null),new(new DateTime(2025,1,1),Enumerable.Repeat<decimal?>(1.25m,6).ToArray(),false)];
        public int Writes{get;private set;}
        public TypePriceSnapshot Load(string kind,int id) {
            ElectricityGroupCatalog.Definition(kind);if(id!=1&&!(kind=="centres"&&id==16))throw new KeyNotFoundException();
            var version=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(rows)));
            return new(2,"EP","kWh","0","0","0",rows.OrderByDescending(r=>r.EffectiveAt).ToArray(),version);
        }
        public void Add(string kind,int id,DateOnly month,string[] values,bool? tariff,string version){
            var current=Load(kind,id);if(version!=current.Version)throw new GroupConfigurationConflictException();
            var parsed=ElectricityTypePrices.Parse(values);ElectricityTypePrices.ValidateNewMonth(current.History,month);
            rows.Add(new(month.ToDateTime(TimeOnly.MinValue),parsed,tariff));Writes++;
        }
    }
    private sealed class FakePriceEditor : IElectricityPriceEditor {
        public EditablePrices Load(int id,DateOnly month)=>new("Test",Enumerable.Repeat("0",28).ToArray(),new string('A',64));
        public void Save(int id,DateOnly month,string[] values,string version)=>ElectricityPriceEditor.Parse(values);
    }
    private sealed class FakePrices : IElectricityPriceStore
    {
        public PriceSnapshot Load(int meterId, DateOnly month) => meterId == 5
            ? new("Test", month, false, ElectricityPriceStore.Fields.Select((f,i)=>new PriceValue(i==0?"Cena test":f.Label, ElectricityPriceStore.Display(f.Column,f.Column.StartsWith("b")?(object)false:1234567.89m),ElectricityPriceStore.Unit(f.Column))).ToArray()) : throw new KeyNotFoundException();
    }
    private sealed class FakeTariffs : IElectricityTariffStore
    {
        public TariffSnapshot Load(int meterId, DateOnly month) => new(meterId, "Test", month, ElectricityTariffs.Defaults(), false, new string('A', 64));
        public int Writes { get; private set; }
        public DateOnly LastMonth { get; private set; }
        public int FirstCell { get; private set; }
        public void Save(int meterId, DateOnly month, IReadOnlyList<int> cells, string expectedVersion)
        {
            if (meterId != 5 || expectedVersion != new string('A', 64)) throw new Exception("Wrong tariff target/version");
            ElectricityTariffs.Validate(cells);
            Writes++; LastMonth = month; FirstCell = cells[0];
        }
    }
}
