using System.Security.Cryptography;
using System.Text;

namespace ShopGold.Aduos.Gaska.ProductsService.Models
{
    /// <summary>
    /// Pola logiczne produktu w ShopGold. Każde pole to jedna lub kilka kolumn CSV - przy aktualizacji
    /// wysyłamy tylko pola, które się zmieniły (z kompletem ich kolumn).
    /// </summary>
    public static class ShopGoldFields
    {
        public const string Name = "Name";
        public const string Description = "Description";
        public const string Price = "Price";
        public const string PurchasePrice = "PurchasePrice";
        public const string ReferenceNumbers = "ReferenceNumbers";
        public const string ExternalId = "ExternalId";
        public const string AdditionalFields = "AdditionalFields";
        public const string Stock = "Stock";
        public const string Weight = "Weight";
        public const string Ean = "Ean";
        public const string Producer = "Producer";
        public const string Status = "Status";
        public const string Unit = "Unit";
        public const string Availability = "Availability";
        public const string ShippingTime = "ShippingTime";
        public const string Shipping = "Shipping";
        public const string Category = "Category";
        public const string CategoryIds = "CategoryIds";
        public const string Images = "Images";
        public const string Gpsr = "Gpsr";

        /// <summary>Kolejność pól w pliku (i kolumn w ramach pola).</summary>
        public static readonly IReadOnlyList<string> Order = new[]
        {
            Category, CategoryIds, Name, Status, Price, PurchasePrice, Stock, Weight, Ean, ReferenceNumbers, ExternalId,
            Producer, Unit, Availability, ShippingTime, Shipping, Images, Description, AdditionalFields, Gpsr
        };

        /// <summary>
        /// Pola wysyłane tylko przy dodawaniu: ścieżka kategorii po nazwach zakłada w sklepie brakujące
        /// kategorie i przypisuje do nich nowy produkt. Potem przypisania ustala już <see cref="CategoryIds"/>.
        /// </summary>
        public static readonly IReadOnlySet<string> AddOnly = new HashSet<string> { Category };

        /// <summary>
        /// Pola wysyłane tylko przy aktualizacji: Kategorie_id (kilka kategorii naraz) według instrukcji
        /// działa przy aktualizacji i jest alternatywą dla nazw kategorii - nie mieszamy ich w jednym pliku.
        /// </summary>
        public static readonly IReadOnlySet<string> UpdateOnly = new HashSet<string> { CategoryIds };

        /// <summary>Pola pliku dodającego nowe produkty.</summary>
        public static readonly IReadOnlyList<string> AddFields = Order.Where(f => !UpdateOnly.Contains(f)).ToList();
    }

    public sealed class ShopGoldField
    {
        public ShopGoldField(string name, IReadOnlyList<KeyValuePair<string, string>> columns, string? fingerprint = null)
        {
            Name = name;
            Columns = columns;
            Fingerprint = fingerprint ?? Hash(string.Join("\u001f", columns.Select(c => c.Key + "=" + c.Value)));
        }

        public string Name { get; }

        /// <summary>Kolumny CSV pola w kolejności (nazwa kolumny -> wartość).</summary>
        public IReadOnlyList<KeyValuePair<string, string>> Columns { get; }

        /// <summary>Odcisk wartości - porównywany z zapisanym stanem, żeby wykryć zmianę pola.</summary>
        public string Fingerprint { get; }

        public static string Hash(string value)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
            return Convert.ToHexString(bytes, 0, 12);
        }
    }

    /// <summary>Jeden produkt do wysłania do ShopGold.</summary>
    public sealed class ShopGoldRow
    {
        public required string CatalogNumber { get; init; }
        public int? ProductId { get; init; }

        public Dictionary<string, ShopGoldField> Fields { get; } = new(StringComparer.Ordinal);

        /// <summary>Zdjęcia do wgrania na FTP: ścieżka lokalna -> nazwa pliku na serwerze.</summary>
        public List<(string LocalPath, string RemoteFileName)> Images { get; init; } = new();

        public string? ImagesHash { get; init; }

        public bool IsActive { get; init; }

        /// <summary>
        /// Ścieżki kategorii Gąski, których jeszcze nie ma w sklepie. Dopóki są, wiersz nie ma pola
        /// <see cref="ShopGoldFields.CategoryIds"/> (częściowa lista odebrałaby produktowi kategorie).
        /// </summary>
        public List<IReadOnlyList<string>> MissingCategoryPaths { get; init; } = new();

        public void Add(ShopGoldField field) => Fields[field.Name] = field;

        public Dictionary<string, string> Fingerprints() =>
            Fields.Values.ToDictionary(f => f.Name, f => f.Fingerprint, StringComparer.Ordinal);
    }

    /// <summary>Zapisany stan produktu w sklepie (co i kiedy zostało wysłane).</summary>
    public sealed class ShopGoldProductState
    {
        public string Code { get; set; } = string.Empty;
        public int? ProductId { get; set; }
        public string FieldsJson { get; set; } = "{}";
        public string? ImagesHash { get; set; }
        public bool IsActive { get; set; }
    }
}
