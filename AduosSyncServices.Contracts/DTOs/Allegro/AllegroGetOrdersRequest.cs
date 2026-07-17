namespace AduosSyncServices.Contracts.DTOs.Allegro
{
    public class AllegroGetOrdersRequest
    {
        public int Limit { get; set; }
        public int Offset { get; set; }
        public string DateFrom { get; set; }
    }
}
