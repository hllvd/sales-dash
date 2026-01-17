using SalesApp.Models;

namespace SalesApp.Services
{
    /// <summary>
    /// Interface for building email messages from templates
    /// Separates email content generation from sending logic
    /// </summary>
    public interface IEmailTemplate
    {
        /// <summary>
        /// Builds an email message using the provided parameters
        /// </summary>
        /// <param name="parameters">Template parameters (e.g., userName, newPassword, etc.)</param>
        /// <returns>A fully constructed EmailMessage ready to be sent</returns>
        EmailMessage Build(Dictionary<string, string> parameters);
    }
}
