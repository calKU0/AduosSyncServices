namespace AduosSyncServices.Contracts.DTOs.Allegro
{
    public class SupportedCategoriesResponse
    {
        public List<SupportedCategory> SupportedCategories { get; set; }
    }

    public class SupportedCategory
    {
        public string CategoryId { get; set; }

        public string Name { get; set; }

        public string ItemsType { get; set; }

        public string InputType { get; set; }

        public ValidationRules ValidationRules { get; set; }
    }

    public class ValidationRules
    {
        public int? MaxRows { get; set; }
        public int? MaxCharactersPerLine { get; set; }
    }
}