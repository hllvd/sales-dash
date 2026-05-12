using SalesApp.Models;
using SalesApp.Repositories;
using SalesApp.ReportFilters.DTOs;
using SalesApp.ReportFilters.Models;
using SalesApp.ReportFilters.Repositories;
using SalesApp.ReportFilters.Validators;

namespace SalesApp.ReportFilters.Services
{
    /// <summary>
    /// Implements all business rules for saved report filters.
    ///
    /// Ownership: A superadmin can only manage reports they created (userId match).
    /// Visibility: shared reports are readable by all authenticated users;
    ///             private reports are readable only by their owner.
    /// column projection: after fetching contract data via IContractRepository,
    ///             each contract is projected to a { label → value } dictionary
    ///             using only the OutputColumns defined in the saved report.
    /// </summary>
    public class ReportFilterService : IReportFilterService
    {
        private readonly IReportFilterRepository _repository;
        private readonly IContractRepository _contractRepository;
        private readonly IUserRepository _userRepository;

        public ReportFilterService(
            IReportFilterRepository repository,
            IContractRepository contractRepository,
            IUserRepository userRepository)
        {
            _repository = repository;
            _contractRepository = contractRepository;
            _userRepository = userRepository;
        }

        // ── List ──────────────────────────────────────────────────────────────

        public async Task<ServiceResult<List<ReportFilterResponse>>> ListAsync(string callerId)
        {
            var filters = await _repository.ListForUserAsync(callerId);
            var response = filters.Select(MapToResponse).ToList();
            return new ServiceResult<List<ReportFilterResponse>>(true, response);
        }

        // ── Get single ────────────────────────────────────────────────────────

        public async Task<ServiceResult<ReportFilterResponse>> GetAsync(string callerId, string filterId)
        {
            // We need to find the filter regardless of who owns it (to support shared reports).
            // Strategy: try the caller's own key first, then search among shared reports.
            var filter = await _repository.GetByIdAsync(callerId, filterId);

            if (filter == null)
            {
                // Caller doesn't own it — check if it exists as a shared report via GSI results
                var all = await _repository.ListForUserAsync(callerId);
                filter = all.FirstOrDefault(f => f.FilterId == filterId && f.Scope == "shared");
            }

            if (filter == null)
                return new ServiceResult<ReportFilterResponse>(false, null, null, 404);

            // Private reports not owned by the caller → 404 (do not reveal existence)
            if (filter.Scope == "private" && filter.UserId != callerId)
                return new ServiceResult<ReportFilterResponse>(false, null, null, 404);

            return new ServiceResult<ReportFilterResponse>(true, MapToResponse(filter));
        }

        // ── Create ────────────────────────────────────────────────────────────

        public async Task<ServiceResult<ReportFilterResponse>> CreateAsync(
            string callerId,
            CreateReportFilterRequest request)
        {
            var errors = ReportFilterValidationRules.Validate(request);
            if (errors.Count > 0)
                return new ServiceResult<ReportFilterResponse>(false, null, errors, 400);

            var now = DateTime.UtcNow;
            var filterId = GenerateFilterId();

            var filter = new ReportFilter
            {
                PK          = "REP#",
                SK          = $"#U-{callerId}#REP-{filterId}",
                UserId      = callerId,
                FilterId    = filterId,
                Name        = request.Name.Trim(),
                Description = request.Description?.Trim(),
                Scope       = request.Scope.ToLower(),
                FilterConfig = MapFilterConfig(request.FilterConfig),
                OutputColumns = MapOutputColumns(request.OutputColumns),
                GroupByEmail = request.GroupByEmail,
                CreatedAt   = now,
                UpdatedAt   = now
            };

            await _repository.CreateAsync(filter);
            return new ServiceResult<ReportFilterResponse>(true, MapToResponse(filter), null, 201);
        }

        // ── Update ────────────────────────────────────────────────────────────

