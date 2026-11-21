using AduosSyncServices.Contracts.DTOs.Allegro;
using AduosSyncServices.Contracts.Models;
using AduosSyncServices.Infrastructure.Helpers;
using System.Globalization;
using System.Text;

namespace Allegro.Aduos.Gaska.ProductsService.Helpers
{
    /// <summary>
    /// Opis oferty Allegro w układzie sklepowym: nagłówki z emotkami (💡 🌟 ⚙️ 🚜 📦) i krótkie akapity
    /// "<b>Etykieta:</b> treść". Allegro w opisie przyjmuje tylko h1, h2, p, b, ul, ol i li - bez styli, tabel
    /// i linków - dlatego nagłówki sekcji to h2. Dane producenta (GPSR) idą w productSet, nie w opisie.
    ///
    /// Sekcje: nazwa (h1), tekst + pierwsze zdjęcie, dalej zdjęcia po dwa. Bez logo/przerywników.
    /// </summary>
    internal static class OfferDescriptionBuilder
    {
        internal const string IntroHeader = "💡 Sprawdzona część do Twojej maszyny!";
        internal const string WhyHeader = "🌟 Dlaczego warto wybrać ten produkt?";
        internal const string SpecificationHeader = "⚙️ Specyfikacja techniczna:";
        internal const string ApplicationHeader = "🚜 Zastosowanie:";
        internal const string PackageHeader = "📦 W zestawie:";
        internal const string ClosingLine = "⭐ Solidna wysokiej jakości renomowana część ⭐";

        /// <param name="imageUrls">Zdjęcia produktu w kolejności galerii.</param>
        public static Description Build(Product product, IReadOnlyList<string> imageUrls, int quantity)
        {
            var images = imageUrls.Where(u => !string.IsNullOrWhiteSpace(u)).Distinct().ToList();
            var imageIndex = 0;

            var description = new Description { Sections = new List<Section>() };

            description.Sections.Add(TextSection($"<h1>{SupplierHtml.Encode(OfferNameFormatter.Format(product.Name))}</h1>"));

            var mainItems = new List<SectionItem>
            {
                new() { Type = "TEXT", Content = BuildMainText(product, quantity) }
            };

            if (imageIndex < images.Count)
                mainItems.Add(new SectionItem { Type = "IMAGE", Url = images[imageIndex++] });

            description.Sections.Add(new Section { SectionItems = mainItems });

            // Pozostałe zdjęcia po dwa w sekcji.
            while (imageIndex < images.Count)
            {
                var sectionImageItems = new List<SectionItem>
                {
                    new() { Type = "IMAGE", Url = images[imageIndex++] }
                };

                if (imageIndex < images.Count)
                    sectionImageItems.Add(new SectionItem { Type = "IMAGE", Url = images[imageIndex++] });

                description.Sections.Add(new Section { SectionItems = sectionImageItems });
            }

            return description;
        }

        internal static string BuildMainText(Product product, int quantity)
        {
            var name = SupplierHtml.Encode(OfferNameFormatter.Format(product.Name));
            var supplier = SupplierHtml.Encode(product.SupplierName);
            var brands = GetRootApplicationNames(product);
            var applications = BuildApplicationLines(product);
            var specifications = BuildSpecificationLines(product);

            var html = new StringBuilder();

            // 💡 Wstęp + opis od Gąski
            html.Append(H2(IntroHeader));
            html.Append("<p><b>").Append(name).Append("</b>");
            if (supplier.Length > 0)
                html.Append(" od producenta <b>").Append(supplier).Append("</b>");
            html.Append(" to wysokiej jakości część zamienna");
            if (brands.Count > 0)
                html.Append(" do maszyn marki <b>").Append(string.Join(", ", brands.Select(SupplierHtml.Encode))).Append("</b>");
            html.Append(". Sprawdź specyfikację i listę zastosowań poniżej, aby mieć pewność, że część pasuje do Twojej maszyny.</p>");

            // Opis od dostawcy bywa HTML-em (sekcje, nagłówki) - zamieniamy go na dozwolone akapity i listy.
            html.Append(SupplierHtml.ToBlocks(product.Description, decodeEscapedTags: true));

            // 🌟 Dlaczego warto
            html.Append(H2(WhyHeader));
            html.Append(Line("Wysoka jakość", supplier.Length > 0
                ? $"część zamienna renomowanej firmy {supplier}."
                : "część zamienna renomowanej firmy."));
            if (applications.Count > 0)
                html.Append(Line("Dopasowanie", "pełna lista maszyn, do których pasuje część, znajduje się w sekcji Zastosowanie."));
            if (!string.IsNullOrWhiteSpace(product.Substitutes))
                html.Append(Line("Łatwy dobór", "numery katalogowe ułatwiają porównanie z częścią, którą wymieniasz."));
            if (product.Specifications?.Any() == true)
                html.Append(Line("Dokładne wymiary", "parametry techniczne podane w specyfikacji pozwalają sprawdzić część przed zakupem."));

            // ⚙️ Specyfikacja
            if (supplier.Length > 0 || specifications.Count > 0 || !string.IsNullOrWhiteSpace(product.Substitutes))
            {
                html.Append(H2(SpecificationHeader));
                if (supplier.Length > 0)
                    html.Append(Line("Producent", supplier));
                if (!string.IsNullOrWhiteSpace(product.Substitutes))
                    html.Append(Line("Numery katalogowe", SupplierHtml.Encode(product.Substitutes)));
                foreach (var spec in specifications)
                    html.Append(spec);
            }

            // 🚜 Zastosowanie
            if (applications.Count > 0)
            {
                html.Append(H2(ApplicationHeader));
                html.Append("<p>Część pasuje do maszyn:</p>");
                foreach (var line in applications)
                    html.Append(line);
            }

            // 📦 W zestawie - tylko gdy Gąska sprzedaje produkt w opakowaniu/komplecie (PackRequired = 1, PackQty > 1).
            // Przy sprzedaży po 1 szt./mb sekcja nic nie wnosi.
            var isSoldInPackage = product.Packages?.Any(p => p.PackRequired == 1) == true && quantity > 1;
            if (isSoldInPackage)
            {
                html.Append(H2(PackageHeader));
                var unit = ConjugationHelper.Unit(quantity, product.Unit);
                html.Append("<p>").Append(name).Append(" – ").Append(quantity.ToString(CultureInfo.InvariantCulture));
                if (!string.IsNullOrWhiteSpace(unit))
                    html.Append(' ').Append(SupplierHtml.Encode(unit));
                html.Append(".</p>");
            }

            // ⭐ Zamknięcie opisu (na prośbę klienta).
            html.Append(H2(ClosingLine));

            return html.ToString();
        }

