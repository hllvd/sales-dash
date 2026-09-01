using System.ComponentModel.DataAnnotations;

namespace SalesApp.Attributes
{
    /// <summary>
    /// Validates that a name contains only letters, spaces, hyphens, apostrophes, and accented characters
    /// </summary>
    public class ValidUserNameAttribute : ValidationAttribute
    {
        private static readonly System.Text.RegularExpressions.Regex NamePattern = new(
            @"^[\p{L}\p{M}\s'/\-&]+$",
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
                if (System.Text.RegularExpressions.Regex.IsMatch(name, @"\d"))
                {
                    return new ValidationResult(
                        $"O campo {displayName} não pode conter números. É necessário remover os números do campo cliente para poder salvar.",
                        new[] { validationContext.MemberName ?? "Name" }
                    );
                }

                return new ValidationResult(
                    $"{displayName} só pode conter letras, espaços, hífens, apóstrofos, acentuação, barras e e-comercial (/ e &).",
                    new[] { validationContext.MemberName ?? "Name" }
                );
            }

            return ValidationResult.Success;
        }
    }
}