        public async Task<ServiceResult<ReportFilterResponse>> UpdateAsync(
            string callerId,
            string filterId,
            UpdateReportFilterRequest request)
        {
            var errors = ReportFilterValidationRules.Validate(request);
            if (errors.Count > 0)
                return new ServiceResult<ReportFilterResponse>(false, null, errors, 400);

            var filter = await _repository.GetByIdAsync(callerId, filterId);
            if (filter == null)
                return new ServiceResult<ReportFilterResponse>(false, null, null, 404);

            // Ownership check — a superadmin may only edit their own reports
            if (filter.UserId != callerId)
                return new ServiceResult<ReportFilterResponse>(false, null, null, 403);

            filter.Name          = request.Name.Trim();
            filter.Description   = request.Description?.Trim();
            filter.Scope         = request.Scope.ToLower();
            filter.FilterConfig  = MapFilterConfig(request.FilterConfig);
            filter.OutputColumns = MapOutputColumns(request.OutputColumns);
            filter.GroupByEmail  = request.GroupByEmail;
            filter.UpdatedAt     = DateTime.UtcNow;

            await _repository.UpdateAsync(filter);
            return new ServiceResult<ReportFilterResponse>(true, MapToResponse(filter));
        }

        // ── Delete ────────────────────────────────────────────────────────────

        public async Task<ServiceResult<bool>> DeleteAsync(string callerId, string filterId)
        {
            var filter = await _repository.GetByIdAsync(callerId, filterId);
            if (filter == null)
                return new ServiceResult<bool>(false, false, null, 404);

            if (filter.UserId != callerId)
                return new ServiceResult<bool>(false, false, null, 403);

            await _repository.DeleteAsync(callerId, filterId);
            return new ServiceResult<bool>(true, true);
        }

        // ── Execute (results) ─────────────────────────────────────────────────

