using AduosSyncServices.Contracts.Data.Enums;
using AduosSyncServices.Contracts.Settings;

namespace Allegro.Aduos.Gaska.ProductsService.Settings
{
    public class AppSettings
    {
        public string CategoriesId { get; set; } = string.Empty;
        public int MinProductStock { get; set; }
        public decimal MinProductPriceNet { get; set; }
        public int LogsExpirationDays { get; set; }
        public int FetchIntervalMinutes { get; set; }
        public DeliveryMatchMode DeliveryMatchMode { get; set; } = DeliveryMatchMode.Weight;
        public List<DeliverySettings> Deliveries { get; set; } = new List<DeliverySettings>();
    }
}