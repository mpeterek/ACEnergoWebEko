using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text.Json;
using ACEnergo.Web.Services;
using Microsoft.Extensions.Configuration;

internal static class WindowsTypePriceCheck
{
    public static void Run(string[] args)
    {
        if(args.Length!=2)throw new ArgumentException("Použití: --type-price-mdb C:\\ACEnergoData\\ACEnergoWebEko.mdb");
        if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException("Vyžaduje Windows a ACE provider stejné bitovosti jako proces.");
        RunWindows(Path.GetFullPath(args[1]));
    }
    [SupportedOSPlatform("windows")]
    private static void RunWindows(string source)
    {
        if(!File.Exists(source)||!string.Equals(Path.GetFileName(source),"ACEnergoWebEko.mdb",StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Zadejte konfigurační MDB.");
        var original=SHA256.HashData(File.ReadAllBytes(source));
        var directory=Path.Combine(Path.GetTempPath(),"acenergo-type-price-"+Guid.NewGuid());Directory.CreateDirectory(directory);
        try {
            File.Copy(source,Path.Combine(directory,"ACEnergoWebEko.mdb"));
            var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"Access:Directory",directory},{"Access:Provider","Microsoft.ACE.OLEDB.16.0"}}).Build();
            var db=new DatabaseCatalog(config);var catalog=new ElectricityGroupCatalog(db,config);var store=new ElectricityTypePriceStore(db,config);
            var group=catalog.Load("fictitious").FirstOrDefault()??throw new Exception("Chybí elektrické fiktivní měření pro kontrolu.");
            var before=store.Load("fictitious",group.Id);
            var month=Enumerable.Range(2000,100).SelectMany(year=>Enumerable.Range(1,12).Select(m=>new DateOnly(year,m,1))).First(m=>before.History.All(r=>r.EffectiveAt!=m.ToDateTime(TimeOnly.MinValue)));
            store.Add("fictitious",group.Id,month,["1,23456","0","9999999999999.99999","0.00001","2",""],null,before.Version);
            var after=store.Load("fictitious",group.Id);var added=after.History.Single(r=>r.EffectiveAt==month.ToDateTime(TimeOnly.MinValue));
            if(added.Values[0]!=1.23456m||added.Values[2]!=9999999999999.99999m||added.Values[5] is not null||added.Tariff is not null||after.History.Length!=before.History.Length+1)throw new Exception("Neodpovídá přesnost, NULL nebo počet ceníků.");
            var old=after.History.Where(r=>r.EffectiveAt!=added.EffectiveAt).ToArray();
            if(JsonSerializer.Serialize(old)!=JsonSerializer.Serialize(before.History))throw new Exception("Původní historie byla změněna.");
            var rejected=false;
            try{store.Add("fictitious",group.Id,month,["1","1","1","1","1","1"],false,before.Version);}catch(GroupConfigurationConflictException){rejected=true;}
            if(!rejected)throw new Exception("Zastaralá verze byla přijata.");
            rejected=false;
            try{store.Add("fictitious",group.Id,month,["1","1","1","1","1","1"],false,after.Version);}catch(ArgumentException){rejected=true;}
            if(!rejected||store.Load("fictitious",group.Id).Version!=after.Version)throw new Exception("Duplicitní platnost byla přijata nebo porušena transakce.");
            Console.WriteLine("PASS: nativní ceny typu na dočasné kopii: přesnost DECIMAL(18,5), NULL, historie, konflikt a duplicitní datum.");
        }
        finally {
            System.Data.OleDb.OleDbConnection.ReleaseObjectPool();
            Directory.Delete(directory,true);
            if(!SHA256.HashData(File.ReadAllBytes(source)).SequenceEqual(original))throw new Exception("Zdrojový soubor se během kontroly změnil.");
        }
    }
}
