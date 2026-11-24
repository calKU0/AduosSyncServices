using ShopGold.Aduos.Gaska.ProductsService.Models;
using System.Text;

namespace ShopGold.Aduos.Gaska.ProductsService.Helpers
{
    /// <summary>
    /// Plik CSV w formacie importu ShopGold: UTF-8, separator średnik, wartości w cudzysłowach,
    /// pierwszy wiersz to nagłówki, jeden produkt w jednej linii.
    /// </summary>
    public static class ShopGoldCsvWriter
    {
        public const char Separator = ';';
        public const string CatalogNumberColumn = "Nr_katalogowy";
        private const string MainImageColumn = "Zdjecie_glowne";
        private const string FirstExtraImageColumn = "Zdjecie_dodatkowe_1";

        /// <summary>
        /// Zapisuje wiersze z podanymi polami. Kolumny pola pojawiają się w pliku, gdy dowolny wiersz je ma;
        /// wiersz bez danego pola dostaje puste wartości (przy aktualizacji pliki grupujemy tak, żeby to nie zachodziło).
        /// </summary>
        /// <param name="isUpdate">
        /// Plik aktualizacji: produkt z samym zdjęciem głównym dostaje je też jako pierwsze zdjęcie dodatkowe.
        /// Sklep podmienia zdjęcia dodatkowe tylko wtedy, gdy plik jakieś zawiera - bez tego po zmniejszeniu
        /// liczby zdjęć do jednego w sklepie zostałyby stare zdjęcia dodatkowe (z usuniętymi już plikami).
        /// </param>
        public static string Write(IReadOnlyCollection<ShopGoldRow> rows, IReadOnlyCollection<string> fields, bool isUpdate = false)
        {
            var orderedFields = ShopGoldFields.Order.Where(fields.Contains).ToList();

            var columns = new List<string> { CatalogNumberColumn };
            foreach (var field in orderedFields)
            {
                var fieldColumns = rows
                    .Select(r => r.Fields.TryGetValue(field, out var f) ? f.Columns.Select(c => c.Key) : Enumerable.Empty<string>())
                    .FirstOrDefault(c => c.Any());

                if (fieldColumns != null)
                    columns.AddRange(fieldColumns.Where(c => !columns.Contains(c)));
            }

            var csv = new StringBuilder();
            csv.Append(string.Join(Separator, columns)).Append("\r\n");

            foreach (var row in rows)
            {
                var values = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [CatalogNumberColumn] = row.CatalogNumber
                };

                foreach (var field in orderedFields)
                {
                    if (!row.Fields.TryGetValue(field, out var f))
                        continue;

                    foreach (var column in f.Columns)
                        values[column.Key] = column.Value;
                }

                if (isUpdate
                    && orderedFields.Contains(ShopGoldFields.Images)
                    && values.TryGetValue(MainImageColumn, out var mainImage) && mainImage.Length > 0
                    && (!values.TryGetValue(FirstExtraImageColumn, out var firstExtra) || firstExtra.Length == 0))
                {
                    values[FirstExtraImageColumn] = mainImage;
                }

                csv.Append(string.Join(Separator, columns.Select(c => Quote(values.TryGetValue(c, out var v) ? v : string.Empty))))
                   .Append("\r\n");
            }

            return csv.ToString();
        }

        public static byte[] ToBytes(string csv) => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(csv);

        /// <summary>Cudzysłów w wartości podwajamy; znaki nowej linii zamieniamy na spacje (panel liczy linie pliku).</summary>
        public static string Quote(string? value)
        {
            var clean = (value ?? string.Empty).Replace("\r", " ").Replace("\n", " ");
            return "\"" + clean.Replace("\"", "\"\"") + "\"";
        }
    }
}
