using SalesApp.Models;
using SalesApp.Repositories;

namespace SalesApp.Services
{
    public class ImportExecutionService : IImportExecutionService
    {
        private readonly IContractRepository _contractRepository;
        private readonly IGroupRepository _groupRepository;
        private readonly IUserRepository _userRepository;
        private readonly IRoleRepository _roleRepository;
        private readonly IUserMatriculaRepository _matriculaRepository;

        public ImportExecutionService(
            IContractRepository contractRepository,
            IGroupRepository groupRepository,
            IUserRepository userRepository,
            IRoleRepository roleRepository,
            IUserMatriculaRepository matriculaRepository)
        {
            _contractRepository = contractRepository;
            _groupRepository = groupRepository;
            _userRepository = userRepository;
            _roleRepository = roleRepository;
            _matriculaRepository = matriculaRepository;
        }

        public async Task<ImportResult> ExecuteContractImportAsync(
            string uploadId,
            List<Dictionary<string, string>> rows,
            Dictionary<string, string> mappings,
            string dateFormat)
        {
            var result = new ImportResult
            {
                TotalRows = rows.Count
            };

            // Create reverse mapping (target field -> source column)
            var reverseMappings = mappings.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);
            var contractsToAdd = new List<Contract>();

            // ✅ Phase 1: Build all contracts (validation only, no DB saves)
            for (int i = 0; i < rows.Count; i++)
            {
                try
                {
                    var row = rows[i];
                    var contract = await BuildContractFromRowAsync(row, reverseMappings, uploadId, dateFormat);
                    
                    if (contract != null)
                    {
                        contractsToAdd.Add(contract);
                        result.ProcessedRows++;
                    }
                    else
                    {
                        result.FailedRows++;
                        result.Errors.Add($"Row {i + 1}: Failed to create contract");
                    }
                }
                catch (Exception ex)
                {
                    result.FailedRows++;
                    result.Errors.Add($"Row {i + 1}: {ex.Message}");
                }
            }

            // ✅ Phase 2: Batch insert all valid contracts in single transaction
            if (contractsToAdd.Any())
            {
                try
                {
                    await _contractRepository.CreateBatchAsync(contractsToAdd);
                    result.CreatedContracts = contractsToAdd;
                }
                catch (Exception ex)
                {
                    // If batch fails, mark all as failed
                    result.FailedRows += contractsToAdd.Count;
                    result.ProcessedRows -= contractsToAdd.Count;
                    result.Errors.Add($"Batch insert failed: {ex.Message}");
                    result.CreatedContracts.Clear();
                }
            }

            return result;
        }

