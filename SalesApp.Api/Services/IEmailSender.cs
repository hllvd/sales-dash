using SalesApp.Models;

namespace SalesApp.Services
{
    /// <summary>
    /// Core interface for sending emails (provider-agnostic)
    /// Implementations can use AWS SES, SendGrid, SMTP, etc.
    /// </summary>
    public interface IEmailSender
    {
        /// <summary>
        /// Sends an email message
        /// </summary>
        /// <param name="message">The email message to send</param>
        /// <returns>True if email was sent successfully, false otherwise</returns>
        Task<bool> SendEmailAsync(EmailMessage message);
    }
}
