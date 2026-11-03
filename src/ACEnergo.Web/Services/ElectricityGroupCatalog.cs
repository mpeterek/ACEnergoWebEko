using System.Data.OleDb;
using System.Runtime.Versioning;
namespace ACEnergo.Web.Services;
public sealed record ElectricityGroup(int Id,string Name,string PowerUnit,string EnergyUnit);
public interface IElectricityGroupCatalog { ElectricityGroup[] Load(string kind); }
public sealed class ElectricityGroupCatalog(DatabaseCatalog catalog,IConfiguration config):IElectricityGroupCatalog {
 public static (string Table,string Title) Definition(string kind)=>kind switch {
  "fictitious"=>("tblKonfigFiktivni","Fiktivní měření"),
  "centres"=>("tblKonfigStredisko","Střediska"),
  "plants"=>("tblKonfigZavod","Závody"),
  _=>throw new ArgumentException("Neznámý oddíl elektřiny.")
 };
 public ElectricityGroup[] Load(string kind){
  var table=Definition(kind).Table;
  if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException();
  return Read(table);
 }
 [SupportedOSPlatform("windows")]
 private ElectricityGroup[] Read(string table){
  var db=new ElectricityConfigurationDatabase(catalog,config);using var connection=db.Open(false);
  // Název tabulky pochází výhradně z pevného whitelistu Definition.
  using var command=new OleDbCommand($"SELECT [numCislo],[strNazev],[strRozmerP],[strRozmerV] FROM [{table}] WHERE [numIDEnergie] IN (SELECT [numKlic] FROM [tblKonfigEnergie] WHERE [strNazevEnergie]=?) ORDER BY [numCislo]",connection);
  command.Parameters.Add("energy",OleDbType.VarWChar,255).Value="Elektřina";
  using var reader=command.ExecuteReader();var rows=new List<ElectricityGroup>();
  while(reader is not null && reader.Read())rows.Add(new(Convert.ToInt32(reader[0]),Convert.ToString(reader[1])??"",reader[2] is DBNull?"Neuvedeno":Convert.ToString(reader[2])??"",reader[3] is DBNull?"Neuvedeno":Convert.ToString(reader[3])??""));
  return rows.ToArray();
 }
}
