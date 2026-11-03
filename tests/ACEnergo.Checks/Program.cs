using ACEnergo.Web.Services;
using Microsoft.Extensions.Configuration;
if (args.Length == 2 && args[0] == "--tariff-lock-probe")
{
    try { using var held = TariffWriteLock.Acquire(args[1]); }
    catch (IOException) { Environment.ExitCode = 23; }
    return;
}
if (args.Length > 0 && args[0] == "--holiday-mdb") { WindowsHolidayCheck.Run(args); return; }
if (args.Length > 0 && args[0] == "--tariff-mdb") { WindowsTariffCheck.Run(args); return; }
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
Equal(true, AccessFailure.Describe(new FileNotFoundException()).StartsWith("MDB-FILE:"));
Equal(true, AccessFailure.Describe(new DirectoryNotFoundException()).StartsWith("MDB-FILE:"));
Equal(true, AccessFailure.Describe(new UnauthorizedAccessException()).StartsWith("MDB-PERMISSION:"));
Equal(true, AccessFailure.Describe(new PlatformNotSupportedException()).StartsWith("MDB-PLATFORM:"));
Equal(true, AccessFailure.Describe(new InvalidOperationException("PRIVATE-MESSAGE")).StartsWith("MDB-INIT:"));
Equal(false, AccessFailure.Describe(new IOException("PRIVATE-MESSAGE")).Contains("PRIVATE-MESSAGE"));
Console.WriteLine($"PASS: {count} database selection, permission-contract and diagnostic checks.");

TariffChecks.Run();

TariffWriteLockChecks.Run();

HolidayChecks.Run();

await AuthenticatedPageChecks.RunAsync();

Equal("MWh", ElectricityPriceStore.Display("bNedocerpMWhProc", true));
Equal("%", ElectricityPriceStore.Display("bPrecerpMWhProc", false));
Equal("Ne", ElectricityPriceStore.Display("bDPH", false));
Equal("Neuvedeno", ElectricityPriceStore.Display("numCenaTarifVysoky", DBNull.Value));
Equal("123,45", ElectricityPriceStore.Display("numCenaTarifVysoky", 123.45m));
Console.WriteLine("PASS: price values, Czech decimals, null and unit flags.");

var priceInput=Enumerable.Repeat("0",28).ToArray();priceInput[0]="123,45";
Equal(123.45m,ElectricityPriceEditor.Parse(priceInput)[0]);
priceInput[0]="";Equal<object?>(null,ElectricityPriceEditor.Parse(priceInput)[0]);
priceInput[0]="-1";Reject(()=>ElectricityPriceEditor.Parse(priceInput));
priceInput[0]="1,2.3";Reject(()=>ElectricityPriceEditor.Parse(priceInput));
Console.WriteLine("PASS price editor comma decimals, NULL and invalid input.");

Equal(2.5m,ElectricityMeterConfiguration.Coefficient("2,5"));
Equal(-1m,ElectricityMeterConfiguration.Coefficient("-1"));
Reject(()=>ElectricityMeterConfiguration.Coefficient("0"));
Reject(()=>ElectricityMeterConfiguration.Coefficient("1.2.3"));
Reject(()=>ElectricityMeterConfiguration.Name(new string('x',31)));
Console.WriteLine("PASS meter name and nonzero coefficient validation.");

Equal("MW",ElectricityPriceStore.Unit("numRezKapacitaRocni"));
Equal("Kč/MW",ElectricityPriceStore.Unit("numCenaRocniRezKapacita"));
Equal("Kč/MWh",ElectricityPriceStore.Unit("numCenaTarifVysoky"));
Equal("MWh",ElectricityPriceStore.Unit("numProcNedocerp",true));
Equal("%",ElectricityPriceStore.Unit("numProcNedocerp",false));
Console.WriteLine("PASS price units and variable threshold units.");

Equal("0001/0002",ElectricityFictitiousComposition.Address(10002));
Equal("0000/0000",ElectricityFictitiousComposition.Address(0));
Equal("0123/9999",ElectricityFictitiousComposition.Address(1239999));
Console.WriteLine("PASS station/input address formatting.");
