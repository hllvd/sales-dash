using System;
using System.ComponentModel.DataAnnotations;
using SalesApp.Attributes;

namespace SalesApp.DTOs
{
    public class AdminRegistrationRequest
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        [StringLength(254)]
        [RegularExpression(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", ErrorMessage = "Invalid email format")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Name is required")]
        [StringLength(150, MinimumLength = 2, ErrorMessage = "Name must be between 2 and 150 characters")]
        [ValidUserName]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        [ValidPassword(MinimumLength = 6)]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Team Name is required")]
        [StringLength(100, ErrorMessage = "Team name cannot exceed 100 characters")]
        public string TeamName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Classification Level is required")]
        public int ClassificationLevelId { get; set; }

        public DateTime? ClassificationStartDate { get; set; }

        [Required(ErrorMessage = "Role is required")]
        [RegularExpression("^(manager|secretary)$", ErrorMessage = "Role must be 'manager' or 'secretary'")]
        public string Role { get; set; } = "manager";

        // Secretary Details (optional, but parsed when Role is secretary)
        [StringLength(150)]
        public string? SecretaryName { get; set; }

        [EmailAddress(ErrorMessage = "Invalid secretary email format")]
        [StringLength(254)]
        public string? SecretaryEmail { get; set; }

        [StringLength(20)]
        public string? SecretaryWhatsapp { get; set; }
    }
}
