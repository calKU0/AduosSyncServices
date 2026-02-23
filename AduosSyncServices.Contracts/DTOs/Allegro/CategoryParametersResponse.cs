using System.Text.Json;

namespace AduosSyncServices.Contracts.DTOs.Allegro
{
    public class CategoryParametersResponse
    {
        public List<CategoryParameterItem> Parameters { get; set; }
    }

    public class CategoryParameterItem
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public bool Required { get; set; }
        public bool RequiredForProduct { get; set; }
        public Restrictions Restrictions { get; set; }
        public List<Dictionary> Dictionary { get; set; }
        public Options Options { get; set; }
    }

    public class Options
    {
        public bool CustomValuesEnabled { get; set; }
        public string AmbiguousValueId { get; set; }
        public bool DescribesProduct { get; set; }
    }

    public class Restrictions
    {
        public JsonElement? Min { get; set; }
        public JsonElement? Max { get; set; }

        public int? GetMin()
        {
            if (Min == null) return null;
            if (Min.Value.ValueKind == JsonValueKind.Number && Min.Value.TryGetInt32(out var num)) return num;
            if (Min.Value.ValueKind == JsonValueKind.String && int.TryParse(Min.Value.GetString(), out var strNum)) return strNum;
            return null;
        }

        public int? GetMax()
        {
            if (Max == null) return null;
            if (Max.Value.ValueKind == JsonValueKind.Number && Max.Value.TryGetInt32(out var num)) return num;
            if (Max.Value.ValueKind == JsonValueKind.String && int.TryParse(Max.Value.GetString(), out var strNum)) return strNum;
            return null;
        }
    }

    public class Dictionary
    {
        public string Id { get; set; }
        public string Value { get; set; }
    }
}