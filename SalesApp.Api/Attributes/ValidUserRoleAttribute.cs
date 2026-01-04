using System.ComponentModel.DataAnnotations;
using SalesApp.Models;

namespace SalesApp.Attributes
{
    /// <summary>
    /// Validates that a role value is one of the valid UserRole constants
    /// </summary>
    public class ValidUserRoleAttribute : ValidationAttribute
    {
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value == null)
            {
                // Allow null for optional fields
                return ValidationResult.Success;
            }

            var role = value.ToString();
            
            if (string.IsNullOrWhiteSpace(role))
            {
                return ValidationResult.Success; // Let [Required] handle empty strings
            }

            if (UserRole.IsValid(role))
            {
                return ValidationResult.Success;
            }

            return new ValidationResult(
                $"Role must be one of: {string.Join(", ", UserRole.ValidRoles)}",
                new[] { validationContext.MemberName ?? "Role" }
            );
        }
    }
}
