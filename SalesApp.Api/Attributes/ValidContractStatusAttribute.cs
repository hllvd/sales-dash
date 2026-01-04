using System.ComponentModel.DataAnnotations;

namespace SalesApp.Attributes
{
    /// <summary>
    /// Validates that a contract status is one of the valid values
    /// </summary>
    public class ValidContractStatusAttribute : ValidationAttribute
    {
        private static readonly string[] ValidStatuses = 
        { 
            "Active", 
            "Late1", 
            "Late2", 
            "Late3", 
            "Defaulted" 
        };

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
            {
                return ValidationResult.Success;
            }

            var status = value.ToString()!;

            if (ValidStatuses.Contains(status, StringComparer.OrdinalIgnoreCase))
            {
                return ValidationResult.Success;
            }

            return new ValidationResult(
                $"Status must be one of: {string.Join(", ", ValidStatuses)}",
                new[] { validationContext.MemberName ?? "Status" }
            );
        }
    }
}
