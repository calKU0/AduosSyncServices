namespace AduosSyncServices.Contracts.DTOs.Allegro
{
    public class ApiResult<T>
    {
        public T? Data { get; set; }
        public HttpResponseMessage Response { get; set; } = default!;
        public string Body { get; set; } = string.Empty;
    }
}