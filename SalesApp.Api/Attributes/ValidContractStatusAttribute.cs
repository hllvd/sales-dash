using System.ComponentModel.DataAnnotations;
using SalesApp.Models;

namespace SalesApp.Attributes
{
    /// <summary>
    /// Validates that a contract status is one of the valid values
    /// </summary>
    public class ValidContractStatusAttribute : ValidationAttribute
    {
        // ✅ Use enum instead of hardcoded strings
        private static readonly string[] ValidStatuses = 
        { 
            ContractStatus.Active.ToApiString(), 
            ContractStatus.Late1.ToApiString(), 
            ContractStatus.Late2.ToApiString(), 
            ContractStatus.Late3.ToApiString(), 
            ContractStatus.Defaulted.ToApiString() 
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
