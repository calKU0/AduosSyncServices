namespace AduosSyncServices.Infrastructure.Helpers
{
    /// <summary>Numery przesyłek z Gąski w postaci, jaką przyjmuje Allegro.</summary>
    public static class TrackingNumbers
    {
        private static readonly char[] Separators = [',', ';'];

        /// <summary>
        /// Rozbija pole numeru przesyłki na pojedyncze numery. Gąska zwraca je czasem jako
        /// "30648325292, 30648325293, 30648325296" - Allegro takiego zapisu nie przyjmuje.
        /// </summary>
        public static IReadOnlyList<string> Split(string? trackingNumbers)
        {
            if (string.IsNullOrWhiteSpace(trackingNumbers))
                return Array.Empty<string>();

            return trackingNumbers
                .Split(Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>Identyfikator przewoźnika w Allegro na podstawie nazwy kuriera z Gąski.</summary>
        public static string? NormalizeCarrierId(string? carrier) => carrier?.ToUpperInvariant() switch
        {
            null => null,
            var s when s.Contains("DPD") => "DPD",
            var s when s.Contains("FEDEX") => "FEDEX",
            var s when s.Contains("GLS") => "GLS",
            _ => carrier
        };

        /// <summary>Klucz przesyłki do porównań (przewoźnik + numer, bez rozróżniania wielkości liter).</summary>
        public static string ShipmentKey(string? carrierId, string waybill) => $"{carrierId ?? string.Empty}|{waybill}".ToUpperInvariant();
    }
}
