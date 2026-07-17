namespace Allegro.Aduos.Gaska.OrdersService.Settings
{
    public class AppSettings
    {
        public int LogsExpirationDays { get; set; }
        public int FetchIntervalMinutes { get; set; }
        public List<string> AllegroDeliveryNames { get; set; } = new();
    }
}
