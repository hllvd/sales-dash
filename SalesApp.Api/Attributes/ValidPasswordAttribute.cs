using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace SalesApp.Attributes
{
    /// <summary>
    /// Validates that a password meets the required security criteria:
    /// - Minimum length (default 6)
    /// - At least one letter
    /// - At least one digit
    /// </summary>
    public class ValidPasswordAttribute : ValidationAttribute
    {
        public int MinimumLength { get; set; } = 6;

        private static readonly Regex LetterRegex = new(@"[a-zA-Z]", RegexOptions.Compiled);
        private static readonly Regex DigitRegex = new(@"\d", RegexOptions.Compiled);

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            // Allow nulls here; use [Required] for mandatory fields
            if (value == null) return ValidationResult.Success;

            var password = value.ToString();
            if (string.IsNullOrEmpty(password)) return ValidationResult.Success;

            if (password.Length < MinimumLength)
            {
                return new ValidationResult(
                    $"Password must be at least {MinimumLength} characters long.",
                    new[] { validationContext.MemberName ?? "Password" }
                );
            }

            if (!LetterRegex.IsMatch(password) || !DigitRegex.IsMatch(password))
            {
                return new ValidationResult(
                    "Password must contain at least one letter and one number.",
                    new[] { validationContext.MemberName ?? "Password" }
                );
            }

            return ValidationResult.Success;
        }
    }
}
