namespace AduosSyncServices.Contracts.DTOs.Allegro
{
    public class CategoriesResponse
    {
        public List<CategoryDto> Categories { get; set; }
    }

    public class ParentDto
    {
        public string Id { get; set; }
    }
}