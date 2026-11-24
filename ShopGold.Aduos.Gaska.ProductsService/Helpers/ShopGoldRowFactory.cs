using AduosSyncServices.Contracts.Models;
using AduosSyncServices.Contracts.Settings;
using AduosSyncServices.Infrastructure.Helpers;
using ShopGold.Aduos.Gaska.ProductsService.Models;
using ShopGold.Aduos.Gaska.ProductsService.Settings;
using System.Globalization;
using System.Text.RegularExpressions;

namespace ShopGold.Aduos.Gaska.ProductsService.Helpers
{
    /// <summary>Zamienia produkt Gąski na pola CSV ShopGold.</summary>
    public static class ShopGoldRowFactory
    {
        /// <summary>ShopGold przyjmuje zdjęcie główne i do 10 dodatkowych.</summary>
        public const int MaxImages = 11;

        /// <summary>Import CSV ShopGold obsługuje do 10 poziomów kategorii.</summary>
        public const int MaxCategoryDepth = 10;

        public const string CategorySeparator = " > ";

        public static string CatalogNumber(string code, ShopGoldSettings settings) =>
            (settings.CatalogNumberPrefix ?? string.Empty) + code.Trim();

        public static ShopGoldRow Build(
            Product product,
            IReadOnlyList<string> localImages,
            string? imagesHash,
            AppSettings appSettings,
            ShopGoldSettings shopSettings,
            FtpSettings ftpSettings,
            GpsrSettings gpsr,
            PriceSettings priceSettings,
            ShopCategoryMap? shopCategories = null,
            IReadOnlyCollection<IReadOnlyList<string>>? excludedCategoryPaths = null)
        {
            var quantity = SalesRules.GetPackageQuantity(product);
            var stock = SalesRules.CalculateAvailableStock(product.InStock, quantity);
            var price = SalesRules.CalculatePrice(product.PriceNet, quantity, priceSettings);
            var delivery = SalesRules.GetDeliveryRule(product, appSettings.Deliveries, appSettings.DeliveryMatchMode, requireName: false);
            var isActive = IsActive(product, stock, appSettings);
            var catalogNumber = CatalogNumber(product.Code, shopSettings);

            var categoryPaths = SelectCategoryPaths(product.Categories, excludedCategoryPaths ?? Array.Empty<IReadOnlyList<string>>());

            // Id kategorii sklepu dla wszystkich ścieżek Gąski. Bez mapy (panel niedostępny) nic nie wiemy
            // o brakujących kategoriach - pole Kategorie_id po prostu nie jest wysyłane.
            var categoryIds = new List<int>();
            var missingPaths = new List<IReadOnlyList<string>>();
            if (shopCategories != null)
            {
                foreach (var path in categoryPaths)
                {
                    // Produkt ma być widoczny na każdym poziomie ścieżki, nie tylko w najgłębszej podkategorii.
                    var levelIds = new List<int>();
                    for (var depth = 1; depth <= path.Count; depth++)
                    {
                        if (shopCategories.TryGetId(path.Take(depth).ToList(), out var id))
                            levelIds.Add(id);
                    }

                    // Od najgłębszej: sklep traktuje pierwsze id jako kategorię główną (ścieżka nawigacji),
                    // więc na początku listy musi być liść ścieżki głównej.
                    if (levelIds.Count == path.Count)
                        categoryIds.AddRange(Enumerable.Reverse(levelIds));
                    else
                        missingPaths.Add(path);
                }
            }

            var images = BuildImageNames(product.Code, localImages, imagesHash);
            var row = new ShopGoldRow
            {
                CatalogNumber = catalogNumber,
                ProductId = product.Id,
                Images = images,
                ImagesHash = imagesHash,
                IsActive = isActive,
                MissingCategoryPaths = missingPaths
            };

            row.Add(Field(ShopGoldFields.Name, ("Nazwa_produktu", Clean(OfferNameFormatter.Format(product.Name)))));
            row.Add(Field(ShopGoldFields.Status, ("Status", isActive ? "tak" : "nie")));
            row.Add(Field(ShopGoldFields.Price,
                ("Cena_brutto", price.ToString("0.00", CultureInfo.InvariantCulture)),
                ("Podatek_Vat", shopSettings.TaxRate)));
            // Cena zakupu (informacyjna, niewidoczna dla klientów): netto Gąski za sprzedawaną jednostkę - całe opakowanie.
            row.Add(Field(ShopGoldFields.PurchasePrice, ("Cena_zakupu", (product.PriceNet * quantity).ToString("0.00", CultureInfo.InvariantCulture))));
            row.Add(Field(ShopGoldFields.Stock, ("Ilosc_produktow", stock.ToString(CultureInfo.InvariantCulture))));
            row.Add(Field(ShopGoldFields.Weight, ("Waga", ((decimal)product.Weight * quantity).ToString("0.0000", CultureInfo.InvariantCulture))));
            row.Add(Field(ShopGoldFields.Ean, ("Kod_ean", Clean(product.Ean))));
            row.Add(ReferenceNumbersField(product.Substitutes));
            // Id produktu w naszej bazie - powiązanie produktu w sklepie z bazą synchronizacji.
            row.Add(Field(ShopGoldFields.ExternalId, ("Id_produktu_magazyn", product.Id.ToString(CultureInfo.InvariantCulture))));
            row.Add(Field(ShopGoldFields.Producer, ("Producent", Clean(product.SupplierName))));
            row.Add(Field(ShopGoldFields.Unit, ("Jednostka_miary", MapUnit(product.Unit, quantity))));
            row.Add(Field(ShopGoldFields.Availability, ("Dostepnosc", stock > 0 ? shopSettings.AvailabilityInStock : shopSettings.AvailabilityOutOfStock)));
            row.Add(Field(ShopGoldFields.ShippingTime, ("Termin_wysylki", delivery != null ? HandlingTimeName(delivery.HandlingTime) : string.Empty)));
            row.Add(Field(ShopGoldFields.Shipping,
                ("Wysylki_id", NormalizeShippingIds(delivery?.DeliveryName)),
                ("Gabaryt", IsBulky(product) ? "tak" : "nie")));
            row.Add(CategoryField(categoryPaths.FirstOrDefault() ?? Array.Empty<string>()));

            if (shopCategories != null && missingPaths.Count == 0 && categoryIds.Count > 0)
                row.Add(Field(ShopGoldFields.CategoryIds, ("Kategorie_id", string.Join(",", categoryIds.Distinct()))));
            row.Add(ImagesField(images, ftpSettings, imagesHash));
            row.Add(Field(ShopGoldFields.Description, ("Opis", Clean(ShopGoldDescriptionBuilder.Build(product, quantity, gpsr, shopSettings.SafetyInfoPdfUrl)))));
            row.Add(AdditionalFieldsField(product));
            row.Add(GpsrField(gpsr, shopSettings.SafetyInfoPdfUrl));

            return row;
        }

