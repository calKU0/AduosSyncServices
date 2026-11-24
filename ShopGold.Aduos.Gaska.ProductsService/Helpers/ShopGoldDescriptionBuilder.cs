using AduosSyncServices.Contracts.Models;
using AduosSyncServices.Infrastructure.Helpers;
using ShopGold.Aduos.Gaska.ProductsService.Settings;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace ShopGold.Aduos.Gaska.ProductsService.Helpers
{
    /// <summary>
    /// Opis produktu w sklepie w układzie opisów sklepu:
    /// 💡 wstęp, 🌟 dlaczego warto (lista - szablon sklepu pokazuje ją z zielonymi „ptaszkami”),
    /// ⚙️ specyfikacja, 🚜 zastosowanie, 📦 w zestawie (tylko gdy Gąska sprzedaje w opakowaniu)
    /// i 🏭 producent / osoba odpowiedzialna (GPSR).
    ///
    /// Emotki zapisujemy jako encje HTML (&amp;#128161;) - tak samo zapisuje je edytor sklepu, a baza sklepu
    /// nie musi wtedy obsługiwać 4-bajtowych znaków UTF-8.
    /// </summary>
    public static class ShopGoldDescriptionBuilder
    {
        internal const string IntroHeader = "&#128161; Sprawdzona część do Twojej maszyny";
        internal const string WhyHeader = "&#127775; Dlaczego warto wybrać ten produkt?";
        internal const string SpecificationHeader = "&#9881;&#65039; Specyfikacja techniczna:";
        internal const string ApplicationHeader = "&#128668; Zastosowanie:";
        internal const string PackageHeader = "&#128230; W zestawie:";
        internal const string SafetyHeader = "&#127981; Producent i osoba odpowiedzialna:";

        private const string CellStyle = "border: 1px solid #dbdbdb; padding: 8px 14px; text-align: left;";

        /// <param name="gpsr">Producent i osoba odpowiedzialna (GPSR) - tabela na końcu opisu, jak w opisach sklepu.</param>
        /// <param name="safetyPdfUrl">Link do PDF "Informacje o bezpieczeństwie produktu". Pusty = bez przycisku.</param>
        public static string Build(Product product, int quantity, GpsrSettings? gpsr = null, string? safetyPdfUrl = null)
        {
            var name = Encode(OfferNameFormatter.Format(product.Name));
            var supplier = Encode(product.SupplierName);
            var brands = RootApplicationNames(product);
            var applications = BuildApplicationLines(product);
            var specifications = BuildSpecificationLines(product);
            var substitutes = SubstituteNumbers(product.Substitutes);
            var isSoldInPackage = product.Packages?.Any(p => p.PackRequired == 1) == true && quantity > 1;
            var unit = isSoldInPackage ? Encode(ConjugationHelper.Unit(quantity, product.Unit ?? string.Empty)) : string.Empty;
            var quantityText = quantity.ToString(CultureInfo.InvariantCulture) + (unit.Length > 0 ? " " + unit : string.Empty);

            var html = new StringBuilder();

            // 💡 Wstęp
            html.Append(H3(IntroHeader));
            html.Append("<p><b>").Append(name).Append("</b>");
            if (supplier.Length > 0)
                html.Append(" od producenta <b>").Append(supplier).Append("</b>");
            html.Append(" to wysokiej jakości część zamienna");
            if (brands.Count > 0)
                html.Append(" do maszyn marki <b>").Append(string.Join(", ", brands.Select(Encode))).Append("</b>");
            html.Append(". Sprawdź specyfikację i listę zastosowań poniżej, aby mieć pewność, że część pasuje do Twojej maszyny.</p>");
            html.Append(SupplierDescription(product.Description));

            // 🌟 Dlaczego warto
            html.Append(H3(WhyHeader)).Append("<ul>");
            html.Append(Item("Wysoka jakość", supplier.Length > 0
                ? $"zamiennik renomowanej firmy {supplier}."
                : "zamiennik renomowanej firmy."));
            if (applications.Count > 0)
                html.Append(Item("Dopasowanie", "pełna lista maszyn, do których pasuje część, znajduje się w sekcji Zastosowanie."));
            if (substitutes.Count > 0)
                html.Append(Item("Łatwy dobór", "numery katalogowe ułatwiają porównanie z częścią, którą wymieniasz."));
            if (specifications.Count > 0)
                html.Append(Item("Dokładne wymiary", "parametry techniczne podane w specyfikacji pozwalają sprawdzić część przed zakupem."));
            if (isSoldInPackage)
                html.Append(Item("Komplet w zestawie", $"{quantityText} w jednym zamówieniu."));
            html.Append("</ul>");

            // ⚙️ Specyfikacja
            html.Append(H3(SpecificationHeader));
            if (supplier.Length > 0)
                html.Append(Line("Producent", supplier));
            html.Append(Line("Numer katalogowy", Encode(product.Code)));
            if (substitutes.Count > 0)
                html.Append(Line("Numery katalogowe", Encode(string.Join(", ", substitutes))));
            foreach (var spec in specifications)
                html.Append(spec);
            if (isSoldInPackage)
                html.Append("<ul>").Append(Item("Ilość w zestawie", quantityText)).Append("</ul>");

            // 🚜 Zastosowanie
            if (applications.Count > 0)
            {
                html.Append(H3(ApplicationHeader));
                html.Append("<p>Część pasuje do maszyn:</p>");
                foreach (var line in applications)
                    html.Append(line);
                html.Append("<p>Przed zakupem warto porównać numer katalogowy i wymiary z częścią, którą wymieniasz.</p>");
            }

            // 📦 W zestawie - tylko gdy Gąska sprzedaje produkt w opakowaniu/komplecie.
            if (isSoldInPackage)
            {
                html.Append(H3(PackageHeader));
                html.Append("<p>").Append(quantityText).Append(" – ").Append(name).Append(".</p>");
            }

            // 🏭 Bezpieczeństwo produktu (GPSR) - ten sam układ co w opisach dodawanych ręcznie w sklepie.
            html.Append(SafetySection(gpsr, safetyPdfUrl));

            return html.ToString();
        }

        internal static string SafetySection(GpsrSettings? gpsr, string? safetyPdfUrl)
        {
            var producer = Encode(gpsr?.Producer?.Name);
            var responsible = Encode(gpsr?.Importer?.Name);
            var html = new StringBuilder();

            if (producer.Length > 0 || responsible.Length > 0)
            {
                html.Append(H3(SafetyHeader));
                html.Append("<table style=\"margin: 0 auto; border-collapse: collapse;\">");
                if (producer.Length > 0)
                    html.Append(SafetyRow("Producent odpowiedzialny", producer));
                if (responsible.Length > 0)
                    html.Append(SafetyRow("Osoba odpowiedzialna za zgodność produktu", responsible));
                html.Append("</table>");
            }

            if (!string.IsNullOrWhiteSpace(safetyPdfUrl))
            {
                html.Append("<p style=\"text-align: center; margin-top: 25px;\"><a href=\"")
                    .Append(WebUtility.HtmlEncode(safetyPdfUrl.Trim()))
                    .Append("\" target=\"_blank\" rel=\"noopener\" style=\"display: inline-block; padding: 12px 25px; background-color: #0f4c3a; color: #ffffff; font-weight: bold; text-decoration: none; border: 0;\">")
                    .Append("&#128196; Informacje o bezpieczeństwie produktu (PDF)</a></p>");
            }

            return html.ToString();
        }

        private static string SafetyRow(string label, string encodedValue) =>
            $"<tr><th style=\"{CellStyle}\">{label}</th><td style=\"{CellStyle}\">{encodedValue}</td></tr>";

        /// <summary>
        /// Opis od Gąski bywa zwykłym tekstem, HTML-em albo HTML-em zakodowanym (&amp;lt;p&amp;gt;).
        /// Zakodowany odkodowujemy, zwykły tekst zamieniamy na akapity. Skrypty i style usuwamy.
        /// </summary>
        public static string SupplierDescription(string? description)
        {
            var text = (description ?? string.Empty).Trim();
            if (text.Length == 0)
                return string.Empty;

            if (text.Contains("&lt;", StringComparison.OrdinalIgnoreCase) && !Regex.IsMatch(text, "<[a-zA-Z/]"))
                text = WebUtility.HtmlDecode(text);

            text = Regex.Replace(text, @"<(script|style|iframe)[^>]*>.*?</\1\s*>", string.Empty, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            text = Regex.Replace(text, @"\son\w+\s*=\s*(""[^""]*""|'[^']*'|[^\s>]+)", string.Empty, RegexOptions.IgnoreCase);

            if (Regex.IsMatch(text, "<[a-zA-Z/][^>]*>"))
                return text;

            var paragraphs = Regex.Split(text, @"(\r?\n){2,}")
                .Select(p => p.Trim())
                .Where(p => p.Length > 0 && p != "\n" && p != "\r\n");

            return string.Concat(paragraphs.Select(p =>
                "<p>" + Encode(p).Replace("\r\n", "<br />").Replace("\n", "<br />") + "</p>"));
        }

        private static List<string> BuildSpecificationLines(Product product) =>
            (product.Specifications ?? new List<ProductSpecification>())
                .Where(s => !string.IsNullOrWhiteSpace(s.Name) && !string.IsNullOrWhiteSpace(s.Value))
                .Select(s =>
                {
                    var value = s.Value.Trim();
                    // Jednostkę dopisujemy tylko do liczb - "M20x1,5 RHT mm" nie ma sensu.
                    var isNumber = decimal.TryParse(value.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out _);
                    var unit = isNumber && !string.IsNullOrWhiteSpace(s.UnitName) ? " " + s.UnitName.Trim() : string.Empty;
                    return Line(Encode(s.Name), Encode(value + unit));
                })
                .ToList();

        private static List<string> SubstituteNumbers(string? substitutes) =>
            (substitutes ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

        /// <summary>Marka – rodzaj maszyny: modele (liście drzewa zastosowań Gąski).</summary>
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

            List<string> LeafNames(int parentId, HashSet<int> visited)
            {
                var names = new List<string>();
                if (!visited.Add(parentId) || !byParent.TryGetValue(parentId, out var children))
                    return names;

                foreach (var child in children)
                {
                    if (byParent.ContainsKey(child.ApplicationId))
                        names.AddRange(LeafNames(child.ApplicationId, visited));
                    else
                        names.Add(child.Name);
                }

                return names;
            }

            foreach (var root in roots)
            {
                if (!byParent.TryGetValue(root.ApplicationId, out var secondLevel))
                {
                    result.Add($"<p>{Encode(root.Name)}</p>");
                    continue;
                }

                foreach (var group in secondLevel)
                {
                    var leafs = LeafNames(group.ApplicationId, new HashSet<int>())
                        .Where(n => !string.IsNullOrWhiteSpace(n))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    var label = $"{Encode(root.Name)} – {Encode(group.Name)}";
                    result.Add(leafs.Count > 0
                        ? Line(label, string.Join(", ", leafs.Select(Encode)))
                        : $"<p>{label}</p>");
                }
            }

            return result;
        }

        private static List<string> RootApplicationNames(Product product) =>
            product.Applications?
                .Where(a => a.ParentID == 0 && !string.IsNullOrWhiteSpace(a.Name))
                .Select(a => a.Name.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(5)
                .ToList()
            ?? new List<string>();

        private static string H3(string text) => $"<h3>{text}</h3>";

        /// <remarks>Etykieta i wartość muszą już być zakodowane.</remarks>
        private static string Line(string encodedLabel, string encodedValue) =>
            $"<p><b>{encodedLabel}:</b> {encodedValue}</p>";

        private static string Item(string encodedLabel, string encodedValue) =>
            $"<li><b>{encodedLabel}:</b> {encodedValue}</li>";

        private static string Encode(string? text) => WebUtility.HtmlEncode(text?.Trim() ?? string.Empty);
    }
}
