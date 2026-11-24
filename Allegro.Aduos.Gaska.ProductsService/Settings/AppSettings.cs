using AduosSyncServices.Contracts.Data.Enums;
using AduosSyncServices.Contracts.Settings;

namespace Allegro.Aduos.Gaska.ProductsService.Settings
{
    public class AppSettings
    {
        /// <summary>
        /// Kategorie Gąski (id) pobierane i wystawiane na Allegro. Pusta lista = cały katalog.
        /// Wypełniana przez ConfigurationLists.ReadIntList (rozumie też stary zapis "id, id").
        /// </summary>
        public List<int> CategoriesId { get; set; } = new();
        public int MinProductStock { get; set; }
        public decimal MinProductPriceNet { get; set; }
        public int LogsExpirationDays { get; set; }
        public int FetchIntervalMinutes { get; set; }
        public int ProductsChangedDaysBack { get; set; } = 1;
        public DeliveryMatchMode DeliveryMatchMode { get; set; } = DeliveryMatchMode.Weight;
        public List<DeliverySettings> Deliveries { get; set; } = new List<DeliverySettings>();
    }
}