        /// <summary>Wiersz wyłączający produkt (zniknął z Gąski lub z kategorii ShopGold).</summary>
        public static ShopGoldRow BuildDeactivation(string catalogNumber, int? productId, string? imagesHash)
        {
            var row = new ShopGoldRow
            {
                CatalogNumber = catalogNumber,
                ProductId = productId,
                ImagesHash = imagesHash,
                IsActive = false
            };

            row.Add(Field(ShopGoldFields.Status, ("Status", "nie")));
            return row;
        }

        /// <summary>
        /// Produkt jest aktywny, gdy da się go kupić: stan i cena Gąski powyżej progów oraz co najmniej jedno
        /// pełne opakowanie (przy sprzedaży w opakowaniach 5 szt. przy opakowaniu 10 szt. to stan 0).
        /// </summary>
        public static bool IsActive(Product product, int availableStock, AppSettings appSettings) =>
            availableStock > 0
            && product.InStock >= appSettings.MinProductStock
            && product.PriceNet >= appSettings.MinProductPriceNet;

        /// <summary>
        /// Kategorie Gąski produktu spoza pomijanych gałęzi (pomijana ścieżka wyklucza też swoje podkategorie). Ścieżka będąca początkiem innej (kategoria
        /// nadrzędna) jest pomijana - produkt przypisujemy do najgłębszych kategorii. Pierwsza jest kategoria
        /// główna (najgłębsza, przy remisie pierwsza alfabetycznie): plik CSV przypisuje po nazwach tylko
        /// do jednej ścieżki, pozostałe trafiają do sklepu przez Kategorie_id.
        /// </summary>
        public static List<IReadOnlyList<string>> SelectCategoryPaths(IEnumerable<ProductCategory>? categories, IReadOnlyCollection<IReadOnlyList<string>> excludedPaths)
        {
            var paths = (categories ?? Enumerable.Empty<ProductCategory>())
                .Select(c => SplitPath(c.Name).Take(MaxCategoryDepth).ToList())
                .Where(p => p.Count > 0)
                .Where(p => !excludedPaths.Any(ex => ex.Count > 0 && ex.Count <= p.Count
                    && p.Take(ex.Count).SequenceEqual(ex, StringComparer.CurrentCultureIgnoreCase)))
                .DistinctBy(p => string.Join(CategorySeparator, p), StringComparer.Ordinal)
                .ToList();

            return paths
                .Where(p => !paths.Any(other => other.Count > p.Count && other.Take(p.Count).SequenceEqual(p, StringComparer.Ordinal)))
                .OrderByDescending(p => p.Count)
                .ThenBy(p => string.Join(CategorySeparator, p), StringComparer.Ordinal)
                .Cast<IReadOnlyList<string>>()
                .ToList();
        }

