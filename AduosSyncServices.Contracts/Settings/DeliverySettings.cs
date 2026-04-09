namespace AduosSyncServices.Contracts.Settings
{
    public class DeliverySettings
    {
        public DeliveryRuleType RuleType { get; set; } = DeliveryRuleType.Standard;
        public decimal? NetPriceThreshold { get; set; }
        public int Width { get; set; }
        public int Length { get; set; }
        public int Height { get; set; }
        public decimal Weight { get; set; }
        public string DeliveryName { get; set; }
    }
}
