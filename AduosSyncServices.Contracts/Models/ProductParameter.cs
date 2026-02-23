namespace AduosSyncServices.Contracts.Models
{
    public class ProductParameter
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int ProductId { get; set; }
        public int CategoryParameterId { get; set; }
        public string Value { get; set; }
        public bool IsForProduct { get; set; }
    }
}