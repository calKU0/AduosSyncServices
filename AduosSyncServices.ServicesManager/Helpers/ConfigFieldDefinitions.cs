using AduosSyncServices.ServicesManager.Enums;
using AduosSyncServices.ServicesManager.Models;

namespace AduosSyncServices.ServicesManager.Helpers
{
    public static class ConfigFieldDefinitions
    {
        public static readonly List<ConfigField> AllFields = new()
        {
            // Gąska API
            new ConfigField { Key = "GaskaApiCredentials:BaseUrl", Label = "Adres API Gąska", Group = "Gąska API", IsEnabled = true },
            new ConfigField { Key = "GaskaApiCredentials:Acronym", Label = "Akronim", Group = "Gąska API", IsEnabled = true },
            new ConfigField { Key = "GaskaApiCredentials:Person", Label = "Osoba", Group = "Gąska API", IsEnabled = true },
            new ConfigField { Key = "GaskaApiCredentials:Password", Label = "Hasło", Group = "Gąska API", IsEnabled = true },
            new ConfigField { Key = "GaskaApiCredentials:Key", Label = "Klucz API", Group = "Gąska API", IsEnabled = true },
            new ConfigField { Key = "GaskaApiCredentials:ProductsPerPage", Label = "Produkty na stronę", Group = "Gąska API", IsEnabled = true },
            new ConfigField { Key = "GaskaApiCredentials:ProductsInterval", Label = "Interwał pobierania produktów", Group = "Gąska API", IsEnabled = true },
            new ConfigField { Key = "GaskaApiCredentials:ProductPerDay", Label = "Produkty dziennie", Group = "Gąska API", IsEnabled = true },
            new ConfigField { Key = "GaskaApiCredentials:ProductInterval", Label = "Interwał pobierania szczegółów", Group = "Gąska API", IsEnabled = true },

            // Allegro API
            new ConfigField { Key = "AllegroApiCredentials:BaseUrl", Label = "Adres API Allegro", Group = "Allegro API", IsEnabled = true },
            new ConfigField { Key = "AllegroApiCredentials:AuthBaseUrl", Label = "Adres Autoryzacji Allegro", Group = "Allegro API", IsEnabled = true },
            new ConfigField { Key = "AllegroApiCredentials:ClientName", Label = "Nazwa klienta", Group = "Allegro API", IsEnabled = true },
            new ConfigField { Key = "AllegroApiCredentials:ClientId", Label = "Client ID", Group = "Allegro API", IsEnabled = true },
            new ConfigField { Key = "AllegroApiCredentials:ClientSecret", Label = "Client Secret", Group = "Allegro API", IsEnabled = true },
         
            // Price Settings
            new ConfigField { Key = "PriceSettings:AllegroMarginUnder5PLN", Label = "Prowizja allegro poniżej 5 PLN", Group = "Narzuty", FieldType = ConfigFieldType.Decimal },

            // AppSettings
            new ConfigField { Key = "AppSettings:CategoriesId", Label = "ID synchronizowanych kategorii", Group = "Ustawienia serwisu" },
            new ConfigField { Key = "AppSettings:MinProductStock", Label = "Minimalny stan produktu", Group = "Ustawienia serwisu", FieldType = ConfigFieldType.Int },
            new ConfigField { Key = "AppSettings:MinProductPriceNet", Label = "Minimalna cena netto produktu (w Gąsce)", Group = "Ustawienia serwisu", FieldType = ConfigFieldType.Decimal },
            new ConfigField { Key = "AppSettings:LogsExpirationDays", Label = "Ilość dni zachowania logów", Group = "Ustawienia serwisu", FieldType = ConfigFieldType.Int },
            new ConfigField { Key = "AppSettings:FetchIntervalMinutes", Label = "Co ile wywoływać synchronizację (min)", Group = "Ustawienia serwisu", FieldType = ConfigFieldType.Int },

            // Allegro Settings
            new ConfigField { Key = "AllegroSettings:AllegroHandlingTime", Label = "Czas realizacji", Group = "Ustawienia Allegro" },
            new ConfigField { Key = "AllegroSettings:AllegroHandlingTimeCustomProducts", Label = "Czas realizacji produktów niestandardowych", Group = "Ustawienia Allegro" },
            new ConfigField { Key = "AllegroSettings:AllegroSafetyMeasures", Label = "Tekst bezpieczeństwa", Group = "Ustawienia Allegro" },
            new ConfigField { Key = "AllegroSettings:AllegroWarranty", Label = "Nazwa polityki gwarancji", Group = "Ustawienia Allegro" },
            new ConfigField { Key = "AllegroSettings:AllegroReturnPolicy", Label = "Nazwa polityki zwrotów", Group = "Ustawienia Allegro" },
            new ConfigField { Key = "AllegroSettings:AllegroImpliedWarranty", Label = "Nazwa polityki reklamacji", Group = "Ustawienia Allegro" },
            new ConfigField { Key = "AllegroSettings:AllegroResponsiblePerson", Label = "Odpowiedzialna osoba", Group = "Ustawienia Allegro" },
            new ConfigField { Key = "AllegroSettings:AllegroResponsibleProducer", Label = "Odpowiedzialny producent", Group = "Ustawienia Allegro" },
        };
    }
}