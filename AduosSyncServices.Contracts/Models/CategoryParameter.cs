namespace AduosSyncServices.Contracts.Models
{
    public class CategoryParameter
    {
        public int Id { get; set; }

        public int ParameterId { get; set; }
        public int CategoryId { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public bool Required { get; set; }
        public bool RequiredForProduct { get; set; }
        public bool DescribesProduct { get; set; }
        public bool CustomValuesEnabled { get; set; }
        public string AmbiguousValueId { get; set; }
        public int? Min { get; set; }
        public int? Max { get; set; }

        public virtual ICollection<ProductParameter> ProductParameters { get; set; }
        public virtual ICollection<CategoryParameterValue> Values { get; set; } = new List<CategoryParameterValue>();
    }
}