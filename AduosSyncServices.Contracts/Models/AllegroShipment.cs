namespace AduosSyncServices.Contracts.Models
{
    /// <summary>Przesyłka zamówienia zgłoszona do Allegro (numer listu przewozowego u danego przewoźnika).</summary>
    public class AllegroShipment
    {
        public string CarrierId { get; set; } = string.Empty;
        public string Waybill { get; set; } = string.Empty;
    }
}
