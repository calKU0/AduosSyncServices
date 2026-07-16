namespace Allegro.Aduos.Gaska.OrdersService.Settings
{
    public class AppSettings
    {
        public int LogsExpirationDays { get; set; }
        public int FetchIntervalMinutes { get; set; }
        public string AllegroDeliveryNames { get; set; } = string.Empty;
    }
}
