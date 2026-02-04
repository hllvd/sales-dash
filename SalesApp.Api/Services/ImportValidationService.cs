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

        public async Task<List<string>> ValidateRowAsync(Dictionary<string, string> row, Dictionary<string, string> mappings, string entityType, List<string>? customRequiredFields = null, bool allowAutoCreateGroups = false, bool allowAutoCreatePVs = false, bool skipMissingContractNumber = false)
        {
            var errors = new List<string>();

            if (!_requiredFields.ContainsKey(entityType) && customRequiredFields == null)
            {
                errors.Add($"Unknown entity type: {entityType}");
                return errors;
            }

            var requiredFields = customRequiredFields ?? _requiredFields[entityType];
            var reverseMappings = mappings.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);

            var startDateCol = reverseMappings.GetValueOrDefault("SaleStartDate");
            bool shouldSilentSkipDate = false;

            if (entityType.Equals("Contract", StringComparison.OrdinalIgnoreCase) && startDateCol != null)
            {
                if (!row.ContainsKey(startDateCol) || string.IsNullOrWhiteSpace(row[startDateCol]))
                {
                    shouldSilentSkipDate = true;
                }
            }

            // ✅ SILENT SKIP: mandatory for SaleStartDate (Requested by user)
            // If it was missing but not even in the required fields list, it wouldn't be in errors yet.
            // But we want to skip the row ENTIRELY now before doing any other validation.
            if (shouldSilentSkipDate)
            {
                return new List<string>(); // Silent skip
            }

            // ✅ SILENT SKIP: mandatory for Cota (Requested by user) - and MUST have contract number
            if (entityType.Equals("Contract", StringComparison.OrdinalIgnoreCase) && reverseMappings.TryGetValue("Cota", out var cotaCol))
            {
                var cotaValue = row.GetValueOrDefault(cotaCol);
                if (string.IsNullOrWhiteSpace(cotaValue))
                {
                    return new List<string>(); // Skip blank Cota
                }

                var cnCol = reverseMappings.GetValueOrDefault("ContractNumber");
                bool hasDirectContractNumber = cnCol != null && 
                                             row.ContainsKey(cnCol) && 
                                             !string.IsNullOrWhiteSpace(row[cnCol]);

                if (!hasDirectContractNumber)
                {
                    // If no direct mapping, Cota MUST have 5+ parts
                    if (!cotaValue.Contains(";") || cotaValue.Split(';').Length < 5)
                    {
                        return new List<string>(); // Skip invalid Cota format
                    }
                }
            }

            // ✅ SILENT SKIP: If skipMissingContractNumber is true and ContractNumber is missing, skip the row
            // MUST be checked early to correctly skip the row without adding validation errors
            if (skipMissingContractNumber && entityType.Equals("Contract", StringComparison.OrdinalIgnoreCase))
            {
                var cnCol = reverseMappings.GetValueOrDefault("ContractNumber");
                var cotaColForCheck = reverseMappings.GetValueOrDefault("Cota");
                
                bool hasContractNumber = cnCol != null && row.ContainsKey(cnCol) && !string.IsNullOrWhiteSpace(row[cnCol]);
                bool hasCota = cotaColForCheck != null && row.ContainsKey(cotaColForCheck) && !string.IsNullOrWhiteSpace(row[cotaColForCheck]);

                if (!hasContractNumber && !hasCota)
                {
                    return new List<string>(); // Silent skip
                }
            }

            // Check required fields
            foreach (var requiredField in requiredFields)
            {
                if (!reverseMappings.ContainsKey(requiredField))
                {
                    // ✅ DASHBOARD EXCEPTION: If Cota is mapped, we can derive ContractNumber, Quota, etc.
                    if (reverseMappings.ContainsKey("Cota") && 
                        (requiredField == "ContractNumber" || requiredField == "Quota" || requiredField == "CustomerName" || requiredField == "GroupId"))
                    {
                        continue;
                    }

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
            if (entityType.Equals("Contract", StringComparison.OrdinalIgnoreCase))
            {
                await ValidateContractRowAsync(row, mappings, reverseMappings, errors, allowAutoCreateGroups, allowAutoCreatePVs);
            }
            else if (entityType.Equals("User", StringComparison.OrdinalIgnoreCase))
            {
                await ValidateUserRowAsync(row, mappings, reverseMappings, errors);
            }

            return errors;
        }

        public async Task<Dictionary<int, List<string>>> ValidateAllRowsAsync(List<Dictionary<string, string>> rows, Dictionary<string, string> mappings, string entityType, List<string>? requiredFields = null, bool allowAutoCreateGroups = false, bool allowAutoCreatePVs = false, bool skipMissingContractNumber = false)
        {
            var allErrors = new Dictionary<int, List<string>>();

            for (int i = 0; i < rows.Count; i++)
            {
                var errors = await ValidateRowAsync(rows[i], mappings, entityType, requiredFields, allowAutoCreateGroups, allowAutoCreatePVs, skipMissingContractNumber);
                if (errors.Any())
                {
                    allErrors[i] = errors;
                }
            }

            return allErrors;
        }

        private async Task ValidateContractRowAsync(Dictionary<string, string> row, Dictionary<string, string> mappings, Dictionary<string, string> reverseMappings, List<string> errors, bool allowAutoCreateGroups = false, bool allowAutoCreatePVs = false)
        {
// Validate contract number uniqueness
            // REMOVED for Upsert logic: existing contracts will be updated instead of rejected.

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
                    var status = NormalizeStatus(statusValue);
                    var validStatuses = new[] { "active", "late1", "late2", "late3", "defaulted" };
                    if (!validStatuses.Contains(status))
                    {
                        errors.Add($"Invalid status: {statusValue} (normalized: {status}). Must be one of: {string.Join(", ", validStatuses)}");
                    }
                }
            }
        }

        private string NormalizeStatus(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "active";

            var normalized = value.Trim().ToUpperInvariant();

            return normalized switch
            {
                "NORMAL" => "active",
                "NCONT 1 AT" or "CONT 1 ATR" => "late1",
                "NCONT 2 AT" or "CONT NÃO ENTREGUE 2 ATR" or "CONT NAO ENTREGUE 2 ATR" or "CONT BEM PEND 2 ATR" => "late2",
                "NCONT 3 AT" or "SUJ. A CANCELAMENTO" or "SUJ. A  CANCELAMENTO" => "late3",
                "EXCLUIDO" or "DESISTENTE" => "defaulted",
                _ => normalized.ToLowerInvariant()
            };
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
