using Microsoft.Extensions.Configuration;
using SalesApp.Models;

namespace SalesApp.Services
{
    /// <summary>
    /// Email service orchestrator that combines templates with email sender
    /// Follows Open/Closed Principle - can add new templates without modifying this class
    /// </summary>
    public class EmailService : IEmailService
    {
        private readonly IEmailSender _emailSender;
        private readonly IConfiguration _configuration;
        private readonly string _fromAddress;

        public EmailService(IEmailSender emailSender, IConfiguration configuration)
        {
            _emailSender = emailSender;
            _configuration = configuration;
            _fromAddress = _configuration["Email:FromAddress"] ?? "noreply@salesapp.com";
        }

        public async Task<bool> SendPasswordResetEmailAsync(string userEmail, string userName, string newPassword)
        {
            var template = new PasswordResetEmailTemplate(_fromAddress);
            var parameters = new Dictionary<string, string>
            {
                ["userName"] = userName,
                ["newPassword"] = newPassword,
                ["userEmail"] = userEmail
            };

            var message = template.Build(parameters);
            return await _emailSender.SendEmailAsync(message);
        }

        public async Task<bool> SendAdminNotificationEmailAsync(string adminEmail, string notificationType, string message, string? details = null)
        {
            var template = new AdminNotificationEmailTemplate();
            var parameters = new Dictionary<string, string>
            {
                ["adminEmail"] = adminEmail,
                ["notificationType"] = notificationType,
                ["message"] = message,
                ["details"] = details ?? ""
            };

            var emailMessage = template.Build(parameters);
            return await _emailSender.SendEmailAsync(emailMessage);
        }

        public async Task<bool> SendRoleAssignmentEmailAsync(string userEmail, string userName, string? roleName = null, string? qualificationName = null, string? assignedBy = null)
        {
            var template = new RoleAssignmentEmailTemplate();
            var parameters = new Dictionary<string, string>
            {
                ["userEmail"] = userEmail,
                ["userName"] = userName,
                ["roleName"] = roleName ?? "",
                ["qualificationName"] = qualificationName ?? "",
                ["assignedBy"] = assignedBy ?? "Administrador"
            };

            var message = template.Build(parameters);
            return await _emailSender.SendEmailAsync(message);
        }
        
        public async Task<bool> SendWelcomeEmailAsync(string userEmail, string userName, string password)
        {
            var template = new WelcomeEmailTemplate();
            var parameters = new Dictionary<string, string>
            {
                ["toEmail"] = userEmail,
                ["userName"] = userName,
                ["password"] = password
            };

            var message = template.Build(parameters);
            return await _emailSender.SendEmailAsync(message);
        }
    }
}
