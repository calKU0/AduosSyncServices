namespace AduosSyncServices.Contracts.Interfaces
{
    public interface IEmailService
    {
        Task SendEmailAsync(string from, string to, string subject, string htmlBody);
    }
}