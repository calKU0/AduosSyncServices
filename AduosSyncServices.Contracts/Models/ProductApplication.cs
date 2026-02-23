namespace AduosSyncServices.Contracts.Models
{
    public class ProductApplication
    {
        public int Id { get; set; }

        public int ApplicationId { get; set; }
        public int ParentID { get; set; }
        public string Name { get; set; }

        public int ProductId { get; set; }
    }
}