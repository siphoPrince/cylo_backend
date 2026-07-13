using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;

public class EmailService
{
    private readonly IConfiguration _config;

    public EmailService(IConfiguration config)
    {
        _config = config;
    }

    public async Task SendSecurityEmailAsync(string email, string subject, string htmlMessage)
    {
        // Retrieve settings from appsettings.json
        var smtpServer = _config["EmailSettings:SmtpServer"];
        var smtpPort = int.Parse(_config["EmailSettings:SmtpPort"] ?? "587");
        var senderName = _config["EmailSettings:SenderName"];
        var senderEmail = _config["EmailSettings:SenderEmail"];
        var password = _config["EmailSettings:Password"];

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(senderName, senderEmail));
        message.To.Add(MailboxAddress.Parse(email));
        message.Subject = subject;

        var bodyBuilder = new BodyBuilder { HtmlBody = htmlMessage };
        message.Body = bodyBuilder.ToMessageBody();

        using (var client = new SmtpClient())
        {
            // Connect to Absolute Hosting SMTP
            await client.ConnectAsync(smtpServer, smtpPort, SecureSocketOptions.StartTls);

            // Authenticate with your security@ address
            await client.AuthenticateAsync(senderEmail, password);

            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }
    }
}