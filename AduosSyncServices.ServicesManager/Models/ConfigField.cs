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
    }
}