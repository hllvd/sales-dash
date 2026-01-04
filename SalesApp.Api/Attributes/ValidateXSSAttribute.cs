using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace SalesApp.Attributes
{
    /// <summary>
    /// Validates that a string does not contain XSS attack patterns
    /// </summary>
    public class ValidateXSSAttribute : ValidationAttribute
    {
        private static readonly Regex XssPattern = new Regex(
            @"<[^>]*>|javascript:|on\w+\s*=|&lt;|&gt;|&#",
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        );

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
            {
                return ValidationResult.Success;
            }

            var input = value.ToString()!;

            if (XssPattern.IsMatch(input))
            {
                return new ValidationResult(
                    $"{validationContext.DisplayName ?? validationContext.MemberName} contains potentially dangerous content (HTML tags or scripts).",
                    new[] { validationContext.MemberName ?? "Field" }
                );
            }

            return ValidationResult.Success;
        }
    }
}
