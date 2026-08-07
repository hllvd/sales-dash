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
        private readonly ITeamRepository _teamRepository;
        private readonly IClassificationLevelRepository _classificationLevelRepository;
        private readonly IUserClassificationRepository _userClassificationRepository;

        // Set during ExecuteAsync when GroupByEmail/GroupByTeam/GroupByClassification is active; null otherwise.
        // Safe as a mutable field because this service is Scoped (one instance per HTTP request).
        private Dictionary<string, decimal>? _retentionByEmail;
        private Dictionary<string, decimal>? _strictRetentionByEmail;
        private Dictionary<string, int>? _contractCountByEmail;
        private Dictionary<string, decimal>? _retentionByTeam;
        private Dictionary<string, decimal>? _strictRetentionByTeam;
        private Dictionary<string, int>? _contractCountByTeam;
        private Dictionary<string, decimal>? _retentionByClassification;
        private Dictionary<string, decimal>? _strictRetentionByClassification;
        private Dictionary<string, int>? _contractCountByClassification;

        public ReportFilterService(
            IReportFilterRepository repository,
            IContractRepository contractRepository,
            IUserRepository userRepository,
            ITeamRepository teamRepository,
            IClassificationLevelRepository classificationLevelRepository,
            IUserClassificationRepository userClassificationRepository)
        {
            _repository = repository;
            _contractRepository = contractRepository;
            _userRepository = userRepository;
            _teamRepository = teamRepository;
            _classificationLevelRepository = classificationLevelRepository;
            _userClassificationRepository = userClassificationRepository;
        }

        // ── List ──────────────────────────────────────────────────────────────

        public async Task<ServiceResult<List<ReportFilterResponse>>> ListAsync(string callerId)
        {
            var filters = await _repository.ListForUserAsync(callerId);
            
            // Load caller details to check visibility restrictions
            var user = await _userRepository.GetByIdAsync(new Guid(callerId));
            if (user == null)
                return new ServiceResult<List<ReportFilterResponse>>(true, new List<ReportFilterResponse>());

            bool isSuperAdmin = user.Role?.Name == "superadmin";
            
            var allowedFilters = new List<ReportFilter>();
            foreach (var f in filters)
            {
                // Owner and Superadmin are always allowed
                if (f.UserId == callerId || isSuperAdmin)
                {
                    allowedFilters.Add(f);
                    continue;
                }

                // Private reports not owned -> skip
                if (f.Scope == "private")
                    continue;

                // Shared report restrictions
                if (f.AllowedRoles?.Count > 0 || f.AllowedTeamIds?.Count > 0)
                {
                    bool roleMatch = f.AllowedRoles?.Count > 0 && 
                                     user.Role?.Name != null && 
                                     f.AllowedRoles.Contains(user.Role.Name);

                    bool teamMatch = false;
                    if (f.AllowedTeamIds?.Count > 0)
                    {
                        var activeTeamIds = user.UserTeams
                            .Where(ut => ut.StartDate <= DateTime.UtcNow && (ut.EndDate == null || ut.EndDate > DateTime.UtcNow))
                            .Select(ut => ut.TeamId)
                            .ToList();

                        teamMatch = activeTeamIds.Any(tid => f.AllowedTeamIds.Contains(tid));
                    }

                    if (roleMatch || teamMatch)
                    {
                        allowedFilters.Add(f);
                    }
                }
                else
                {
                    // No restrictions -> allowed
                    allowedFilters.Add(f);
                }
            }

            var response = allowedFilters.Select(MapToResponse).ToList();
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

            // Owner is always allowed
            if (filter.UserId == callerId)
                return new ServiceResult<ReportFilterResponse>(true, MapToResponse(filter));

            // Load caller details
            var user = await _userRepository.GetByIdAsync(new Guid(callerId));
            if (user == null)
                return new ServiceResult<ReportFilterResponse>(false, null, null, 404);

            // Superadmin is always allowed
            bool isSuperAdmin = user.Role?.Name == "superadmin";
            if (isSuperAdmin)
                return new ServiceResult<ReportFilterResponse>(true, MapToResponse(filter));

            // If private and not owned by caller -> 404
            if (filter.Scope == "private")
                return new ServiceResult<ReportFilterResponse>(false, null, null, 404);

            // Check shared report restrictions
            if (filter.AllowedRoles?.Count > 0 || filter.AllowedTeamIds?.Count > 0)
            {
                bool roleMatch = filter.AllowedRoles?.Count > 0 && 
                                 user.Role?.Name != null && 
                                 filter.AllowedRoles.Contains(user.Role.Name);

                bool teamMatch = false;
                if (filter.AllowedTeamIds?.Count > 0)
                {
                    var activeTeamIds = user.UserTeams
                        .Where(ut => ut.StartDate <= DateTime.UtcNow && (ut.EndDate == null || ut.EndDate > DateTime.UtcNow))
                        .Select(ut => ut.TeamId)
                        .ToList();

                    teamMatch = activeTeamIds.Any(tid => filter.AllowedTeamIds.Contains(tid));
                }

                if (!roleMatch && !teamMatch)
                {
                    return new ServiceResult<ReportFilterResponse>(false, null, null, 404);
                }
            }

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
                GroupByTeam = request.GroupByTeam,
                GroupByClassification = request.GroupByClassification,
                HideUnassignedTeams = request.HideUnassignedTeams,
                OrderByField = request.OrderByField,
                OrderByDirection = request.OrderByDirection,
                AllowedTeamIds = request.AllowedTeamIds,
                AllowedRoles = request.AllowedRoles,
                SumTotal = request.SumTotal,
                OutputType = request.OutputType ?? "table",
                ChartType = request.ChartType ?? "bar",
                SummaryRetentionType = request.SummaryRetentionType ?? "standard",
                ChartMetric = request.ChartMetric,
                ExportedFields = MapExportedFields(request.ExportedFields),
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
            filter.GroupByTeam   = request.GroupByTeam;
            filter.GroupByClassification = request.GroupByClassification;
            filter.HideUnassignedTeams = request.HideUnassignedTeams;
            filter.OrderByField  = request.OrderByField;
            filter.OrderByDirection = request.OrderByDirection;
            filter.AllowedTeamIds = request.AllowedTeamIds;
            filter.AllowedRoles = request.AllowedRoles;
            filter.SumTotal = request.SumTotal;
            filter.OutputType = request.OutputType ?? "table";
            filter.ChartType = request.ChartType ?? "bar";
            filter.SummaryRetentionType = request.SummaryRetentionType ?? "standard";
            filter.ChartMetric = request.ChartMetric;
            filter.ExportedFields = MapExportedFields(request.ExportedFields);
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
            int pageSize,
            List<int>? overrideTeamIds = null,
            List<string>? overrideEmails = null,
            List<int>? overrideStoreIds = null)
        {
            // Resolve report (same visibility rules as GetAsync)
            var getResult = await GetAsync(callerId, filterId);
            if (!getResult.Success)
                return new ServiceResult<ReportResultsResponse>(false, null, null, getResult.StatusCode);

            var report = getResult.Data!;
            var fc = report.FilterConfig;

            // Apply viewer overrides (replace the filter entirely if supplied)
            if (overrideTeamIds != null)
            {
                fc.Teams = overrideTeamIds.Count > 0 ? overrideTeamIds : new List<int>();
            }
            if (overrideEmails != null)
            {
                fc.Emails = overrideEmails.Count > 0 ? overrideEmails : new List<string>();
            }
            if (overrideStoreIds != null)
            {
                fc.Stores = overrideStoreIds.Count > 0 ? overrideStoreIds : new List<int>();
            }

            var resolvedStartDate = ResolveDate(fc.StartDate, fc.RelativeStartDate);
            var resolvedEndDate = ResolveDate(fc.EndDate, fc.RelativeEndDate);

            // Ensure the end date is inclusive of the entire day if it's an absolute date
            if (fc.EndDate.HasValue && string.IsNullOrEmpty(fc.RelativeEndDate))
            {
                resolvedEndDate = fc.EndDate.Value.Date.AddDays(1).AddTicks(-1);
            }

            // Load all teams and memberships in memory once for fast lookup
            var teamsList = await _teamRepository.GetAllAsync();
            var memberTeamMapping = teamsList
                .SelectMany(t => t.UserTeams.Select(ut => new
                {
                    ut.UserInternalId,
                    ut.StartDate,
                    ut.EndDate,
                    TeamId = t.Id,
                    TeamName = t.Name,
                    TeamOwnerName = t.Owner?.Name ?? "(Sem chefe)",
                    StoreId = t.StoreId,
                    StoreName = t.Store?.Name ?? "(Sem loja)"
                }))
                .ToList();

            Func<SalesApp.Models.Contract, string> getStoreName = c =>
            {
                if (c.UserInternalId == null) return "(Sem loja)";
                var match = memberTeamMapping.FirstOrDefault(x =>
                    x.UserInternalId == c.UserInternalId.Value &&
                    TeamMembershipResolver.IsMembershipActiveForSale(x.StartDate, x.EndDate, c.SaleStartDate, resolvedStartDate, resolvedEndDate));
                return match?.StoreName ?? "(Sem loja)";
            };

            Func<SalesApp.Models.Contract, string> getTeamName = c =>
            {
                if (c.UserInternalId == null) return "(Sem equipe)";
                var match = memberTeamMapping.FirstOrDefault(x => 
                    x.UserInternalId == c.UserInternalId.Value &&
                    TeamMembershipResolver.IsMembershipActiveForSale(x.StartDate, x.EndDate, c.SaleStartDate, resolvedStartDate, resolvedEndDate));
                return match?.TeamName ?? "(Sem equipe)";
            };

            Func<SalesApp.Models.Contract, string> getTeamOwnerName = c =>
            {
                if (c.UserInternalId == null) return "(Sem chefe)";
                var match = memberTeamMapping.FirstOrDefault(x => 
                    x.UserInternalId == c.UserInternalId.Value &&
                    TeamMembershipResolver.IsMembershipActiveForSale(x.StartDate, x.EndDate, c.SaleStartDate, resolvedStartDate, resolvedEndDate));
                return match?.TeamOwnerName ?? "(Sem chefe)";
            };

            // Load classifications once in memory for fast lookup
            var levelsList = await _classificationLevelRepository.GetAllAsync();
            var levelsMap = levelsList.ToDictionary(l => l.Id, l => l.Name);
            var allClassifications = new List<UserClassification>();
            foreach (var level in levelsList)
            {
                var cls = await _userClassificationRepository.GetForLevelAsync(level.Id);
                allClassifications.AddRange(cls);
            }

            Func<SalesApp.Models.Contract, string> getClassification = c =>
            {
                if (c.UserInternalId == null) return "—";
                var match = allClassifications.FirstOrDefault(x => 
                    x.UserInternalId == c.UserInternalId.Value &&
                    TeamMembershipResolver.IsMembershipActiveForSale(x.StartDate, x.EndDate, c.SaleStartDate, resolvedStartDate, resolvedEndDate));
                if (match != null)
                {
                    return levelsMap.GetValueOrDefault(match.LevelId, "—");
                }
                var currentMatch = allClassifications.FirstOrDefault(x =>
                    x.UserInternalId == c.UserInternalId.Value &&
                    x.EndDate == null);
                return currentMatch != null ? levelsMap.GetValueOrDefault(currentMatch.LevelId, "—") : "—";
            };

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

            // currentUserTeam / currentUserMatricula: load the current user entity once if either flag is set.
            // GetByIdAsync eagerly loads UserMatriculas (with Matricula) and UserTeams, so no extra round-trips.
            if ((fc.CurrentUserTeam == true || fc.CurrentUserMatricula == true) && currentUserId.HasValue)
            {
                var currentUserEntity = await _userRepository.GetByIdAsync(currentUserId.Value);
                if (currentUserEntity != null)
                {
                    // currentUserTeam: inject the user's currently-active team IDs into fc.Teams
                    if (fc.CurrentUserTeam == true)
                    {
                        var now = DateTime.UtcNow;
                        var activeTeamIds = memberTeamMapping
                            .Where(x => x.UserInternalId == currentUserEntity.InternalId
                                     && x.StartDate <= now
                                     && (x.EndDate == null || x.EndDate > now))
                            .Select(x => x.TeamId)
                            .Distinct()
                            .ToList();

                        fc.Teams = (fc.Teams ?? new List<int>())
                            .Union(activeTeamIds)
                            .Distinct()
                            .ToList();
                    }

                    // currentUserMatricula: inject the user's active matricula numbers into fc.Matriculas
                    if (fc.CurrentUserMatricula == true)
                    {
                        var userMatriculaNumbers = currentUserEntity.UserMatriculas
                            .Where(um => um.Matricula != null
                                      && !string.IsNullOrWhiteSpace(um.Matricula.MatriculaNumber))
                            .Select(um => um.Matricula!.MatriculaNumber)
                            .Distinct()
                            .ToList();

                        fc.Matriculas = (fc.Matriculas ?? new List<string>())
                            .Union(userMatriculaNumbers)
                            .Distinct()
                            .ToList();
                    }
                }

                // If dynamic resolution resulted in no values (or user entity not found),
                // we must force an empty match instead of skipping the filter and returning everything.
                if (fc.CurrentUserTeam == true && (fc.Teams == null || fc.Teams.Count == 0))
                {
                    fc.Teams = new List<int> { -1 };
                }
                if (fc.CurrentUserMatricula == true && (fc.Matriculas == null || fc.Matriculas.Count == 0))
                {
                    fc.Matriculas = new List<string> { "__invalid_non_existent_matricula__" };
                }
            }


            // Reuse existing GetAllAsync — same logic as /api/contracts, no duplication
            var contracts = await _contractRepository.GetAllAsync(
                userId: userIdFilter,
                groupId: groupIdFilter,
                startDate: resolvedStartDate,
                endDate: resolvedEndDate,
                contractNumber: null,
                showUnassigned: null,
                matriculaNumbers: !string.IsNullOrEmpty(matriculaFilter) ? new List<string> { matriculaFilter } : null,
                userEmail: emailFilter,
                scope: null // No scope restriction for superadmin-executed reports
            );

            // Filter PVs in memory since IContractRepository does not support PvId lists
            if (fc.Pvs?.Count > 0)
            {
                contracts = contracts.Where(c => c.PvId.HasValue && fc.Pvs.Contains(c.PvId.Value)).ToList();
            }

            // Filter by selected Team IDs.
            // Behaviour is controlled by TeamMembershipMode:
            //   "current"    (default) – user must be CURRENTLY an active member of the team.
            //                           A user moved to another team will NOT appear here.
            //   "historical"           – user must have been a member at the time of the sale
            //                           AND within the report date range (original temporal logic).
            if (fc.Teams?.Count > 0)
            {
                var isHistorical = string.Equals(
                    fc.TeamMembershipMode, "historical", StringComparison.OrdinalIgnoreCase);

                if (isHistorical)
                {
                    // Historical: membership active at the time of the contract sale
                    contracts = contracts.Where(c =>
                    {
                        if (c.UserInternalId == null) return false;
                        var match = memberTeamMapping.FirstOrDefault(x =>
                            x.UserInternalId == c.UserInternalId.Value &&
                            TeamMembershipResolver.IsMembershipActiveForSale(x.StartDate, x.EndDate, c.SaleStartDate, resolvedStartDate, resolvedEndDate));
                        return match != null && fc.Teams.Contains(match.TeamId);
                    }).ToList();
                }
                else
                {
                    // Current (default): user must be an active member RIGHT NOW.
                    // A membership is "current" when StartDate <= now and EndDate is null or in the future.
                    var now = DateTime.UtcNow;
                    var currentTeamByUser = memberTeamMapping
                        .Where(x => x.StartDate <= now && (x.EndDate == null || x.EndDate > now))
                        .GroupBy(x => x.UserInternalId)
                        .ToDictionary(g => g.Key, g => g.Select(x => x.TeamId).ToHashSet());

                    contracts = contracts.Where(c =>
                    {
                        if (c.UserInternalId == null) return false;
                        return currentTeamByUser.TryGetValue(c.UserInternalId.Value, out var userTeams)
                               && userTeams.Any(tid => fc.Teams.Contains(tid));
                    }).ToList();
                }
            }

            // Filter by selected Store IDs.
            // Resolves which teams belong to the selected stores, then filters contracts using the same
            // TeamMembershipMode logic as the Teams filter (current or historical).
            if (fc.Stores?.Count > 0)
            {
                var isHistorical = string.Equals(
                    fc.TeamMembershipMode, "historical", StringComparison.OrdinalIgnoreCase);

                if (isHistorical)
                {
                    contracts = contracts.Where(c =>
                    {
                        if (c.UserInternalId == null) return false;
                        var match = memberTeamMapping.FirstOrDefault(x =>
                            x.UserInternalId == c.UserInternalId.Value &&
                            TeamMembershipResolver.IsMembershipActiveForSale(x.StartDate, x.EndDate, c.SaleStartDate, resolvedStartDate, resolvedEndDate));
                        return match != null && match.StoreId.HasValue && fc.Stores.Contains(match.StoreId.Value);
                    }).ToList();
                }
                else
                {
                    // Current: user must currently be in a team that belongs to the selected store(s).
                    var now = DateTime.UtcNow;
                    var currentStoreByUser = memberTeamMapping
                        .Where(x => x.StartDate <= now && (x.EndDate == null || x.EndDate > now) && x.StoreId.HasValue)
                        .GroupBy(x => x.UserInternalId)
                        .ToDictionary(g => g.Key, g => g.Select(x => x.StoreId!.Value).ToHashSet());

                    contracts = contracts.Where(c =>
                    {
                        if (c.UserInternalId == null) return false;
                        return currentStoreByUser.TryGetValue(c.UserInternalId.Value, out var userStores)
                               && userStores.Any(sid => fc.Stores.Contains(sid));
                    }).ToList();
                }
            }

            // Exclude contracts from unassigned teams if HideUnassignedTeams is active
            if (report.HideUnassignedTeams)
            {
                contracts = contracts.Where(c => getTeamName(c) != "(Sem equipe)").ToList();
            }

            // Filter by selected Classification Level IDs
            if (fc.ClassificationLevelIds?.Count > 0)
            {
                contracts = contracts.Where(c =>
                {
                    if (c.UserInternalId == null) return false;
                    var match = allClassifications.FirstOrDefault(x => 
                        x.UserInternalId == c.UserInternalId.Value &&
                        TeamMembershipResolver.IsMembershipActiveForSale(x.StartDate, x.EndDate, c.SaleStartDate, resolvedStartDate, resolvedEndDate));
                    if (match != null)
                    {
                        return fc.ClassificationLevelIds.Contains(match.LevelId);
                    }
                    var currentMatch = allClassifications.FirstOrDefault(x =>
                        x.UserInternalId == c.UserInternalId.Value &&
                        x.EndDate == null);
                    return currentMatch != null && fc.ClassificationLevelIds.Contains(currentMatch.LevelId);
                }).ToList();
            }

            // ── Performance Metrics Filters ──────────────────────────────────────
            bool hasMetricFilter = fc.MinRetention.HasValue || fc.MaxRetention.HasValue
                || fc.MinStrictRetention.HasValue || fc.MaxStrictRetention.HasValue
                || fc.MinProduction.HasValue || fc.MaxProduction.HasValue;

            if (hasMetricFilter)
            {
                // Single-pass GroupBy to calculate production and retention metrics per user
                var metricsByUser = contracts
                    .GroupBy(c => c.UserInternalId ?? -1)
                    .ToDictionary(
                        g => g.Key,
                        g =>
                        {
                            var total = g
                                .Where(c => !string.Equals(c.ContractStatus?.Name, ContractStatus.AwaitingPayment.ToApiString(), StringComparison.OrdinalIgnoreCase))
                                .Sum(c => c.TotalAmount);
                            var activeSum = g
                                .Where(c => !string.Equals(c.ContractStatus?.Name, ContractStatus.Defaulted.ToApiString(), StringComparison.OrdinalIgnoreCase)
                                         && !string.Equals(c.ContractStatus?.Name, ContractStatus.AwaitingPayment.ToApiString(), StringComparison.OrdinalIgnoreCase))
                                .Sum(c => c.TotalAmount);
                            
                            var strictActiveSum = g
                                .Where(c => !string.Equals(c.ContractStatus?.Name, ContractStatus.Defaulted.ToApiString(), StringComparison.OrdinalIgnoreCase)
                                         && !string.Equals(c.ContractStatus?.Name, ContractStatus.Late1.ToApiString(), StringComparison.OrdinalIgnoreCase)
                                         && !string.Equals(c.ContractStatus?.Name, ContractStatus.Late2.ToApiString(), StringComparison.OrdinalIgnoreCase)
                                         && !string.Equals(c.ContractStatus?.Name, ContractStatus.Late3.ToApiString(), StringComparison.OrdinalIgnoreCase)
                                         && !string.Equals(c.ContractStatus?.Name, ContractStatus.AwaitingPayment.ToApiString(), StringComparison.OrdinalIgnoreCase))
                                .Sum(c => c.TotalAmount);

                            return new
                            {
                                Production = total,
                                Retention = total <= 0 ? 0m : activeSum / total,
                                StrictRetention = total <= 0 ? 0m : strictActiveSum / total
                            };
                        });

                contracts = contracts.Where(c =>
                {
                    var uid = c.UserInternalId ?? -1;
                    if (!metricsByUser.TryGetValue(uid, out var metrics)) return false;

                    if (fc.MinRetention.HasValue && metrics.Retention < fc.MinRetention.Value) return false;
                    if (fc.MaxRetention.HasValue && metrics.Retention > fc.MaxRetention.Value) return false;
                    
                    if (fc.MinStrictRetention.HasValue && metrics.StrictRetention < fc.MinStrictRetention.Value) return false;
                    if (fc.MaxStrictRetention.HasValue && metrics.StrictRetention > fc.MaxStrictRetention.Value) return false;
                    
                    if (fc.MinProduction.HasValue && metrics.Production < fc.MinProduction.Value) return false;
                    if (fc.MaxProduction.HasValue && metrics.Production > fc.MaxProduction.Value) return false;
                    
                    return true;
                }).ToList();
            }

            // ── Compute per-user/team retention BEFORE status filtering ───────
            // Retention must reflect a user's/team's FULL portfolio (all statuses), not just
            // the subset visible after a status filter is applied.
            if (report.GroupByEmail)
            {
                _retentionByEmail = contracts
                    .GroupBy(c => c.User?.Email ?? "(Sem usuário)")
                    .ToDictionary(
                        g => g.Key,
                        g =>
                        {
                            var total = g
                                .Where(c => !string.Equals(c.ContractStatus?.Name, ContractStatus.AwaitingPayment.ToApiString(), StringComparison.OrdinalIgnoreCase))
                                .Sum(c => c.TotalAmount);
                            if (total <= 0) return 0m;
                            var active = g
                                .Where(c => !string.Equals(
                                    c.ContractStatus?.Name,
                                    ContractStatus.Defaulted.ToApiString(),
                                    StringComparison.OrdinalIgnoreCase) &&
                                            !string.Equals(
                                    c.ContractStatus?.Name,
                                    ContractStatus.AwaitingPayment.ToApiString(),
                                    StringComparison.OrdinalIgnoreCase))
                                .Sum(c => c.TotalAmount);
                            return active / total;
                        });

                _strictRetentionByEmail = contracts
                    .GroupBy(c => c.User?.Email ?? "(Sem usuário)")
                    .ToDictionary(
                        g => g.Key,
                        g =>
                        {
                            var total = g
                                .Where(c => !string.Equals(c.ContractStatus?.Name, ContractStatus.AwaitingPayment.ToApiString(), StringComparison.OrdinalIgnoreCase))
                                .Sum(c => c.TotalAmount);
                            if (total <= 0) return 0m;
                            var strictActive = g
                                .Where(c => !string.Equals(
                                    c.ContractStatus?.Name,
                                    ContractStatus.Defaulted.ToApiString(),
                                    StringComparison.OrdinalIgnoreCase) &&
                                            !string.Equals(
                                    c.ContractStatus?.Name,
                                    ContractStatus.Late1.ToApiString(),
                                    StringComparison.OrdinalIgnoreCase) &&
                                            !string.Equals(
                                    c.ContractStatus?.Name,
                                    ContractStatus.Late2.ToApiString(),
                                    StringComparison.OrdinalIgnoreCase) &&
                                            !string.Equals(
                                    c.ContractStatus?.Name,
                                    ContractStatus.Late3.ToApiString(),
                                    StringComparison.OrdinalIgnoreCase) &&
                                            !string.Equals(
                                    c.ContractStatus?.Name,
                                    ContractStatus.AwaitingPayment.ToApiString(),
                                    StringComparison.OrdinalIgnoreCase))
                                .Sum(c => c.TotalAmount);
                            return strictActive / total;
                        });
            }
            else
            {
                _retentionByEmail = null;
                _strictRetentionByEmail = null;
            }

            if (report.GroupByTeam)
            {
                _retentionByTeam = contracts
                    .GroupBy(getTeamName)
                    .ToDictionary(
                        g => g.Key,
                        g =>
                        {
                            var total = g
                                .Where(c => !string.Equals(c.ContractStatus?.Name, ContractStatus.AwaitingPayment.ToApiString(), StringComparison.OrdinalIgnoreCase))
                                .Sum(c => c.TotalAmount);
                            if (total <= 0) return 0m;
                            var active = g
                                .Where(c => !string.Equals(
                                    c.ContractStatus?.Name,
                                    ContractStatus.Defaulted.ToApiString(),
                                    StringComparison.OrdinalIgnoreCase) &&
                                            !string.Equals(
                                    c.ContractStatus?.Name,
                                    ContractStatus.AwaitingPayment.ToApiString(),
                                    StringComparison.OrdinalIgnoreCase))
                                .Sum(c => c.TotalAmount);
                            return active / total;
                        });

                _strictRetentionByTeam = contracts
                    .GroupBy(getTeamName)
                    .ToDictionary(
                        g => g.Key,
                        g =>
                        {
                            var total = g
                                .Where(c => !string.Equals(c.ContractStatus?.Name, ContractStatus.AwaitingPayment.ToApiString(), StringComparison.OrdinalIgnoreCase))
                                .Sum(c => c.TotalAmount);
                            if (total <= 0) return 0m;
                            var strictActive = g
                                .Where(c => !string.Equals(
                                    c.ContractStatus?.Name,
                                    ContractStatus.Defaulted.ToApiString(),
                                    StringComparison.OrdinalIgnoreCase) &&
                                            !string.Equals(
                                    c.ContractStatus?.Name,
                                    ContractStatus.Late1.ToApiString(),
                                    StringComparison.OrdinalIgnoreCase) &&
                                            !string.Equals(
                                    c.ContractStatus?.Name,
                                    ContractStatus.Late2.ToApiString(),
                                    StringComparison.OrdinalIgnoreCase) &&
                                            !string.Equals(
                                    c.ContractStatus?.Name,
                                    ContractStatus.Late3.ToApiString(),
                                    StringComparison.OrdinalIgnoreCase) &&
                                            !string.Equals(
                                    c.ContractStatus?.Name,
                                    ContractStatus.AwaitingPayment.ToApiString(),
                                    StringComparison.OrdinalIgnoreCase))
                                .Sum(c => c.TotalAmount);
                            return strictActive / total;
                        });
            }
            else
            {
                _retentionByTeam = null;
                _strictRetentionByTeam = null;
            }

            if (report.GroupByClassification)
            {
                _retentionByClassification = contracts
                    .GroupBy(getClassification)
                    .ToDictionary(
                        g => g.Key,
                        g =>
                        {
                            var total = g
                                .Where(c => !string.Equals(c.ContractStatus?.Name, ContractStatus.AwaitingPayment.ToApiString(), StringComparison.OrdinalIgnoreCase))
                                .Sum(c => c.TotalAmount);
                            if (total <= 0) return 0m;
                            var active = g
                                .Where(c => !string.Equals(
                                    c.ContractStatus?.Name,
                                    ContractStatus.Defaulted.ToApiString(),
                                    StringComparison.OrdinalIgnoreCase) &&
                                            !string.Equals(
                                    c.ContractStatus?.Name,
                                    ContractStatus.AwaitingPayment.ToApiString(),
                                    StringComparison.OrdinalIgnoreCase))
                                .Sum(c => c.TotalAmount);
                            return active / total;
                        });

                _strictRetentionByClassification = contracts
                    .GroupBy(getClassification)
                    .ToDictionary(
                        g => g.Key,
                        g =>
                        {
                            var total = g
                                .Where(c => !string.Equals(c.ContractStatus?.Name, ContractStatus.AwaitingPayment.ToApiString(), StringComparison.OrdinalIgnoreCase))
                                .Sum(c => c.TotalAmount);
                            if (total <= 0) return 0m;
                            var strictActive = g
                                .Where(c => !string.Equals(
                                    c.ContractStatus?.Name,
                                    ContractStatus.Defaulted.ToApiString(),
                                    StringComparison.OrdinalIgnoreCase) &&
                                            !string.Equals(
                                    c.ContractStatus?.Name,
                                    ContractStatus.Late1.ToApiString(),
                                    StringComparison.OrdinalIgnoreCase) &&
                                            !string.Equals(
                                    c.ContractStatus?.Name,
                                    ContractStatus.Late2.ToApiString(),
                                    StringComparison.OrdinalIgnoreCase) &&
                                            !string.Equals(
                                    c.ContractStatus?.Name,
                                    ContractStatus.Late3.ToApiString(),
                                    StringComparison.OrdinalIgnoreCase) &&
                                            !string.Equals(
                                    c.ContractStatus?.Name,
                                    ContractStatus.AwaitingPayment.ToApiString(),
                                    StringComparison.OrdinalIgnoreCase))
                                .Sum(c => c.TotalAmount);
                            return strictActive / total;
                        });
            }
            else
            {
                _retentionByClassification = null;
                _strictRetentionByClassification = null;
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

            decimal? totalSum = null;
            decimal? overallRetention = null;

            if (report.SumTotal)
            {
                totalSum = contracts.Sum(c => c.TotalAmount);
                overallRetention = ReportRetentionCalculator.CalculateOverallRetention(contracts, report.SummaryRetentionType);
            }

            if (report.GroupByEmail)
            {
                // Group by user email; null email maps to a shared "(Sem usuário)" bucket
                // Retention is already pre-computed above in _retentionByEmail
                var grouped = contracts
                    .GroupBy(c => c.User?.Email ?? "(Sem usuário)")
                    .Select(g =>
                    {
                        var first = g.First();
                        // ✅ IMPORTANT: Since we use AsNoTracking, we can safely mutate the object in memory
                        first.TotalAmount = g.Sum(c => c.TotalAmount);
                        return (Contract: first, EmailKey: g.Key, Count: g.Count());
                    })
                    .OrderByDescending(x => x.Contract.TotalAmount)
                    .ToList();

                _contractCountByEmail = grouped.ToDictionary(x => x.EmailKey, x => x.Count);
                contracts = grouped.Select(x => x.Contract).ToList();
            }

            if (report.GroupByTeam)
            {
                // Group by team; null/no team maps to a shared "(Sem equipe)" bucket
                // Retention is already pre-computed above in _retentionByTeam
                var grouped = contracts
                    .GroupBy(getTeamName)
                    .Select(g =>
                    {
                        var first = g.First();
                        // ✅ IMPORTANT: Since we use AsNoTracking, we can safely mutate the object in memory
                        first.TotalAmount = g.Sum(c => c.TotalAmount);
                        return (Contract: first, TeamKey: g.Key, Count: g.Count());
                    })
                    .OrderByDescending(x => x.Contract.TotalAmount)
                    .ToList();

                _contractCountByTeam = grouped.ToDictionary(x => x.TeamKey, x => x.Count);
                contracts = grouped.Select(x => x.Contract).ToList();
            }

            if (report.GroupByClassification)
            {
                var grouped = contracts
                    .GroupBy(getClassification)
                    .Select(g =>
                    {
                        var first = g.First();
                        first.TotalAmount = g.Sum(c => c.TotalAmount);
                        return (Contract: first, ClassKey: g.Key, Count: g.Count());
                    })
                    .OrderByDescending(x => x.Contract.TotalAmount)
                    .ToList();

                _contractCountByClassification = grouped.ToDictionary(x => x.ClassKey, x => x.Count);
                contracts = grouped.Select(x => x.Contract).ToList();
            }

            // Project each contract to only the outputColumns fields
            var columns = report.OutputColumns.OrderBy(c => c.Order).ToList();
            var allRows = contracts.Select(c => ProjectContract(c, columns, getTeamName, getTeamOwnerName, getClassification, getStoreName)).ToList();

            // Apply ordering if specified
            if (!string.IsNullOrWhiteSpace(report.OrderByField))
            {
                var isDesc = string.Equals(report.OrderByDirection, "desc", StringComparison.OrdinalIgnoreCase);
                var field = report.OrderByField;

                allRows = isDesc 
                    ? allRows.OrderByDescending(r => r.ContainsKey(field) ? r[field] : null).ToList()
                    : allRows.OrderBy(r => r.ContainsKey(field) ? r[field] : null).ToList();
            }

            // Apply pagination
            var totalCount = allRows.Count;
            var safePage = Math.Max(1, page);
            var safePageSize = Math.Clamp(pageSize, 1, 200);
            var totalPages = (int)Math.Ceiling(totalCount / (double)safePageSize);
            var pagedRows = allRows
                .Skip((safePage - 1) * safePageSize)
                .Take(safePageSize)
                .ToList();

            // Apply formatting only to the paged results
            foreach (var row in pagedRows)
            {
                foreach (var col in columns)
                {
                    if (row.ContainsKey(col.Label))
                    {
                        row[col.Label] = ApplyFormat(row[col.Label], col.Format);
                    }
                }
            }

            var result = new ReportResultsResponse
            {
                Page       = safePage,
                PageSize   = safePageSize,
                TotalCount = totalCount,
                TotalPages = totalPages,
                TotalSum   = totalSum,
                OverallRetention = overallRetention,
                Columns    = columns.Select(col => new OutputColumnResponse
                {
                    Source = col.Source,
                    Field  = col.Field,
                    Label  = col.Label,
                    Order  = col.Order,
                    Format = col.Format
                }).ToList(),
                Rows = pagedRows
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

            if (report.GroupByTeam && !result.Columns.Any(c => c.Source == "Users_Contract" && c.Field == "team"))
            {
                result.Columns.Insert(0, new OutputColumnResponse
                {
                    Source = "Users_Contract",
                    Field  = "team",
                    Label  = "Equipe",
                    Order  = 0
                });
            }

            if (report.GroupByClassification && !result.Columns.Any(c => c.Source == "Users_Contract" && c.Field == "classification"))
            {
                result.Columns.Insert(0, new OutputColumnResponse
                {
                    Source = "Users_Contract",
                    Field  = "classification",
                    Label  = "Classificação",
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
                            "email",
                            "team",
                            "teamOwner",
                            "classification",
                            "store"
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
                    },
                    new SourceColumns
                    {
                        Source = "Computed",
                        Fields = new List<string>
                        {
                            "contractCount",
                            "retention",
                            "strictRetention"
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
        private Dictionary<string, object?> ProjectContract(
            SalesApp.Models.Contract contract,
            List<OutputColumnResponse> columns,
            Func<SalesApp.Models.Contract, string> getTeamName,
            Func<SalesApp.Models.Contract, string> getTeamOwnerName,
            Func<SalesApp.Models.Contract, string> getClassification,
            Func<SalesApp.Models.Contract, string> getStoreName)
        {
            var row = new Dictionary<string, object?>();

            foreach (var col in columns)
            {
                row[col.Label] = ResolveField(contract, col.Source, col.Field, getTeamName, getTeamOwnerName, getClassification, getStoreName);
            }

            // Synthetic Email column is still injected if not present, to ensure Group By works visually
            if (!row.ContainsKey("Email"))
            {
                row["Email"] = contract.User?.Email ?? "(Sem usuário)";
            }

            // Synthetic Team column is still injected if not present to ensure team displays nicely
            if (!row.ContainsKey("Equipe"))
            {
                row["Equipe"] = getTeamName(contract);
            }

            if (!row.ContainsKey("Classificação"))
            {
                row["Classificação"] = getClassification(contract);
            }

            // Synthetic Store column — always injected so grouping/filtering works consistently
            if (!row.ContainsKey("Loja"))
            {
                row["Loja"] = getStoreName(contract);
            }

            return row;
        }

        private object? ResolveField(
            SalesApp.Models.Contract c,
            string source,
            string field,
            Func<SalesApp.Models.Contract, string> getTeamName,
            Func<SalesApp.Models.Contract, string> getTeamOwnerName,
            Func<SalesApp.Models.Contract, string> getClassification,
            Func<SalesApp.Models.Contract, string> getStoreName)
        {
            return source switch
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
                    "name"      => c.User?.Name,
                    "email"     => c.User?.Email,
                    "team"      => getTeamName(c),
                    "teamOwner" => getTeamOwnerName(c),
                    "classification" => getClassification(c),
                    "store"     => getStoreName(c),
                    _           => null
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
                "Computed" => field switch
                {
                    "contractCount" => _contractCountByTeam != null
                        ? _contractCountByTeam.GetValueOrDefault(getTeamName(c), 0)
                        : _contractCountByClassification != null
                        ? _contractCountByClassification.GetValueOrDefault(getClassification(c), 0)
                        : _contractCountByEmail?.GetValueOrDefault(c.User?.Email ?? "(Sem usuário)", 0) ?? 0,
                    "retention"     => _retentionByTeam != null
                        ? _retentionByTeam.GetValueOrDefault(getTeamName(c), 0m)
                        : _retentionByClassification != null
                        ? _retentionByClassification.GetValueOrDefault(getClassification(c), 0m)
                        : _retentionByEmail?.GetValueOrDefault(c.User?.Email ?? "(Sem usuário)", 0m) ?? 0m,
                    "strictRetention" => _strictRetentionByTeam != null
                        ? _strictRetentionByTeam.GetValueOrDefault(getTeamName(c), 0m)
                        : _strictRetentionByClassification != null
                        ? _strictRetentionByClassification.GetValueOrDefault(getClassification(c), 0m)
                        : _strictRetentionByEmail?.GetValueOrDefault(c.User?.Email ?? "(Sem usuário)", 0m) ?? 0m,
                    _               => null
                },
                _ => null
            };
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
            else if (format.ToLower() == "percentage")
            {
                var ptBr = new System.Globalization.CultureInfo("pt-BR");
                
                if (value is decimal d)
                {
                    return d.ToString("P2", ptBr);
                }
                
                if (value is double db)
                {
                    return db.ToString("P2", ptBr);
                }

                if (value is float f)
                {
                    return f.ToString("P2", ptBr);
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
            if (expr.Equals("thisMonthEnd", StringComparison.OrdinalIgnoreCase))
            {
                var now = DateTime.UtcNow;
                var lastDay = DateTime.DaysInMonth(now.Year, now.Month);
                return new DateTime(now.Year, now.Month, lastDay, 23, 59, 59, DateTimeKind.Utc);
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
                GroupByTeam = f.GroupByTeam,
                GroupByClassification = f.GroupByClassification,
                HideUnassignedTeams = f.HideUnassignedTeams,
                OrderByField = f.OrderByField,
                OrderByDirection = f.OrderByDirection,
                CreatedAt   = f.CreatedAt,
                UpdatedAt   = f.UpdatedAt,
                AllowedTeamIds = f.AllowedTeamIds,
                AllowedRoles = f.AllowedRoles,
                SumTotal = f.SumTotal,
                OutputType = f.OutputType ?? "table",
                ChartType = f.ChartType ?? "bar",
                SummaryRetentionType = f.SummaryRetentionType ?? "standard",
                ChartMetric = f.ChartMetric,
                ExportedFields = f.ExportedFields.Select(e => new ExportedFieldResponse
                {
                    FieldType = e.FieldType,
                    Label = e.Label
                }).ToList(),
                FilterConfig = new FilterConfigResponse
                {
                    Matriculas          = f.FilterConfig.Matriculas,
                    StartDate           = f.FilterConfig.StartDate,
                    EndDate             = f.FilterConfig.EndDate,
                    RelativeStartDate   = f.FilterConfig.RelativeStartDate,
                    RelativeEndDate     = f.FilterConfig.RelativeEndDate,
                    CurrentUserAsParent = f.FilterConfig.CurrentUserAsParent,
                    CurrentUserTeam     = f.FilterConfig.CurrentUserTeam,
                    CurrentUserMatricula = f.FilterConfig.CurrentUserMatricula,
                    Emails              = f.FilterConfig.Emails,
                    Groups              = f.FilterConfig.Groups,
                    Teams               = f.FilterConfig.Teams,
                    Stores              = f.FilterConfig.Stores,
                    TeamMembershipMode  = f.FilterConfig.TeamMembershipMode,
                    Pvs                 = f.FilterConfig.Pvs,
                    Statuses            = f.FilterConfig.Statuses,
                    StatusOperator      = f.FilterConfig.StatusOperator,
                    ClassificationLevelIds = f.FilterConfig.ClassificationLevelIds,
                    MinRetention        = f.FilterConfig.MinRetention,
                    MaxRetention        = f.FilterConfig.MaxRetention,
                    MinStrictRetention  = f.FilterConfig.MinStrictRetention,
                    MaxStrictRetention  = f.FilterConfig.MaxStrictRetention,
                    MinProduction       = f.FilterConfig.MinProduction,
                    MaxProduction       = f.FilterConfig.MaxProduction
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

        private static List<ExportedField> MapExportedFields(List<ExportedFieldRequest>? req)
        {
            if (req == null) return new List<ExportedField>();
            return req.Select(e => new ExportedField
            {
                FieldType = e.FieldType,
                Label = e.Label
            }).ToList();
        }

        private static FilterConfig MapFilterConfig(FilterConfigRequest req) =>
            new()
            {
                Matriculas          = req.Matriculas,
                StartDate           = req.StartDate,
                EndDate             = req.EndDate,
                RelativeStartDate   = req.RelativeStartDate,
                RelativeEndDate     = req.RelativeEndDate,
                CurrentUserAsParent = req.CurrentUserAsParent,
                CurrentUserTeam     = req.CurrentUserTeam,
                CurrentUserMatricula = req.CurrentUserMatricula,
                Emails              = req.Emails,
                Groups              = req.Groups,
                Teams               = req.Teams,
                Stores              = req.Stores,
                TeamMembershipMode  = req.TeamMembershipMode,
                Pvs                 = req.Pvs,
                Statuses            = req.Statuses,
                StatusOperator      = req.StatusOperator,
                ClassificationLevelIds = req.ClassificationLevelIds,
                MinRetention        = req.MinRetention,
                MaxRetention        = req.MaxRetention,
                MinStrictRetention  = req.MinStrictRetention,
                MaxStrictRetention  = req.MaxStrictRetention,
                MinProduction       = req.MinProduction,
                MaxProduction       = req.MaxProduction
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
