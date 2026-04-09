namespace AduosSyncServices.ServicesManager.Validation
{
    public static class ValidationMessages
    {
        public const string DeliveryInvalidNumbers = "Pola liczbowe reguły dostawy muszą być poprawnymi liczbami.";
        public const string DeliveryInvalidMatchMode = "Tryb dopasowania dostawy jest nieprawidłowy.";
        public const string DeliveryInvalidRuleType = "Typ reguły dostawy jest nieprawidłowy.";
        public const string DeliveryNetPriceThresholdNonPositive = "Próg ceny netto musi być większy od zera.";
        public const string DeliveryMissingNetPriceThreshold = "Dla tej reguły podaj próg ceny netto.";
        public const string DeliveryWeightRuleRequiresWeight = "W trybie Po wymiarach i wadze typy towarów Standardowe, Niestandardowe i Gabarytowe wymagają podanej wagi.";
        public const string DeliveryDimensionsRequiredInWeightMode = "W trybie Po wymiarach i wadze typy towarów Standardowe, Niestandardowe i Gabarytowe wymagają podania długości, szerokości i wysokości.";
        public const string DeliveryNoThresholdInWeightMode = "W trybie Po wymiarach i wadze nie podawaj progu ceny netto.";
        public const string DeliveryNoWeightInPriceMode = "W trybie Po cenie nie podawaj wagi.";
        public const string DeliveryNoDimensionsInPriceMode = "W trybie Po cenie nie podawaj wymiarów.";
        public const string DeliveryMissingName = "Nazwa dostawy nie może być pusta.";
        public const string DeliveryMissingAnyRule = "Zdefiniuj przynajmniej jedną regułę dostawy.";
        public const string DeliveryMissingStandardRule = "Musi istnieć przynajmniej jedna reguła dla towarów Standardowych.";
        public const string DeliveryMissingBulkyRule = "Musi istnieć reguła dla towarów Gabarytowych.";
        public const string DeliveryMissingCustomTypeRule = "Musi istnieć reguła dla towarów Niestandardowych.";
        public const string DeliveryPriceModeMustStartAtZero = "W trybie Po cenie muszą istnieć reguły od 0 PLN dla towarów Standardowych, Niestandardowych i Gabarytowych.";
        public const string DeliveryWeightModeRequiresFallbackRule = "W trybie Po wymiarach i wadze muszą istnieć reguły fallback (co najmniej 9999x9999x9999 i 9999 kg) dla typów Standardowe, Niestandardowe i Gabarytowe.";
        public const string MarginInvalidNumbers = "Zakresy marży muszą być poprawnymi liczbami dziesiętnymi.";
        public const string MarginNegative = "Zakresy marży nie mogą mieć wartości ujemnych.";
        public const string MarginMinGreaterThanMax = "Wartość „Od” nie może być większa niż „Do” w zakresie marży.";
        public const string MarginMissingRange = "Zdefiniuj przynajmniej jeden zakres marży.";
        public const string MarginMustStartAtZero = "Pierwszy zakres marży musi zaczynać się od 0 PLN.";
        public const string MarginOverlap = "Zakresy marży nie mogą się nakładać.";
    }
}
