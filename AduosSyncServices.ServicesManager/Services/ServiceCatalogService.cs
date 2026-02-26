using AduosSyncServices.ServicesManager.Models;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;

namespace AduosSyncServices.ServicesManager.Services
{
    public class ServiceCatalogService
    {
        public List<ServiceItem> LoadServices()
        {
            var keys = ConfigurationManager.AppSettings.AllKeys
                .Where(k => k.StartsWith("Service_"))
                .Select(k => k.Split('_')[1])
                .Distinct();

            var services = new List<ServiceItem>();

            foreach (var key in keys)
            {
                services.Add(new ServiceItem
                {
                    Id = key,
                    Name = ConfigurationManager.AppSettings[$"Service_{key}_Name"] ?? key,
                    Account = ConfigurationManager.AppSettings[$"Service_{key}_Account"] ?? string.Empty,
                    LogoPath = ConfigurationManager.AppSettings[$"Service_{key}_LogoPath"] ?? string.Empty,
                    ServiceName = ConfigurationManager.AppSettings[$"Service_{key}_ServiceName"] ?? string.Empty,
                    LogFolderPath = ConfigurationManager.AppSettings[$"Service_{key}_LogFolder"] ?? string.Empty,
                    ExternalConfigPath = ConfigurationManager.AppSettings[$"Service_{key}_ConfigPath"] ?? string.Empty
                });
            }

            return services;
        }

        public List<string> GetAccounts(IEnumerable<ServiceItem> services)
        {
            return services
                .Select(s => s.Account)
                .Distinct()
                .ToList();
        }
    }
}
