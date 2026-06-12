using System.ComponentModel.DataAnnotations;

namespace SalesApp.Attributes
{
    /// <summary>
    /// Validates that a name contains only letters, spaces, hyphens, apostrophes, and accented characters
    /// </summary>
    public class ValidUserNameAttribute : ValidationAttribute
    {
        private static readonly System.Text.RegularExpressions.Regex NamePattern = new(
            @"^[\p{L}\p{M}\s'-]+$",
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
                var displayName = validationContext.DisplayName ?? validationContext.MemberName ?? "Nome";
                return new ValidationResult(
                    $"{displayName} só pode conter letras, espaços, hífens, apóstrofos e acentuação.",
                    new[] { validationContext.MemberName ?? "Name" }
                );
            }

            return ValidationResult.Success;
        }
    }
}
