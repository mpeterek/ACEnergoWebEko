using System.Data.OleDb;
using System.Globalization;
using System.Runtime.Versioning;
namespace ACEnergo.Web.Services;
public sealed record FictitiousComponent(int Input,string Name,string PowerUnit,string EnergyUnit,string A,string B,string P,bool Defined,string? Recalculation=null);
public sealed record FictitiousComposition(string Name,FictitiousComponent[] Components);
public interface IElectricityFictitiousComposition { FictitiousComposition Load(int id); }
public sealed class ElectricityFictitiousComposition(DatabaseCatalog catalog,IConfiguration configuration):IElectricityFictitiousComposition {
 public static string Address(int input) {
  if(input<0) return "Nedefinováno";
  return $"{input/10000:D4}/{input%10000:D4}";
 }
 public FictitiousComposition Load(int id){
  if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException();
  return Read(id);
 }
 [SupportedOSPlatform("windows")]
 private FictitiousComposition Read(int id){
  var db=new ElectricityConfigurationDatabase(catalog,configuration);using var connection=db.Open(false);
  string name;
  using(var parent=new OleDbCommand("SELECT [strNazev] FROM [tblKonfigFiktivni] WHERE [numCislo]=? AND [numIDEnergie] IN (SELECT [numKlic] FROM [tblKonfigEnergie] WHERE [strNazevEnergie]='Elektřina')",connection)){
   parent.Parameters.Add("id",OleDbType.Integer).Value=id;
   var value=parent.ExecuteScalar();if(value is null)throw new KeyNotFoundException();name=Convert.ToString(value)??"";
  }
  using var command=new OleDbCommand("SELECT p.[numImpuls], i.[strNazev], i.[strRozmerP], i.[strRozmerV], p.[numKonstA], p.[numKonstB], p.[numKonstP], i.[numCislo] FROM [tblKonfigFiktivniPolozky] AS p LEFT JOIN [tblKonfigImpuls] AS i ON p.[numImpuls]=i.[numCislo] WHERE p.[numCislo]=? ORDER BY p.[numImpuls]",connection);
  command.Parameters.Add("id",OleDbType.Integer).Value=id;using var reader=command.ExecuteReader();var rows=new List<FictitiousComponent>();
  while(reader is not null && reader.Read()){
   if(reader.IsDBNull(0)) throw new InvalidDataException("Vazba má prázdné ID vstupu.");
   string Text(int column)=>reader.IsDBNull(column)?"Neuvedeno":Convert.ToString(reader[column],CultureInfo.GetCultureInfo("cs-CZ"))??"";
   var defined=!reader.IsDBNull(7);
   rows.Add(new(Convert.ToInt32(reader[0]),defined?Text(1):"Nedefinováno",defined?Text(2):"—",defined?Text(3):"—",Text(4),Text(5),Text(6),defined));
  }
  return new(name,rows.ToArray());
 }
}
