using SalesApp.Models;
using SalesApp.Repositories;
using SalesApp.Data;
using Microsoft.EntityFrameworkCore;

namespace SalesApp.Services
{
    public class ImportExecutionService : IImportExecutionService
    {
        private readonly IContractRepository _contractRepository;
        private readonly IGroupRepository _groupRepository;
        private readonly IUserRepository _userRepository;
        private readonly IRoleRepository _roleRepository;
        private readonly IUserMatriculaRepository _matriculaRepository;
        private readonly IEmailService _emailService;
        private readonly AppDbContext _context;
        private readonly IContractMetadataRepository _metadataRepository;

        public ImportExecutionService(
            IContractRepository contractRepository,
            IGroupRepository groupRepository,
            IUserRepository userRepository,
            IRoleRepository roleRepository,
            IUserMatriculaRepository matriculaRepository,
            IEmailService emailService,
            AppDbContext context,
            IContractMetadataRepository metadataRepository)
        {
            _contractRepository = contractRepository;
            _groupRepository = groupRepository;
            _userRepository = userRepository;
            _roleRepository = roleRepository;
            _matriculaRepository = matriculaRepository;
            _emailService = emailService;
            _context = context;
            _metadataRepository = metadataRepository;
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

            // Dictionary to cache group name/ID lookups during this import session
            var groupCache = new Dictionary<string, int?>();

            // ✅ Phase 1: Build all contracts (validation only, no DB saves)
            for (int i = 0; i < rows.Count; i++)
            {
                try
                {
                    var row = rows[i];
                    var contract = await BuildContractFromRowAsync(row, reverseMappings, uploadId, dateFormat, groupCache, result);
                    
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
            string dateFormat,
            Dictionary<string, int?> groupCache,
            ImportResult result)
        {
            // Extract required fields
            var contractNumber = GetFieldValue(row, reverseMappings, "ContractNumber");
            var userEmail = GetFieldValue(row, reverseMappings, "UserEmail");
            var totalAmountStr = GetFieldValue(row, reverseMappings, "TotalAmount");
            var groupValue = GetFieldValue(row, reverseMappings, "GroupId");

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
            
            // Resolve Group ID from name or ID value
            var groupId = await ResolveGroupIdAsync(groupValue, groupCache, result);

            // Verify group exists if a value was provided but resolution failed
            if (!string.IsNullOrWhiteSpace(groupValue) && !groupId.HasValue)
            {
                throw new ArgumentException($"Group not found: {groupValue}");
            }

            // Look up user by email
            var user = await _userRepository.GetByEmailAsync(userEmail);
            if (user == null || !user.IsActive)
            {
                throw new ArgumentException($"User not found or inactive: {userEmail}");
            }

            // Extract optional fields
            var statusInput = GetFieldValue(row, reverseMappings, "Status");
            // ✅ Use enum for default status
            var status = ContractStatusMapper.MapStatus(statusInput) ?? ContractStatus.Active.ToApiString();
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
                CustomerName = customerName
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
            
            // Extract SendEmail field
            var sendEmailStr = GetFieldValue(row, reverseMappings, "SendEmail");
            bool sendEmail = ParseBooleanValue(sendEmailStr);
            
            // Create User
            var defaultPassword = "ChangeMe123!";
            var user = new User
            {
                Name = fullName,
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(defaultPassword),
                RoleId = roleId,
                ParentUserId = parentId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var createdUser = await _userRepository.CreateAsync(user);
            
            // Handle matricula assignment if provided
            if (createdUser != null && !string.IsNullOrWhiteSpace(matricula))
            {
                try
                {
                    var userMatricula = new UserMatricula
                    {
                        UserId = createdUser.Id,
                        MatriculaNumber = matricula,
                        StartDate = DateTime.UtcNow,
                        IsOwner = isMatriculaOwner,
                        IsActive = true,
                        Status = MatriculaStatus.Active.ToApiString()
                    };
                    
                    await _matriculaRepository.CreateAsync(userMatricula);
                }
                catch (InvalidOperationException ex)
                {
                    // Log but don't fail user creation - the user *was* created
                    // The error will be handled by the caller of this method if we rethrow or handle it here
                    // Given the loop in ExecuteUserImportAsync, we should probably throw a specific exception 
                    // or just let this one bubble up to be caught by the row-level try-catch.
                    throw new ArgumentException($"User created, but matricula failed: {ex.Message}");
                }
                catch (Exception ex)
                {
                    throw new ArgumentException($"User created, but matricula failed: {ex.Message}");
                }
            }
            
            // Send welcome email if requested
            if (sendEmail && createdUser != null)
            {
                try
                {
                    await _emailService.SendWelcomeEmailAsync(createdUser.Email, createdUser.Name, defaultPassword);
                }
                catch (Exception ex)
                {
                    // Log but don't fail import
                    Console.WriteLine($"[ImportExecutionService] Failed to send welcome email to {createdUser.Email}: {ex.Message}");
                }
            }
            
            return createdUser;
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
        
        private bool ParseBooleanValue(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }
            
            var normalized = value.Trim().ToLowerInvariant();
            return normalized == "true" || 
                   normalized == "1" || 
                   normalized == "yes" || 
                   normalized == "sim" ||
                   normalized == "y" ||
                   normalized == "s";
        }
        
        public async Task<ImportResult> ExecuteContractDashboardImportAsync(
            string uploadId,
            List<Dictionary<string, string>> rows,
            Dictionary<string, string> mappings)
        {
            var result = new ImportResult
            {
                TotalRows = rows.Count
            };

            var reverseMappings = mappings.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);
            var contractsToAdd = new List<Contract>();
            var groupCache = new Dictionary<string, int?>();

            for (int i = 0; i < rows.Count; i++)
            {
                try
                {
                    var row = rows[i];
                    var contract = await BuildContractDashboardFromRowAsync(row, reverseMappings, uploadId, groupCache, result);
                    
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

            if (contractsToAdd.Any())
            {
                try
                {
                    await _contractRepository.CreateBatchAsync(contractsToAdd);
                    result.CreatedContracts = contractsToAdd;
                }
                catch (Exception ex)
                {
                    result.FailedRows += contractsToAdd.Count;
                    result.ProcessedRows -= contractsToAdd.Count;
                    result.Errors.Add($"Batch insert failed: {ex.Message}");
                    result.CreatedContracts.Clear();
                }
            }

            return result;
        }
        
        private async Task<Contract?> BuildContractDashboardFromRowAsync(
            Dictionary<string, string> row,
            Dictionary<string, string> reverseMappings,
            string uploadId,
            Dictionary<string, int?> groupCache,
            ImportResult result)
        {
            // Try to get fields directly first (may be mapped from virtual columns like cota.group, etc.)
            var contractNumber = GetFieldValue(row, reverseMappings, "ContractNumber");
            var customerName = GetFieldValue(row, reverseMappings, "CustomerName");
            
            var groupValue = GetFieldValue(row, reverseMappings, "GroupId");
            var quotaStr = GetFieldValue(row, reverseMappings, "Quota");

            // Fallback to Cota split only if critical fields are missing AND it actually looks like the grouped format
            if (string.IsNullOrWhiteSpace(contractNumber) || string.IsNullOrWhiteSpace(customerName) || string.IsNullOrWhiteSpace(groupValue) || string.IsNullOrWhiteSpace(quotaStr))
            {
                // Look for "Cota" column directly in row if not mapped, or from mappings
                var cotaValue = GetFieldValue(row, reverseMappings, "Cota");
                if (string.IsNullOrWhiteSpace(cotaValue))
                {
                    // Internal check: search for any column named "Cota" regardless of mapping
                    var cotaKey = row.Keys.FirstOrDefault(k => k.Equals("Cota", StringComparison.OrdinalIgnoreCase));
                    if (cotaKey != null) cotaValue = row[cotaKey];
                }

                if (!string.IsNullOrWhiteSpace(cotaValue) && cotaValue.Contains(";"))
                {
                    var cotaParts = cotaValue.Split(';');
                    if (cotaParts.Length >= 5)
                    {
                        // Safely fallback to values from Cota split if they weren't mapped directly
                        if (string.IsNullOrWhiteSpace(contractNumber)) contractNumber = cotaParts[^1].Trim();
                        if (string.IsNullOrWhiteSpace(customerName)) customerName = cotaParts[3].Trim();
                        if (string.IsNullOrWhiteSpace(groupValue)) groupValue = cotaParts[0].Trim();
                        if (string.IsNullOrWhiteSpace(quotaStr)) quotaStr = cotaParts[1].Trim();
                    }
                }
            }
            
            // Resolve Group ID
            var groupId = await ResolveGroupIdAsync(groupValue, groupCache, result);

            // Resolve Quota (numeric)
            int? quota = null;
            if (!string.IsNullOrWhiteSpace(quotaStr) && int.TryParse(quotaStr, out var parsedQuota))
            {
                quota = parsedQuota;
            }
            
            // Final validation for required data after all fallback attempts
            if (string.IsNullOrWhiteSpace(contractNumber)) throw new ArgumentException("Contract Number is required");
            if (!groupId.HasValue) throw new ArgumentException($"Group not found or required: {groupValue}");
            if (!quota.HasValue) throw new ArgumentException("Quota is required");
            
            // Parse TotalAmount
            var totalAmountStr = GetFieldValue(row, reverseMappings, "TotalAmount");
            if (!TryParseBrazilianCurrency(totalAmountStr, out var totalAmount))
            {
                throw new ArgumentException($"Invalid Total Amount: '{totalAmountStr}' (empty or invalid format)");
            }
            
            // Parse SaleStartDate - supports both Excel serial numbers and formatted dates
            var saleStartDateStr = GetFieldValue(row, reverseMappings, "SaleStartDate");
            DateTime saleStartDate;
            
            // Try parsing as Excel serial number first (e.g., 45747)
            if (double.TryParse(saleStartDateStr, System.Globalization.NumberStyles.Any, 
                System.Globalization.CultureInfo.InvariantCulture, out var excelDate))
            {
                // Excel dates are days since 1900-01-01 (with a leap year bug, so we use 1899-12-30)
                saleStartDate = new DateTime(1899, 12, 30).AddDays(excelDate);
            }
            // Try parsing as YYYY-MM-DD
            else if (!DateTime.TryParseExact(saleStartDateStr, "yyyy-MM-dd", 
                System.Globalization.CultureInfo.InvariantCulture, 
                System.Globalization.DateTimeStyles.None, out saleStartDate))
            {
                throw new ArgumentException($"Invalid Sale Start Date: '{saleStartDateStr}'");
            }
            
            // Parse Version
            var versionStr = GetFieldValue(row, reverseMappings, "Version");
            byte? version = null;
            if (!string.IsNullOrWhiteSpace(versionStr) && byte.TryParse(versionStr, out var parsedVersion))
            {
                version = parsedVersion;
            }
            
            // Get TempMatricula
            var tempMatricula = GetFieldValue(row, reverseMappings, "TempMatricula");
            
            // Parse PvId
            var pvIdStr = GetFieldValue(row, reverseMappings, "PvId");
            int? pvId = null;
            if (!string.IsNullOrWhiteSpace(pvIdStr) && int.TryParse(pvIdStr, out var parsedPvId))
            {
                // Validate PV exists
                var pvExists = await _context.PVs.AnyAsync(p => p.Id == parsedPvId);
                if (!pvExists)
                {
                    throw new ArgumentException($"PV (Point of Sale) not found: {parsedPvId}");
                }
                pvId = parsedPvId;
            }
            
            // Map Status
            var statusStr = GetFieldValue(row, reverseMappings, "Status");
            var status = MapSituacaoCobrancaToStatus(statusStr);
            
            // Handle Category metadata
            int? categoryMetadataId = null;
            var categoryValue = GetFieldValue(row, reverseMappings, "Category");
            if (!string.IsNullOrWhiteSpace(categoryValue))
            {
                var categoryMetadata = await GetOrCreateMetadataAsync("Category", categoryValue);
                categoryMetadataId = categoryMetadata.Id;
            }
            
            // Handle PlanoVenda metadata
            int? planoVendaMetadataId = null;
            var planoVendaValue = GetFieldValue(row, reverseMappings, "PlanoVenda");
            if (!string.IsNullOrWhiteSpace(planoVendaValue))
            {
                var planoVendaMetadata = await GetOrCreateMetadataAsync("PlanoVenda", planoVendaValue);
                planoVendaMetadataId = planoVendaMetadata.Id;
            }
            
            // Create contract (UserId is null as per requirements)
            var contract = new Contract
            {
                ContractNumber = contractNumber,
                UserId = null,
                TotalAmount = totalAmount,
                GroupId = groupId,
                Status = status,
                SaleStartDate = saleStartDate,
                UploadId = uploadId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CustomerName = customerName,
                PvId = pvId,
                Quota = quota,
                Version = version,
                TempMatricula = tempMatricula,
                CategoryMetadataId = categoryMetadataId,
                PlanoVendaMetadataId = planoVendaMetadataId
            };

            return contract;
        }
        
        private string MapSituacaoCobrancaToStatus(string? situacaoCobranca)
        {
            if (string.IsNullOrWhiteSpace(situacaoCobranca))
            {
                return ContractStatus.Active.ToApiString();
            }
            
            var normalized = situacaoCobranca.Trim().ToUpperInvariant();
            
            return normalized switch
            {
                "SUJ. A CANCELAMENTO" or "SUJ. A  CANCELAMENTO" => "late3",
                "NCONT 1 AT" => "late1",
                "NCONT 2 AT" => "late2",
                "NORMAL" => ContractStatus.Active.ToApiString(),
                "EXCLUIDO" => ContractStatus.Defaulted.ToApiString(),
                "DESISTENTE" => "canceled",
                _ => ContractStatus.Active.ToApiString()
            };
        }
        
        private async Task<int?> ResolveGroupIdAsync(string? groupValue, Dictionary<string, int?> cache, ImportResult? result = null)
        {
            if (string.IsNullOrWhiteSpace(groupValue)) return null;
            
            if (cache.TryGetValue(groupValue, out var cachedId)) return cachedId;
            
            // 1. Try lookup by Name (smart case or exact)
            var groupByName = await _groupRepository.GetByNameAsync(groupValue.Trim());
            if (groupByName != null)
            {
                cache[groupValue] = groupByName.Id;
                return groupByName.Id;
            }
            
            // 2. Try lookup by ID if numeric
            if (int.TryParse(groupValue.Trim(), out var id))
            {
                var groupById = await _groupRepository.GetByIdAsync(id);
                if (groupById != null)
                {
                    cache[groupValue] = groupById.Id;
                    return groupById.Id;
                }
            }
            
            // 3. Automatic Creation (Only if it's a name, not just a failed numeric lookup)
            // If the user provided a value that looks like a name (not just an ID that doesn't exist)
            // Or if they provided something and want it created regardless.
            // We'll create it if it's not numeric or if it's a numeric that's NOT found.
            try
            {
                var newGroup = new Group
                {
                    Name = groupValue.Trim(),
                    Description = $"Auto-created during import {DateTime.UtcNow:yyyy-MM-dd}",
                    Commission = 0,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                
                var createdGroup = await _groupRepository.CreateAsync(newGroup);
                cache[groupValue] = createdGroup.Id;
                
                // Track for UI
                if (result != null && !result.CreatedGroups.Contains(groupValue.Trim()))
                {
                    result.CreatedGroups.Add(groupValue.Trim());
                }
                
                return createdGroup.Id;
            }
            catch (Exception)
            {
                // Fallback to null if creation fails (e.g. unique constraint if name just popped up)
                cache[groupValue] = null;
                return null;
            }
        }

        private async Task<ContractMetadata> GetOrCreateMetadataAsync(string name, string value)
        {
            var existing = await _metadataRepository.GetByNameAndValueAsync(name, value);
            if (existing != null)
            {
                return existing;
            }
            
            var newMetadata = new ContractMetadata
            {
                Name = name,
                Value = value,
                CreatedAt = DateTime.UtcNow
            };
            
            return await _metadataRepository.CreateAsync(newMetadata);
        }
    }
}
