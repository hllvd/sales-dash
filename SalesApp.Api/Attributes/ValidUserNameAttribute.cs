using System.ComponentModel.DataAnnotations;

namespace SalesApp.Attributes
{
    /// <summary>
    /// Validates that a name contains only letters, spaces, hyphens, apostrophes, and accented characters
    /// </summary>
    public class ValidUserNameAttribute : ValidationAttribute
    {
        private static readonly System.Text.RegularExpressions.Regex NamePattern = new(
            @"^[a-zA-Z\u00C0-\u00FF\s'-]+$",
            System.Text.RegularExpressions.RegexOptions.Compiled
        );

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
            {
                return ValidationResult.Success;
            }

            var name = value.ToString()!;

            if (!NamePattern.IsMatch(name))
            {
                return new ValidationResult(
                    $"{validationContext.DisplayName ?? validationContext.MemberName} can only contain letters, spaces, hyphens, and apostrophes.",
                    new[] { validationContext.MemberName ?? "Name" }
                );
            }

            return ValidationResult.Success;
        }
    }
}
