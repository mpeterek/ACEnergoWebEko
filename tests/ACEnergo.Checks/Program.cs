using ACEnergo.Web.Services;
using Microsoft.Extensions.Configuration;
var count=0;
void Equal<T>(T expected,T actual) {if(!EqualityComparer<T>.Default.Equals(expected,actual))throw new Exception($"Expected {expected}, got {actual}");count++;}
void Reject(Action action){try{action();}catch(ArgumentException){count++;return;}throw new Exception("Invalid input accepted");}
Equal("ACEnergoWebEko0001.mdb", DatabaseCatalog.MonthlyName(2000,1));
Equal("ACEnergoWebEko2610.mdb", DatabaseCatalog.MonthlyName(2026,10));
Equal("ACEnergoWebEko9912.mdb", DatabaseCatalog.MonthlyName(2099,12));
Equal("ACEnergoWebEko10001.mdb", DatabaseCatalog.MonthlyName(2100,1));
Reject(()=>DatabaseCatalog.MonthlyName(1999,1));Reject(()=>DatabaseCatalog.MonthlyName(2026,13));Reject(()=>DatabaseCatalog.MonthlyName(2026,0));
var dir=Path.Combine(Path.GetTempPath(),"acenergo-check-"+Guid.NewGuid());Directory.CreateDirectory(dir);
try {
 var catalog=new DatabaseCatalog(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"Access:Directory",dir}}).Build());
 foreach(var f in new[]{"ACEnergoWebEko2610.mdb","ACEnergoWebEko2609.mdb","ACEnergoWebEko2613.mdb","ACEnergoWebEko.mdb","ACWebEkoLogin.mdb"})File.WriteAllBytes(Path.Combine(dir,f),[]);
 Equal(2,catalog.AvailableMonths().Length);
 Equal(Path.Combine(dir,"ACWebEkoLogin.mdb"),catalog.Resolve("ACWebEkoLogin.mdb"));
 Reject(()=>catalog.Resolve("../ACWebEkoLogin.mdb"));Reject(()=>catalog.Resolve("other.mdb"));
 Equal(13,AccessStore.Rights.Length);
}finally{Directory.Delete(dir,true);}
Console.WriteLine($"PASS: {count} database selection and permission-contract checks.");
