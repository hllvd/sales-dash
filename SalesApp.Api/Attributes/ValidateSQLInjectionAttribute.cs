using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace SalesApp.Attributes
{
    /// <summary>
    /// Validates that a string does not contain SQL injection attack patterns
    /// </summary>
    public class ValidateSQLInjectionAttribute : ValidationAttribute
    {
        // More targeted pattern - only flag dangerous combinations, not standalone keywords
        private static readonly Regex SqlInjectionPattern = new Regex(
            @"('.*--)|('.*;)|(';)|(\bOR\b\s+['\d]+=\s*['\d]+)|(\bAND\b\s+['\d]+=\s*['\d]+)|(\/\*)|(\*\/)|(\bEXEC\s*\()|(\bEXECUTE\s*\()|(\bUNION\s+SELECT)|(\bDROP\s+TABLE)|(\bDELETE\s+FROM)|(\bINSERT\s+INTO)|(\bUPDATE\s+\w+\s+SET)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        );

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
            {
                return ValidationResult.Success;
            }

            var input = value.ToString()!;

            if (SqlInjectionPattern.IsMatch(input))
            {
                return new ValidationResult(
                    $"{validationContext.DisplayName ?? validationContext.MemberName} contains potentially dangerous SQL patterns.",
                    new[] { validationContext.MemberName ?? "Field" }
                );
            }

            return ValidationResult.Success;
        }
    }
}
