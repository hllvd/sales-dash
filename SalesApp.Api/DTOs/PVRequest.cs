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
        [RegularExpression(@"^[a-zA-Z0-9\s\-_]+$", ErrorMessage = "PV name must be alphanumeric (spaces, hyphens, and underscores allowed)")]
        public string Name { get; set; } = string.Empty;
    }
}
