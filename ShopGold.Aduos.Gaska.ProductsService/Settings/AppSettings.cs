using AduosSyncServices.Contracts.Data.Enums;
using AduosSyncServices.Contracts.Settings;

namespace ShopGold.Aduos.Gaska.ProductsService.Settings
{
    public class AppSettings
    {
        /// <summary>
        /// Kategorie Gąski (id, lista JSON) wysyłane do ShopGold. Kategorie, których nie ma w konfiguracji
        /// serwisu Allegro, serwis Allegro i tak pobierze do bazy - ale nie wystawi ich na Allegro.
        /// </summary>
        public List<int> CategoriesId { get; set; } = new();

        /// <summary>
        /// Kategorie Gąski (id, lista JSON), których nie zakładamy w sklepie - razem z podkategoriami. Produkty
        /// z tych gałęzi trafiają do sklepu tylko wtedy, gdy są też w innej kategorii (bez pomijanych kategorii).
        /// </summary>
        public List<int> ExcludedCategoryIds { get; set; } = new();

        /// <summary>Poniżej tego stanu (w sztukach Gąski) produkt jest w sklepie wyłączony.</summary>
        public int MinProductStock { get; set; }

        /// <summary>Poniżej tej ceny netto Gąski produkt jest w sklepie wyłączony.</summary>
        public decimal MinProductPriceNet { get; set; }

        public int LogsExpirationDays { get; set; } = 14;
        public int FetchIntervalMinutes { get; set; } = 60;

        /// <summary>Ile nowych produktów dodać w jednym cyklu (import dodający jest najwolniejszy - zdjęcia i kategorie).</summary>
        public int MaxNewProductsPerCycle { get; set; } = 2000;

        /// <summary>Nowe produkty bez zdjęć nie są dodawane do sklepu (jak w Allegro).</summary>
        public bool SkipProductsWithoutImages { get; set; } = true;

        public DeliveryMatchMode DeliveryMatchMode { get; set; } = DeliveryMatchMode.Price;

        /// <summary>
        /// Reguły dostaw - te same co w serwisie Allegro. DeliveryName to tutaj lista ID modułów wysyłek ShopGold
        /// (menu Moduły / Wysyłki, kolumna ID) rozdzielonych przecinkiem, np. "1,4". HandlingTime trafia do
        /// pola "Termin wysyłki" (nazwa ze słownika Konfiguracja / Słowniki / Terminy wysyłek).
        /// </summary>
        public List<DeliverySettings> Deliveries { get; set; } = new();

        public List<int> GetCategoryIds() =>
            (CategoriesId ?? new List<int>())
                .Where(id => id != 0)
                .Distinct()
                .ToList();
    }
}
