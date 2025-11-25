using SalesApp.Models;
using SalesApp.Repositories;

namespace SalesApp.Services
{
    public class ImportExecutionService : IImportExecutionService
    {
        private readonly IContractRepository _contractRepository;
        private readonly IGroupRepository _groupRepository;

        public ImportExecutionService(
            IContractRepository contractRepository,
            IGroupRepository groupRepository)
        {
            _contractRepository = contractRepository;
            _groupRepository = groupRepository;
        }

        public async Task<ImportResult> ExecuteContractImportAsync(
            string uploadId,
            List<Dictionary<string, string>> rows,
            Dictionary<string, string> mappings,
            Dictionary<string, Guid> userMappings)
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
                    var contract = await CreateContractFromRowAsync(row, reverseMappings, userMappings, uploadId);
                    
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
            Dictionary<string, Guid> userMappings,
            string uploadId)
        {
            // Extract required fields
            var contractNumber = GetFieldValue(row, reverseMappings, "ContractNumber");
            var userName = GetFieldValue(row, reverseMappings, "UserName");
            var userSurname = GetFieldValue(row, reverseMappings, "UserSurname");
            var totalAmountStr = GetFieldValue(row, reverseMappings, "TotalAmount");
            var groupIdStr = GetFieldValue(row, reverseMappings, "GroupId");

            // Validate required fields
            if (string.IsNullOrWhiteSpace(contractNumber) ||
                string.IsNullOrWhiteSpace(userName) ||
                string.IsNullOrWhiteSpace(userSurname) ||
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

            // Get user ID from mappings
            var userKey = $"{userName}|{userSurname}";
            if (!userMappings.ContainsKey(userKey))
            {
                throw new ArgumentException($"User not mapped: {userName} {userSurname}");
            }
            var userId = userMappings[userKey];

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
                UserId = userId,
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