        public async Task<ServiceResult<ReportResultsResponse>> ExecuteAsync(
            string callerId,
            string filterId,
            Guid? currentUserId,
            int page,
            int pageSize)
        {
            // Resolve report (same visibility rules as GetAsync)
            var getResult = await GetAsync(callerId, filterId);
            if (!getResult.Success)
                return new ServiceResult<ReportResultsResponse>(false, null, null, getResult.StatusCode);

            var report = getResult.Data!;
            var fc = report.FilterConfig;

            // Build filter arguments to pass to existing IContractRepository.GetAllAsync
            Guid? userIdFilter = null;
            string? emailFilter = null;
            int? groupIdFilter = null;
            string? matriculaFilter = null;

            // Handle emails: if multiple, use first (IContractRepository only accepts a single email)
            // For multi-email scenarios, callers can chain — this is a known limitation of the
            // existing repository interface.
            if (fc.Emails?.Count > 0)
                emailFilter = fc.Emails[0];

            if (fc.Groups?.Count > 0)
                groupIdFilter = fc.Groups[0];

            if (fc.Matriculas?.Count > 0)
                matriculaFilter = fc.Matriculas[0];

            // currentUserAsParent: if true and currentUserId is known, resolve as a userId filter
            // The JWT user identity is injected as currentUserId at query time — never stored in the filter.
            if (fc.CurrentUserAsParent == true && currentUserId.HasValue)
            {
                // Find contracts owned by the authenticated user directly
                userIdFilter = currentUserId;
            }

            var resolvedStartDate = ResolveDate(fc.StartDate, fc.RelativeStartDate);
            var resolvedEndDate = ResolveDate(fc.EndDate, fc.RelativeEndDate);

            // Reuse existing GetAllAsync — same logic as /api/contracts, no duplication
            var contracts = await _contractRepository.GetAllAsync(
                userId: userIdFilter,
                groupId: groupIdFilter,
                startDate: resolvedStartDate,
                endDate: resolvedEndDate,
                contractNumber: null,
                showUnassigned: null,
                matriculaNumber: matriculaFilter,
                userEmail: emailFilter,
                scope: null // No scope restriction for superadmin-executed reports
            );

            // Filter PVs in memory since IContractRepository does not support PvId lists
            if (fc.Pvs?.Count > 0)
            {
                contracts = contracts.Where(c => c.PvId.HasValue && fc.Pvs.Contains(c.PvId.Value)).ToList();
            }

            // Filter by status in memory (same pattern as PV filter)
            if (fc.Statuses?.Count > 0)
            {
                var op = (fc.StatusOperator ?? "or").ToLower();
                if (op == "and")
                    contracts = contracts.Where(c =>
                        fc.Statuses.All(s => string.Equals(c.ContractStatus?.Name, s, StringComparison.OrdinalIgnoreCase))
                    ).ToList();
                else // "or" (default)
                    contracts = contracts.Where(c =>
                        fc.Statuses.Any(s => string.Equals(c.ContractStatus?.Name, s, StringComparison.OrdinalIgnoreCase))
                    ).ToList();
            }

            if (report.GroupByEmail)
            {
                // Group by user email; null email maps to a shared "(Sem usuário)" bucket
                var grouped = contracts
                    .GroupBy(c => c.User?.Email ?? "(Sem usuário)")
                    .Select(g =>
                    {
                        // Aggregate: sum totalAmount, keep first contract for other fields
                        var first = g.First();
                        // ✅ IMPORTANT: Since we use AsNoTracking, we can safely mutate the object in memory
                        // for projection without affecting the database.
                        first.TotalAmount = g.Sum(c => c.TotalAmount);
                        return first;
                    })
                    .OrderByDescending(c => c.TotalAmount)
                    .ToList();

                contracts = grouped;
            }

            // Apply pagination
            var totalCount = contracts.Count;
            var safePage = Math.Max(1, page);
            var safePageSize = Math.Clamp(pageSize, 1, 200);
            var totalPages = (int)Math.Ceiling(totalCount / (double)safePageSize);
            var paged = contracts
                .Skip((safePage - 1) * safePageSize)
                .Take(safePageSize)
                .ToList();

            // Project each contract to only the outputColumns fields
            var columns = report.OutputColumns.OrderBy(c => c.Order).ToList();
            var rows = paged.Select(c => ProjectContract(c, columns)).ToList();

            var result = new ReportResultsResponse
            {
                Page       = safePage,
                PageSize   = safePageSize,
                TotalCount = totalCount,
                TotalPages = totalPages,
                Columns    = columns.Select(col => new OutputColumnResponse
                {
                    Source = col.Source,
                    Field  = col.Field,
                    Label  = col.Label,
                    Order  = col.Order,
                    Format = col.Format
                }).ToList(),
                Rows = rows
            };

            if (report.GroupByEmail && !result.Columns.Any(c => c.Source == "Users_Contract" && c.Field == "email"))
            {
                result.Columns.Insert(0, new OutputColumnResponse
                {
                    Source = "Users_Contract",
                    Field  = "email",
                    Label  = "Email",
                    Order  = 0
                });
            }

            return new ServiceResult<ReportResultsResponse>(true, result);
        }

        // ── Available columns ─────────────────────────────────────────────────

        public AvailableColumnsResponse GetAvailableColumns()
        {
            // Field names are the actual property names from the domain models.
            // Contracts: Contract.cs
            // Users_Contract / Users_Matricula: User.cs (fields meaningful for contract context)
            // Status: ContractStatusEntity.cs
            // PV: PV.cs
            // Group: Group.cs
            return new AvailableColumnsResponse
            {
                Sources = new List<SourceColumns>
                {
                    new SourceColumns
                    {
                        Source = "Contracts",
                        Fields = new List<string>
                        {
                            "contractNumber",
                            "totalAmount",
                            "saleStartDate",
                            "isActive",
                            "contractType",
                            "quota",
                            "customerName",
                            "tempMatricula",
                            "matriculaNumber",
                            "createdAt",
                            "updatedAt"
                        }
                    },
                    new SourceColumns
                    {
                        Source = "Users_Contract",
                        Fields = new List<string>
                        {
                            "name",
                            "email"
                        }
                    },
                    new SourceColumns
                    {
                        Source = "Users_Matricula",
                        Fields = new List<string>
                        {
                            "name",
                            "email"
                        }
                    },
                    new SourceColumns
                    {
                        Source = "Status",
                        Fields = new List<string>
                        {
                            "name"
                        }
                    },
                    new SourceColumns
                    {
                        Source = "PV",
                        Fields = new List<string>
                        {
                            "id",
                            "name"
                        }
                    },
                    new SourceColumns
                    {
                        Source = "Group",
                        Fields = new List<string>
                        {
                            "name",
                            "description",
                            "commission"
                        }
                    }
                }
            };
        }

