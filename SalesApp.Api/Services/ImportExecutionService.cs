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

        public ImportExecutionService(
            IContractRepository contractRepository,
            IGroupRepository groupRepository,
            IUserRepository userRepository,
            IRoleRepository roleRepository)
        {
            _contractRepository = contractRepository;
            _groupRepository = groupRepository;
            _userRepository = userRepository;
            _roleRepository = roleRepository;
        }

        public async Task<ImportResult> ExecuteContractImportAsync(
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
                    var contract = await CreateContractFromRowAsync(row, reverseMappings, uploadId);
                    
                    if (contract != null)
                    {
                        result.CreatedContracts.Add(contract);
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

            return result;
        }

        private async Task<Contract?> CreateContractFromRowAsync(
            Dictionary<string, string> row,
            Dictionary<string, string> reverseMappings,
            string uploadId)
        {
            // Extract required fields
            var contractNumber = GetFieldValue(row, reverseMappings, "ContractNumber");
            var userEmail = GetFieldValue(row, reverseMappings, "UserEmail");
            var totalAmountStr = GetFieldValue(row, reverseMappings, "TotalAmount");
            var groupIdStr = GetFieldValue(row, reverseMappings, "GroupId");

            // Validate required fields
            if (string.IsNullOrWhiteSpace(contractNumber) ||
                string.IsNullOrWhiteSpace(userEmail) ||
                string.IsNullOrWhiteSpace(totalAmountStr) ||
                string.IsNullOrWhiteSpace(groupIdStr))
            {
                throw new ArgumentException("Missing required fields");
            }

            // Parse and validate total amount
            if (!decimal.TryParse(totalAmountStr, out var totalAmount))
            {
                throw new ArgumentException($"Invalid total amount: {totalAmountStr}");
            }

            // Parse and validate group ID
            if (!int.TryParse(groupIdStr, out var groupId))
            {
                throw new ArgumentException($"Invalid group ID: {groupIdStr}");
            }

            // Verify group exists
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null || !group.IsActive)
            {
                throw new ArgumentException($"Group not found: {groupId}");
            }

            // Look up user by email
            var user = await _userRepository.GetByEmailAsync(userEmail);
            if (user == null || !user.IsActive)
            {
                throw new ArgumentException($"User not found or inactive: {userEmail}");
            }

            // Extract optional fields
            var status = GetFieldValue(row, reverseMappings, "Status") ?? "active";
            var saleStartDateStr = GetFieldValue(row, reverseMappings, "SaleStartDate");
            var saleEndDateStr = GetFieldValue(row, reverseMappings, "SaleEndDate");

            // Parse dates if provided
            DateTime saleStartDate = DateTime.UtcNow;
            if (!string.IsNullOrWhiteSpace(saleStartDateStr))
            {
                if (!DateTime.TryParse(saleStartDateStr, out saleStartDate))
                {
                    throw new ArgumentException($"Invalid start date: {saleStartDateStr}");
                }
            }

            DateTime? saleEndDate = null;
            if (!string.IsNullOrWhiteSpace(saleEndDateStr))
            {
                if (DateTime.TryParse(saleEndDateStr, out var parsedEndDate))
                {
                    saleEndDate = parsedEndDate;
                }
            }

            // Create contract
            var contract = new Contract
            {
                ContractNumber = contractNumber,
                UserId = user.Id,
                TotalAmount = totalAmount,
                GroupId = groupId,
                Status = status.ToLowerInvariant(),
                SaleStartDate = saleStartDate,
                SaleEndDate = saleEndDate,
                UploadId = uploadId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Save to database
            return await _contractRepository.CreateAsync(contract);
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
            int roleId = 3; // Default User
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
                Matricula = matricula,
                IsMatriculaOwner = isMatriculaOwner,
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
    }
}