        /// <summary>
        /// Czy produkt ma choć jedną kategorię spoza pomijanych gałęzi. Produkt tylko z pomijanych kategorii
        /// (np. wyłącznie w Wyprzedaży) nie trafia do sklepu - a jeśli już tam był, zostaje wyłączony.
        /// </summary>
        public static bool HasAllowedCategory(Product product, IReadOnlyCollection<IReadOnlyList<string>> excludedPaths) =>
            SelectCategoryPaths(product.Categories, excludedPaths).Count > 0;

        /// <summary>Ścieżka kategorii Gąski ("A > B") w tej samej postaci co ścieżki produktów.</summary>
        public static IReadOnlyList<string> ParseCategoryPath(string? path) => SplitPath(path);

        private static List<string> SplitPath(string? path) =>
            (path ?? string.Empty)
                .Split('>', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => FormatCategoryName(Clean(s)))
                .Where(s => s.Length > 0)
                .ToList();

        private static readonly CultureInfo Polish = CultureInfo.GetCultureInfo("pl-PL");

        /// <summary>
        /// Nazwa kategorii w sklepie: wielka tylko pierwsza litera ("MASSEY FERGUSON" -> "Massey ferguson",
        /// "Silnik i osprzęt silnika" bez zmian). Oznaczenia z cyframi (MF 1150, C-360) zostają bez zmian.
        /// </summary>
        public static string FormatCategoryName(string name)
        {
            var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(w => w.Any(char.IsDigit) ? w : w.ToLower(Polish))
                .ToList();

            if (words.Count == 0)
                return string.Empty;

            var first = words[0];
            var letter = first.ToList().FindIndex(char.IsLetter);
            if (letter >= 0)
                words[0] = first[..letter] + char.ToUpper(first[letter], Polish) + first[(letter + 1)..];

            return string.Join(' ', words);
        }

        public static ShopGoldField CategoryField(IReadOnlyList<string> path)
        {
            var columns = new List<KeyValuePair<string, string>>();
            for (var i = 0; i < MaxCategoryDepth; i++)
                columns.Add(new($"Kategoria_{i + 1}_nazwa", i < path.Count ? path[i] : string.Empty));

            return new ShopGoldField(ShopGoldFields.Category, columns);
        }