        // ── Private helpers ───────────────────────────────────────────────────

        /// <summary>
        /// Projects a Contract entity down to only the requested columns.
        /// Returns { label → value } dictionary — null for unknown/missing fields.
        /// </summary>
        private static Dictionary<string, object?> ProjectContract(
            SalesApp.Models.Contract contract,
            List<OutputColumnResponse> columns)
        {
            var row = new Dictionary<string, object?>();

            foreach (var col in columns)
            {
                row[col.Label] = ResolveField(contract, col.Source, col.Field, col.Format);
            }

            // Synthetic column injection for grouped reports
            if (!row.ContainsKey("Email") && contract.User != null)
            {
                // We use a fixed label "Email" to match the synthetic column definition in ExecuteAsync
                row["Email"] = contract.User.Email;
            }
            else if (!row.ContainsKey("Email") && contract.User == null)
            {
                row["Email"] = "(Sem usuário)";
            }

            return row;
        }

        private static object? ResolveField(SalesApp.Models.Contract c, string source, string field, string? format = null)
        {
            object? rawValue = source switch
            {
                "Contracts" => field switch
                {
                    "contractNumber"  => c.ContractNumber,
                    "totalAmount"     => c.TotalAmount,
                    "saleStartDate"   => (object?)c.SaleStartDate,
                    "isActive"        => c.IsActive,
                    "contractType"    => c.ContractType,
                    "quota"           => c.Quota,
                    "customerName"    => c.CustomerName,
                    "tempMatricula"   => c.TempMatricula,
                    "matriculaNumber" => c.Matricula?.MatriculaNumber,
                    "createdAt"       => c.CreatedAt,
                    "updatedAt"       => c.UpdatedAt,
                    _                 => null
                },
                "Users_Contract" => field switch
                {
                    "name"  => c.User?.Name,
                    "email" => c.User?.Email,
                    _       => null
                },
                "Users_Matricula" => c.Matricula?.UserMatriculas
                    .Select(um => um.User)
                    .Where(u => u != null)
                    .Select(u => field switch
                    {
                        "name"  => (object?)u!.Name,
                        "email" => u!.Email,
                        _       => null
                    })
                    .FirstOrDefault(),
                "Status" => field switch
                {
                    "name" => c.ContractStatus?.Name,
                    _      => null
                },
                "PV" => field switch
                {
                    "id"   => c.PV?.Id,
                    "name" => c.PV?.Name,
                    _      => null
                },
                "Group" => field switch
                {
                    "name"        => c.Group?.Name,
                    "description" => c.Group?.Description,
                    "commission"  => (object?)c.Group?.Commission,
                    _             => null
                },
                _ => null
            };

            return ApplyFormat(rawValue, format);
        }

        private static object? ApplyFormat(object? value, string? format)
        {
            if (value == null) return null;
            if (string.IsNullOrEmpty(format)) return value;

            if (format.ToLower() == "br")
            {
                var ptBr = new System.Globalization.CultureInfo("pt-BR");
                
                if (value is DateTime dt)
                {
                    return dt.ToString("dd/MM/yyyy", ptBr);
                }
                
                if (value is decimal d)
                {
                    return d.ToString("N2", ptBr);
                }
                
                if (value is double db)
                {
                    return db.ToString("N2", ptBr);
                }

                if (value is float f)
                {
                    return f.ToString("N2", ptBr);
                }
            }

            return value;
        }

