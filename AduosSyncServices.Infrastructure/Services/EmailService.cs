using AduosSyncServices.Contracts.Interfaces;
using AduosSyncServices.Contracts.Settings;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace AduosSyncServices.Infrastructure.Services
{
    public class EmailService : IEmailService
    {
        private readonly SmtpSettings _smtpSettings;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IOptions<SmtpSettings> options, ILogger<EmailService> logger)
        {
            _smtpSettings = options.Value;
            _logger = logger;
        }

        public async Task SendEmailAsync(string from, string to, string subject, string htmlBody)
        {
            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(from, _smtpSettings.User));

                var recipients = to.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var address in recipients)
                {
                    message.To.Add(MailboxAddress.Parse(address.Trim()));
                }

                message.Subject = subject;
                message.Body = new TextPart("html") { Text = htmlBody };

                using var client = new SmtpClient();
                await client.ConnectAsync(_smtpSettings.Host, _smtpSettings.Port, SecureSocketOptions.SslOnConnect);
                await client.AuthenticateAsync(_smtpSettings.User, _smtpSettings.Password);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {To}", to);
            }
        }
    }
}