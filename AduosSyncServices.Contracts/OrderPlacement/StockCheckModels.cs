namespace AduosSyncServices.Contracts.OrderPlacement
{
    public enum StockCheckItemStatus
    {
        Checking,
        Available,
        Insufficient
    }

    public class StockShortage
    {
        public string ProductCode { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public float RequestedQty { get; set; }
        public float AvailableQty { get; set; }
    }

    public class StockCheckResult
    {
        public bool IsSuccessful { get; set; }
        public List<StockShortage> Shortages { get; set; } = new();
    }

    public class StockCheckProgressItem
    {
        public string ProductCode { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public StockCheckItemStatus Status { get; set; }
        public float RequestedQty { get; set; }
        public float? AvailableQty { get; set; }
    }
}