        private static DateTime? ResolveDate(DateTime? absoluteDate, string? relativeExpr)
        {
            if (absoluteDate.HasValue) return absoluteDate;
            if (string.IsNullOrWhiteSpace(relativeExpr)) return null;
            
            var expr = relativeExpr.Trim();
            if (expr.Equals("now", StringComparison.OrdinalIgnoreCase)) return DateTime.UtcNow;
            if (expr.Equals("thisMonth", StringComparison.OrdinalIgnoreCase)) 
            {
                var now = DateTime.UtcNow;
                return new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            }
            
            bool isNegative = expr.StartsWith("-");
            bool isPositive = expr.StartsWith("+");
            if (isNegative || isPositive)
            {
                var valueStr = expr.Substring(1, expr.Length - 2);
                var unit = expr.Last();
                if (int.TryParse(valueStr, out int val))
                {
                    int multiplier = isNegative ? -1 : 1;
                    return unit switch
                    {
                        'd' => DateTime.UtcNow.AddDays(val * multiplier),
                        'M' => DateTime.UtcNow.AddMonths(val * multiplier),
                        'y' => DateTime.UtcNow.AddYears(val * multiplier),
                        _ => null
                    };
                }
            }
            return null;
        }

        // ── Mapping helpers ───────────────────────────────────────────────────

        private static ReportFilterResponse MapToResponse(ReportFilter f) =>
            new()
            {
                FilterId    = f.FilterId,
                UserId      = f.UserId,
                Name        = f.Name,
                Description = f.Description,
                Scope       = f.Scope,
                GroupByEmail = f.GroupByEmail,
                CreatedAt   = f.CreatedAt,
                UpdatedAt   = f.UpdatedAt,
                FilterConfig = new FilterConfigResponse
                {
                    Matriculas          = f.FilterConfig.Matriculas,
                    StartDate           = f.FilterConfig.StartDate,
                    EndDate             = f.FilterConfig.EndDate,
                    RelativeStartDate   = f.FilterConfig.RelativeStartDate,
                    RelativeEndDate     = f.FilterConfig.RelativeEndDate,
                    CurrentUserAsParent = f.FilterConfig.CurrentUserAsParent,
                    Emails              = f.FilterConfig.Emails,
                    Groups              = f.FilterConfig.Groups,
                    Pvs                 = f.FilterConfig.Pvs,
                    Statuses            = f.FilterConfig.Statuses,
                    StatusOperator      = f.FilterConfig.StatusOperator
                },
                OutputColumns = f.OutputColumns
                    .OrderBy(c => c.Order)
                    .Select(c => new OutputColumnResponse
                    {
                        Source = c.Source,
                        Field  = c.Field,
                        Label  = c.Label,
                        Order  = c.Order,
                        Format = c.Format
                    }).ToList()
            };

        private static FilterConfig MapFilterConfig(FilterConfigRequest req) =>
            new()
            {
                Matriculas          = req.Matriculas,
                StartDate           = req.StartDate,
                EndDate             = req.EndDate,
                RelativeStartDate   = req.RelativeStartDate,
                RelativeEndDate     = req.RelativeEndDate,
                CurrentUserAsParent = req.CurrentUserAsParent,
                Emails              = req.Emails,
                Groups              = req.Groups,
                Pvs                 = req.Pvs,
                Statuses            = req.Statuses,
                StatusOperator      = req.StatusOperator
            };

        private static List<OutputColumn> MapOutputColumns(List<OutputColumnRequest> columns) =>
            columns.Select(c => new OutputColumn
            {
                Source = c.Source,
                Field  = c.Field,
                Label  = c.Label,
                Order  = c.Order,
                Format = c.Format
            }).ToList();

        /// <summary>
        /// Generates a time-ordered unique ID suitable for use as a filterId.
        /// Format: {YYYYMMDDHHmmssfff}-{guid-suffix} — lexicographically sortable by creation time.
        /// This is a pragmatic substitute for ULID without requiring an additional NuGet package.
        /// </summary>
        private static string GenerateFilterId()
        {
            var ts = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
            var suffix = Guid.NewGuid().ToString("N")[..12];
            return $"{ts}{suffix}";
        }
    }
}
