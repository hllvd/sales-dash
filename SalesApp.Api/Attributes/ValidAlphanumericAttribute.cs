using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace SalesApp.Attributes
{
    /// <summary>
    /// Validates that a string contains only alphanumeric characters, spaces, hyphens, and underscores
    /// </summary>
    public class ValidAlphanumericAttribute : ValidationAttribute
    {
        private readonly bool _allowSpaces;
        private readonly bool _required;

        /// <summary>
        /// Creates a new ValidAlphanumeric attribute
        /// </summary>
        /// <param name="allowSpaces">Whether to allow spaces in the value (default: false)</param>
        /// <param name="required">Whether the field is required (default: true for non-nullable, false for nullable)</param>
        public ValidAlphanumericAttribute(bool allowSpaces = false, bool required = true)
        {
            _allowSpaces = allowSpaces;
            _required = required;
        }

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
            {
                // If required and empty, fail validation
                if (_required && (value == null || string.IsNullOrWhiteSpace(value.ToString())))
                {
                    return new ValidationResult(
                        $"{validationContext.DisplayName ?? validationContext.MemberName} is required.",
                        new[] { validationContext.MemberName ?? "Field" }
                    );
                }
                // If not required and empty, pass validation
                return ValidationResult.Success;
            }

            var input = value.ToString()!;
            
            // Pattern with or without spaces
            var pattern = _allowSpaces 
                ? @"^[a-zA-Z0-9\s\-_]+$" 
                : @"^[a-zA-Z0-9\-_]+$";
            
            var regex = new Regex(pattern, RegexOptions.Compiled);

            if (!regex.IsMatch(input))
            {
                var spacesPart = _allowSpaces ? "spaces, " : "";
                return new ValidationResult(
                    $"{validationContext.DisplayName ?? validationContext.MemberName} must be alphanumeric ({spacesPart}hyphens and underscores allowed).",
                    new[] { validationContext.MemberName ?? "Field" }
                );
            }

            return ValidationResult.Success;
        }
    }
}
