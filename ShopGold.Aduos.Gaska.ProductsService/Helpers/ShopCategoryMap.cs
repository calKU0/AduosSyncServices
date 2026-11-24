using ShopGold.Aduos.Gaska.ProductsService.Services;
using System.Text.RegularExpressions;

namespace ShopGold.Aduos.Gaska.ProductsService.Helpers
{
    /// <summary>
    /// Kategorie sklepu: ścieżka nazw -> id. Sklep przy imporcie dopasowuje nazwy kategorii BEZ rozróżniania
    /// wielkości liter (sprawdzone na sklepie, wbrew instrukcji): "Claas" trafia do istniejącej "CLAAS".
    /// Porównujemy tak samo - inaczej taka kategoria byłaby wiecznie "brakująca".
    /// </summary>
    public sealed class ShopCategoryMap
    {
        private readonly Dictionary<string, int> _ids = new(StringComparer.OrdinalIgnoreCase);

        public ShopCategoryMap(IEnumerable<ShopCategory> categories)
        {
            // Przy zdublowanej ścieżce (dwie kategorie o tej samej nazwie pod jednym rodzicem) bierzemy
            // starszą - do niej sklep dopasuje też nazwę z importu.
            foreach (var category in categories.OrderBy(c => c.Id))
                _ids.TryAdd(Key(category.Path), category.Id);
        }

        public int Count => _ids.Count;

        public bool TryGetId(IReadOnlyList<string> path, out int id) => _ids.TryGetValue(Key(path), out id);

        public static string Key(IEnumerable<string> path) =>
            string.Join(" > ", path.Select(s => Regex.Replace(s ?? string.Empty, @"\s+", " ").Trim()));
    }
}
