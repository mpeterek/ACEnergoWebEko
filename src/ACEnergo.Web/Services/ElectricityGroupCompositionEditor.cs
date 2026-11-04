using System.Data;
using System.Data.OleDb;
using System.Globalization;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text.Json;
using ACEnergo.Web.Domain;
namespace ACEnergo.Web.Services;

public sealed record CompositionCandidate(int Id,string Name,string PowerUnit,string EnergyUnit,string Type);
public sealed record CompositionEditRow(int Id,string? Coefficient,bool? Recalculate);
public sealed record CompositionEditSnapshot(string Name,string Version,CompositionEditRow[] Rows,int MainMeter=0);
public sealed record CompositionSearch(CompositionCandidate[] Rows,bool HasMore);
public interface IElectricityGroupCompositionEditor {
 CompositionEditSnapshot Load(string kind,int id);
 CompositionSearch Search(string kind,int id,string search);
 void Add(string kind,int id,int child,string version);
 void Update(string kind,int id,int child,string? coefficient,bool recalculate,string version);
 void Remove(string kind,int id,int child,string version);
}
public sealed class ElectricityGroupCompositionEditor(DatabaseCatalog catalog,IConfiguration configuration):IElectricityGroupCompositionEditor {
 public const int FictitiousOffset=50000000;
 private readonly ElectricityConfigurationDatabase db=new(catalog,configuration);
 public static (string Table,string Child) Definition(string kind)=>kind switch {
  "fictitious"=>("tblKonfigFiktivniPolozky","numImpuls"),
  "centres"=>("tblKonfigStrediskoPolozky","numImpuls"),
  "plants"=>("tblKonfigZavodPolozky","numStredisko"),
  _=>throw new ArgumentException("Neznámý oddíl elektřiny.")
 };
 public static double Coefficient(string? value){
  if(!double.TryParse(value?.Replace(',','.'),NumberStyles.AllowLeadingSign|NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out var result)||!double.IsFinite(result))
   throw new ArgumentException("Koeficient P musí být platné číslo; lze použít desetinnou čárku nebo tečku.");
  return result;
 }
 public static string ComponentLabel(string kind,int id)=>kind=="plants"?$"Středisko {id}":kind=="centres"&&id>=FictitiousOffset?$"Fiktivní měření {id-FictitiousOffset}":ElectricityFictitiousComposition.Address(id);
 public CompositionEditSnapshot Load(string kind,int id){
  Definition(kind);if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException();
  using var connection=db.Open(false);return Read(connection,kind,id);
 }
 public CompositionSearch Search(string kind,int id,string search){
  Definition(kind);if(string.IsNullOrWhiteSpace(search))return new([],false);
  if(search.Length>80)throw new ArgumentException("Hledaný text může obsahovat nejvýše 80 znaků.");
  if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException();
  using var connection=db.Open(false);Read(connection,kind,id);
  return SearchWindows(connection,kind,search.Trim());
 }
 public void Add(string kind,int id,int child,string version)=>Change(kind,id,child,version,"add",null,false);
 public void Update(string kind,int id,int child,string? coefficient,bool recalculate,string version)=>Change(kind,id,child,version,"update",kind=="plants"?null:Coefficient(coefficient),recalculate);
 public void Remove(string kind,int id,int child,string version)=>Change(kind,id,child,version,"remove",null,false);
 private void Change(string kind,int id,int child,string version,string action,double? coefficient,bool recalculate){
  Definition(kind);ElectricityHolidays.ValidateVersion(version);
  if(child<=0)throw new ArgumentException("Vyberte platnou položku.");
  if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException();
  lock(ElectricityConfigurationDatabase.WriteGate)Write(kind,id,child,version,action,coefficient,recalculate);
 }
 [SupportedOSPlatform("windows")]
 private static CompositionEditSnapshot Read(OleDbConnection connection,string kind,int id,OleDbTransaction? tx=null){
  var parent=ElectricityGroupCatalog.Definition(kind).Table;
  using var parentCommand=new OleDbCommand($"SELECT * FROM [{parent}] WHERE [numCislo]=? AND [numIDEnergie] IN (SELECT [numKlic] FROM [tblKonfigEnergie] WHERE [strNazevEnergie]=?)",connection,tx);
  parentCommand.Parameters.Add("id",OleDbType.Integer).Value=id;parentCommand.Parameters.Add("energy",OleDbType.VarWChar,255).Value="Elektřina";
  object[] parentRaw;string name;int mainMeter=0;
  using(var reader=parentCommand.ExecuteReader()){
   if(reader is null||!reader.Read())throw new KeyNotFoundException();
   name=Convert.ToString(reader["strNazev"])??"";
   if(kind=="plants"&&reader["numIDHlavni"] is not DBNull)mainMeter=Convert.ToInt32(reader["numIDHlavni"]);
   parentRaw=Enumerable.Range(0,reader.FieldCount).Select(reader.GetValue).ToArray();
   if(reader.Read())throw new InvalidDataException("Duplicitní identifikátor rodiče.");
  }
  var (table,child)=Definition(kind);
  using var command=new OleDbCommand($"SELECT [{child}],[numKonstA],[numKonstB],[numKonstP]{(kind=="plants"?",[bPrepocet]":"")} FROM [{table}] WHERE [numCislo]=? ORDER BY [{child}]",connection,tx);
  command.Parameters.Add("id",OleDbType.Integer).Value=id;var raw=new List<object[]>();var rows=new List<CompositionEditRow>();var seen=new HashSet<int>();
  using(var reader=command.ExecuteReader())while(reader is not null&&reader.Read()){
   if(reader.IsDBNull(0))throw new InvalidDataException("Prázdný identifikátor položky.");
   var childId=Convert.ToInt32(reader[0]);if(!seen.Add(childId))throw new InvalidDataException("Duplicitní položka složení; úprava byla zastavena.");
   raw.Add(Enumerable.Range(0,reader.FieldCount).Select(reader.GetValue).ToArray());
   rows.Add(new(childId,reader.IsDBNull(3)?null:Convert.ToString(reader[3],CultureInfo.GetCultureInfo("cs-CZ")),kind=="plants"&&!reader.IsDBNull(4)?Convert.ToBoolean(reader[4]):null));
  }
  var version=Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new object[]{kind,id,parentRaw,raw})));
  return new(name,version,rows.ToArray(),mainMeter);
 }
 [SupportedOSPlatform("windows")]
 private static CompositionSearch SearchWindows(OleDbConnection connection,string kind,string text){
  var rows=new List<CompositionCandidate>();var numeric=int.TryParse(text,out var number)?number:-1;
  void Query(string table,string restriction,string type){
   using var command=new OleDbCommand($"SELECT TOP 101 [numCislo],[strNazev],[strRozmerP],[strRozmerV] FROM [{table}] WHERE ({restriction}) AND ([numCislo]=? OR InStr(1,[strNazev],?)>0) ORDER BY [numCislo]",connection);
   command.Parameters.Add("energy",OleDbType.VarWChar,255).Value="Elektřina";
   command.Parameters.Add("number",OleDbType.Integer).Value=numeric;command.Parameters.Add("text",OleDbType.VarWChar,80).Value=text;
   using var reader=command.ExecuteReader();while(reader is not null&&reader.Read()){
    string Text(int i)=>reader.IsDBNull(i)?"Neuvedeno":Convert.ToString(reader[i])??"";
    rows.Add(new(Convert.ToInt32(reader[0]),Text(1),Text(2),Text(3),type));
   }
  }
  if(kind=="plants")Query("tblKonfigStredisko","[numIDEnergie] IN (SELECT [numKlic] FROM [tblKonfigEnergie] WHERE [strNazevEnergie]=?)","Středisko");
  else {
   Query("tblKonfigImpuls","[boolArchiv]<>0 AND [numCislo]>0 AND [numCislo]<50000000 AND [strTyp] IN (SELECT [strTypEnergie] FROM [tblKonfigEnergie] WHERE [strNazevEnergie]=?)","Reálný vstup");
   if(kind=="centres")Query("tblKonfigFiktivni","[numIDEnergie] IN (SELECT [numKlic] FROM [tblKonfigEnergie] WHERE [strNazevEnergie]=?)","Fiktivní měření");
  }
  return new(rows.Take(100).ToArray(),rows.Count>100);
 }
 [SupportedOSPlatform("windows")]
 private static void ValidateChild(OleDbConnection connection,OleDbTransaction tx,string kind,int child){
  var table=kind=="plants"?"tblKonfigStredisko":kind=="centres"&&child>=FictitiousOffset?"tblKonfigFiktivni":"tblKonfigImpuls";
  var filter=table=="tblKonfigImpuls"?"[boolArchiv]<>0 AND [numCislo]<50000000 AND [strTyp] IN (SELECT [strTypEnergie] FROM [tblKonfigEnergie] WHERE [strNazevEnergie]=?)":"[numIDEnergie] IN (SELECT [numKlic] FROM [tblKonfigEnergie] WHERE [strNazevEnergie]=?)";
  using var command=new OleDbCommand($"SELECT COUNT(*) FROM [{table}] WHERE [numCislo]=? AND ({filter})",connection,tx);
  command.Parameters.Add("id",OleDbType.Integer).Value=child;command.Parameters.Add("energy",OleDbType.VarWChar,255).Value="Elektřina";
  if(Convert.ToInt32(command.ExecuteScalar())!=1)throw new ArgumentException("Vybraná položka není dostupná pro tento oddíl elektřiny.");
 }
 [SupportedOSPlatform("windows")]
 private void Write(string kind,int id,int child,string version,string action,double? coefficient,bool recalculate){
  using var held=db.AcquireWriteLock();using var connection=db.Open(true);using var tx=connection.BeginTransaction(IsolationLevel.ReadCommitted);
  var current=Read(connection,kind,id,tx);if(current.Version!=version)throw new GroupConfigurationConflictException();
  var exists=current.Rows.Any(row=>row.Id==child);
  if(action=="add"&&exists)throw new ArgumentException("Položka je již přiřazena.");
  if(action!="add"&&!exists)throw new ArgumentException("Položka již není součástí složení. Obnovte stránku.");
  if(action=="add")ValidateChild(connection,tx,kind,child);
  if(kind=="plants"&&action=="update"&&recalculate&&current.MainMeter<=0)throw new ArgumentException("Přepočet ztrát vyžaduje přiřazené hlavní měření závodu.");
  var (table,column)=Definition(kind);
  var sql=action=="remove"?$"DELETE FROM [{table}] WHERE [numCislo]=? AND [{column}]=?":action=="add"?$"INSERT INTO [{table}] ([numCislo],[{column}],{(kind=="plants"?"[bPrepocet]":"[numKonstP]")}) VALUES (?,?,?)":$"UPDATE [{table}] SET {(kind=="plants"?"[bPrepocet]":"[numKonstP]") }=? WHERE [numCislo]=? AND [{column}]=?";
  using var command=new OleDbCommand(sql,connection,tx);
  void Value()=>command.Parameters.Add("value",kind=="plants"?OleDbType.Boolean:OleDbType.Double).Value=kind=="plants"?(object)recalculate:coefficient??1d;
  if(action=="update")Value();
  command.Parameters.Add("id",OleDbType.Integer).Value=id;command.Parameters.Add("child",OleDbType.Integer).Value=child;
  if(action=="add")Value();
  if(command.ExecuteNonQuery()!=1)throw new InvalidDataException("Změněn neočekávaný počet položek.");
  if(kind=="plants"&&action=="remove"){
   // Původní UpDateZavodyP.asp při odebrání ruší také příznak na středisku.
   using var clear=new OleDbCommand("UPDATE [tblKonfigStredisko] SET [bPrepocet]=False WHERE [numCislo]=? AND [numIDEnergie] IN (SELECT [numKlic] FROM [tblKonfigEnergie] WHERE [strNazevEnergie]=?)",connection,tx);
   clear.Parameters.Add("child",OleDbType.Integer).Value=child;clear.Parameters.Add("energy",OleDbType.VarWChar,255).Value="Elektřina";clear.ExecuteNonQuery();
  }
  tx.Commit();
 }
}
