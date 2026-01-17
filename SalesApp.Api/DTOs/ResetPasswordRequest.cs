namespace SalesApp.DTOs
{
    /// <summary>
    /// Request DTO for password reset endpoint
    /// </summary>
    public class ResetPasswordRequest
    {
        /// <summary>
        /// Whether to send an email to the user with the new password
        /// </summary>
        public bool SendEmail { get; set; } = false;
    }
}
