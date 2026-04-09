using AduosSyncServices.Contracts.Data.Enums;
using AduosSyncServices.Contracts.Settings;

namespace AduosSyncServices.Infrastructure.Settings
{
    public class RepositorySettings
    {
        public IntegrationCompany Company { get; set; }
        public AllegroAccount Account { get; set; }
        public List<DeliverySettings> Deliveries { get; set; } = new();
    }
}
