namespace AduosSyncServices.Contracts.DTOs.Allegro
{
    public class AllegroErrorResponse
    {
        public List<AllegroError> Errors { get; set; }
    }

    public class AllegroError
    {
        public string Code { get; set; }
        public string Details { get; set; }
        public string Message { get; set; }
        public string Path { get; set; }
        public string UserMessage { get; set; }
        public Dictionary<string, object> Metadata { get; set; }
    }
}