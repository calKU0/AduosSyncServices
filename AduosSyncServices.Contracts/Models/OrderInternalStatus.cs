namespace AduosSyncServices.Contracts.Models
{
    // A user-defined internal workflow status (name + colour) that operators assign to orders.
    // Global (not scoped to account/company).
    public class OrderInternalStatus
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Color { get; set; } = "#3B82F6";
        public DateTime CreatedDate { get; set; }
    }
}