        /// <summary>
        /// Ścieżki zdjęć na serwerze względem katalogu zdjęć Gąski: folder produktu (z kodu) i plik.
        /// Nazwa pliku zawiera skrót zawartości - sklep trzyma miniatury pod nazwą pliku, więc nowe zdjęcie
        /// pod starą nazwą pokazywałoby się ze starą miniaturą.
        /// </summary>
        public static List<(string LocalPath, string RemoteFileName)> BuildImageNames(string code, IReadOnlyList<string> localImages, string? imagesHash)
        {
            var folder = ImageFolder(code);
            var suffix = string.IsNullOrEmpty(imagesHash) ? string.Empty : "_" + imagesHash[..Math.Min(8, imagesHash.Length)].ToLowerInvariant();

            return localImages
                .Take(MaxImages)
                .Select((path, index) =>
                {
                    var extension = Path.GetExtension(path).ToLowerInvariant();
                    if (string.IsNullOrEmpty(extension))
                        extension = ".jpg";
                    return (path, $"{folder}/{index + 1}{suffix}{extension}");
                })
                .ToList();
        }

        /// <summary>
        /// Folder zdjęć produktu - kod Gąski bez znaków niedozwolonych w ścieżce/URL. Gdy trzeba było coś
        /// zamienić, dopisujemy skrót oryginalnego kodu: "A/B" i "A_B" nie mogą dzielić folderu, bo serwis
        /// usuwa z folderu pliki spoza zestawu zdjęć produktu.
        /// </summary>
        public static string ImageFolder(string code)
        {
            var trimmed = (code ?? string.Empty).Trim();
            var safe = Regex.Replace(trimmed, @"[^A-Za-z0-9\-_]", "_");

            return safe == trimmed ? safe : $"{safe}_{ShopGoldField.Hash(trimmed)[..8].ToLowerInvariant()}";
        }

        public static string JoinPath(string? directory, string relative)
        {
            var dir = (directory ?? string.Empty).Replace('\\', '/').Trim().TrimEnd('/');
            return dir.Length == 0 ? relative : dir + "/" + relative;
        }

        private static ShopGoldField ImagesField(IReadOnlyList<(string LocalPath, string RemoteFileName)> images, FtpSettings ftp, string? imagesHash)
        {
            string CsvPath(int i) => i < images.Count ? JoinPath(ftp.ImagesCsvPath, images[i].RemoteFileName) : string.Empty;

            var columns = new List<KeyValuePair<string, string>> { new("Zdjecie_glowne", CsvPath(0)) };

            for (var i = 1; i < MaxImages; i++)
                columns.Add(new($"Zdjecie_dodatkowe_{i}", CsvPath(i)));

            // Odcisk z zawartości zdjęć - ten sam skrót decyduje o ponownym wgraniu plików na serwer.
            return new ShopGoldField(ShopGoldFields.Images, columns, imagesHash ?? string.Empty);
        }

        /// <summary>Pól dodatkowych w pliku - stała liczba kolumn, żeby wszystkie wiersze miały ten sam nagłówek.</summary>
        public const int MaxAdditionalFields = 25;

        /// <summary>Wartości jednego pola dodatkowego (sklep pokazuje je po przecinku).</summary>
        private const int MaxAdditionalFieldValues = 3;

        /// <summary>Bezpieczna długość jednej wartości pola dodatkowego.</summary>
        private const int MaxAdditionalFieldValueLength = 250;

        public const string SubstitutesFieldName = "Numery zamienników";

        /// <summary>
        /// Pola dodatkowe (parametry) produktu: specyfikacja z Gąski oraz numery zamienników. Przy aktualizacji
        /// sklep zastępuje cały zestaw pól produktu tym z pliku. Puste pary kolumn sklep pomija.
        /// </summary>
        public static ShopGoldField AdditionalFieldsField(Product product)
        {
            var fields = new List<(string Name, List<string> Values)>();

            foreach (var spec in product.Specifications ?? new List<ProductSpecification>())
            {
                var name = Clean(spec.Name);
                var value = Clean(spec.Value);
                if (name.Length == 0 || value.Length == 0 || fields.Any(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase)))
                    continue;

                // Jednostkę dopisujemy tylko do liczb - "M20x1,5 RHT mm" nie ma sensu.
                var isNumber = decimal.TryParse(value.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out _);
                var unit = isNumber && !string.IsNullOrWhiteSpace(spec.UnitName) ? " " + Clean(spec.UnitName) : string.Empty;
                fields.Add((name, new List<string> { Truncate(value + unit) }));
            }

