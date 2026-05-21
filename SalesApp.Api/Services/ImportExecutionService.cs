using SalesApp.Models;
using SalesApp.Repositories;
using SalesApp.Data;
using Microsoft.EntityFrameworkCore;
using SalesApp.Libs;
using SalesApp.Utils;

namespace SalesApp.Services
{
    public class ImportExecutionService : IImportExecutionService
    {
        private readonly IContractRepository _contractRepository;
        private readonly IGroupRepository _groupRepository;
        private readonly IUserRepository _userRepository;
        private readonly IRoleRepository _roleRepository;
        private readonly IUserMatriculaRepository _userMatriculaRepository;
        private readonly IMatriculaRepository _matriculaRepository;
        private readonly IEmailService _emailService;
        private readonly AppDbContext _context;
        private readonly IContractMetadataRepository _metadataRepository;
        private readonly IPVRepository _pvRepository;
        private readonly IContractStatusMapper _statusMapper;
        private readonly IContractStatusService _statusService;
        private readonly IImportErrorService _errorService;
        private readonly IPendingClaimService _pendingClaimService;

        public ImportExecutionService(
            IContractRepository contractRepository,
            IGroupRepository groupRepository,
            IUserRepository userRepository,
            IRoleRepository roleRepository,
            IUserMatriculaRepository userMatriculaRepository,
            IMatriculaRepository matriculaRepository,
            IEmailService emailService,
            AppDbContext context,
            IContractMetadataRepository metadataRepository,
            IPVRepository pvRepository,
            IContractStatusMapper statusMapper,
            IContractStatusService statusService,
            IImportErrorService errorService,
            IPendingClaimService pendingClaimService)
        {
            _contractRepository = contractRepository;
            _groupRepository = groupRepository;
            _userRepository = userRepository;
            _roleRepository = roleRepository;
            _userMatriculaRepository = userMatriculaRepository;
            _matriculaRepository = matriculaRepository;
            _emailService = emailService;
            _context = context;
            _metadataRepository = metadataRepository;
            _pvRepository = pvRepository;
            _statusMapper = statusMapper;
            _statusService = statusService;
            _errorService = errorService;
            _pendingClaimService = pendingClaimService;
        }

