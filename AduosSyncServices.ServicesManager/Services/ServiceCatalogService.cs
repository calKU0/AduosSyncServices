using AduosSyncServices.ServicesManager.Models;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
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
                    LogoPath = ResolveLogoPath(ConfigurationManager.AppSettings[$"Service_{key}_LogoPath"]),
                    ServiceName = ConfigurationManager.AppSettings[$"Service_{key}_ServiceName"] ?? string.Empty,
                    LogFolderPath = ConfigurationManager.AppSettings[$"Service_{key}_LogFolder"] ?? string.Empty,
                    ExternalConfigPath = ConfigurationManager.AppSettings[$"Service_{key}_ConfigPath"] ?? string.Empty,
                    HasOrdersPanel = bool.TryParse(ConfigurationManager.AppSettings[$"Service_{key}_HasOrdersPanel"], out var hasOrdersPanel) && hasOrdersPanel
                });
            }

            return services;
        }

        /// <summary>
        /// Ścieżka względna (np. "Resources\gaska-products-sync-logo.png") jest liczona od katalogu aplikacji -
        /// dzięki temu App.config nie zawiera ścieżek do konkretnego komputera ani folderu bin.
        /// </summary>
        private static string ResolveLogoPath(string? configured)
        {
            var path = (configured ?? string.Empty).Trim();

            if (path.Length == 0 || Path.IsPathRooted(path))
                return path;

            return Path.Combine(AppContext.BaseDirectory, path);
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
