using System.ComponentModel.DataAnnotations;

namespace SalesApp.Attributes
{
    /// <summary>
    /// Validates that a password meets the required security criteria:
    /// - Minimum length (default 6)
    /// </summary>
    public class ValidPasswordAttribute : ValidationAttribute
    {
        public int MinimumLength { get; set; } = 6;

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            // Allow nulls here; use [Required] for mandatory fields
            if (value == null) return ValidationResult.Success;

            var password = value.ToString();
            if (string.IsNullOrEmpty(password)) return ValidationResult.Success;

            if (password.Length < MinimumLength)
            {
                return new ValidationResult(
                    $"A senha deve ter pelo menos {MinimumLength} caracteres.",
                    new[] { validationContext.MemberName ?? "Password" }
                );
            }

            return ValidationResult.Success;
        }
    }
}

