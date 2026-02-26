using AduosSyncServices.ServicesManager.Enums;

namespace AduosSyncServices.ServicesManager.Models
{
    public class LogLine
    {
        public LogLevel Level { get; set; }
        public string Message { get; set; }
    }
}