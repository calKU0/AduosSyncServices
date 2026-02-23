using System.Text.Json.Serialization;

namespace AduosSyncServices.Contracts.DTOs.GaskaApi
{
    public class GaskaGetDeliveryAddressesResponse
    {
        [JsonPropertyName("deliveryAdressDetails")]
        public List<DeliveryAdressDetails>? AdressDetails { get; set; }

        [JsonPropertyName("result")]
        public int Result { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;
    }

    public class DeliveryAdressDetails
    {
        [JsonPropertyName("deliveryAddressId")]
        public int Id { get; set; }
        [JsonPropertyName("deliveryAddressName1")]
        public string Name1 { get; set; }
        [JsonPropertyName("deliveryAddressName2")]
        public string Name2 { get; set; }
        [JsonPropertyName("deliveryAddressStreet")]
        public string Street { get; set; }
        [JsonPropertyName("deliveryAddressCity")]
        public string City { get; set; }
        [JsonPropertyName("deliveryAddressPostCode")]
        public string PostCode { get; set; }
        [JsonPropertyName("deliveryAddressCountry")]
        public string Country { get; set; }
        [JsonPropertyName("deliveryAddressPhone")]
        public string Phone { get; set; }
        [JsonPropertyName("deliveryAddressEmail")]
        public string Email { get; set; }
        [JsonPropertyName("deliveryAddressDefault")]
        public bool Default { get; set; }
    }
}
