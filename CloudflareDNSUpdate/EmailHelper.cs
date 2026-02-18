using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Mail;

namespace CloudflareDNSUpdate
{
    public class EmailHelper
    {
        public bool Enabled { get; set; }
        public string FromAddress { get; set; }
        public string SmtpServer { get; set; }
        public int Port { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public bool EnableSsl { get; set; }

        public EmailHelper(IConfiguration configuration)
        {
            Enabled = configuration.GetValue<bool?>("Email:Enabled") ?? throw new ArgumentNullException("Email:Enabled is not configured.");
            FromAddress = configuration.GetValue<string>("Email:fromAddress") ?? throw new ArgumentNullException("Email:fromAddress is not configured.");
            SmtpServer = configuration.GetValue<string>("Email:SmtpServer") ?? throw new ArgumentNullException("Email:SmtpServer is not configured.");
            Port = configuration.GetValue<int?>("Email:Port") ?? throw new ArgumentNullException("Email:Port is not configured.");
            Username = configuration.GetValue<string>("Email:Username") ?? throw new ArgumentNullException("Email:Username is not configured.");
            Password = configuration.GetValue<string>("Email:Password") ?? throw new ArgumentNullException("Email:Password is not configured.");
            EnableSsl = configuration.GetValue<bool?>("Email:EnableSsl") ?? throw new ArgumentNullException("Email:EnableSsl is not configured.");
        }

        public void Send(List<string> recipients, string subject, string body)
        {
            if (Enabled)
            {
                using var client = new SmtpClient(SmtpServer, Port);
                client.Credentials = new NetworkCredential(Username, Password);
                client.EnableSsl = EnableSsl;

                var message = new MailMessage
                {
                    From = new MailAddress(FromAddress),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true
                };

                recipients.ForEach(r => message.To.Add(r));
                client.Send(message);
            }
        }
    }
}
