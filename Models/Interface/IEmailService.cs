namespace Cylo_Backend.Models.Interface
{
    public interface IEmailService
    {
        Task SendEmailAsync(string to, string subject, string body);
    }
}
