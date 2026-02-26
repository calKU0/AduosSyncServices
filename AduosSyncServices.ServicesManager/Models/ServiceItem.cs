namespace AduosSyncServices.ServicesManager.Models
{
    public class ServiceItem
    {
        public string Id { get; set; } = "";
        public string Account { get; set; } = "";
        public string Name { get; set; } = "";
        public string LogoPath { get; set; } = "";
        public string ServiceName { get; set; } = "";
        public string LogFolderPath { get; set; } = "";
        public string ExternalConfigPath { get; set; } = "";
    }
}