        private async Task<Contract?> BuildContractFromRowAsync(
            Dictionary<string, string> row,
            Dictionary<string, string> reverseMappings,
            string uploadId,
            string dateFormat)
        {
            // Extract required fields
            var contractNumber = GetFieldValue(row, reverseMappings, "ContractNumber");
            var userEmail = GetFieldValue(row, reverseMappings, "UserEmail");
            var totalAmountStr = GetFieldValue(row, reverseMappings, "TotalAmount");
            var groupIdStr = GetFieldValue(row, reverseMappings, "GroupId");

            // Validate required fields
            // Validate required fields
            if (string.IsNullOrWhiteSpace(contractNumber) ||
                string.IsNullOrWhiteSpace(userEmail) ||
                string.IsNullOrWhiteSpace(totalAmountStr))
            {
                throw new ArgumentException("Missing required fields");
            }

            // Parse and validate total amount
            if (!TryParseBrazilianCurrency(totalAmountStr, out var totalAmount))
            {
                throw new ArgumentException($"Invalid total amount: {totalAmountStr}");
            }
            
            // Parse and validate group ID (optional - defaults to null)
            int? groupId = null;
            if (!string.IsNullOrWhiteSpace(groupIdStr))
            {
                if (!int.TryParse(groupIdStr, out var parsedGroupId))
                {
                    throw new ArgumentException($"Invalid group ID: {groupIdStr}");
                }
                groupId = parsedGroupId;
            }

            // Verify group exists only if groupId is provided
            if (groupId.HasValue)
            {
                var group = await _groupRepository.GetByIdAsync(groupId.Value);
                if (group == null || !group.IsActive)
                {
                    throw new ArgumentException($"Group not found: {groupId}");
                }
            }

            // Look up user by email
            var user = await _userRepository.GetByEmailAsync(userEmail);
            if (user == null || !user.IsActive)
            {
                throw new ArgumentException($"User not found or inactive: {userEmail}");
            }

            // Extract optional fields
            var statusInput = GetFieldValue(row, reverseMappings, "Status");
            var status = ContractStatusMapper.MapStatus(statusInput) ?? "Active";
            var saleStartDateStr = GetFieldValue(row, reverseMappings, "SaleStartDate");
            var contractTypeStr = GetFieldValue(row, reverseMappings, "ContractType");
            var quotaStr = GetFieldValue(row, reverseMappings, "Quota");
            var pvIdStr = GetFieldValue(row, reverseMappings, "PvId");
            var customerName = GetFieldValue(row, reverseMappings, "CustomerName");

            // Parse dates if provided
            DateTime saleStartDate = DateTime.UtcNow;
            if (!string.IsNullOrWhiteSpace(saleStartDateStr))
            {
                if (!TryParseFlexibleDate(saleStartDateStr, out saleStartDate))
                {
                    throw new ArgumentException($"Invalid start date: {saleStartDateStr}");
                }
            }

            // Parse ContractType - try string first, then fallback to int for backwards compatibility
            int? contractType = null;
            if (!string.IsNullOrWhiteSpace(contractTypeStr))
            {
                try
                {
                    // Try parsing as string ("lar" or "motores")
                    contractType = ContractTypeExtensions.FromApiStringToInt(contractTypeStr);
                }
                catch (ArgumentException)
                {
                    // Fallback to int parsing for backwards compatibility
                    if (int.TryParse(contractTypeStr, out var parsedType))
                    {
                        contractType = parsedType;
                    }
                }
            }

            // Parse Quota
            int? quota = null;
            if (!string.IsNullOrWhiteSpace(quotaStr))
            {
                if (int.TryParse(quotaStr, out var parsedQuota))
                {
                    quota = parsedQuota;
                }
            }

            // Parse PvId
            int? pvId = null;
            if (!string.IsNullOrWhiteSpace(pvIdStr))
            {
                if (int.TryParse(pvIdStr, out var parsedPvId))
                {
                    pvId = parsedPvId;
                }
            }

            // Extract and validate matricula (optional)
            var matriculaNumber = GetFieldValue(row, reverseMappings, "MatriculaNumber");
            int? matriculaId = null;
            
            if (!string.IsNullOrWhiteSpace(matriculaNumber))
            {
                var matricula = await _matriculaRepository.GetByMatriculaNumberAsync(matriculaNumber);
                
                if (matricula == null)
                {
                    throw new ArgumentException($"Matricula '{matriculaNumber}' not found");
                }
                
                if (!matricula.IsActive)
                {
                    throw new ArgumentException($"Matricula '{matriculaNumber}' is not active");
                }
                
                if (matricula.UserId != user.Id)
                {
                    throw new ArgumentException($"Matricula '{matriculaNumber}' does not belong to user {userEmail}");
                }
                
                matriculaId = matricula.Id;
            }

            // ✅ Create contract object (don't save yet)
            var contract = new Contract
            {
                ContractNumber = contractNumber,
                UserId = user.Id,
                TotalAmount = totalAmount,
                GroupId = groupId,
                Status = status,
                SaleStartDate = saleStartDate,
                UploadId = uploadId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                ContractType = contractType,
                Quota = quota,
                PvId = pvId,
                CustomerName = customerName,
                MatriculaId = matriculaId
            };

            return contract;
        }

        public async Task<ImportResult> ExecuteUserImportAsync(
            string uploadId,
            List<Dictionary<string, string>> rows,
            Dictionary<string, string> mappings)
        {
            var result = new ImportResult
            {
                TotalRows = rows.Count
            };

            // Create reverse mapping (target field -> source column)
            var reverseMappings = mappings.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);

            for (int i = 0; i < rows.Count; i++)
            {
                try
                {
                    var row = rows[i];
                    var user = await CreateUserFromRowAsync(row, reverseMappings);
                    
                    if (user != null)
                    {
                        result.CreatedUsers.Add(user);
                        result.ProcessedRows++;
                    }
                    else
                    {
                        result.FailedRows++;
                        result.Errors.Add($"Row {i + 1}: Failed to create user");
                    }
                }
                catch (Exception ex)
                {
                    result.FailedRows++;
                    result.Errors.Add($"Row {i + 1}: {ex.Message}");
                }
            }

