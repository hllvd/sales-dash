using System.ComponentModel.DataAnnotations;
using SalesApp.Attributes;

namespace SalesApp.DTOs
{
    public class PVRequest
    {
        [Required]
        public int Id { get; set; }
        
        [Required]
        [StringLength(100, MinimumLength = 1, ErrorMessage = "Name must be between 1 and 100 characters")]
        [ValidateXSS]
        [ValidateSQLInjection]
        [ValidAlphanumeric(allowSpaces: true)]
        public string Name { get; set; } = string.Empty;
    }
}
