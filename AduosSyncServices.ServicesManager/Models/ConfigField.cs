using AduosSyncServices.ServicesManager.Enums;

namespace AduosSyncServices.ServicesManager.Models
{
    public class ConfigField
    {
        public string Key { get; set; } = "";
        public string Label { get; set; } = "";
        public string Description { get; set; } = "";
        public string Group { get; set; } = "Other";
        public bool IsEnabled { get; set; } = true;
        public ConfigFieldType FieldType { get; set; } = ConfigFieldType.String;

        /// <summary>Lista id kategorii Gąski wybierana z drzewa kategorii (zapis: "id, id, id").</summary>
        public bool IsCategoryPicker { get; set; }

        /// <summary>Tekst przy pustym wyborze kategorii.</summary>
        public string EmptyText { get; set; } = "";
    }
}