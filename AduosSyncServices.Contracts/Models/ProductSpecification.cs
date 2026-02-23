namespace AduosSyncServices.Contracts.Models
{
    public class ProductSpecification
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public string Name { get; set; }
        public string Value { get; set; }
        public string UnitName { get; set; }
    }
}