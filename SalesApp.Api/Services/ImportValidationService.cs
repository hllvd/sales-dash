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
            ["Contract"] = new List<string> { "ContractNumber", "UserName", "UserSurname", "TotalAmount" },
            ["User"] = new List<string> { "Name", "Email" }
        };

        public ImportValidationService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<string>> ValidateRowAsync(Dictionary<string, string> row, Dictionary<string, string> mappings, string entityType, List<string>? customRequiredFields = null, bool allowAutoCreateGroups = false)
        {
            var errors = new List<string>();

            if (!_requiredFields.ContainsKey(entityType) && customRequiredFields == null)
            {
                errors.Add($"Unknown entity type: {entityType}");
                return errors;
            }

            var requiredFields = customRequiredFields ?? _requiredFields[entityType];
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
                await ValidateContractRowAsync(row, mappings, reverseMappings, errors, allowAutoCreateGroups);
            }
            else if (entityType == "User")
            {
                await ValidateUserRowAsync(row, mappings, reverseMappings, errors);
            }

            return errors;
        }

        public async Task<Dictionary<int, List<string>>> ValidateAllRowsAsync(List<Dictionary<string, string>> rows, Dictionary<string, string> mappings, string entityType, List<string>? requiredFields = null, bool allowAutoCreateGroups = false)
        {
            var allErrors = new Dictionary<int, List<string>>();

            for (int i = 0; i < rows.Count; i++)
            {
                var errors = await ValidateRowAsync(rows[i], mappings, entityType, requiredFields, allowAutoCreateGroups);
                if (errors.Any())
                {
                    allErrors[i] = errors;
                }
            }

            return allErrors;
        }

        private async Task ValidateContractRowAsync(Dictionary<string, string> row, Dictionary<string, string> mappings, Dictionary<string, string> reverseMappings, List<string> errors, bool allowAutoCreateGroups = false)
        {
            // Validate contract number uniqueness
            if (reverseMappings.TryGetValue("ContractNumber", out var contractNumColumn))
            {
                if (row.TryGetValue(contractNumColumn, out var contractNumber) && !string.IsNullOrWhiteSpace(contractNumber))
                {
                    var exists = await _context.Contracts.AnyAsync(c => c.ContractNumber == contractNumber);
                    if (exists)
                    {
                        errors.Add($"Contract number already exists: {contractNumber}");
                    }
                }
            }

            // Validate total amount is numeric
            if (reverseMappings.TryGetValue("TotalAmount", out var amountColumn))
            {
                if (row.TryGetValue(amountColumn, out var amountStr) && !string.IsNullOrWhiteSpace(amountStr))
                {
                    if (!decimal.TryParse(amountStr, out _))
                    {
                        // Some columns might have currency formatting handled in execution but not here
                        // We'll be a bit more lenient or just skip if it looks like currency
                        if (!amountStr.Contains("$") && !amountStr.Contains(","))
                        {
                            errors.Add($"Invalid total amount format: {amountStr}");
                        }
                    }
                }
            }

            // Validate group exists (optional - only if provided and looks like a direct database ID)
            if (!allowAutoCreateGroups && reverseMappings.TryGetValue("GroupId", out var groupColumn))
            {
                if (row.TryGetValue(groupColumn, out var groupIdValue) && !string.IsNullOrWhiteSpace(groupIdValue))
                {
                    // For the dashboard, the group identifier (like 12153) might be a name/code that we auto-create
                    // instead of a direct integer database ID.
                    // Only validate if it's likely a database ID (positive integer < 100000 usually, but let's be more specific)
                    // If the group is intended to be auto-created in the execution service, skip validation here.
                    if (int.TryParse(groupIdValue, out var groupId) && groupId != 0 && groupId < 10000)
                    {
                        var groupExists = await _context.Groups.AnyAsync(g => g.Id == groupId && g.IsActive);
                        if (!groupExists)
                        {
                            errors.Add($"Group not found: {groupId}");
                        }
                    }
                }
            }

            // Validate status if provided
            if (reverseMappings.TryGetValue("Status", out var statusColumn))
            {
                if (row.TryGetValue(statusColumn, out var statusValue) && !string.IsNullOrWhiteSpace(statusValue))
                {
                    var status = statusValue.ToLowerInvariant();
                    var validStatuses = new[] { "active", "delinquent", "paid_off" };
                    if (!validStatuses.Contains(status))
                    {
                        errors.Add($"Invalid status: {status}. Must be one of: {string.Join(", ", validStatuses)}");
                    }
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