        private static List<string> BuildSpecificationLines(Product product)
        {
            if (product.Specifications == null)
                return new List<string>();

            return product.Specifications
                .Where(s => !string.IsNullOrWhiteSpace(s.Name) && !string.IsNullOrWhiteSpace(s.Value))
                .Select(s =>
                {
                    var value = s.Value.Trim();
                    // Jednostkę dopisujemy tylko do liczb - "M20x1,5 RHT mm" nie ma sensu.
                    var isNumber = decimal.TryParse(value.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out _);
                    var unit = isNumber && !string.IsNullOrWhiteSpace(s.UnitName) ? " " + s.UnitName.Trim() : string.Empty;
                    return Line(SupplierHtml.Encode(s.Name), SupplierHtml.Encode(value + unit));
                })
                .ToList();
        }

        /// <summary>Marka - rodzaj maszyny: modele (liście drzewa zastosowań Gąski).</summary>
        private static List<string> BuildApplicationLines(Product product)
        {
            var result = new List<string>();

            if (product.Applications == null || product.Applications.Count == 0)
                return result;

            var byParent = product.Applications
                .GroupBy(a => a.ParentID)
                .ToDictionary(g => g.Key, g => g.ToList());

            if (!byParent.TryGetValue(0, out var roots))
                return result;

            List<string> LeafNames(int parentId)
            {
                var names = new List<string>();
                if (!byParent.TryGetValue(parentId, out var children))
                    return names;

                foreach (var child in children)
                {
                    if (byParent.ContainsKey(child.ApplicationId))
                        names.AddRange(LeafNames(child.ApplicationId));
                    else
                        names.Add(child.Name);
                }

                return names;
            }

            foreach (var root in roots)
            {
                if (!byParent.TryGetValue(root.ApplicationId, out var secondLevel))
                    continue;

                foreach (var group in secondLevel)
                {
                    var leafs = LeafNames(group.ApplicationId).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                    var label = $"{SupplierHtml.Encode(root.Name)} – {SupplierHtml.Encode(group.Name)}";

                    result.Add(leafs.Count > 0
                        ? Line(label, string.Join(", ", leafs.Select(SupplierHtml.Encode)))
                        : $"<p>{label}</p>");
                }
            }

            return result;
        }

        private static List<string> GetRootApplicationNames(Product product) =>
            product.Applications?
                .Where(a => a.ParentID == 0 && !string.IsNullOrWhiteSpace(a.Name))
                .Select(a => a.Name.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(5)
                .ToList()
            ?? new List<string>();

        private static string H2(string text) => $"<h2>{text}</h2>";

        /// <remarks>Etykieta i wartość muszą już być zakodowane.</remarks>
        private static string Line(string encodedLabel, string encodedValue) =>
            $"<p><b>{encodedLabel}:</b> {encodedValue}</p>";

        private static Section TextSection(string html) =>
            new() { SectionItems = new List<SectionItem> { new() { Type = "TEXT", Content = html } } };
    }
}
