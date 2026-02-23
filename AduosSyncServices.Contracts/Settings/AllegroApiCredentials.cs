namespace AduosSyncServices.Contracts.Settings
{
    public class AllegroApiCredentials
    {
        public string BaseUrl { get; set; } = string.Empty;
        public string AuthBaseUrl { get; set; } = string.Empty;
        public string ClientName { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
    }
}