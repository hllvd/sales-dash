namespace SalesApp.DTOs
{
    /// <summary>
    /// Response DTO for password reset endpoint
    /// </summary>
    public class ResetPasswordResponse
    {
        /// <summary>
        /// The new password (displayed only once)
        /// </summary>
        public string NewPassword { get; set; } = string.Empty;

        /// <summary>
        /// Whether an email was sent to the user
        /// </summary>
        public bool EmailSent { get; set; }
    }
}
