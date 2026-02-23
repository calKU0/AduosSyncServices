namespace AduosSyncServices.Contracts.Models
{
    public class AllegroOfferDescription
    {
        public int Id { get; set; }
        public string OfferId { get; set; }
        public virtual AllegroOffer Offer { get; set; }
        public string Type { get; set; }
        public string Content { get; set; }
        public int SectionId { get; set; }
    }
}