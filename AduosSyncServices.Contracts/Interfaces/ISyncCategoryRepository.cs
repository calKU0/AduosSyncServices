namespace AduosSyncServices.Contracts.Interfaces
{
    /// <summary>
    /// Kategorie Gąski skonfigurowane dla kanałów sprzedaży (Allegro, ShopGold) i powiązania produktów
    /// z kategoriami, w których zostały pobrane. Produkty pobiera tylko serwis Allegro - dlatego musi znać
    /// sumę kategorii wszystkich kanałów, a każdy kanał sprzedaje tylko produkty ze swoich kategorii.
    /// </summary>
    public interface ISyncCategoryRepository
    {
        /// <summary>Zapisuje kategorie kanału (usuwa te, których nie ma już w jego konfiguracji).</summary>
        Task ReplaceChannelCategoriesAsync(string channel, IEnumerable<int> categoryIds, CancellationToken ct);

        /// <summary>Kategorie wszystkich kanałów: kanał -> id kategorii Gąski.</summary>
        Task<List<(string Channel, int CategoryId)>> GetAllChannelCategoriesAsync(CancellationToken ct);

        /// <summary>Zastępuje powiązania produktów z kategorią pobraną w całości w tym przebiegu.</summary>
        Task ReplaceProductLinksForCategoryAsync(int categoryId, IEnumerable<int> integrationIds, CancellationToken ct);

        /// <summary>Usuwa powiązania z kategoriami spoza podanej listy. Zwraca liczbę usuniętych powiązań.</summary>
        Task<int> DeleteProductLinksNotInAsync(IEnumerable<int> categoryIds, CancellationToken ct);

        /// <summary>Zapisuje drzewo kategorii Gąski (lista wyboru kategorii w ServicesManagerze). Zwraca liczbę węzłów.</summary>
        Task<int> UpsertGaskaCategoryTreeAsync(IEnumerable<(int Id, int ParentId, string Name)> categories, CancellationToken ct);
    }
}
