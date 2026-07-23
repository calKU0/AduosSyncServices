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

        // Per-order infeasibility (key = local AllegroOrder.Id): orders whose items cannot be fully
        // covered by Gąska's stock, allocated greedily in the order the orders were passed in. Orders
        // absent from this map CAN be placed with the stock that remains after feasible predecessors.
        // Lets callers skip only the short orders instead of rejecting the whole batch.
        public Dictionary<int, List<StockShortage>> ShortagesByOrderId { get; set; } = new();
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
