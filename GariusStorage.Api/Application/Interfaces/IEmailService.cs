namespace GariusStorage.Api.Application.Interfaces
{
    public interface IEmailService
    {
        Task SendEmailAsync(string toEmail, string subject, string htmlContent, string? textContent = null);
        Task SendEmailConfirmationLinkAsync(string userEmail, string userName, string confirmationLink);
        Task SendPasswordResetLinkAsync(string userEmail, string userName, string resetLink);
    }
}
