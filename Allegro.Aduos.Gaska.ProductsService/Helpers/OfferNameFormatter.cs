using System.Globalization;
using System.Text.RegularExpressions;

namespace Allegro.Aduos.Gaska.ProductsService.Helpers
{
    /// <summary>
    /// Nazwa oferty na Allegro: każdy wyraz od wielkiej litery, reszta małymi
    /// ("ROZRUSZNIK 3.2KW Z REDUKTOREM URSUS C 330" -> "Rozrusznik 3.2Kw z Reduktorem Ursus C 330").
    /// Krótkie przyimki i spójniki (z, do, i, w...) zostają małymi literami, chyba że zaczynają nazwę.
    /// Liczby rzymskie (II, IV...) zostają wielkimi literami.
    /// </summary>
    internal static class OfferNameFormatter
    {
        private static readonly CultureInfo Polish = CultureInfo.GetCultureInfo("pl-PL");

        private static readonly HashSet<string> LowercaseWords = new(StringComparer.Ordinal)
        {
            "a", "i", "o", "u", "w", "z", "we", "ze", "do", "na", "od", "po", "za",
            "dla", "bez", "pod", "nad", "przy", "lub", "oraz",
        };

        private static readonly Regex RomanNumeral = new(@"^(?=[IVX])X{0,3}(IX|IV|V?I{0,3})$", RegexOptions.Compiled);
        private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

        /// <summary>Liczba z jednostką: 3.2KW, 12V, 50A, 1,5MM, 100KM...</summary>
        private static readonly Regex NumberWithUnit = new(
            @"^\d+([.,]\d+)?(kw|km|hp|v|a|ah|w|mm|cm|m|mb|kg|g|l|ml|bar|nm|obr|szt)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static string Format(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return name ?? string.Empty;

            var words = Whitespace.Split(name.Trim()).ToList();

            // Gąska zaczyna nazwę od symbolu/numeru katalogowego ("JAG59-0030 KOŁO ZĘBATE...").
            // Przenosimy wszystkie takie kody z początku na koniec, żeby nazwa zaczynała się od tego, czym jest produkt.
            var leadingCodes = words.TakeWhile(IsCatalogCode).ToList();
            if (leadingCodes.Count > 0 && leadingCodes.Count < words.Count)
            {
                var rest = words.Skip(leadingCodes.Count).ToList();
                // Kod, który i tak jest dalej w nazwie ("150N1280 ORBITROL 150N1280"), nie jest dopisywany drugi raz.
                var codesToAppend = leadingCodes.Where(c => !rest.Contains(c, StringComparer.OrdinalIgnoreCase));
                words = rest.Concat(codesToAppend).ToList();
            }

            for (var i = 0; i < words.Count; i++)
                words[i] = FormatWord(words[i], isFirst: i == 0);

            return string.Join(' ', words);
        }

        /// <summary>
        /// Symbol/numer katalogowy: litery z cyframi (JAG59-0030, 3541414M1, RE520906+) albo sam numer
        /// z co najmniej 4 cyframi (82991684, 0434.5858, 46/42-790/3). Liczba z jednostką (3.2KW, 12V) to nie kod.
        /// </summary>
        internal static bool IsCatalogCode(string word)
        {
            if (!word.Any(char.IsDigit) || NumberWithUnit.IsMatch(word))
                return false;

            return word.Any(char.IsLetter) || word.Count(char.IsDigit) >= 4;
        }

        private static string FormatWord(string word, bool isFirst)
        {
            if (RomanNumeral.IsMatch(word))
                return word;

            // Kod katalogowy (litery + cyfry, np. JAG59-0030, UCFL207, 3541414M1) zostaje wielkimi literami.
            // Wyjątek: liczba z jednostką ("3.2KW", "12V", "50A") - jednostka jak zwykły wyraz ("3.2Kw").
            if (word.Any(char.IsDigit) && word.Any(char.IsLetter) && !NumberWithUnit.IsMatch(word))
                return word.ToUpper(Polish);

            var lower = word.ToLower(Polish);

            if (!isFirst && LowercaseWords.Contains(lower))
                return lower;

            // Wielka litera na pierwszej literze wyrazu - także po cyfrach ("3.2kw" -> "3.2Kw").
            var chars = lower.ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                if (char.IsLetter(chars[i]))
                {
                    chars[i] = char.ToUpper(chars[i], Polish);
                    break;
                }
            }

            return new string(chars);
        }
    }
}