            var substitutes = (product.Substitutes ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(Clean)
                .Where(n => n.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (substitutes.Count > 0)
                fields.Add((SubstitutesFieldName, ChunkJoined(substitutes)));

            // Numery zamienników zawsze się mieszczą - przy limicie pól tniemy specyfikację.
            if (fields.Count > MaxAdditionalFields)
                fields = fields.Take(MaxAdditionalFields - 1).Concat(fields.Where(f => f.Name == SubstitutesFieldName)).ToList();

            var columns = new List<KeyValuePair<string, string>>();
            for (var i = 0; i < MaxAdditionalFields; i++)
            {
                var field = i < fields.Count ? fields[i] : (Name: string.Empty, Values: new List<string>());
                columns.Add(new($"Dodatkowe_pole_{i + 1}_nazwa", field.Name));
                columns.Add(new($"Dodatkowe_pole_{i + 1}_wartosc", field.Values.ElementAtOrDefault(0) ?? string.Empty));
                columns.Add(new($"Dodatkowe_pole_{i + 1}_wartosc_2", field.Values.ElementAtOrDefault(1) ?? string.Empty));
                columns.Add(new($"Dodatkowe_pole_{i + 1}_wartosc_3", field.Values.ElementAtOrDefault(2) ?? string.Empty));
            }

            return new ShopGoldField(ShopGoldFields.AdditionalFields, columns);
        }

        /// <summary>Lista numerów połączona przecinkami w maksymalnie 3 wartości po 250 znaków (reszta jest w opisie).</summary>
        private static List<string> ChunkJoined(IEnumerable<string> items)
        {
            var values = new List<string>();
            var current = string.Empty;

            foreach (var item in items)
            {
                var candidate = current.Length == 0 ? item : current + ", " + item;
                if (candidate.Length <= MaxAdditionalFieldValueLength)
                {
                    current = candidate;
                    continue;
                }

                values.Add(current);
                if (values.Count == MaxAdditionalFieldValues)
                    return values;

                current = Truncate(item);
            }

            if (current.Length > 0 && values.Count < MaxAdditionalFieldValues)
                values.Add(current);

            return values;
        }

        private static string Truncate(string value) =>
            value.Length <= MaxAdditionalFieldValueLength ? value : value[..MaxAdditionalFieldValueLength];

        /// <summary>Sklep przyjmuje do 5 numerów referencyjnych - pierwsze numery zamienników z Gąski.</summary>
        public const int MaxReferenceNumbers = 5;

        public static ShopGoldField ReferenceNumbersField(string? substitutes)
        {
            var numbers = (substitutes ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(Clean)
                .Where(n => n.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(MaxReferenceNumbers)
                .ToList();

            var columns = Enumerable.Range(1, MaxReferenceNumbers)
                .Select(i => new KeyValuePair<string, string>($"Nr_referencyjny_{i}", i <= numbers.Count ? numbers[i - 1] : string.Empty))
                .ToList();

            return new ShopGoldField(ShopGoldFields.ReferenceNumbers, columns);
        }

        private static ShopGoldField GpsrField(GpsrSettings gpsr, string? safetyInfoUrl) => new(ShopGoldFields.Gpsr, new List<KeyValuePair<string, string>>
        {
            new("Informacja_o_bezpieczenstwie", Clean(safetyInfoUrl)),
            new("GPSR_producent_nazwa", Clean(gpsr.Producer.Name)),
            new("GPSR_producent_ulica", Clean(gpsr.Producer.Street)),
            new("GPSR_producent_kod_pocztowy", Clean(gpsr.Producer.PostalCode)),
            new("GPSR_producent_miasto", Clean(gpsr.Producer.City)),
            new("GPSR_producent_kraj", Clean(gpsr.Producer.Country)),
            new("GPSR_producent_email", Clean(gpsr.Producer.Email)),
            new("GPSR_producent_telefon", Clean(gpsr.Producer.Phone)),
            new("GPSR_importer_nazwa", Clean(gpsr.Importer.Name)),
            new("GPSR_importer_ulica", Clean(gpsr.Importer.Street)),
            new("GPSR_importer_kod_pocztowy", Clean(gpsr.Importer.PostalCode)),
            new("GPSR_importer_miasto", Clean(gpsr.Importer.City)),
            new("GPSR_importer_kraj", Clean(gpsr.Importer.Country)),
            new("GPSR_importer_email", Clean(gpsr.Importer.Email)),
            new("GPSR_importer_telefon", Clean(gpsr.Importer.Phone)),
        });

        /// <summary>Gabaryt: przesyłka gabarytowa (DeliveryType 1) i paletowa (DeliveryType 2) z Gąski.</summary>
        public static bool IsBulky(Product product) => product.DeliveryType is 1 or 2;

        /// <summary>
        /// Słownik "Terminy wysyłek" sklepu (Słowniki > Terminy wysyłek). Import CSV dopasowuje termin
        /// po nazwie, więc nazwa musi być identyczna ze słownikiem.
        /// </summary>
        public static readonly IReadOnlyList<(int Days, string Name)> ShopShippingTimes = new[]
        {
            (1, "24 godziny"),
            (2, "48 godzin"),
            (3, "3 dni"),
            (4, "4 dni"),
            (5, "5 dni"),
            (10, "10 dni"),
            (14, "14 dni")
        };

        /// <summary>
        /// Termin wysyłki z reguły dostawy przełożony na słownik sklepu. Gdy dokładnego terminu nie ma
        /// (np. 7 dni), bierzemy najbliższy dłuższy - lepiej obiecać za długi termin niż za krótki.
        /// </summary>
        public static string HandlingTimeName(DeliveryHandlingTime handlingTime)
        {
            var days = HandlingDays(handlingTime);

            return ShopShippingTimes.FirstOrDefault(t => t.Days >= days).Name
                ?? ShopShippingTimes[^1].Name;
        }

        /// <summary>Liczba dni z kodu ISO 8601 enuma (PT0S, PT24H, P2D...), zaokrąglona w górę, minimum 1.</summary>
        private static int HandlingDays(DeliveryHandlingTime handlingTime)
        {
            try
            {
                var span = System.Xml.XmlConvert.ToTimeSpan(handlingTime.ToString());
                return Math.Max(1, (int)Math.Ceiling(span.TotalDays));
            }
            catch (FormatException)
            {
                return 1;
            }
        }

        /// <summary>"1, 4 ;7" -> "1,4,7". Nazwa reguły dostawy to w ShopGold lista ID modułów wysyłek.</summary>
        public static string NormalizeShippingIds(string? deliveryName) =>
            string.Join(",", (deliveryName ?? string.Empty)
                .Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(id => int.TryParse(id, out _))
                .Distinct());

        /// <summary>Jednostka sklepu. Gdy Gąska sprzedaje w opakowaniu (kilka sztuk), sprzedajemy komplet.</summary>
        public static string MapUnit(string? unit, int quantity)
        {
            if (quantity > 1)
                return "kpl.";

            var normalized = (unit ?? string.Empty).Trim().TrimEnd('.').ToLowerInvariant();

            return normalized switch
            {
                "" or "szt" or "sztuka" => "szt.",
                "kpl" or "komplet" => "kpl.",
                "par" or "para" or "pary" => "para",
                "m" or "mb" => "mb",
                "kg" => "kg",
                "l" or "litr" => "l",
                _ => normalized
            };
        }

        /// <summary>Usuwa znaki nowej linii - panel liczy wiersze pliku jak linie tekstu.</summary>
        public static string Clean(string? value) =>
            Regex.Replace(value ?? string.Empty, @"[\r\n\t]+", " ").Trim();

        private static ShopGoldField Field(string name, params (string Column, string Value)[] columns) =>
            new(name, columns.Select(c => new KeyValuePair<string, string>(c.Column, c.Value)).ToList());
    }
}