        public async Task<ImportResult> ExecuteContractImportAsync(
            string uploadId,
            int importSessionId,
            List<Dictionary<string, string>> rows,
            Dictionary<string, string> mappings,
            string dateFormat,
            bool skipMissingContractNumber = false,
            bool allowAutoCreateGroups = false,
            bool allowAutoCreatePVs = false,
            Action<MatriculaChangeRecord>? onMatriculaChange = null)
        {
            var result = new ImportResult();
            result.TotalRows = rows.Count;

            // Create reverse mapping (target field -> list of source columns)
            var reverseMappings = mappings.GroupBy(kvp => kvp.Value)
                .ToDictionary(g => g.Key, g => g.Select(kvp => kvp.Key).ToList());
            var contractsToAdd = new List<Contract>();

            // Dictionary to cache lookups during this import session
            var groupCache = new Dictionary<string, int?>();
            var pvCache = new Dictionary<string, int?>();
            var matriculaCache = new Dictionary<string, int?>();
            var newContractsMap = new Dictionary<string, Contract>();

            // 1. Pre-identify potential contract numbers for bulk fetch
            var allContractNumbers = rows
                .Select(r => ParseContractNumber(GetFieldValue(r, reverseMappings, "ContractNumber")))
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct()
                .Select(n => n!) 
                .ToList();

            // 2. Fetch existing contracts in bulk
            var existingContracts = await _contractRepository.GetByContractNumbersAsync(allContractNumbers);
            var existingMap = existingContracts.ToDictionary(c => c.ContractNumber);

            // Phase 1: Build all contracts (validation only, no DB saves)
            Console.WriteLine($"[Import] Starting batch processing for {rows.Count} rows...");
            if (rows.Any())
            {
                var keys = rows.First().Keys.ToList();
                Console.WriteLine($"[Import] Column headers detected: {string.Join("|", keys)}");

                // Detect unmapped columns and add to warnings (as requested by user)
                var mappedColumns = reverseMappings.Values.SelectMany(v => v).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var unmappedColumns = keys.Where(k => !mappedColumns.Contains(k)).ToList();
                if (unmappedColumns.Any())
                {
                    result.Warnings.Add($"Colunas não mapeadas detectadas na origem: {string.Join(", ", unmappedColumns)}. Estas colunas estão sendo ignoradas.");
                    
                    // ✅ Log to DynamoDB for external monitoring
                    await _errorService.LogErrorAsync(
                        ImportErrorType.HeaderMismatch, 
                        "Contract", 
                        $"PBI Headers changed. Unmapped columns: {string.Join(", ", unmappedColumns)}",
                        new { UploadId = uploadId, UnmappedHeaders = unmappedColumns },
                        importSessionId);
                }
            }
            for (int i = 0; i < rows.Count; i++)
            {
                try
                {
                    var row = rows[i];
                    var contractNumber = ParseContractNumber(GetFieldValue(row, reverseMappings, "ContractNumber"));

                    // Skip row if contract number is missing and skip option is enabled
                    if (skipMissingContractNumber)
                    {
                        if (string.IsNullOrWhiteSpace(contractNumber))
                        {
                            continue;
                        }
                    }

                    // Look for existing contract (in DB or already seen in this batch)
                    existingMap.TryGetValue(contractNumber ?? "", out var existingContract);
                    
                    if (existingContract == null && !string.IsNullOrWhiteSpace(contractNumber))
                    {
                        newContractsMap.TryGetValue(contractNumber, out existingContract);
                    }

                    // ✅ MANDATORY SILENT SKIP: mandatory for SaleStartDate IF NEW CONTRACT
                    var startDateStr = GetFieldValue(row, reverseMappings, "SaleStartDate");
                    if (existingContract == null && string.IsNullOrWhiteSpace(startDateStr))
                    {
                        continue;
                    }

                    var contract = await BuildContractFromRowAsync(
                        row, reverseMappings, uploadId, importSessionId, dateFormat, 
                        groupCache, pvCache, result, allowAutoCreateGroups, allowAutoCreatePVs, 
                        existingContract, matriculaCache,
                        onMatriculaChange: change => result.MatriculaChanges.Add(change));

                    if (contract != null)
                    {
                        // If it's a new contract (not tracked in DB), we might need to add it to the batch
                        if (existingContract == null)
                        {
                            // Double check it wasn't added to newContractsMap by another row (should be handled by the lookup above)
                            contractsToAdd.Add(contract);
                            if (!string.IsNullOrWhiteSpace(contractNumber))
                            {
                                newContractsMap[contractNumber] = contract;
                            }
                        }
                        // If it's existing (either in DB or already in newContractsMap), 
                        // it was updated in place by BuildContractFromRowAsync

                        result.ProcessedRows++;
                    }
                    else
                    {
                        result.FailedRows++;
                        result.Errors.Add($"Row {i + 1}: Failed to create/update contract");
                    }
                }
                catch (Exception ex)
                {
                    result.FailedRows++;
                    result.Errors.Add($"Row {i + 1}: {ex.Message}");
                }
            }

            Console.WriteLine($"[Import] Processing complete. Processed: {result.ProcessedRows}, Failed: {result.FailedRows}, ToAdd: {contractsToAdd.Count}, ToUpdate: {existingMap.Count}");
            if (result.Errors.Any())
            {
                Console.WriteLine($"[Import] First 5 errors: {string.Join(" | ", result.Errors.Take(5))}");
            }

            // ✅ Phase 2: Batch insert all valid contracts in single transaction
            if (contractsToAdd.Any())
            {
                try
                {
                    Console.WriteLine($"[Import] Attempting batch insert of {contractsToAdd.Count} new contracts...");
                    await _contractRepository.CreateBatchAsync(contractsToAdd);
                    Console.WriteLine("[Import] Batch insert successful.");
                }
                catch (DbUpdateException ex)
                {
                    var innerMessage = ex.InnerException?.Message ?? ex.Message;
                    Console.WriteLine($"[Import] Batch insert FAILED: {innerMessage}");
                    result.FailedRows += contractsToAdd.Count;
                    result.ProcessedRows -= contractsToAdd.Count;
                    result.Errors.Add($"Erro ao salvar novos contratos: {innerMessage}");
                    
                    // ✅ Log critical DB error to DynamoDB
                    await _errorService.LogErrorAsync(ImportErrorType.SystemError, "Contract", $"Batch insert failed: {innerMessage}", new { Exception = ex.ToString() }, importSessionId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Import] Batch insert FAILED (Generic): {ex.Message}");
                    result.FailedRows += contractsToAdd.Count;
                    result.ProcessedRows -= contractsToAdd.Count;
                    result.Errors.Add($"Erro inesperado no banco de dados: {ex.Message}");
                }
            }

            // ✅ Phase 3: Auto-assign pending claims
            var allImportedContracts = contractsToAdd.Concat(existingContracts).ToList();
            if (allImportedContracts.Any())
            {
                try
                {
                    var importedNumbers = allImportedContracts.Select(c => c.ContractNumber).Where(n => !string.IsNullOrEmpty(n)).Distinct().ToList();
                    Console.WriteLine($"[Import Phase 3 Debug] importedNumbers count: {importedNumbers.Count}");
                    Console.WriteLine($"[Import Phase 3 Debug] importedNumbers: {string.Join(", ", importedNumbers)}");
                    
                    var pendingClaims = await _context.PendingContractClaims
                        .Include(c => c.User)
                        .Where(c => !c.IsResolved)
                        .ToListAsync(); // Fetch all and filter in memory to be 100% sure about trimming/case
                    
                    Console.WriteLine($"[Import Phase 3 Debug] Unresolved pending claims in DB: {pendingClaims.Count}");
                    if (pendingClaims.Any()) {
                        Console.WriteLine($"[Import Phase 3 Debug] Claims: {string.Join(", ", pendingClaims.Select(c => c.ContractNumber))}");
                    }
                    
                    var matchedClaims = pendingClaims
                        .Where(c => importedNumbers.Contains(c.ContractNumber.Trim(), StringComparer.OrdinalIgnoreCase))
                        .ToList();

                    if (matchedClaims.Any())
                    {
                        Console.WriteLine($"[Import] Found {matchedClaims.Count} pending claims to resolve.");
                        
                        // Use a dictionary to map contract numbers to objects, handling any theoretical duplicates gracefully
                        var contractMap = new Dictionary<string, Contract>(StringComparer.OrdinalIgnoreCase);
                        foreach (var c in allImportedContracts)
                        {
                            if (!string.IsNullOrEmpty(c.ContractNumber))
                            {
                                var normalizedKey = c.ContractNumber.Trim();
                                contractMap[normalizedKey] = c;
                            }
                        }

                        foreach (var claim in matchedClaims)
                        {
                            var claimKey = claim.ContractNumber.Trim();
                            if (contractMap.TryGetValue(claimKey, out var contract))
                            {
                                // Reconciliation: The pending claim MUST be resolved.
                                // It takes priority over 'Owner fallback' (matricula owner) 
                                // to ensure the person who actually 'worked' the contract gets it.
                                contract.UserId = claim.UserId;
                                contract.MatriculaId = claim.MatriculaId;
                                
                                claim.IsResolved = true;
                                claim.ResolvedAt = DateTime.UtcNow;
                                
                                Console.WriteLine($"[Import] RECONCILED: Contract {claimKey} assigned to user {claim.UserId} (was pending claim)");
                            }
                        }
                        
                        await _context.SaveChangesAsync();
                        Console.WriteLine("[Import] Phase 3: Pending claims resolution committed to database.");
                    }
                    else
                    {
                        Console.WriteLine("[Import] No matching pending claims found.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Import] Error resolving pending claims: {ex.Message}");
                    result.Warnings.Add($"Aviso: Não foi possível reconciliar contratos solicitados: {ex.Message}");
                }
            }

            // 4. Save updates to existing contracts
            try
            {
                await _context.SaveChangesAsync();
                result.CreatedContracts = contractsToAdd.Concat(existingContracts).ToList();
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Failed to save updates to existing contracts: {ex.Message}");
            }

            return result;
        }

        private async Task<Contract?> BuildContractFromRowAsync(
            Dictionary<string, string> row,
            Dictionary<string, List<string>> reverseMappings,
            string uploadId,
            int importSessionId,
            string dateFormat,
            Dictionary<string, int?> groupCache,
            Dictionary<string, int?> pvCache,
            ImportResult result,
            bool allowAutoCreateGroups = false,
            bool allowAutoCreatePVs = false,
            Contract? existingContract = null,
            Dictionary<string, int?>? matriculaCache = null,
            Action<MatriculaChangeRecord>? onMatriculaChange = null)
        {
            var rawCota = GetFieldValue(row, reverseMappings, "ContractNumber");
            var cotaInfo = CotaDecomposer.Decompose(rawCota);
            
            var contractNumber = cotaInfo.Contract;
            var userEmail = GetFieldValue(row, reverseMappings, "UserEmail");
            var totalAmountStr = GetFieldValue(row, reverseMappings, "TotalAmount");
            var groupValue = GetFieldValue(row, reverseMappings, "GroupId") ?? cotaInfo.Group;
            var matriculaNumber = GetFieldValue(row, reverseMappings, "MatriculaNumber") ?? cotaInfo.Matricula;

            // Optional fields might also be in the cota string if not mapped directly
            var customerName = GetFieldValue(row, reverseMappings, "CustomerName");
            if (string.IsNullOrWhiteSpace(customerName)) customerName = cotaInfo.Customer;

            // Validate required fields (UserEmail is no longer required for scraper imports)
            if (string.IsNullOrWhiteSpace(contractNumber) ||
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
            var groupId = await ResolveGroupIdAsync(groupValue, groupCache, importSessionId, allowAutoCreateGroups, result);

            // Verify group exists if a value was provided but resolution failed
            if (!string.IsNullOrWhiteSpace(groupValue) && !groupId.HasValue)
            {
                throw new ArgumentException($"Group not found: {groupValue}");
            }

            // Look up user by email if provided
            User? user = null;
            if (!string.IsNullOrWhiteSpace(userEmail))
            {
                user = await _userRepository.GetByEmailAsync(userEmail);
                if (user == null || !user.IsActive)
                {
                    throw new ArgumentException($"User not found or inactive: {userEmail}");
                }
            }

            // Resolve the specific Matricula from the CSV row
            int? matriculaId = await EnsureMatriculaExistsAsync(matriculaNumber, importSessionId, matriculaCache);

            // If user is not provided by email, try to find the owner of this matricula
            if (user == null && matriculaId.HasValue)
            {
                var ownerRel = await _userMatriculaRepository.GetOwnerByMatriculaIdAsync(matriculaId.Value);
                if (ownerRel != null)
                {
                    user = ownerRel.User;
                }
            }

            // Extract optional fields
            var statusInput = GetFieldValue(row, reverseMappings, "Status");
            var mappedStatus = _statusMapper.MapStatus(statusInput);
            if (mappedStatus == null && !string.IsNullOrWhiteSpace(statusInput))
            {
                var warning = $"Unrecognized status value: '{statusInput}' for contract '{contractNumber}'. Defaulting to Active.";
                result.Warnings.Add(warning);
                
                // ✅ Log Status Anomaly to DynamoDB
                await _errorService.LogErrorAsync(
                    ImportErrorType.StatusAnomaly,
                    "Contract",
                    warning,
                    new { ContractNumber = contractNumber, RawStatus = statusInput },
                    importSessionId);
            }
            var status = mappedStatus ?? ContractStatus.Active.ToApiString();
            var saleStartDateStr = GetFieldValue(row, reverseMappings, "SaleStartDate");
            var contractTypeStr = GetFieldValue(row, reverseMappings, "ContractType");
            var quotaStr = GetFieldValue(row, reverseMappings, "Quota");
            var pvIdStr = GetFieldValue(row, reverseMappings, "PvId");
            // Use the customerName extracted earlier from Cota if direct column is empty
            var directCustomerName = GetFieldValue(row, reverseMappings, "CustomerName");
            if (!string.IsNullOrWhiteSpace(directCustomerName)) customerName = directCustomerName;

            // Parse dates if provided
            DateTime? saleStartDate = null;
            if (!string.IsNullOrWhiteSpace(saleStartDateStr))
            {
                if (!TryParseFlexibleDate(saleStartDateStr, out var parsedDate))
                {
                    throw new ArgumentException($"Invalid start date: {saleStartDateStr}");
                }
                saleStartDate = parsedDate;
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

            // Parse PvId and PvName
            var pvNameStr = GetFieldValue(row, reverseMappings, "PvName");
            int? pvId = await ResolvePvIdAsync(pvIdStr, pvCache, importSessionId, allowAutoCreatePVs, result, pvNameStr);


            // ✅ Create or update contract object
            var contract = existingContract ?? new Contract { CreatedAt = DateTime.UtcNow };

            if (existingContract != null)
            {
                contract.UpdatedAt = DateTime.UtcNow;

                // ✅ Matricula change detection
                if (IsMatriculaChanged(existingContract.MatriculaId, matriculaId))
                {
                    var oldMatriculaNumber = existingContract.Matricula?.MatriculaNumber
                                            ?? existingContract.MatriculaId!.Value.ToString();

                    // Update contract link to new matricula
                    contract.MatriculaId = matriculaId;

                    // Ensure the currently-assigned user is linked to the new matricula
                    if (contract.UserId.HasValue)
                    {
                        var existingLink = await _userMatriculaRepository
                            .GetByMatriculaNumberAndUserIdAsync(matriculaNumber!, contract.UserId.Value);
                        if (existingLink == null)
                        {
                            await _userMatriculaRepository.CreateAsync(new UserMatricula
                            {
                                UserId = contract.UserId.Value,
                                MatriculaId = matriculaId!.Value,
                                IsOwner = false,
                                IsActive = true,
                                ImportSessionId = importSessionId
                            });
                        }
                    }

                    onMatriculaChange?.Invoke(new MatriculaChangeRecord(
                        contractNumber!,
                        oldMatriculaNumber,
                        matriculaNumber ?? matriculaId!.Value.ToString()
                    ));
                }
            }

            contract.ContractNumber = contractNumber;
            contract.UserId = user?.Id; // Can be null if unassigned
            if (user == null && !string.IsNullOrWhiteSpace(matriculaNumber) && string.IsNullOrWhiteSpace(contract.TempMatricula))
            {
                contract.TempMatricula = matriculaNumber;
            }
            contract.TotalAmount = totalAmount;
            contract.GroupId = groupId;
            contract.ContractStatusId = await _statusService.GetStatusIdByNameAsync(status);
            if (saleStartDate.HasValue) contract.SaleStartDate = saleStartDate.Value;
            contract.UploadId = uploadId;
            contract.ImportSessionId = importSessionId;
            contract.IsActive = true;
            contract.UpdatedAt = DateTime.UtcNow;
            contract.ContractType = contractType;
            contract.Quota = quota;
            contract.PvId = pvId;
            contract.CustomerName = customerName;
            
            // ✅ Link contract directly to Matricula (if not already handled by change detection)
            if (matriculaId.HasValue && contract.MatriculaId != matriculaId)
            {
                contract.MatriculaId = matriculaId;
            }

            return contract;
        }

        public async Task<ImportResult> ExecuteUserImportAsync(
            string uploadId,
            int importSessionId,
            List<Dictionary<string, string>> rows,
            Dictionary<string, string> mappings)
        {
            var result = new ImportResult
            {
                TotalRows = rows.Count
            };

            // Create reverse mapping (target field -> list of source columns)
            var reverseMappings = mappings.GroupBy(kvp => kvp.Value)
                .ToDictionary(g => g.Key, g => g.Select(kvp => kvp.Key).ToList());

            // ✅ Topological sort: ensure parent users are always created before their children.
            // Without this, if Julio Mota (parentEmail=carlos) appears before Carlos in the CSV,
            // the GetByEmailAsync(carlos) call returns null and parentId is silently lost.
            rows = SortRowsTopologically(rows, reverseMappings, result);

            var matriculaCache = new Dictionary<string, int?>();
            
            // Header Integrity Check
            if (rows.Any())
            {
                var keys = rows.First().Keys.ToList();
                var mappedColumns = reverseMappings.Values.SelectMany(v => v).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var unmappedColumns = keys.Where(k => !mappedColumns.Contains(k)).ToList();
                if (unmappedColumns.Any())
                {
                    result.Warnings.Add($"Colunas não mapeadas detectadas na origem: {string.Join(", ", unmappedColumns)}. Estas colunas estão sendo ignoradas.");
                    
                    // ✅ Log to DynamoDB for external monitoring
                    await _errorService.LogErrorAsync(
                        ImportErrorType.HeaderMismatch, 
                        "User", 
                        $"User Import Headers changed. Unmapped columns: {string.Join(", ", unmappedColumns)}",
                        new { UploadId = uploadId, UnmappedHeaders = unmappedColumns },
                        importSessionId);
                }
            }
            
            for (int i = 0; i < rows.Count; i++)
            {
                try
                {
                    var row = rows[i];
                    var user = await CreateUserFromRowAsync(row, reverseMappings, importSessionId, result, matriculaCache);

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

        /// <summary>
        /// Sorts user import rows in topological order so that a user whose parentEmail
        /// references another user in the same batch is always processed AFTER that parent.
        ///
        /// Algorithm: Kahn's BFS on a DAG where nodes are unique email addresses and
        /// edges represent parent→child dependencies within the batch.
        ///
        /// Rows whose parent email is NOT in the batch (resolves from existing DB users)
        /// are treated as root nodes and processed first.
        ///
        /// If a cycle is detected (should not happen in valid data) the remaining rows
        /// are appended at the end unchanged, so no rows are ever dropped.
        /// </summary>
        private List<Dictionary<string, string>> SortRowsTopologically(
            List<Dictionary<string, string>> rows,
            Dictionary<string, List<string>> reverseMappings,
            ImportResult result)
        {
            string? GetEmail(Dictionary<string, string> row) =>
                GetFieldValue(row, reverseMappings, "Email")?.ToLowerInvariant();

            string? GetParentEmail(Dictionary<string, string> row) =>
                GetFieldValue(row, reverseMappings, "ParentEmail")?.ToLowerInvariant();

            // 1. Collect every unique email that appears in this batch
            var allEmailsInBatch = new HashSet<string>(
                rows.Select(GetEmail).Where(e => e != null)!,
                StringComparer.OrdinalIgnoreCase);

            // 2. Build a dependency map: email → parent email within the batch.
            //    If multiple rows exist for the same email, the FIRST row that declares
            //    an in-batch parent wins (subsequent rows only add matriculas).
            var parentMap = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows)
            {
                var email = GetEmail(row);
                if (email == null) continue;

                var parentEmail = GetParentEmail(row);
                // Ignore self-references — a user cannot be their own parent.
                if (string.Equals(parentEmail, email, StringComparison.OrdinalIgnoreCase))
                    parentEmail = null;
                bool hasInBatchParent = !string.IsNullOrWhiteSpace(parentEmail)
                    && allEmailsInBatch.Contains(parentEmail);

                // Record the dependency if any row for this email references an in-batch parent
                if (hasInBatchParent && (!parentMap.ContainsKey(email) || parentMap[email] == null))
                    parentMap[email] = parentEmail;
                else if (!parentMap.ContainsKey(email))
                    parentMap[email] = null;
            }

            // 3. Initialise Kahn's structures
            var inDegree = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var childrenOf = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var email in allEmailsInBatch)
            {
                inDegree[email] = 0;
                childrenOf[email] = new List<string>();
            }

            foreach (var (email, parent) in parentMap)
            {
                if (parent != null)
                {
                    inDegree[email]++;
                    childrenOf[parent].Add(email);
                }
            }

            // 4. Kahn's BFS – start from all roots (no unresolved in-batch parent)
            var queue = new Queue<string>(
                allEmailsInBatch.Where(e => inDegree.GetValueOrDefault(e, 0) == 0));

            var sortedEmails = new List<string>();
            while (queue.Count > 0)
            {
                var email = queue.Dequeue();
                sortedEmails.Add(email);

                foreach (var child in childrenOf.GetValueOrDefault(email, new List<string>()))
                {
                    inDegree[child]--;
                    if (inDegree[child] == 0)
                        queue.Enqueue(child);
                }
            }

            // 5. Detect and break any remaining emails (circular references)
            var sortedSet = new HashSet<string>(sortedEmails, StringComparer.OrdinalIgnoreCase);
            var cyclicEmails = allEmailsInBatch.Where(e => !sortedSet.Contains(e)).ToList();

            if (cyclicEmails.Any())
            {
                var warning = $"Encontramos referências circulares para estes usuários:\n{string.Join("\n", cyclicEmails)}\n\nDesabilitamos o campo de e-mail do superior (parentEmail) para eles.";
                result.Warnings.Add(warning);
                Console.WriteLine($"[Hierarchy] CIRCULAR REFERENCE DETECTED in batch import: {string.Join(", ", cyclicEmails)}");

                // Group all rows by email to easily find rows belonging to cyclic emails
                var allRowsByEmail = new Dictionary<string, List<Dictionary<string, string>>>(StringComparer.OrdinalIgnoreCase);
                foreach (var r in rows)
                {
                    var email = GetEmail(r);
                    if (email == null) continue;
                    if (!allRowsByEmail.ContainsKey(email)) allRowsByEmail[email] = new List<Dictionary<string, string>>();
                    allRowsByEmail[email].Add(r);
                }

                foreach (var email in cyclicEmails)
                {
                    if (allRowsByEmail.TryGetValue(email, out var rowList))
                    {
                        foreach (var row in rowList)
                        {
                            // Clear the parentEmail field in the row so it doesn't try to resolve it later
                            if (reverseMappings.TryGetValue("ParentEmail", out var cols))
                            {
                                foreach (var col in cols)
                                {
                                    if (row.ContainsKey(col)) row[col] = "";
                                }
                            }
                        }
                    }
                    sortedEmails.Add(email);
                }
            }

            // 6. Group all rows by email, preserving original relative order within each email
            var rowsByEmail = new Dictionary<string, List<Dictionary<string, string>>>(
                StringComparer.OrdinalIgnoreCase);

            const string unknownKey = "__no_email__";
            foreach (var row in rows)
            {
                var key = GetEmail(row) ?? unknownKey;
                if (!rowsByEmail.ContainsKey(key))
                    rowsByEmail[key] = new List<Dictionary<string, string>>();
                rowsByEmail[key].Add(row);
            }

            // 7. Reconstruct the sorted rows list
            var sortedRows = new List<Dictionary<string, string>>(rows.Count);
            foreach (var email in sortedEmails)
                if (rowsByEmail.TryGetValue(email, out var bucket))
                    sortedRows.AddRange(bucket);

            // Rows without any email come last
            if (rowsByEmail.TryGetValue(unknownKey, out var noEmailRows))
                sortedRows.AddRange(noEmailRows);

            return sortedRows;
        }

        private async Task<User?> CreateUserFromRowAsync(
            Dictionary<string, string> row,
            Dictionary<string, List<string>> reverseMappings,
            int importSessionId,
            ImportResult result,
            Dictionary<string, int?>? matriculaCache = null)
        {
            // Extract required fields
            var name = GetFieldValue(row, reverseMappings, "Name");
            var email = GetFieldValue(row, reverseMappings, "Email")?.ToLowerInvariant(); // Force lowercase

            // Extract Matricula fields early for validation
            var matricula = GetFieldValue(row, reverseMappings, "Matricula");

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(matricula))
            {
                throw new ArgumentException("Nome, Email, and Matricula are required fields for each row.");
            }

            // Check if email exists
            var existingUser = await _userRepository.GetByEmailAsync(email);
            bool isNewUser = existingUser == null;

            // Extract optional fields
            var surname = GetFieldValue(row, reverseMappings, "Surname");
            var roleName = GetFieldValue(row, reverseMappings, "Role");
            var parentEmail = GetFieldValue(row, reverseMappings, "ParentEmail")?.ToLowerInvariant(); // Force lowercase


            // Combine name and surname if surname exists
            var fullName = name;
            if (!string.IsNullOrWhiteSpace(surname))
            {
                fullName = $"{name} {surname}".Trim();
            }

            // Resolve Role
            Role? newRole = null;
            if (!string.IsNullOrWhiteSpace(roleName))
            {
                newRole = await _roleRepository.GetByNameAsync(roleName);
            }
            
            int resolvedRoleId = newRole?.Id ?? (int)Models.RoleId.User;

            // Resolve Parent
            // Guard: if parentEmail == own email, ignore it — a user cannot be their own parent.
            // This silently corrects a common data-entry mistake before it creates a self-loop
            // in the hierarchy tree (which would later cause infinite recursion or JSON cycle errors).
            if (!string.IsNullOrWhiteSpace(parentEmail) && parentEmail == email)
                parentEmail = null;

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
            matricula = GetFieldValue(row, reverseMappings, "Matricula");
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

            // Extract custom password if provided
            var customPassword = GetFieldValue(row, reverseMappings, "Password");
            var defaultPassword = "ChangeMe123!";
            var passwordToUse = !string.IsNullOrWhiteSpace(customPassword) ? customPassword : defaultPassword;

            // Upsert User
            var user = existingUser ?? new User
            {
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(passwordToUse),
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            // If existing user and custom password provided, update it
            if (existingUser != null && !string.IsNullOrWhiteSpace(customPassword))
            {
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(customPassword);
            }

            user.Name = fullName;
            
            // Only update RoleId if new role has higher priority (lower Level) or if it's a new user
            if (isNewUser)
            {
                user.RoleId = resolvedRoleId;
            }
            else if (newRole != null && existingUser!.Role != null)
            {
                // Lower Level numerical value means higher priority (1:SuperAdmin, 2:Admin, 3:User)
                if (newRole.Level < existingUser.Role.Level)
                {
                    user.RoleId = newRole.Id;
                }
            }
            // Only set ParentUserId if:
            //   - it's a new user (first time we see this email), OR
            //   - this row explicitly declares a parent (never silently clear an existing link)
            // Without this guard, a second row for juliomota@example.com with empty ParentEmail
            // would overwrite the parentId correctly set by the first row.
            if (isNewUser || parentId.HasValue)
            {
                if (parentId.HasValue && !isNewUser)
                {
                    // Check for cross-boundary circular reference
                    if (await _userRepository.WouldCreateCycleAsync(user.Id, parentId))
                    {
                        var warning = $"Encontramos referências circulares para estes usuários:\n{user.Email}\n\nDesabilitamos o campo de e-mail do superior (parentEmail) para eles.";
                        if (!result.Warnings.Contains(warning))
                        {
                            result.Warnings.Add(warning);
                            Console.WriteLine($"[Hierarchy] CROSS-BOUNDARY CIRCULAR REFERENCE DETECTED for user: {user.Email} pointing to parentId: {parentId}");
                        }
                        parentId = null;
                    }
                }
                user.ParentUserId = parentId;
            }
            user.UpdatedAt = DateTime.UtcNow;
            user.ImportSessionId = importSessionId;

            User? createdUser;
            if (isNewUser)
            {
                createdUser = await _userRepository.CreateAsync(user);
            }
            else
            {
                await _userRepository.UpdateAsync(user);
                createdUser = user;
            }

            // Handle matricula assignment if provided
            if (createdUser != null && !string.IsNullOrWhiteSpace(matricula))
            {
                try
                {
                    // 1. Ensure the Matricula exists
                    int? matriculaId = await EnsureMatriculaExistsAsync(matricula, importSessionId, matriculaCache);
                    
                    if (!matriculaId.HasValue)
                    {
                        throw new ArgumentException("Failed to resolve or create matricula.");
                    }

                    // 2. Link User to Matricula via UserMatricula
                    var existingLink = await _userMatriculaRepository.GetByMatriculaNumberAndUserIdAsync(matricula, createdUser.Id);

                    if (isMatriculaOwner)
                    {
                        // Check if someone else is the current owner
                        var currentOwner = await _userMatriculaRepository.GetOwnerByMatriculaIdAsync(matriculaId.Value);
                        if (currentOwner != null && currentOwner.UserId != createdUser.Id)
                        {
                            // ✅ Log Ownership Conflict to DynamoDB
                            await _errorService.LogErrorAsync(
                                ImportErrorType.OwnershipConflict,
                                "UserMatricula",
                                $"Ownership transfer for matricula {matricula}: from {currentOwner.UserId} to {createdUser.Id}",
                                new { Matricula = matricula, OldOwnerId = currentOwner.UserId, NewOwnerId = createdUser.Id },
                                importSessionId);
                        }
                    }

                    if (existingLink == null)
                    {
                        var userMatricula = new UserMatricula
                        {
                            UserId = createdUser.Id,
                            MatriculaId = matriculaId.Value,
                            IsOwner = isMatriculaOwner,
                            IsActive = true,
                            ImportSessionId = importSessionId
                        };

                        await _userMatriculaRepository.CreateAsync(userMatricula);
                    }
                    else
                    {
                        // Update existing link properties
                        existingLink.IsOwner = isMatriculaOwner;
                        existingLink.UpdatedAt = DateTime.UtcNow;
                        existingLink.ImportSessionId = importSessionId;
                        await _userMatriculaRepository.UpdateAsync(existingLink);
                    }
                }
                catch (InvalidOperationException ex)
                {
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
            Dictionary<string, List<string>> reverseMappings,
            string targetField)
        {
            if (!reverseMappings.ContainsKey(targetField))
            {
                return null;
            }

            var sourceColumns = reverseMappings[targetField];
            foreach (var sourceColumn in sourceColumns)
            {
                if (row.ContainsKey(sourceColumn))
                {
                    return row[sourceColumn]?.Trim();
                }
            }

            return null;
        }

        /// <summary>
        /// Overload for backward compatibility with single-mapping dictionaries
        /// </summary>
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

        /// <summary>
        /// Safely extracts the actual contract number from potentially concatenated PowerBI values 
        /// (e.g. '012173;4103;0;MARIO;1100326334' -> '1100326334')
        /// </summary>
        private string? ParseContractNumber(string? rawValue)
        {
            return CotaDecomposer.Decompose(rawValue).Contract;
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

            // Fallback 1: Try as Unix Milliseconds if it's a large long (e.g. 1774828800000)
            if (long.TryParse(cleanedInput, out long unixMs) && unixMs > 1000000000000)
            {
                try
                {
                    result = DateTimeOffset.FromUnixTimeMilliseconds(unixMs).DateTime;
                    return true;
                }
                catch { }
            }

            // Fallback 2: Try as Excel OADate (serial number) if it's numeric
            if (double.TryParse(cleanedInput, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double oaDate))
            {
                try
                {
                    result = DateTime.FromOADate(oaDate);
                    return true;
                }
                catch { } // Not a valid OADate
            }

            // Fallback 2: General parsing
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
            int importSessionId,
            List<Dictionary<string, string>> rows,
            Dictionary<string, string> mappings,
            bool skipMissingContractNumber = false,
            bool allowAutoCreateGroups = false,
            bool allowAutoCreatePVs = false)
        {
            var result = new ImportResult();
            result.TotalRows = rows.Count;

            // Create reverse mapping (target field -> list of source columns)
            var reverseMappings = mappings.GroupBy(kvp => kvp.Value)
                .ToDictionary(g => g.Key, g => g.Select(kvp => kvp.Key).ToList());
            var groupCache = new Dictionary<string, int?>();
            var pvCache = new Dictionary<string, int?>();
            var matriculaCache = new Dictionary<string, int?>();
            var contractsToAdd = new List<Contract>();

            if (rows.Any())
            {
                var keys = rows.First().Keys.ToList();
                var mappedColumns = reverseMappings.Values.SelectMany(v => v).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var unmappedColumns = keys.Where(k => !mappedColumns.Contains(k)).ToList();
                if (unmappedColumns.Any())
                {
                    result.Warnings.Add($"Colunas não mapeadas detectadas na origem: {string.Join(", ", unmappedColumns)}. Estas colunas estão sendo ignoradas.");
                }
            }

            // 1. Pre-identify potential contract numbers for bulk fetch
            var allContractNumbers = new List<string>();
            foreach (var row in rows)
            {
                var contractNumber = ParseContractNumber(GetFieldValue(row, reverseMappings, "ContractNumber"));
                if (string.IsNullOrWhiteSpace(contractNumber))
                {
                    var cotaValue = ParseContractNumber(GetFieldValue(row, reverseMappings, "Cota"));
                    if (string.IsNullOrWhiteSpace(cotaValue))
                    {
                        continue; // Skip row if Cota is missing
                    }
                    contractNumber = cotaValue;
                }

                // ✅ MANDATORY SILENT SKIP if no contract number found (New user request)
                if (string.IsNullOrWhiteSpace(contractNumber))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(contractNumber))
                {
                    allContractNumbers.Add(contractNumber);
                }
            }

            // 2. Fetch existing contracts in bulk
            var existingContracts = await _contractRepository.GetByContractNumbersAsync(allContractNumbers.Distinct().ToList());
            var existingMap = existingContracts.ToDictionary(c => c.ContractNumber);

            for (int i = 0; i < rows.Count; i++)
            {
                try
                {
                    var row = rows[i];

                    // Identify contract number again for skip check or lookup
                    var contractNumber = GetFieldValue(row, reverseMappings, "ContractNumber");
                    var cotaValue = GetFieldValue(row, reverseMappings, "Cota"); // Get cotaValue here for potential fallback
                    if (string.IsNullOrWhiteSpace(contractNumber))
                    {
                        // Same fallback as above
                        if (string.IsNullOrWhiteSpace(cotaValue))
                        {
                            continue;
                        }

                        if (!string.IsNullOrWhiteSpace(cotaValue) && cotaValue.Contains(";"))
                        {
                            var cotaParts = cotaValue.Split(';');
                            if (cotaParts.Length >= 5) contractNumber = cotaParts[^1].Trim();
                        }
                    }

                    // ✅ MANDATORY SILENT SKIP if no contract number found (New user request)
                    if (string.IsNullOrWhiteSpace(contractNumber))
                    {
                        continue;
                    }

                    // Look for existing contract
                    existingMap.TryGetValue(contractNumber ?? "", out var existingContract);

                    // ✅ MANDATORY SILENT SKIP: mandatory for SaleStartDate IF NEW CONTRACT
                    var startDateStr = GetFieldValue(row, reverseMappings, "SaleStartDate");
                    if (existingContract == null && string.IsNullOrWhiteSpace(startDateStr))
                    {
                        continue;
                    }

                    var contract = await BuildContractDashboardFromRowAsync(
                        row, reverseMappings, uploadId, importSessionId,
                        groupCache, pvCache, result,
                        allowAutoCreateGroups, allowAutoCreatePVs,
                        existingContract, matriculaCache,
                        onMatriculaChange: change => result.MatriculaChanges.Add(change));

                    if (contract != null)
                    {
                        // If it's a new contract (not tracked), we add to list
                        if (existingContract == null)
                        {
                            contractsToAdd.Add(contract);
                        }
                        // If it's existing, it's already updated and tracked by the context

                        result.ProcessedRows++;
                    }
                    else
                    {
                        result.FailedRows++;
                        result.Errors.Add($"Row {i + 1}: Failed to create/update contract");
                    }
                }
                catch (Exception ex)
                {
                    result.FailedRows++;
                    result.Errors.Add($"Row {i + 1}: {ex.Message}");
                }
            }

            // 3. Phase 3: Auto-assign pending claims before any database saves
            var allContractsForReconciliation = contractsToAdd.Concat(existingContracts).ToList();
            if (allContractsForReconciliation.Any())
            {
                try
                {
                    var importedNumbers = allContractsForReconciliation.Select(c => c.ContractNumber).Where(n => !string.IsNullOrEmpty(n)).Distinct().ToList();
                    
                    var pendingClaims = await _context.PendingContractClaims
                        .Include(c => c.User)
                        .Where(c => !c.IsResolved)
                        .ToListAsync(); // Fetch all unresolved and filter in memory
                    
                    var matchedClaims = pendingClaims
                        .Where(c => importedNumbers.Contains(c.ContractNumber.Trim(), StringComparer.OrdinalIgnoreCase))
                        .ToList();

                    if (matchedClaims.Any())
                    {
                        Console.WriteLine($"[Import Dashboard] Found {matchedClaims.Count} pending claims to reconcile BEFORE save.");
                        
                        var contractMap = new Dictionary<string, Contract>(StringComparer.OrdinalIgnoreCase);
                        foreach (var c in allContractsForReconciliation)
                        {
                            if (!string.IsNullOrEmpty(c.ContractNumber))
                            {
                                var normalizedKey = c.ContractNumber.Trim();
                                contractMap[normalizedKey] = c;
                            }
                        }

                        foreach (var claim in matchedClaims)
                        {
                            var claimKey = claim.ContractNumber.Trim();
                            if (contractMap.TryGetValue(claimKey, out var contract))
                            {
                                contract.UserId = claim.UserId;
                                contract.MatriculaId = claim.MatriculaId;
                                
                                claim.IsResolved = true;
                                claim.ResolvedAt = DateTime.UtcNow;
                                
                                Console.WriteLine($"[Import Dashboard] PRE-RECONCILED: Contract {claimKey} will be assigned to user {claim.UserId}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Import Dashboard] Pending claims reconciliation warning: {ex.Message}");
                    result.Warnings.Add($"Failed to reconcile some pending claims: {ex.Message}");
                }
            }

            // 4. Batch insert new contracts (now with reconciled UserIds)
            if (contractsToAdd.Any())
            {
                try
                {
                    await _contractRepository.CreateBatchAsync(contractsToAdd);
                }
                catch (Exception ex)
                {
                    result.FailedRows += contractsToAdd.Count;
                    result.ProcessedRows -= contractsToAdd.Count;
                    result.Errors.Add($"Batch insert failed: {ex.Message}");
                }
            }

            // 5. Save updates to existing contracts and resolved claims
            try
            {
                await _context.SaveChangesAsync();
                result.CreatedContracts = contractsToAdd.Concat(existingContracts).ToList();
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Failed to save updates to existing contracts: {ex.Message}");
            }

            return result;
        }

        private async Task<Contract?> BuildContractDashboardFromRowAsync(
            Dictionary<string, string> row,
            Dictionary<string, List<string>> reverseMappings,
            string uploadId,
            int importSessionId,
            Dictionary<string, int?> groupCache,
            Dictionary<string, int?> pvCache,
            ImportResult result,
            bool allowAutoCreateGroups = false,
            bool allowAutoCreatePVs = false,
            Contract? existingContract = null,
            Dictionary<string, int?>? matriculaCache = null,
            Action<MatriculaChangeRecord>? onMatriculaChange = null)
        {
            // Try to get fields directly first (may be mapped from virtual columns like cota.group, etc.)
            var contractNumber = ParseContractNumber(GetFieldValue(row, reverseMappings, "ContractNumber"));
            var customerName = GetFieldValue(row, reverseMappings, "CustomerName");
            var groupValue = GetFieldValue(row, reverseMappings, "GroupId");
            var quotaStr = GetFieldValue(row, reverseMappings, "Quota");

            // Fallback to Cota split only if critical fields are missing
            if (string.IsNullOrWhiteSpace(contractNumber) || string.IsNullOrWhiteSpace(customerName) || string.IsNullOrWhiteSpace(groupValue) || string.IsNullOrWhiteSpace(quotaStr))
            {
                var cotaValue = GetFieldValue(row, reverseMappings, "Cota");
                if (string.IsNullOrWhiteSpace(cotaValue))
                {
                    var cotaKey = row.Keys.FirstOrDefault(k => k.Equals("Cota", StringComparison.OrdinalIgnoreCase));
                    if (cotaKey != null) cotaValue = row[cotaKey];
                }

                var cotaInfo = CotaDecomposer.Decompose(cotaValue);
                if (!string.IsNullOrWhiteSpace(cotaInfo.Contract))
                {
                    if (string.IsNullOrWhiteSpace(contractNumber)) contractNumber = cotaInfo.Contract;
                    if (string.IsNullOrWhiteSpace(customerName)) customerName = cotaInfo.Customer;
                    if (string.IsNullOrWhiteSpace(groupValue)) groupValue = cotaInfo.Group;
                    if (string.IsNullOrWhiteSpace(quotaStr)) quotaStr = cotaInfo.Matricula; // In this context Matricula is the Quota/Cota number
                }
            }

            // Resolve Matricula Id
            var matriculaNumber = GetFieldValue(row, reverseMappings, "MatriculaNumber");
            int? matriculaId = await EnsureMatriculaExistsAsync(matriculaNumber, importSessionId, matriculaCache);

            // Resolve Group ID
            var groupId = await ResolveGroupIdAsync(groupValue, groupCache, importSessionId, allowAutoCreateGroups, result);

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
            DateTime? saleStartDate = null;
            
            if (!string.IsNullOrWhiteSpace(saleStartDateStr))
            {
                // Try parsing as Excel serial number first (e.g., 45747)
                if (double.TryParse(saleStartDateStr, System.Globalization.NumberStyles.Any, 
                    System.Globalization.CultureInfo.InvariantCulture, out var excelDate))
                {
                    // Excel dates are days since 1900-01-01 (with a leap year bug, so we use 1899-12-30)
                    saleStartDate = new DateTime(1899, 12, 30).AddDays(excelDate);
                }
                // Try parsing as YYYY-MM-DD
                else if (DateTime.TryParseExact(saleStartDateStr, "yyyy-MM-dd", 
                    System.Globalization.CultureInfo.InvariantCulture, 
                    System.Globalization.DateTimeStyles.None, out var parsedSaleStartDate))
                {
                    saleStartDate = parsedSaleStartDate;
                }
                else
                {
                    throw new ArgumentException($"Invalid Sale Start Date: '{saleStartDateStr}'");
                }
            }
            else if (existingContract == null)
            {
                throw new ArgumentException("Sale Start Date is required for new contracts");
            }
            
            // Parse Version
            var versionStr = GetFieldValue(row, reverseMappings, "Version");
            byte? version = null;
            if (!string.IsNullOrWhiteSpace(versionStr) && byte.TryParse(versionStr, out var parsedVersion))
            {
                version = parsedVersion;
            }
            
            // Parse PvId and PvName
            var pvIdStr = GetFieldValue(row, reverseMappings, "PvId");
            var pvNameStr = GetFieldValue(row, reverseMappings, "PvName");
            int? pvId = await ResolvePvIdAsync(pvIdStr, pvCache, importSessionId, allowAutoCreatePVs, result, pvNameStr);
            
            // Get TempMatricula if present
            var tempMatricula = GetFieldValue(row, reverseMappings, "TempMatricula");
            
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
            
            // Resolve User if email is provided (Wizard support)
            Guid? userId = null;
            var userEmail = GetFieldValue(row, reverseMappings, "UserEmail");
            if (!string.IsNullOrWhiteSpace(userEmail))
            {
                var user = await _userRepository.GetByEmailAsync(userEmail);
                if (user != null && user.IsActive)
                {
                    userId = user.Id;
                }
            }

            // If user is not provided by email, try to find the owner of this matricula
            if (!userId.HasValue && matriculaId.HasValue)
            {
                var ownerRel = await _userMatriculaRepository.GetOwnerByMatriculaIdAsync(matriculaId.Value);
                if (ownerRel?.User != null)
                {
                    userId = ownerRel.User.Id;
                }
            }
            
            // Create or Update contract
            var contract = existingContract ?? new Contract { CreatedAt = DateTime.UtcNow };

            if (existingContract != null)
            {
                // Update status and reactivate
                contract.ContractStatusId = await _statusService.GetStatusIdByNameAsync(status);
                if (userId.HasValue) contract.UserId = userId;
                contract.IsActive = true;
                contract.UpdatedAt = DateTime.UtcNow;

                // ✅ Matricula change detection
                if (IsMatriculaChanged(existingContract.MatriculaId, matriculaId))
                {
                    var oldMatriculaNumber = existingContract.Matricula?.MatriculaNumber
                                            ?? existingContract.MatriculaId!.Value.ToString();

                    // Update contract link to new matricula
                    contract.MatriculaId = matriculaId;

                    // Ensure the currently-assigned user is linked to the new matricula
                    if (contract.UserId.HasValue)
                    {
                        var existingLink = await _userMatriculaRepository
                            .GetByMatriculaNumberAndUserIdAsync(matriculaNumber!, contract.UserId.Value);
                        if (existingLink == null)
                        {
                            await _userMatriculaRepository.CreateAsync(new UserMatricula
                            {
                                UserId = contract.UserId.Value,
                                MatriculaId = matriculaId!.Value,
                                IsOwner = false,
                                IsActive = true,
                                ImportSessionId = importSessionId
                            });
                        }
                    }

                    onMatriculaChange?.Invoke(new MatriculaChangeRecord(
                        contractNumber!,
                        oldMatriculaNumber,
                        matriculaNumber ?? matriculaId!.Value.ToString()
                    ));
                }

                return contract;
            }

            contract.ContractNumber = contractNumber;
            contract.UserId = userId;
            contract.TotalAmount = totalAmount;
            contract.GroupId = groupId;
            contract.ContractStatusId = await _statusService.GetStatusIdByNameAsync(status);
            if (saleStartDate.HasValue) contract.SaleStartDate = saleStartDate.Value;
            contract.UploadId = uploadId;
            contract.IsActive = true;
            contract.UpdatedAt = DateTime.UtcNow;
            contract.CustomerName = customerName;
            contract.PvId = pvId;
            contract.Quota = quota;
            contract.Version = version;
            contract.TempMatricula = tempMatricula;
            contract.ImportSessionId = importSessionId;
            contract.CategoryMetadataId = categoryMetadataId;
            contract.PlanoVendaMetadataId = planoVendaMetadataId;
            
            if (matriculaId.HasValue)
            {
                contract.MatriculaId = matriculaId;
            }

            return contract;
        }
        
        /// <summary>
        /// Pure function: determines whether a contract's matricula has changed.
        /// Returns true only when both IDs are known (non-null) and differ.
        /// </summary>
        internal static bool IsMatriculaChanged(int? existingMatriculaId, int? incomingMatriculaId)
            => incomingMatriculaId.HasValue
               && existingMatriculaId.HasValue
               && existingMatriculaId.Value != incomingMatriculaId.Value;

        private string MapSituacaoCobrancaToStatus(string? situacaoCobranca)
        {
            return _statusMapper.MapStatus(situacaoCobranca) ?? ContractStatus.Active.ToApiString();
        }
        
        private async Task<int?> ResolveGroupIdAsync(string? groupValue, Dictionary<string, int?> cache, int importSessionId, bool allowAutoCreate = false, ImportResult? result = null)
        {
            groupValue = NormalizationUtils.NormalizeNumber(groupValue);
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

            // 3. Automatic Creation (Only if enabled)
            if (!allowAutoCreate)
            {
                return null;
            }

            try
            {
                var newGroup = new Group
                {
                    Name = groupValue.Trim(),
                    Description = $"Auto-created during import {DateTime.UtcNow:yyyy-MM-dd}",
                    Commission = 0,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    ImportSessionId = importSessionId
                };
                
                var createdGroup = await _groupRepository.CreateAsync(newGroup);
                cache[groupValue] = createdGroup.Id;
                
                if (result != null && !result.CreatedGroups.Contains(createdGroup.Name))
                {
                    result.CreatedGroups.Add(createdGroup.Name);
                }
                
                return createdGroup.Id;
            }
            catch (Exception ex)
            {
                // Fallback to null if creation fails (e.g. unique constraint if name just popped up)
                cache[groupValue] = null;
                result?.Errors.Add($"Error auto-creating group '{groupValue}': {ex.Message}");
                return null;
            }
        }

        private async Task<int?> ResolvePvIdAsync(
            string? pvValue,
            Dictionary<string, int?> cache,
            int importSessionId,
            bool allowAutoCreate,
            ImportResult? result = null,
            string? pvName = null)
        {
            pvValue = NormalizationUtils.NormalizeNumber(pvValue);
            
            // 1. Check cache by ID first
            if (!string.IsNullOrWhiteSpace(pvValue) && cache.TryGetValue(pvValue, out var cachedId))
            {
                return cachedId;
            }
            
            // 2. Check cache by Name if ID is missing or fails
            var nameToLookup = !string.IsNullOrWhiteSpace(pvName) ? pvName.Trim() : pvValue?.Trim();
            if (!string.IsNullOrWhiteSpace(nameToLookup) && cache.TryGetValue($"NAME:{nameToLookup.ToLower()}", out var cachedNameId))
            {
                return cachedNameId;
            }

            // 3. Try lookup by ID if numeric
            if (!string.IsNullOrWhiteSpace(pvValue) && int.TryParse(pvValue.Trim(), out var id))
            {
                var pvById = await _pvRepository.GetByIdAsync(id);
                if (pvById != null)
                {
                    cache[pvValue] = pvById.Id;
                    return pvById.Id;
                }
            }

            // 4. Try lookup by Name (case-insensitive)
            if (!string.IsNullOrWhiteSpace(nameToLookup))
            {
                var pvByName = await _pvRepository.GetByNameAsync(nameToLookup);
                if (pvByName != null)
                {
                    if (!string.IsNullOrWhiteSpace(pvValue)) cache[pvValue] = pvByName.Id;
                    cache[$"NAME:{nameToLookup.ToLower()}"] = pvByName.Id;
                    return pvByName.Id;
                }
            }

            // 5. Automatic Creation (Only if enabled)
            if (!allowAutoCreate)
            {
                return null;
            }

            try
            {
                // Case A: We have a numeric ID provided in the CSV
                if (!string.IsNullOrWhiteSpace(pvValue) && int.TryParse(pvValue.Trim(), out var newId))
                {
                    var newPV = new PV
                    {
                        Id = newId,
                        Name = !string.IsNullOrWhiteSpace(pvName) ? pvName.Trim() : pvValue.Trim(),
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        ImportSessionId = importSessionId
                    };
                    
                    _context.PVs.Add(newPV);
                    await _context.SaveChangesAsync();
                    
                    cache[pvValue] = newPV.Id;
                    
                    if (result != null && !result.CreatedPVs.Contains(newPV.Name))
                    {
                        result.CreatedPVs.Add(newPV.Name);
                    }
                    
                    return newPV.Id;
                }
                // Case B: We only have a name (scraper import), let database handle ID auto-increment
                else if (!string.IsNullOrWhiteSpace(pvName))
                {
                    var newPV = new PV
                    {
                        Name = pvName.Trim(),
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        ImportSessionId = importSessionId
                    };
                    
                    _context.PVs.Add(newPV);
                    await _context.SaveChangesAsync();
                    
                    cache[$"NAME:{pvName.Trim().ToLower()}"] = newPV.Id;
                    
                    if (result != null && !result.CreatedPVs.Contains(newPV.Name))
                    {
                        result.CreatedPVs.Add(newPV.Name);
                    }
                    
                    return newPV.Id;
                }
                
                return null;
            }
            catch (Exception)
            {
                cache[pvValue] = null;
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

        private async Task<int?> EnsureMatriculaExistsAsync(
            string? matriculaNumber, 
            int importSessionId, 
            Dictionary<string, int?>? cache = null)
        {
            matriculaNumber = NormalizationUtils.NormalizeNumber(matriculaNumber);
            if (string.IsNullOrWhiteSpace(matriculaNumber))
            {
                return null;
            }

            if (cache != null && cache.TryGetValue(matriculaNumber, out var cachedId))
            {
                return cachedId;
            }

            var matricula = await _matriculaRepository.GetByMatriculaNumberAsync(matriculaNumber.Trim());
            if (matricula == null)
            {
                matricula = new Matricula
                {
                    MatriculaNumber = matriculaNumber.Trim(),
                    StartDate = DateTime.UtcNow,
                    Status = "active",
                    ImportSessionId = importSessionId
                };
                await _matriculaRepository.CreateAsync(matricula);
            }

            if (cache != null)
            {
                cache[matriculaNumber] = matricula.Id;
            }

            return matricula.Id;
        }

        public async Task<bool> UndoImportAsync(int importSessionId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. Delete Contracts from this session
                var contracts = await _context.Contracts
                    .Where(c => c.ImportSessionId == importSessionId)
                    .ToListAsync();
                _context.Contracts.RemoveRange(contracts);

                // 2. Delete UserMatriculas from this session
                var matriculas = await _context.UserMatriculas
                    .Where(m => m.ImportSessionId == importSessionId)
                    .ToListAsync();
                _context.UserMatriculas.RemoveRange(matriculas);

                // 3. Delete Users ONLY if they have no external dependencies
                var usersFromSession = await _context.Users
                    .Where(u => u.ImportSessionId == importSessionId)
                    .ToListAsync();

                foreach (var user in usersFromSession)
                {
                    // Check if user has any contracts NOT from this session
                    var hasExternalContracts = await _context.Contracts
                        .AnyAsync(c => c.User.Id == user.Id && c.ImportSessionId != importSessionId);

                    // Check if user has any matriculas NOT from this session
                    var hasExternalMatriculas = await _context.UserMatriculas
                        .AnyAsync(m => m.User.Id == user.Id && m.ImportSessionId != importSessionId);

                    // Check if user has any child users (is a parent)
                    var hasChildUsers = await _context.Users
                        .AnyAsync(u => u.ParentUserId == user.Id);

                    // Only delete if user has NO external dependencies
                    if (!hasExternalContracts && !hasExternalMatriculas && !hasChildUsers)
                    {
                        _context.Users.Remove(user);
                    }
                }

                // 4. Delete PVs (only if created via import and no external dependencies)
                var pvsFromSession = await _context.PVs
                    .Where(p => p.ImportSessionId == importSessionId)
                    .ToListAsync();
                
                foreach (var pv in pvsFromSession)
                {
                    var hasExternalContracts = await _context.Contracts
                        .AnyAsync(c => c.PvId == pv.Id && c.ImportSessionId != importSessionId);
                    
                    if (!hasExternalContracts)
                    {
                        _context.PVs.Remove(pv);
                    }
                }

                // 5. Delete Groups (only if created via import and no external dependencies)
                var groupsFromSession = await _context.Groups
                    .Where(g => g.ImportSessionId == importSessionId)
                    .ToListAsync();
                
                foreach (var group in groupsFromSession)
                {
                    var hasExternalContracts = await _context.Contracts
                        .AnyAsync(c => c.GroupId == group.Id && c.ImportSessionId != importSessionId);
                    
                    if (!hasExternalContracts)
                    {
                        _context.Groups.Remove(group);
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Console.WriteLine($"[ImportExecutionService] Undo failed for session {importSessionId}: {ex.Message}");
                return false;
            }
        }
    }
}
