using System.Data;
using System.Data.OleDb;
using System.Globalization;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text.Json;
using ACEnergo.Web.Domain;
namespace ACEnergo.Web.Services;
public sealed record EditablePrices(string Name, string[] Values, string Version);
public interface IElectricityPriceEditor {
 EditablePrices Load(int meterId, DateOnly month);
 void Save(int meterId, DateOnly month, string[] values, string version);
}
public sealed class ElectricityPriceEditor(DatabaseCatalog catalog, IConfiguration config) : IElectricityPriceEditor {
 private readonly ElectricityConfigurationDatabase db = new(catalog,config);
 public static object?[] Parse(string[] values) {
  if(values.Length != ElectricityPriceStore.Fields.Length) throw new ArgumentException("Chybí položky ceníku.");
  return values.Select((value,index) => {
   if(ElectricityPriceStore.Fields[index].Column.StartsWith("b")) {
    if(string.IsNullOrWhiteSpace(value)) return null;
    if(value is not "0" and not "1") throw new ArgumentException("Neplatný přepínač ceníku.");
    return (object?)(value == "1");
   }
   if(string.IsNullOrWhiteSpace(value)) return null;
   if(!decimal.TryParse(value.Replace(',', '.'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number) || number < 0)
    throw new ArgumentException("Ceny a kapacity musí být nezáporná čísla bez oddělovačů tisíců.");
   return (object?)number;
  }).ToArray();
 }
 public EditablePrices Load(int meterId, DateOnly month) {
  if(!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
  return LoadWindows(meterId,month);
 }
 public void Save(int meterId,DateOnly month,string[] values,string version) {
  var parsed=Parse(values);
  ElectricityHolidays.ValidateVersion(version);
  if(!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
  lock(ElectricityConfigurationDatabase.WriteGate) SaveWindows(meterId,month,parsed,version);
 }
 [SupportedOSPlatform("windows")]
 private static List<object?[]> Read(OleDbConnection conn,int id,DateOnly month,OleDbTransaction? tx=null) {
  using var cmd=new OleDbCommand("SELECT [dtDatumPlatnost], " + string.Join(",",ElectricityPriceStore.Fields.Select(f=>$"[{f.Column}]")) + " FROM [tblCenyHlavniElektrina] WHERE [numIDHlavni]=? AND [dtDatumPlatnost]>=? AND [dtDatumPlatnost]<? ORDER BY [dtDatumPlatnost]",conn,tx);
  cmd.Parameters.Add("id",OleDbType.Integer).Value=id;
  cmd.Parameters.Add("from",OleDbType.Date).Value=month.ToDateTime(TimeOnly.MinValue);
  cmd.Parameters.Add("until",OleDbType.Date).Value=new DateTime(month.Year+1,1,1);
  using var reader=cmd.ExecuteReader(); var rows=new List<object?[]>();
  while(reader is not null && reader.Read()) rows.Add(Enumerable.Range(0,reader.FieldCount).Select(i=>reader.IsDBNull(i)?null:reader[i]).ToArray());
  return rows;
 }
 private static string Version(List<object?[]> rows)=>Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(rows)));
 [SupportedOSPlatform("windows")]
 private EditablePrices LoadWindows(int id,DateOnly month) {
  using var conn=db.Open(false); var name=ElectricityConfigurationDatabase.MeterName(conn,id);
  var rows=Read(conn,id,month); var current=rows.Where(r=>r[0] is DateTime date && date==month.ToDateTime(TimeOnly.MinValue)).ToArray();
  if(current.Length>1) throw new InvalidDataException("Duplicitní ceník.");
  var values=ElectricityPriceStore.Fields.Select((f,i)=> {
   object? value=current.Length==0 ? (f.Column.StartsWith("b") ? false : 0) : current[0][i+1];
   return value is null ? "" : f.Column.StartsWith("b") ? (Convert.ToBoolean(value)?"1":"0") : Convert.ToDecimal(value,CultureInfo.InvariantCulture).ToString(CultureInfo.GetCultureInfo("cs-CZ"));
  }).ToArray();
  return new(name,values,Version(rows));
 }
 [SupportedOSPlatform("windows")]
 private void SaveWindows(int id,DateOnly month,object?[] values,string version) {
  using var held=db.AcquireWriteLock(); using var conn=db.Open(true);
  using var tx=conn.BeginTransaction(IsolationLevel.ReadCommitted);
  _=ElectricityConfigurationDatabase.MeterName(conn,id,tx);
  if(Version(Read(conn,id,month,tx))!=version) throw new TariffConflictException();
  foreach(var date in ElectricityTariffs.WriteMonths(month)) {
   using var update=new OleDbCommand("UPDATE [tblCenyHlavniElektrina] SET "+string.Join(",",ElectricityPriceStore.Fields.Select(f=>$"[{f.Column}]=?"))+" WHERE [numIDHlavni]=? AND [dtDatumPlatnost]=?",conn,tx);
   Add(update,values,id,date); var count=update.ExecuteNonQuery();
   if(count>1) throw new InvalidDataException("Duplicitní ceník; zápis zrušen.");
   if(count==0) {
    var columns=ElectricityPriceStore.Fields.Select(f=>$"[{f.Column}]").Concat(["[numIDHlavni]","[dtDatumPlatnost]"]).ToArray();
    using var insert=new OleDbCommand("INSERT INTO [tblCenyHlavniElektrina] ("+string.Join(",",columns)+") VALUES ("+string.Join(",",columns.Select(_=>"?"))+")",conn,tx);
    Add(insert,values,id,date); insert.ExecuteNonQuery();
   }
  }
  tx.Commit();
 }
 [SupportedOSPlatform("windows")]
 private static void Add(OleDbCommand cmd,object?[] values,int id,DateOnly date) {
  for(var i=0;i<values.Length;i++) cmd.Parameters.Add("v"+i,ElectricityPriceStore.Fields[i].Column.StartsWith("b")?OleDbType.Boolean:OleDbType.Double).Value=values[i]??DBNull.Value;
  cmd.Parameters.Add("id",OleDbType.Integer).Value=id; cmd.Parameters.Add("date",OleDbType.Date).Value=date.ToDateTime(TimeOnly.MinValue);
 }
}
