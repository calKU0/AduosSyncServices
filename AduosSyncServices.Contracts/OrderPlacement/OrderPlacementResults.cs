namespace AduosSyncServices.Contracts.OrderPlacement
{
    public class HeadquartersOrderPlacementResult
    {
        public bool IsSuccessful { get; set; }
        public string? ErrorMessage { get; set; }
        public List<int> GaskaOrderIds { get; set; } = new();
        public List<string> GaskaOrderNumbers { get; set; } = new();
        public List<int> AllegroOrderIds { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
    }

    public class CustomerOrderPlacementResult
    {
        public int AllegroOrderId { get; set; }
        public bool IsSuccessful { get; set; }
        public string? ErrorMessage { get; set; }
        public string? Warning { get; set; }
        public int? GaskaOrderId { get; set; }
        public string? GaskaOrderNumber { get; set; }
    }

    public class CustomerOrdersPlacementResult
    {
        public List<CustomerOrderPlacementResult> Results { get; set; } = new();
    }
}