            return result;
        }

        private async Task<User?> CreateUserFromRowAsync(
            Dictionary<string, string> row,
            Dictionary<string, string> reverseMappings)
        {
            // Extract required fields
            var name = GetFieldValue(row, reverseMappings, "Name");
            var email = GetFieldValue(row, reverseMappings, "Email");

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email))
            {
                throw new ArgumentException("Missing required fields");
            }

            // Check if email exists
            if (await _userRepository.GetByEmailAsync(email) != null)
            {
                throw new ArgumentException($"User with email {email} already exists");
            }

            // Extract optional fields
            var surname = GetFieldValue(row, reverseMappings, "Surname");
            var roleName = GetFieldValue(row, reverseMappings, "Role");
            var parentEmail = GetFieldValue(row, reverseMappings, "ParentEmail");

            // Combine name and surname if surname exists
            var fullName = name;
            if (!string.IsNullOrWhiteSpace(surname))
            {
                fullName = $"{name} {surname}".Trim();
            }

            // Resolve Role
            int roleId = (int)Models.RoleId.User; // Default User
            if (!string.IsNullOrWhiteSpace(roleName))
            {
                var role = await _roleRepository.GetByNameAsync(roleName);
                if (role != null)
                {
                    roleId = role.Id;
                }
            }

            // Resolve Parent
            Guid? parentId = null;
            if (!string.IsNullOrWhiteSpace(parentEmail))
            {
                var parent = await _userRepository.GetByEmailAsync(parentEmail);
                if (parent != null)
                {
                    parentId = parent.Id;
                }
            }

            // Extract Matricula fields
            var matricula = GetFieldValue(row, reverseMappings, "Matricula");
            var isMatriculaOwnerStr = GetFieldValue(row, reverseMappings, "IsMatriculaOwner");
            bool isMatriculaOwner = false;
            
            if (!string.IsNullOrWhiteSpace(isMatriculaOwnerStr))
            {
                var val = isMatriculaOwnerStr.Trim().ToLowerInvariant();
                isMatriculaOwner = val == "true" || val == "1" || val == "yes" || val == "sim";
            }
            // Create User
            var user = new User
            {
                Name = fullName,
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("ChangeMe123!"), // Default password
                RoleId = roleId,
                ParentUserId = parentId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            return await _userRepository.CreateAsync(user);
        }

        private string? GetFieldValue(
            Dictionary<string, string> row,
            Dictionary<string, string> reverseMappings,
            string targetField)
        {
            if (!reverseMappings.ContainsKey(targetField))
            {
                return null;
            }

            var sourceColumn = reverseMappings[targetField];
            if (!row.ContainsKey(sourceColumn))
            {
                return null;
            }

            return row[sourceColumn]?.Trim();
        }

        private bool TryParseBrazilianCurrency(string? input, out decimal result)
        {
            result = 0;
            
            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            try
            {
                // Remove currency symbol and whitespace
                var cleaned = input.Trim()
                    .Replace("R$", "")
                    .Replace("$", "")
                    .Trim();

                // Handle both Brazilian (100.000,00) and US (100,000.00) formats
                // Count dots and commas to determine format
                int dotCount = cleaned.Count(c => c == '.');
                int commaCount = cleaned.Count(c => c == ',');

                if (dotCount > 1 || commaCount > 1)
                {
                    // Multiple separators - likely Brazilian format with thousand separators
                    // Brazilian: 100.000,00 or 1.000.000,00
                    cleaned = cleaned.Replace(".", "").Replace(",", ".");
                }
                else if (dotCount == 1 && commaCount == 1)
                {
                    // Both separators present - determine which is decimal
                    int lastDotIndex = cleaned.LastIndexOf('.');
                    int lastCommaIndex = cleaned.LastIndexOf(',');
                    
                    if (lastCommaIndex > lastDotIndex)
                    {
                        // Brazilian format: 1.000,00
                        cleaned = cleaned.Replace(".", "").Replace(",", ".");
                    }
                    else
                    {
                        // US format: 1,000.00
                        cleaned = cleaned.Replace(",", "");
                    }
                }
                else if (commaCount == 1 && dotCount == 0)
                {
                    // Only comma - check if it's decimal separator or thousand separator
                    int commaIndex = cleaned.IndexOf(',');
                    int digitsAfterComma = cleaned.Length - commaIndex - 1;
                    
                    if (digitsAfterComma == 2)
                    {
                        // Likely decimal separator: 100,00
                        cleaned = cleaned.Replace(",", ".");
                    }
                    else
                    {
                        // Likely thousand separator: 1,000
                        cleaned = cleaned.Replace(",", "");
                    }
                }
                // If only dots or no separators, use as-is

                return decimal.TryParse(cleaned, System.Globalization.NumberStyles.Number, 
                    System.Globalization.CultureInfo.InvariantCulture, out result);
            }
            catch
            {
                return false;
            }
        }

        private bool TryParseFlexibleDate(string? input, out DateTime result)
        {
            result = DateTime.MinValue;
            
            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            var cleanedInput = input.Trim();

            // Try multiple date formats
            var formats = new[]
            {
                "MM/dd/yyyy",      // US format: 08/31/2025
                "M/d/yyyy",        // US format without leading zeros: 8/31/2025
                "dd/MM/yyyy",      // European/Brazilian format: 31/08/2025
                "d/M/yyyy",        // European/Brazilian format without leading zeros: 31/8/2025
                "yyyy-MM-dd",      // ISO format: 2025-08-31
                "yyyy/MM/dd",      // ISO format with slashes: 2025/08/31
                "MM-dd-yyyy",      // US format with dashes: 08-31-2025
                "dd-MM-yyyy",      // European format with dashes: 31-08-2025
            };

            // Try parsing with explicit formats
            if (DateTime.TryParseExact(cleanedInput, formats, 
                System.Globalization.CultureInfo.InvariantCulture, 
                System.Globalization.DateTimeStyles.None, out result))
            {
                return true;
            }

            // Fallback to general parsing
            return DateTime.TryParse(cleanedInput, 
                System.Globalization.CultureInfo.InvariantCulture, 
                System.Globalization.DateTimeStyles.None, out result);
        }
    }
}
