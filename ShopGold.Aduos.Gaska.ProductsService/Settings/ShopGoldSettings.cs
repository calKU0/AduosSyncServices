namespace ShopGold.Aduos.Gaska.ProductsService.Settings
{
    /// <summary>Panel administracyjny sklepu - import CSV uruchamiany jest tak samo jak z przeglądarki.</summary>
    public class ShopGoldSettings
    {
        /// <summary>Adres sklepu, np. https://sklep.example.com</summary>
        public string BaseUrl { get; set; } = string.Empty;

        /// <summary>Ścieżka panelu zarządzania.</summary>
        public string AdminPath { get; set; } = "/zarzadzanie";

        /// <summary>Strona z formularzem logowania (względem panelu). Pusta = strona główna panelu.</summary>
        public string LoginPage { get; set; } = string.Empty;

        public string AdminLogin { get; set; } = string.Empty;
        public string AdminPassword { get; set; } = string.Empty;

        /// <summary>
        /// Nazwy pól formularza logowania. Puste = wykrywane automatycznie (pierwsze pole tekstowe/e-mail
        /// i pole hasła w formularzu z hasłem).
        /// </summary>
        public string LoginFieldName { get; set; } = string.Empty;
        public string PasswordFieldName { get; set; } = string.Empty;

        /// <summary>Przedrostek numeru katalogowego w sklepie - pozwala odróżnić produkty Gąski od dodanych ręcznie.</summary>
        public string CatalogNumberPrefix { get; set; } = string.Empty;

        public string TaxRate { get; set; } = "23";

        /// <summary>Dostępność (nazwa ze słownika sklepu) gdy produkt jest na stanie. "AUTOMATYCZNY" = dostępność automatyczna.</summary>
        public string AvailabilityInStock { get; set; } = "Dostępny";

        /// <summary>Dostępność gdy brak stanu.</summary>
        public string AvailabilityOutOfStock { get; set; } = "Niedostępny";

        /// <summary>Opcja "import zdjęć" z formularza importu panelu (tak/nie).</summary>
        public string ImportImages { get; set; } = "tak";

        /// <summary>Usuwać pliki CSV z serwera po udanym imporcie.</summary>
        public bool DeleteCsvAfterImport { get; set; } = true;

        /// <summary>Limit czasu jednego zapytania importu (paczka 40 linii).</summary>
        public int RequestTimeoutSeconds { get; set; } = 300;

        /// <summary>
        /// Link do PDF "Informacje o bezpieczeństwie produktu" - przycisk na końcu opisu, jak w opisach sklepu.
        /// Pusty = bez przycisku.
        /// </summary>
        public string SafetyInfoPdfUrl { get; set; } = string.Empty;

        public string UserAgent { get; set; } = "AduosSyncServices/1.0 (+ShopGold import)";
    }

    public class FtpSettings
    {
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; } = 21;
        public string User { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;

        /// <summary>FTPS (jawne TLS). Większość hostingów to obsługuje.</summary>
        public bool UseFtps { get; set; } = true;

        /// <summary>
        /// Akceptuj dowolny certyfikat FTPS (bez sprawdzania). Tylko awaryjnie, np. dla serwera z certyfikatem
        /// self-signed - wyłączona weryfikacja pozwala podszyć się pod serwer i przechwycić hasło.
        /// </summary>
        public bool AcceptAnyCertificate { get; set; }

        /// <summary>Katalog /import sklepu na FTP - z niego panel importuje pliki CSV.</summary>
        public string ImportDirectory { get; set; } = "/import";

        /// <summary>Katalog na FTP, do którego trafiają zdjęcia produktów Gąski (wewnątrz głównego katalogu zdjęć sklepu).</summary>
        public string ImagesDirectory { get; set; } = "/images/gaska";

        /// <summary>
        /// Ścieżka katalogu zdjęć w pliku CSV - względem głównego katalogu zdjęć sklepu (bez jego nazwy),
        /// np. "gaska" gdy zdjęcia sklepu są w /images, a ImagesDirectory to /images/gaska.
        /// </summary>
        public string ImagesCsvPath { get; set; } = "gaska";

        /// <summary>
        /// Po ilu dniach usuwać z katalogu importu pliki gaska_*.csv, które zostały po nieudanych importach
        /// (udane są usuwane od razu). 0 = nie usuwać.
        /// </summary>
        public int OldCsvRetentionDays { get; set; } = 7;

        /// <summary>
        /// Ile połączeń FTP naraz wgrywa zdjęcia. Więcej = szybciej, ale hostingi limitują liczbę połączeń
        /// jednego użytkownika (zwykle 8-10) - zostawiamy zapas na import CSV i ręczne logowanie.
        /// </summary>
        public int ImageUploadConnections { get; set; } = 4;
    }

    /// <summary>Dane GPSR (producent i importer) - odpowiedniki "producenta odpowiedzialnego" i "osoby odpowiedzialnej" z Allegro.</summary>
    public class GpsrSettings
    {
        /// <summary>Odpowiedzialny producent (Allegro: AllegroResponsibleProducer).</summary>
        public GpsrContact Producer { get; set; } = new();

        /// <summary>Osoba odpowiedzialna / importer (Allegro: AllegroResponsiblePerson).</summary>
        public GpsrContact Importer { get; set; } = new();
    }

    public class GpsrContact
    {
        public string Name { get; set; } = string.Empty;
        public string Street { get; set; } = string.Empty;
        public string PostalCode { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;

        /// <summary>Kod kraju ISO 3166, np. PL.</summary>
        public string Country { get; set; } = "PL";
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
    }
}
