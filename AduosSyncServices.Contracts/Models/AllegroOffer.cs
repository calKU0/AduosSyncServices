using AduosSyncServices.Contracts.Data.Enums;

namespace AduosSyncServices.Contracts.Models
{
    public class AllegroOffer
    {
        public string Id { get; set; }
        public AllegroAccount Account { get; set; }
        public int? ProductId { get; set; }
        public string ExternalId { get; set; }
        public string Name { get; set; }
        public int CategoryId { get; set; }
        public decimal Price { get; set; }
        public int Stock { get; set; }
        public int WatchersCount { get; set; }
        public int VisitsCount { get; set; }
        public string Status { get; set; }
        public string DeliveryName { get; set; }
        public DateTime StartingAt { get; set; }
        public bool ExistsInErli { get; set; } = false;
        public string Images { get; set; }
        public decimal Weight { get; set; }
        public string HandlingTime { get; set; }
        public string ResponsibleProducer { get; set; }
        public string ResponsiblePerson { get; set; }
        public virtual Product Product { get; set; }
        public virtual List<AllegroOfferDescription> Descriptions { get; set; }
        public virtual List<AllegroOfferAttribute> Attributes { get; set; }
    }
}