using System.Globalization;
using System.Text;

namespace AduosSyncServices.ServicesManager.Helpers
{
    /// <summary>
    /// Wyszukiwanie tolerancyjne: bez wielkości liter i polskich znaków, po wszystkich słowach zapytania.
    /// Dzięki temu "lozyska kulk" znajdzie "Części według rodzaju > Łożyska > Łożyska kulkowe".
    /// </summary>
    public static class TolerantSearch
    {
        public static string Normalize(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            var decomposed = text.ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(decomposed.Length);

            foreach (var c in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                    continue;

                // "ł" nie rozkłada się na literę + znak diakrytyczny.
                sb.Append(c == 'ł' ? 'l' : c);
            }

            return sb.ToString();
        }

        public static string[] Terms(string? query) =>
            Normalize(query).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        public static bool Matches(string normalizedText, string[] terms) =>
            terms.All(term => normalizedText.Contains(term, StringComparison.Ordinal));
    }
}
