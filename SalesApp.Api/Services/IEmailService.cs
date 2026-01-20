namespace SalesApp.Services
{
    /// <summary>
    /// High-level email service interface for application use
    /// </summary>
    public interface IEmailService
    {
        /// <summary>
        /// Sends a password reset email to a user
        /// </summary>
        Task<bool> SendPasswordResetEmailAsync(string userEmail, string userName, string newPassword);

        /// <summary>
        /// Sends an admin notification email
        /// </summary>
        Task<bool> SendAdminNotificationEmailAsync(string adminEmail, string notificationType, string message, string? details = null);

        /// <summary>
        /// Sends a role assignment notification email
        /// </summary>
        Task<bool> SendRoleAssignmentEmailAsync(string userEmail, string userName, string? roleName = null, string? qualificationName = null, string? assignedBy = null);
        
        /// <summary>
        /// Sends a welcome email with credentials to a newly created user
        /// </summary>
        Task<bool> SendWelcomeEmailAsync(string userEmail, string userName, string password);
    }
}
