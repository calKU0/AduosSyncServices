namespace AduosSyncServices.Contracts.DTOs.Allegro
{
    public class CompatibleProductGroupsResponse
    {
        public List<CompatibleGroupDto> Groups { get; set; }

        public int Count { get; set; }

        public int TotalCount { get; set; }
    }

    public class CompatibleGroupDto
    {
        public string Id { get; set; }

        public string Text { get; set; }
    }
}