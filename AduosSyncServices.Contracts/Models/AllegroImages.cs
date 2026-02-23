using AduosSyncServices.Contracts.Data.Enums;

namespace AduosSyncServices.Contracts.Models
{
    public class AllegroImages
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public string Url { get; set; }
        public AllegroAccount Account { get; set; }
        public bool Connected { get; set; } = false;
    }
}