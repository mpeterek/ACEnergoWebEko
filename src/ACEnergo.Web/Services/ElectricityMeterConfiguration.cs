using System.Data;
using System.Data.OleDb;
using System.Globalization;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text.Json;
using ACEnergo.Web.Domain;
namespace ACEnergo.Web.Services;
public sealed record MeterConfiguration(string Name,int Input,string Coefficient,int ReactiveInput,string ReactiveCoefficient,string Version,bool ReactiveWasNull=false,decimal? StoredReactiveCoefficient=1m);
public interface IElectricityMeterConfiguration {
 MeterConfiguration Load(int id);
 void Save(int id,string name,string coefficient,string reactiveCoefficient,bool disconnectReactive,string version);
}
public sealed class ElectricityMeterConfiguration(DatabaseCatalog catalog,IConfiguration config):IElectricityMeterConfiguration {
 private readonly ElectricityConfigurationDatabase db=new(catalog,config);
 public static decimal Coefficient(string? text) {
  if(!decimal.TryParse(text?.Replace(',','.'),NumberStyles.AllowDecimalPoint|NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out var value)||value==0)
   throw new ArgumentException("Koeficient musí být platné nenulové číslo.");
  return value;
 }
 public static string Name(string? text) {
  if(string.IsNullOrWhiteSpace(text)||text.Length>30) throw new ArgumentException("Název musí obsahovat 1 až 30 znaků.");
  return text;
 }
 public MeterConfiguration Load(int id) {
  if(!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
  using var conn=db.Open(false);return Read(conn,id);
 }
 public void Save(int id,string name,string coefficient,string reactiveCoefficient,bool disconnectReactive,string version) {
  name=Name(name);var value=Coefficient(coefficient);var reactive=disconnectReactive?1:Coefficient(reactiveCoefficient);
  ElectricityHolidays.ValidateVersion(version);
  if(!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
  lock(ElectricityConfigurationDatabase.WriteGate) SaveWindows(id,name,value,reactive,disconnectReactive,version);
 }
 [SupportedOSPlatform("windows")]
 private static MeterConfiguration Read(OleDbConnection conn,int id,OleDbTransaction? tx=null) {
  using var cmd=new OleDbCommand("SELECT [strNazev],[numVstup],[numKonst],[numVstupJal],[numKonstJal] FROM [tblKonfigHlavniElektrina] WHERE [numID]=?",conn,tx);
  cmd.Parameters.Add("id",OleDbType.Integer).Value=id;using var reader=cmd.ExecuteReader();
  if(reader is null||!reader.Read()) throw new KeyNotFoundException();
  var raw=Enumerable.Range(0,5).Select(reader.GetValue).ToArray();
  if(raw.Take(3).Any(v=>v is DBNull)) throw new InvalidDataException("Konfigurace činného vstupu obsahuje NULL.");
  var reactiveInput=raw[3] is DBNull?0:Convert.ToInt32(raw[3]);
  if(reactiveInput!=0 && raw[4] is DBNull) throw new InvalidDataException("Přiřazený jalový vstup nemá koeficient.");
  var result=new MeterConfiguration(Convert.ToString(raw[0])??"",Convert.ToInt32(raw[1]),Convert.ToDecimal(raw[2]).ToString(CultureInfo.GetCultureInfo("cs-CZ")),reactiveInput,reactiveInput==0?"1":Convert.ToDecimal(raw[4]).ToString(CultureInfo.GetCultureInfo("cs-CZ")),Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(raw))),raw[3] is DBNull,raw[4] is DBNull?null:Convert.ToDecimal(raw[4]));
  if(reader.Read()) throw new InvalidDataException("Duplicitní ID měření.");return result;
 }
 [SupportedOSPlatform("windows")]
 private void SaveWindows(int id,string name,decimal value,decimal reactive,bool disconnect,string version) {
  using var held=db.AcquireWriteLock();using var conn=db.Open(true);using var tx=conn.BeginTransaction(IsolationLevel.ReadCommitted);
  var current=Read(conn,id,tx);if(current.Version!=version) throw new TariffConflictException();
  // Nezakládá nové měření a nemění vazbu činného vstupu. Jalový vstup lze jen odpojit.
  using var cmd=new OleDbCommand("UPDATE [tblKonfigHlavniElektrina] SET [strNazev]=?,[numKonst]=?,[numVstupJal]=?,[numKonstJal]=? WHERE [numID]=?",conn,tx);
  cmd.Parameters.Add("name",OleDbType.VarWChar,30).Value=name;
  cmd.Parameters.Add("coef",OleDbType.Double).Value=value;
  cmd.Parameters.Add("reactiveInput",OleDbType.Integer).Value=disconnect?0:current.ReactiveWasNull?DBNull.Value:current.ReactiveInput;
  cmd.Parameters.Add("reactiveCoef",OleDbType.Double).Value=disconnect?1:current.ReactiveInput==0?(object?)current.StoredReactiveCoefficient??DBNull.Value:reactive;
  cmd.Parameters.Add("id",OleDbType.Integer).Value=id;
  if(cmd.ExecuteNonQuery()!=1) throw new InvalidDataException("Změněn neočekávaný počet měření.");tx.Commit();
 }
}
