using System.Data;
using System.Data.OleDb;
using System.Globalization;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text.Json;
namespace ACEnergo.Web.Services;

public sealed record GroupConfiguration(string Name,string? PowerUnit,string? EnergyUnit,string Version,FictitiousComponent[] Components,string? MainMeter,string? Recalculation);
public interface IElectricityGroupConfiguration {
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
 [SupportedOSPlatform("windows")]
 private static GroupConfiguration Read(OleDbConnection connection,string kind,int id,bool composition,OleDbTransaction? tx=null){
  var table=ElectricityGroupCatalog.Definition(kind).Table;
  var extra=kind=="fictitious"?"":kind=="centres"?",[numIDHlavni],[bPrepocet]":",[numIDHlavni]";
  using var command=new OleDbCommand($"SELECT [strNazev],[strRozmerP],[strRozmerV]{extra} FROM [{table}] WHERE [numCislo]=? AND [numIDEnergie] IN (SELECT [numKlic] FROM [tblKonfigEnergie] WHERE [strNazevEnergie]=?)",connection,tx);
  command.Parameters.Add("id",OleDbType.Integer).Value=id;command.Parameters.Add("energy",OleDbType.VarWChar,255).Value="Elektřina";
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
 private void Write(string kind,int id,string name,string? power,string? energy,string version){
  using var held=db.AcquireWriteLock();using var connection=db.Open(true);using var tx=connection.BeginTransaction(IsolationLevel.ReadCommitted);
  var current=Read(connection,kind,id,false,tx);if(current.Version!=version)throw new GroupConfigurationConflictException();
  var table=ElectricityGroupCatalog.Definition(kind).Table;
  using var command=new OleDbCommand($"UPDATE [{table}] SET [strNazev]=?,[strRozmerP]=?,[strRozmerV]=? WHERE [numCislo]=? AND [numIDEnergie] IN (SELECT [numKlic] FROM [tblKonfigEnergie] WHERE [strNazevEnergie]=?)",connection,tx);
  // Prázdné původní hodnoty zachovávají rozdíl mezi NULL a prázdným řetězcem.
  static object Unit(string? value,string? old)=>string.IsNullOrEmpty(value)?(object?)(string.IsNullOrEmpty(old)?old:null)??DBNull.Value:value;
  command.Parameters.Add("name",OleDbType.VarWChar,30).Value=name;
  command.Parameters.Add("power",OleDbType.VarWChar,15).Value=Unit(power,current.PowerUnit);
  command.Parameters.Add("energyUnit",OleDbType.VarWChar,15).Value=Unit(energy,current.EnergyUnit);
  command.Parameters.Add("id",OleDbType.Integer).Value=id;command.Parameters.Add("energy",OleDbType.VarWChar,255).Value="Elektřina";
  if(command.ExecuteNonQuery()!=1)throw new InvalidDataException("Změněn neočekávaný počet konfigurací.");tx.Commit();
 }
}
