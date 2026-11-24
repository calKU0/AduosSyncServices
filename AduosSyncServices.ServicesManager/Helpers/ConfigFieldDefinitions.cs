using AduosSyncServices.ServicesManager.Enums;
using AduosSyncServices.ServicesManager.Models;

namespace AduosSyncServices.ServicesManager.Helpers
{
    public static class ConfigFieldDefinitions
    {
        public static readonly List<ConfigField> AllFields = new()
        {
            // Gąska API

            // Allegro API
         
            // Price Settings

            // AppSettings
            new ConfigField { Key = "AppSettings:CategoriesId", Label = "Synchronizowane kategorie", Group = "Ustawienia serwisu", IsCategoryPicker = true, Description = "Kategorie Gąski (razem z podkategoriami), których produkty trafiają do tego kanału. Produkty pobiera serwis Allegro - także z kategorii wybranych tylko w ShopGold, ale na Allegro ich nie wystawia. Usunięcie kategorii kończy oferty / wyłącza produkty tej kategorii.", EmptyText = "Brak wybranych kategorii - serwis Allegro pobiera i wystawia wszystkie produkty, serwis ShopGold nie wysyła żadnych." },
            new ConfigField { Key = "AppSettings:MinProductStock", Label = "Minimalny stan produktu", Group = "Ustawienia serwisu", FieldType = ConfigFieldType.Int },
            new ConfigField { Key = "AppSettings:MinProductPriceNet", Label = "Minimalna cena netto produktu (w Gąsce)", Group = "Ustawienia serwisu", FieldType = ConfigFieldType.Decimal },
            new ConfigField { Key = "AppSettings:LogsExpirationDays", Label = "Ilość dni zachowania logów", Group = "Ustawienia serwisu", FieldType = ConfigFieldType.Int },
            new ConfigField { Key = "AppSettings:FetchIntervalMinutes", Label = "Co ile wywoływać synchronizację (min)", Group = "Ustawienia serwisu", FieldType = ConfigFieldType.Int },
            new ConfigField { Key = "AppSettings:ProductsChangedDaysBack", Label = "Ile dni wstecz pobierać zmienione produkty", Group = "Ustawienia serwisu", FieldType = ConfigFieldType.Int },
            new ConfigField { Key = "AppSettings:ExcludedCategoryIds", Label = "Kategorie Gąski pomijane w sklepie", Group = "Ustawienia serwisu", IsCategoryPicker = true, Description = "Te kategorie (razem z podkategoriami) nie są zakładane w sklepie. Produkty z nich nadal trafiają do sklepu - w pozostałych kategoriach.", EmptyText = "Brak - produkty dostają w sklepie wszystkie swoje kategorie Gąski." },
            new ConfigField { Key = "AppSettings:MaxNewProductsPerCycle", Label = "Maks. liczba nowych produktów w jednym cyklu", Group = "Ustawienia serwisu", FieldType = ConfigFieldType.Int },

            // Allegro Settings
            new ConfigField { Key = "AllegroSettings:AllegroSafetyMeasures", Label = "Tekst bezpieczeństwa", Group = "Ustawienia Allegro" },
            new ConfigField { Key = "AllegroSettings:AllegroWarranty", Label = "Nazwa polityki gwarancji", Group = "Ustawienia Allegro" },
            new ConfigField { Key = "AllegroSettings:AllegroReturnPolicy", Label = "Nazwa polityki zwrotów", Group = "Ustawienia Allegro" },
            new ConfigField { Key = "AllegroSettings:AllegroImpliedWarranty", Label = "Nazwa polityki reklamacji", Group = "Ustawienia Allegro" },
            new ConfigField { Key = "AllegroSettings:AllegroResponsiblePerson", Label = "Odpowiedzialna osoba", Group = "Ustawienia Allegro" },
            new ConfigField { Key = "AllegroSettings:AllegroResponsibleProducer", Label = "Odpowiedzialny producent", Group = "Ustawienia Allegro" },
        };
    }
}