namespace AduosSyncServices.Contracts.Settings
{
    public class GaskaApiCredentials
    {
        public string BaseUrl { get; set; } = string.Empty;
        public string Acronym { get; set; } = string.Empty;
        public string Person { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        public int ProductsPerPage { get; set; }
        public int ProductsInterval { get; set; }
        public int ProductPerDay { get; set; }
        public int ProductInterval { get; set; }
    }
}