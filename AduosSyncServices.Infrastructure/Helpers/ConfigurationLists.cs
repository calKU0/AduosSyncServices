using Microsoft.Extensions.Configuration;

namespace AduosSyncServices.Infrastructure.Helpers
{
    /// <summary>
    /// Odczyt listy liczb z konfiguracji. Lista kategorii była kiedyś zapisywana jako tekst po przecinku
    /// ("101, 202"), teraz jako lista JSON ([101, 202]). Standardowe wiązanie konfiguracji zamienia
    /// stary zapis po cichu na PUSTĄ listę - dla serwisu Allegro to "pobierz i wystaw cały katalog Gąski".
    /// Dlatego listy kategorii czytamy tylko przez tę metodę, która rozumie oba formaty.
    /// </summary>
    public static class ConfigurationLists
    {
        public static List<int> ReadIntList(IConfigurationSection section)
        {
            IEnumerable<string?> values = section.Value != null
                ? section.Value.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                : section.GetChildren().Select(child => child.Value);

            return values
                .Select(v => int.TryParse(v?.Trim(), out var id) ? id : 0)
                .Where(id => id != 0)
                .Distinct()
                .ToList();
        }
    }
}
