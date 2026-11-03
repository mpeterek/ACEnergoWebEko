using System.Globalization;
namespace ACEnergo.Web.Services;
public sealed class DatabaseCatalog(IConfiguration configuration)
{
    public static string MonthlyName(int year, int month)
    {
        if (year < 2000 || year > 9999 || month < 1 || month > 12) throw new ArgumentOutOfRangeException(nameof(year));
        return $"ACEnergoWebEko{(year - 2000).ToString("D2", CultureInfo.InvariantCulture)}{month:D2}.mdb";
    }
    public string DirectoryPath => Path.GetFullPath(configuration["Access:Directory"] ?? throw new InvalidOperationException("Chybí Access:Directory."));
    public string Resolve(string file) => Path.Combine(DirectoryPath, file switch {
        "ACEnergoWebEko.mdb" or "ACWebEkoLogin.mdb" => file,
        _ when Path.GetFileName(file) == file && System.Text.RegularExpressions.Regex.IsMatch(file, @"^ACEnergoWebEko[0-9]{4,6}\.mdb$") => file,
        _ => throw new ArgumentException("Neplatný název databáze.") });
    public string[] AvailableMonths() => System.IO.Directory.Exists(DirectoryPath)
        ? System.IO.Directory.GetFiles(DirectoryPath, "ACEnergoWebEko*.mdb").Select(Path.GetFileName).OfType<string>()
            .Where(f => System.Text.RegularExpressions.Regex.IsMatch(f, @"^ACEnergoWebEko[0-9]{2,4}(0[1-9]|1[0-2])\.mdb$")).OrderDescending().ToArray() : [];
}
