using System.Data;
using System.Data.OleDb;
using System.Globalization;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text.Json;
namespace ACEnergo.Web.Services;

public sealed record GroupConfiguration(string Name,string? PowerUnit,string? EnergyUnit,string Version,FictitiousComponent[] Components,string? MainMeter,string? Recalculation);
public sealed record MainMeterChoice(int Id,string Name);
public interface IElectricityGroupConfiguration {
 MainMeterChoice[] MainMeters();
 void SaveSettings(string kind,int id,string name,string? powerUnit,string? energyUnit,string version,int? mainMeter,bool? recalculate);
 GroupConfiguration Load(string kind,int id);
 void Save(string kind,int id,string name,string? powerUnit,string? energyUnit,string version);
}
public sealed class GroupConfigurationConflictException:Exception;
public sealed class ElectricityGroupConfiguration(DatabaseCatalog catalog,IConfiguration config):IElectricityGroupConfiguration {
 private readonly ElectricityConfigurationDatabase db=new(catalog,config);
 public static void Validate(string name,string? powerUnit,string? energyUnit){
  ElectricityMeterConfiguration.Name(name);
  if(powerUnit?.Length>15 || energyUnit?.Length>15)throw new ArgumentException("Jednotka může obsahovat nejvýše 15 znaků.");
 }
 public GroupConfiguration Load(string kind,int id){
  ElectricityGroupCatalog.Definition(kind);
  if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException();
  using var connection=db.Open(false);return Read(connection,kind,id,true);
 }
 public void Save(string kind,int id,string name,string? powerUnit,string? energyUnit,string version){
  ElectricityGroupCatalog.Definition(kind);Validate(name,powerUnit,energyUnit);
  ACEnergo.Web.Domain.ElectricityHolidays.ValidateVersion(version);
  if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException();
  lock(ElectricityConfigurationDatabase.WriteGate)Write(kind,id,name,powerUnit,energyUnit,version);
 }
 public MainMeterChoice[] MainMeters(){
  if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException();
  using var connection=db.Open(false);using var command=new OleDbCommand("SELECT [numID],[strNazev] FROM [tblKonfigHlavniElektrina] ORDER BY [strNazev],[numID]",connection);
  using var reader=command.ExecuteReader();var rows=new List<MainMeterChoice>();
  while(reader is not null&&reader.Read())rows.Add(new(Convert.ToInt32(reader[0]),Convert.ToString(reader[1])??""));return rows.ToArray();
 }
 public static void ValidateSettings(string kind,int? mainMeter,bool? recalculate){
  ElectricityGroupCatalog.Definition(kind);
  if(mainMeter<0)throw new ArgumentException("Identifikátor hlavního měření nesmí být záporný.");
  if(kind=="fictitious"&&(mainMeter is not null||recalculate is not null))throw new ArgumentException("Fiktivní vstup tato nastavení neobsahuje.");
  if(kind=="plants"&&recalculate is not null)throw new ArgumentException("Přepočet závodu se nastavuje na jednotlivých položkách.");
 }
 public void SaveSettings(string kind,int id,string name,string? powerUnit,string? energyUnit,string version,int? mainMeter,bool? recalculate){
  ValidateSettings(kind,mainMeter,recalculate);Validate(name,powerUnit,energyUnit);ACEnergo.Web.Domain.ElectricityHolidays.ValidateVersion(version);
  if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException();
  lock(ElectricityConfigurationDatabase.WriteGate)Write(kind,id,name,powerUnit,energyUnit,version,new(mainMeter,recalculate));
 }
 private sealed record Settings(int? MainMeter,bool? Recalculate);
 [SupportedOSPlatform("windows")]
 private static GroupConfiguration Read(OleDbConnection connection,string kind,int id,bool composition,OleDbTransaction? tx=null){
  var table=ElectricityGroupCatalog.Definition(kind).Table;
  var extra=kind=="fictitious"?"":kind=="centres"?",[numIDHlavni],[bPrepocet]":",[numIDHlavni]";
  // ACE může vyhodnocovat poziční parametry poddotazu před parametry vnějšího dotazu.
  // Energie je pevná konstanta aplikace; veškeré vstupy uživatele zůstávají parametry.
  using var command=new OleDbCommand($"SELECT [strNazev],[strRozmerP],[strRozmerV]{extra} FROM [{table}] WHERE [numCislo]=? AND [numIDEnergie] IN (SELECT [numKlic] FROM [tblKonfigEnergie] WHERE [strNazevEnergie]='Elektřina')",connection,tx);
  command.Parameters.Add("id",OleDbType.Integer).Value=id;
  object[] raw;
  using(var reader=command.ExecuteReader()){
   if(reader is null||!reader.Read())throw new KeyNotFoundException();
   raw=Enumerable.Range(0,reader.FieldCount).Select(reader.GetValue).ToArray();
   if(reader.Read())throw new InvalidDataException("Duplicitní identifikátor konfigurace.");
  }
  string? Text(int i)=>raw[i] is DBNull?null:Convert.ToString(raw[i],CultureInfo.GetCultureInfo("cs-CZ"));
  var version=Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new object[]{kind,id,raw})));
  return new(Text(0)??"",Text(1),Text(2),version,composition?Components(connection,kind,id):[],raw.Length>3?Text(3)??"Neuvedeno":null,raw.Length>4?Text(4)??"Neuvedeno":null);
 }
 [SupportedOSPlatform("windows")]
 private static FictitiousComponent[] Components(OleDbConnection connection,string kind,int id){
  var plants=kind=="plants";
  var parts=kind switch {"fictitious"=>"tblKonfigFiktivniPolozky","centres"=>"tblKonfigStrediskoPolozky","plants"=>"tblKonfigZavodPolozky",_=>throw new ArgumentException()};
  var child=plants?"numStredisko":"numImpuls";var target=plants?"tblKonfigStredisko":"tblKonfigImpuls";
  using var command=new OleDbCommand($"SELECT p.[{child}],i.[strNazev],i.[strRozmerP],i.[strRozmerV],p.[numKonstA],p.[numKonstB],p.[numKonstP],i.[numCislo]{(plants?",p.[bPrepocet]":"")} FROM [{parts}] AS p LEFT JOIN [{target}] AS i ON p.[{child}]=i.[numCislo] WHERE p.[numCislo]=? ORDER BY p.[{child}]",connection);
  if(kind=="centres"){
   // Středisko může odkazovat také na fiktivní měření (ID od 50000000).
   command.CommandText="SELECT p.[numImpuls], IIF(p.[numImpuls]>=50000000,f.[strNazev],i.[strNazev]), IIF(p.[numImpuls]>=50000000,f.[strRozmerP],i.[strRozmerP]), IIF(p.[numImpuls]>=50000000,f.[strRozmerV],i.[strRozmerV]),p.[numKonstA],p.[numKonstB],p.[numKonstP], IIF(p.[numImpuls]>=50000000,f.[numCislo],i.[numCislo]) FROM ([tblKonfigStrediskoPolozky] AS p LEFT JOIN [tblKonfigImpuls] AS i ON p.[numImpuls]=i.[numCislo]) LEFT JOIN [tblKonfigFiktivni] AS f ON p.[numImpuls]=f.[numCislo] WHERE p.[numCislo]=? ORDER BY p.[numImpuls]";
  }
  command.Parameters.Add("id",OleDbType.Integer).Value=id;
  using var reader=command.ExecuteReader();var rows=new List<FictitiousComponent>();
  while(reader is not null&&reader.Read()){
   if(reader.IsDBNull(0))throw new InvalidDataException("Prázdný identifikátor vazby.");
   string Text(int i)=>reader.IsDBNull(i)?"Neuvedeno":Convert.ToString(reader[i],CultureInfo.GetCultureInfo("cs-CZ"))??"";
   rows.Add(new(Convert.ToInt32(reader[0]),reader.IsDBNull(7)?"Nedefinováno":Text(1),Text(2),Text(3),Text(4),Text(5),Text(6),!reader.IsDBNull(7),plants?Text(8):null));
  }
  return rows.ToArray();
 }
 [SupportedOSPlatform("windows")]
 private void Write(string kind,int id,string name,string? power,string? energy,string version,Settings? settings=null){
  using var held=db.AcquireWriteLock();using var connection=db.Open(true);using var tx=connection.BeginTransaction(IsolationLevel.ReadCommitted);
  var current=Read(connection,kind,id,false,tx);if(current.Version!=version)throw new GroupConfigurationConflictException();
  var table=ElectricityGroupCatalog.Definition(kind).Table;
  if(settings?.MainMeter is >0 && current.MainMeter!=settings.MainMeter.Value.ToString(CultureInfo.GetCultureInfo("cs-CZ"))){
   using var exists=new OleDbCommand("SELECT COUNT(*) FROM [tblKonfigHlavniElektrina] WHERE [numID]=?",connection,tx);
   exists.Parameters.Add("id",OleDbType.Integer).Value=settings.MainMeter.Value;
   if(Convert.ToInt32(exists.ExecuteScalar())!=1)throw new ArgumentException("Vybrané hlavní měření neexistuje. Obnovte seznam.");
  }
  var extra=settings is null||kind=="fictitious"?"":kind=="centres"?",[numIDHlavni]=?,[bPrepocet]=?":",[numIDHlavni]=?";
  using var command=new OleDbCommand($"UPDATE [{table}] SET [strNazev]=?,[strRozmerP]=?,[strRozmerV]=?{extra} WHERE [numCislo]=? AND [numIDEnergie] IN (SELECT [numKlic] FROM [tblKonfigEnergie] WHERE [strNazevEnergie]='Elektřina')",connection,tx);
  // Prázdné původní hodnoty zachovávají rozdíl mezi NULL a prázdným řetězcem.
  static object Unit(string? value,string? old)=>string.IsNullOrEmpty(value)?(object?)(string.IsNullOrEmpty(old)?old:null)??DBNull.Value:value;
  command.Parameters.Add("name",OleDbType.VarWChar,30).Value=name;
  command.Parameters.Add("power",OleDbType.VarWChar,15).Value=Unit(power,current.PowerUnit);
  command.Parameters.Add("energyUnit",OleDbType.VarWChar,15).Value=Unit(energy,current.EnergyUnit);
  if(settings is not null&&kind!="fictitious"){
   command.Parameters.Add("mainMeter",OleDbType.Integer).Value=settings.MainMeter is >0?settings.MainMeter.Value:current.MainMeter=="Neuvedeno"?DBNull.Value:0;
   if(kind=="centres")command.Parameters.Add("recalculate",OleDbType.Boolean).Value=(object?)settings.Recalculate??DBNull.Value;
  }
  command.Parameters.Add("id",OleDbType.Integer).Value=id;
  if(command.ExecuteNonQuery()!=1)throw new InvalidDataException("Změněn neočekávaný počet konfigurací.");
  if(kind=="plants"&&settings is not null&&settings.MainMeter.GetValueOrDefault()==0){
   // UpDateZavodyH.asp: bez hlavního měření se vypíná přepočet položek závodu.
   using var clear=new OleDbCommand("UPDATE [tblKonfigZavodPolozky] SET [bPrepocet]=False WHERE [numCislo]=?",connection,tx);
   clear.Parameters.Add("id",OleDbType.Integer).Value=id;clear.ExecuteNonQuery();
  }
  tx.Commit();
 }
}
