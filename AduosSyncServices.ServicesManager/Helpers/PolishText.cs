namespace AduosSyncServices.ServicesManager.Helpers
{
    public static class PolishText
    {
        /// <summary>
        /// Standard Polish numeral-noun agreement: 1 uses the singular form; 2-4 (except 12-14) use
        /// the "few" plural form; everything else (0, 5+, 12-14) uses the genitive "many" plural form.
        /// E.g. 1 zamówienie, 3 zamówienia, 5 zamówień.
        /// </summary>
        public static string Count(int count, string singular, string few, string many)
        {
            if (count == 1)
                return singular;

            var lastDigit = count % 10;
            var lastTwoDigits = count % 100;

            if (lastDigit is >= 2 and <= 4 && (lastTwoDigits < 12 || lastTwoDigits > 14))
                return few;

            return many;
        }
    }
}
