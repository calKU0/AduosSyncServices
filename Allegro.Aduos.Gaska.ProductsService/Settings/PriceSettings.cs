namespace Allegro.Aduos.Gaska.ProductsService.Settings
{
    public class PriceSettings
    {
        public List<MarginRange> MarginRanges { get; set; } = new();
    }

    public class MarginRange
    {
        public decimal Min { get; set; }
        public decimal Max { get; set; }
        public decimal Margin { get; set; }
    }
}
