namespace AduosSyncServices.Contracts.Models
{
    public class AllegroOfferAttribute
    {
        public int Id { get; set; }
        public string OfferId { get; set; }
        public virtual AllegroOffer Offer { get; set; }
        public string AttributeId { get; set; }
        public string Type { get; set; }
        public string ValuesJson { get; set; }
        public string ValuesIdsJson { get; set; }
    }
}