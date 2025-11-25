using SalesApp.Data;
using Microsoft.EntityFrameworkCore;

namespace SalesApp.Services
{
    public class ImportValidationService : IImportValidationService
    {
        private readonly AppDbContext _context;

        // Define required fields for each entity type
        private readonly Dictionary<string, List<string>> _requiredFields = new()
        {
            ["Contract"] = new List<string> { "ContractNumber", "UserName", "UserSurname", "TotalAmount", "GroupId" },
            ["User"] = new List<string> { "Name", "Email" }
        };

        public ImportValidationService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<string>> ValidateRowAsync(Dictionary<string, string> row, Dictionary<string, string> mappings, string entityType)
        {
            var errors = new List<string>();

            if (!_requiredFields.ContainsKey(entityType))
            {
                errors.Add($"Unknown entity type: {entityType}");
                return errors;
            }

            var requiredFields = _requiredFields[entityType];
            var reverseMappings = mappings.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);

            // Check required fields
            foreach (var requiredField in requiredFields)
            {
                if (!reverseMappings.ContainsKey(requiredField))
                {
                    errors.Add($"Missing required field mapping: {requiredField}");
                    continue;
                }

                var sourceColumn = reverseMappings[requiredField];
                if (!row.ContainsKey(sourceColumn) || string.IsNullOrWhiteSpace(row[sourceColumn]))
                {
                    errors.Add($"Missing required value for field: {requiredField}");
                }
            }

            // Entity-specific validation
            if (entityType == "Contract")
            {
                await ValidateContractRowAsync(row, mappings, reverseMappings, errors);
            }
            else if (entityType == "User")
            {
                await ValidateUserRowAsync(row, mappings, reverseMappings, errors);
            }

            return errors;
        }

        public async Task<Dictionary<int, List<string>>> ValidateAllRowsAsync(List<Dictionary<string, string>> rows, Dictionary<string, string> mappings, string entityType)
        {
            var allErrors = new Dictionary<int, List<string>>();

            for (int i = 0; i < rows.Count; i++)
            {
                var errors = await ValidateRowAsync(rows[i], mappings, entityType);
                if (errors.Any())
                {
                    allErrors[i] = errors;
                }
            }

            return allErrors;
        }

        private async Task ValidateContractRowAsync(Dictionary<string, string> row, Dictionary<string, string> mappings, Dictionary<string, string> reverseMappings, List<string> errors)
        {
            // Validate contract number uniqueness
            if (reverseMappings.ContainsKey("ContractNumber"))
            {
                var contractNumber = row[reverseMappings["ContractNumber"]];
                var exists = await _context.Contracts.AnyAsync(c => c.ContractNumber == contractNumber);
                if (exists)
                {
                    errors.Add($"Contract number already exists: {contractNumber}");
                }
            }

            // Validate total amount is numeric
            if (reverseMappings.ContainsKey("TotalAmount"))
            {
                var amountStr = row[reverseMappings["TotalAmount"]];
                if (!decimal.TryParse(amountStr, out _))
                {
                    errors.Add($"Invalid total amount: {amountStr}");
                }
            }

            // Validate group exists
            if (reverseMappings.ContainsKey("GroupId"))
            {
                var groupIdStr = row[reverseMappings["GroupId"]];
                if (int.TryParse(groupIdStr, out var groupId))
                {
                    var groupExists = await _context.Groups.AnyAsync(g => g.Id == groupId && g.IsActive);
                    if (!groupExists)
                    {
                        errors.Add($"Group not found: {groupId}");
                    }
                }
                else
                {
                    errors.Add($"Invalid group ID: {groupIdStr}");
                }
            }

            // Validate status if provided
            if (reverseMappings.ContainsKey("Status"))
            {
                var status = row[reverseMappings["Status"]].ToLowerInvariant();
                var validStatuses = new[] { "active", "delinquent", "paid_off" };
                if (!validStatuses.Contains(status))
                {
                    errors.Add($"Invalid status: {status}. Must be one of: {string.Join(", ", validStatuses)}");
                }
            }
        }

        private async Task ValidateUserRowAsync(Dictionary<string, string> row, Dictionary<string, string> mappings, Dictionary<string, string> reverseMappings, List<string> errors)
        {
            // Validate email format
            if (reverseMappings.ContainsKey("Email"))
            {
                var email = row[reverseMappings["Email"]];
                if (!IsValidEmail(email))
                {
                    errors.Add($"Invalid email format: {email}");
                }

                // Check email uniqueness
                var emailExists = await _context.Users.AnyAsync(u => u.Email == email);
                if (emailExists)
                {
                    errors.Add($"Email already exists: {email}");
                }
            }
        }

        private bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }
    }
}
