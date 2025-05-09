namespace GariusStorage.Api.Application.Interfaces
{
    public interface IEmailService
    {
        Task<bool> SendEmailAsync(string toEmail, string subject, string htmlContent, string? textContent = null);
        Task<bool> SendEmailConfirmationLinkAsync(string userEmail, string userName, string confirmationLink);
        Task<bool> SendPasswordResetLinkAsync(string userEmail, string userName, string resetLink);
    }
}
