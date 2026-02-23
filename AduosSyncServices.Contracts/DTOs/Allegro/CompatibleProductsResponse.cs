namespace AduosSyncServices.Contracts.DTOs.Allegro
{
    // DTOs for deserialization
    public class CompatibleProductsResponse
    {
        public List<CompatibleProductDto> CompatibleProducts { get; set; }
    }

    public class CompatibleProductDto
    {
        public string Id { get; set; }

        public string Text { get; set; }

        public CompatibleProductGroupDto Group { get; set; }

        public List<CompatibleAttribute> Attributes { get; set; }
    }

    public class CompatibleProductGroupDto
    {
        public string Id { get; set; }
    }

    public class CompatibleAttribute
    {
        public string Id { get; set; }

        public List<string> Values { get; set; }
    }
}