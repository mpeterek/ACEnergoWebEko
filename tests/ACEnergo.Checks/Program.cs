using ACEnergo.Web.Services;
using Microsoft.Extensions.Configuration;
if (args.Length == 2 && args[0] == "--tariff-lock-probe")
{
    try { using var held = TariffWriteLock.Acquire(args[1]); }
    catch (IOException) { Environment.ExitCode = 23; }
    return;
}
if (args.Length > 0 && args[0] == "--submeter-mdb") { WindowsSubmeterReadCheck.Run(args); return; }
if (args.Length > 0 && args[0] == "--type-price-mdb") { WindowsTypePriceCheck.Run(args); return; }
if (args.Length > 0 && args[0] == "--group-mdb") { WindowsGroupReadCheck.Run(args); return; }
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

ElectricityGroupConfiguration.Validate(new string('x',30),new string('W',15),null);
ElectricityGroupConfiguration.Validate("Měření",null,"");
Reject(()=>ElectricityGroupConfiguration.Validate("",null,null));
Reject(()=>ElectricityGroupConfiguration.Validate(new string('x',31),null,null));
Reject(()=>ElectricityGroupConfiguration.Validate("Test",new string('W',16),null));
Reject(()=>ElectricityGroupConfiguration.Validate("Test",null,new string('W',16)));
Console.WriteLine("PASS group name and optional unit schema limits.");

Equal(-1.25d,ElectricityGroupCompositionEditor.Coefficient("-1,25"));
Equal(0d,ElectricityGroupCompositionEditor.Coefficient("0"));
Equal(1.25d,ElectricityGroupCompositionEditor.Coefficient("1.25"));
foreach(var invalid in new[]{"NaN","Infinity","1e309","1,2.3","",new string('9',400)})Reject(()=>ElectricityGroupCompositionEditor.Coefficient(invalid));
Equal("Fiktivní měření 1",ElectricityGroupCompositionEditor.ComponentLabel("centres",50000001));
Equal("0001/0002",ElectricityGroupCompositionEditor.ComponentLabel("centres",10002));
Equal("Středisko 12",ElectricityGroupCompositionEditor.ComponentLabel("plants",12));
Reject(()=>ElectricityGroupCompositionEditor.Definition("analog"));
Console.WriteLine("PASS composition coefficients, virtual IDs and table whitelist.");

ElectricityGroupConfiguration.ValidateSettings("centres",null,null);
ElectricityGroupConfiguration.ValidateSettings("centres",5,true);
ElectricityGroupConfiguration.ValidateSettings("plants",5,null);
Reject(()=>ElectricityGroupConfiguration.ValidateSettings("centres",-1,false));
Reject(()=>ElectricityGroupConfiguration.ValidateSettings("fictitious",5,null));
Reject(()=>ElectricityGroupConfiguration.ValidateSettings("plants",5,true));
Console.WriteLine("PASS main meter and recalculation settings scope validation.");

ElectricityGroupCompositionEditor.ValidatePicker("centres","physical",null,null,0);
ElectricityGroupCompositionEditor.ValidatePicker("centres","fictitious","","EP",50000001);
ElectricityGroupCompositionEditor.ValidatePicker("plants","centres",null,null,0);
Reject(()=>ElectricityGroupCompositionEditor.ValidatePicker("fictitious","fictitious",null,null,0));
Reject(()=>ElectricityGroupCompositionEditor.ValidatePicker("centres","analog",null,null,0));
Reject(()=>ElectricityGroupCompositionEditor.ValidatePicker("centres","physical",null,null,-1));
Reject(()=>ElectricityGroupCompositionEditor.ValidatePicker("centres","physical",new string('x',81),null,0));
Console.WriteLine("PASS picker source scope, filter limits and cursor validation.");

Equal((1,3),((int)ElectricityGroupCompositionEditor.AddressParts("physical",10003).Station!,ElectricityGroupCompositionEditor.AddressParts("physical",10003).Input));
Equal((5000,1),((int)ElectricityGroupCompositionEditor.AddressParts("fictitious",50000001).Station!,ElectricityGroupCompositionEditor.AddressParts("fictitious",50000001).Input));
Equal<int?>(null,ElectricityGroupCompositionEditor.AddressParts("centres",16).Station);
Equal(16,ElectricityGroupCompositionEditor.AddressParts("centres",16).Input);
Reject(()=>ElectricityGroupCompositionEditor.ValidatePicker("plants","centres",null,null,0,1));
Reject(()=>ElectricityGroupCompositionEditor.ValidatePicker("centres","physical",null,null,0,214749));
Console.WriteLine("PASS legacy station/input decomposition and station filter scope.");

TypePriceChecks.Run();

SubmeterChecks.Run();
