using SalesApp.Models;

namespace SalesApp.Services
{
    /// <summary>
    /// Service for retrieving translated application messages
    /// </summary>
    public interface IMessageService
    {
        /// <summary>
        /// Gets the translated message for the specified message key
        /// </summary>
        string Get(AppMessage message);
        
        /// <summary>
        /// Gets the translated message with formatted parameters
        /// </summary>
        string Get(AppMessage message, params object[] args);
    }
}
