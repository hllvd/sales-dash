using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SalesApp.CareerPlan;
using SalesApp.CareerPlan.Repositories;
using SalesApp.Data;
using SalesApp.DTOs;
using SalesApp.Models;
using SalesApp.Repositories;

namespace SalesApp.Services
{
    public class CareerPlanService : ICareerPlanService
    {
        private readonly AppDbContext _context;
        private readonly IClassificationLevelRepository _classificationLevelRepo;
        private readonly ICareerPlanConfigProvider _configProvider;
        private readonly ICareerPlanSnapshotRepository _snapshotRepository;
        private readonly ICareerPlanEvaluationCacheRepository _evaluationCacheRepository;
        private readonly ILogger<CareerPlanService> _logger;

        public CareerPlanService(
            AppDbContext context,
            IClassificationLevelRepository classificationLevelRepo,
            ICareerPlanConfigProvider configProvider,
            ICareerPlanSnapshotRepository snapshotRepository,
            ICareerPlanEvaluationCacheRepository evaluationCacheRepository,
            ILogger<CareerPlanService> logger)
        {
            _context = context;
            _classificationLevelRepo = classificationLevelRepo;
            _configProvider = configProvider;
            _snapshotRepository = snapshotRepository;
            _evaluationCacheRepository = evaluationCacheRepository;
            _logger = logger;
        }

        public CareerPlanConfigResponse GetConfigSummary()
        {
            var cfg = _configProvider.GetConfig();
            return new CareerPlanConfigResponse
            {
                ClosingDay = cfg.ClosingDay,
                ConfiguredLevels = cfg.Levels.Keys.OrderBy(k => k).ToList()
            };
        }

        public async Task<CareerPlanEvaluationResponse> EvaluateUserCareerPlanAsync(Guid userId, DateTime? asOfDate = null)
        {
            var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
            {
                throw new KeyNotFoundException($"User with ID {userId} not found.");
            }

            var config = _configProvider.GetConfig();
            var evaluationDate = asOfDate ?? DateTime.UtcNow;
            var referenceDate = CareerReferenceDate.Resolve(evaluationDate, config.ClosingDay);

            // 1. Load active classification
            var activeClassification = await _context.UserClassifications
                .AsNoTracking()
                .Include(uc => uc.Level)
                .Where(uc => uc.UserInternalId == user.InternalId
                             && (uc.EndDate == null || uc.EndDate > DateTime.UtcNow))
                .OrderByDescending(uc => uc.StartDate)
                .FirstOrDefaultAsync();

            var currentLevel = activeClassification?.Level;

            // 2. Load all classification levels (ordered chain)
            var allLevels = await _classificationLevelRepo.GetAllAsync();

            // 3. Resolve target level
            var targetLevel = CareerLevelResolver.ResolveTargetLevel(currentLevel, allLevels);

            // 4. Load user contracts with status
            var contracts = await _context.Contracts
                .AsNoTracking()
                .Include(c => c.ContractStatus)
                .Where(c => c.UserInternalId == user.InternalId)
                .ToListAsync();

            // 5. Team metrics
            bool isTeamOwner = await _context.Teams
                .AnyAsync(t => t.OwnerUserInternalId == user.InternalId && t.IsActive);

            int activeTeamMembers = await _context.UserTeams
                .AsNoTracking()
                .Include(ut => ut.Team)
                .Include(ut => ut.User)
                .Where(ut => ut.Team.OwnerUserInternalId == user.InternalId
                             && ut.Team.IsActive
                             && ut.User.IsActive
                             && (ut.EndDate == null || ut.EndDate > DateTime.UtcNow)
                             && ut.UserInternalId != user.InternalId)
                .Select(ut => ut.UserInternalId)
                .Distinct()
                .CountAsync();

            var directChildInternalIds = await _context.Users
                .AsNoTracking()
                .Where(u => u.ParentUserId == user.Id && u.IsActive)
                .Select(u => u.InternalId)
                .ToListAsync();

            int directSubTeams = directChildInternalIds.Count > 0
                ? await _context.Teams.CountAsync(t => t.IsActive && t.OwnerUserInternalId.HasValue && directChildInternalIds.Contains(t.OwnerUserInternalId.Value))
                : 0;

            // 6. Assemble inputs
            var metricInputs = new CareerMetricInputs(
                UserContracts: contracts,
                IsTeamOwner: isTeamOwner,
                ActiveTeamMembers: activeTeamMembers,
                DirectSubTeams: directSubTeams,
                ReferenceDate: referenceDate
            );

            // 7. Compute raw metrics
            var raw = CareerMetricsCalculator.ComputeRawMetrics(metricInputs);
            var rawMetricsDto = new CareerRawMetricsDto
            {
                Retention6m = raw.Retention6m,
                Retention12m = raw.Retention12m,
                Retention15m = raw.Retention15m,
                StrictRetention6m = raw.StrictRetention6m,
                StrictRetention12m = raw.StrictRetention12m,
                StrictRetention15m = raw.StrictRetention15m,
                StrictRetention15mMinAge2 = raw.StrictRetention15mMinAge2,
                Production12m = raw.Production12m,
                Production15m = raw.Production15m,
                ActiveProduction12mMinPaid2 = raw.ActiveProduction12mMinPaid2,
                IsTeamOwner = raw.IsTeamOwner,
                ActiveTeamMembers = raw.ActiveTeamMembers,
                DirectSubTeams = raw.DirectSubTeams
            };

            // 8. Evaluate rules if target level has rules configured
            var requirements = new List<CareerPlanRequirementDto>();
            bool hasRulesConfigured = false;

            if (targetLevel != null && config.Levels.TryGetValue(targetLevel.Name, out var levelRule))
            {
                hasRulesConfigured = true;
                var evalResult = CareerRuleEvaluator.Evaluate(levelRule.Requirements, metricInputs, targetLevel);

                requirements = evalResult.Requirements.Select(r => new CareerPlanRequirementDto
                {
                    Label = r.Label,
                    Metric = r.Metric,
                    RawMetricKey = r.RawMetricKey,
                    CurrentValue = r.CurrentValue,
                    TargetValue = r.TargetValue,
                    FormattedCurrent = r.FormattedCurrent,
                    FormattedTarget = r.FormattedTarget,
                    Op = r.Op,
                    Passed = r.Passed,
                    Progress = r.Progress
                }).ToList();
            }

            return new CareerPlanEvaluationResponse
            {
                UserId = user.Id,
                UserInternalId = user.InternalId,
                UserName = user.Name,
                UserEmail = user.Email,
                ReferenceDate = referenceDate,
                ClosingDay = config.ClosingDay,
                CurrentLevel = currentLevel != null ? MapLevelSummary(currentLevel) : null,
                TargetLevel = targetLevel != null ? MapLevelSummary(targetLevel) : null,
                HasTargetLevel = targetLevel != null,
                HasRulesConfigured = hasRulesConfigured,
                Requirements = requirements,
                RawMetrics = rawMetricsDto
            };
        }

        public async Task<ContractRtPageDto> GetContractRtTrackingAsync(Guid userId, int page = 1, int pageSize = 100, DateTime? asOfDate = null)
        {
            var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
            {
                throw new KeyNotFoundException($"User with ID {userId} not found.");
            }

            var evaluationDate = asOfDate ?? DateTime.UtcNow;

            // 1. Check user active classification
            var activeClassification = await _context.UserClassifications
                .AsNoTracking()
                .Include(uc => uc.Level)
                .Where(uc => uc.UserInternalId == user.InternalId
                             && (uc.EndDate == null || uc.EndDate > DateTime.UtcNow))
                .OrderByDescending(uc => uc.StartDate)
                .FirstOrDefaultAsync();

            var currentLevelName = activeClassification?.Level?.Name;
            bool isApplicable = ContractPaymentTracking.IsLevelEligibleForRtTable(currentLevelName);

            if (!isApplicable)
            {
                return new ContractRtPageDto
                {
                    IsApplicable = false,
                    CurrentLevelName = currentLevelName,
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = 0,
                    TotalPages = 0,
                    Items = new List<ContractRtRowDto>()
                };
            }

            // 2. Fetch contracts
            var contracts = await _context.Contracts
                .AsNoTracking()
                .Include(c => c.ContractStatus)
                .Where(c => c.UserInternalId == user.InternalId)
                .ToListAsync();

            // 3. Evaluate contracts purely
            var evaluated = ContractPaymentTracking.EvaluateContracts(contracts, evaluationDate, 15);

            int totalCount = evaluated.Count;
            int totalPages = pageSize > 0 ? (int)Math.Ceiling(totalCount / (double)pageSize) : 0;
            int skip = Math.Max(0, (page - 1) * pageSize);

            var paged = evaluated
                .Skip(skip)
                .Take(pageSize)
                .Select(e => new ContractRtRowDto
                {
                    ContractNumber = e.ContractNumber,
                    CustomerName = e.CustomerName,
                    TotalAmount = e.TotalAmount,
                    SaleStartDate = e.SaleStartDate,
                    Status = e.Status,
                    Rts = e.Rts.Select(r => r switch
                    {
                        RtCellState.Paid => "paid",
                        RtCellState.Unpaid => "unpaid",
                        _ => "blank"
                    }).ToList(),
                    Current = e.Current == RtCurrentState.Overdue ? "overdue" : "upToDate",
                    AgeMonths = e.AgeMonths,
                    OverdueMonths = e.OverdueMonths,
                    PaidMonths = e.PaidMonths
                })
                .ToList();

            return new ContractRtPageDto
            {
                IsApplicable = true,
                CurrentLevelName = currentLevelName,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalPages,
                Items = paged
            };
        }

        public async Task<CareerPlanConsultantsListResponse> GetConsultantsListAsync(bool autoCalculateIfEmpty = true)
        {
            var existing = await _snapshotRepository.GetLatestSnapshotsAsync();
            if (existing != null && existing.Count > 0)
            {
                var activeUserIds = (await _context.Users
                    .Where(u => u.IsActive)
                    .Select(u => u.Id)
                    .ToListAsync())
                    .ToHashSet();

                var validSnapshots = existing.Where(s => activeUserIds.Contains(s.UserId)).ToList();
                if (validSnapshots.Count > 0)
                {
                    var maxCalculatedAt = validSnapshots.Max(e => (DateTime?)e.CalculatedAt);
                    return new CareerPlanConsultantsListResponse
                    {
                        Consultants = validSnapshots.OrderBy(c => c.UserName).ToList(),
                        TotalCount = validSnapshots.Count,
                        LastCalculatedAt = maxCalculatedAt
                    };
                }
            }

            if (autoCalculateIfEmpty)
            {
                var result = await RecalculateAllConsultantsSnapshotsAsync();
                var fresh = await _snapshotRepository.GetLatestSnapshotsAsync();
                var activeUserIds = (await _context.Users
                    .Where(u => u.IsActive)
                    .Select(u => u.Id)
                    .ToListAsync())
                    .ToHashSet();
                var validFresh = fresh.Where(s => activeUserIds.Contains(s.UserId)).ToList();

                return new CareerPlanConsultantsListResponse
                {
                    Consultants = validFresh.OrderBy(c => c.UserName).ToList(),
                    TotalCount = validFresh.Count,
                    LastCalculatedAt = validFresh.Count > 0 ? validFresh.Max(e => (DateTime?)e.CalculatedAt) : result.CalculatedAt
                };
            }

            return new CareerPlanConsultantsListResponse
            {
                Consultants = new List<CareerPlanConsultantSnapshotDto>(),
                TotalCount = 0,
                LastCalculatedAt = null
            };
        }

        public async Task<RecalculateCareerPlanResponse> RecalculateAllConsultantsSnapshotsAsync()
        {
            var calculatedAt = DateTime.UtcNow;
            _logger.LogInformation("Starting career plan consultants snapshot calculation at {Time}", calculatedAt);

            // 1. Load active users
            var activeUsers = await _context.Users
                .AsNoTracking()
                .Where(u => u.IsActive)
                .OrderBy(u => u.Name)
                .ToListAsync();

            // 2. Load all active classifications
            var activeClassifications = await _context.UserClassifications
                .AsNoTracking()
                .Include(uc => uc.Level)
                .Where(uc => uc.EndDate == null || uc.EndDate > calculatedAt)
                .ToListAsync();

            var classificationByUserInternalId = activeClassifications
                .GroupBy(uc => uc.UserInternalId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.StartDate).First().Level);

            var allLevels = await _classificationLevelRepo.GetAllAsync();

            var snapshots = new List<CareerPlanConsultantSnapshotDto>();

            foreach (var user in activeUsers)
            {
                classificationByUserInternalId.TryGetValue(user.InternalId, out var currentLevel);
                string currentLevelName = currentLevel?.Name ?? string.Empty;

                // User filter: Sem classificação (null/empty), Alef, or Beth
                bool isEligibleLevel = string.IsNullOrWhiteSpace(currentLevelName)
                                       || string.Equals(currentLevelName, "Alef", StringComparison.OrdinalIgnoreCase)
                                       || string.Equals(currentLevelName, "Beth", StringComparison.OrdinalIgnoreCase);

                if (!isEligibleLevel)
                {
                    continue;
                }

                try
                {
                    var eval = await EvaluateUserCareerPlanAsync(user.Id, calculatedAt);

                    decimal activeProd = eval.RawMetrics.ActiveProduction12mMinPaid2;
                    decimal? salesGoal = eval.TargetLevel?.SalesGoal;
                    decimal progressPct = 0m;
                    if (salesGoal.HasValue && salesGoal.Value > 0)
                    {
                        progressPct = Math.Min(1m, Math.Max(0m, activeProd / salesGoal.Value));
                    }

                    snapshots.Add(new CareerPlanConsultantSnapshotDto
                    {
                        UserId = user.Id,
                        UserName = user.Name ?? string.Empty,
                        UserEmail = user.Email ?? string.Empty,
                        CurrentLevelName = string.IsNullOrWhiteSpace(currentLevelName) ? "Sem Classificação" : currentLevelName,
                        TargetLevelName = eval.TargetLevel?.Name ?? "N/A",
                        SalesGoal = salesGoal,
                        RetentionTarget = eval.TargetLevel?.Retention,
                        ActiveProduction12m = activeProd,
                        ProductionProgressPct = progressPct,
                        StrictRetention15m = eval.RawMetrics.StrictRetention15m,
                        CalculatedAt = calculatedAt
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to evaluate career plan snapshot for user {UserId} ({Email})", user.Id, user.Email);
                }
            }

            await _snapshotRepository.SaveSnapshotsAsync(snapshots);

            _logger.LogInformation("Completed career plan consultants snapshot calculation. Processed {Count} consultants.", snapshots.Count);

            return new RecalculateCareerPlanResponse
            {
                Success = true,
                ProcessedCount = snapshots.Count,
                CalculatedAt = calculatedAt
            };
        }

        public async Task<MyCareerPlanResponse> GetMyCareerPlanAsync(Guid userId, bool forceRefresh = false)
        {
            if (!forceRefresh)
            {
                var cached = await _evaluationCacheRepository.GetAsync(userId);
                if (cached != null)
                {
                    return new MyCareerPlanResponse
                    {
                        Evaluation = cached.Evaluation,
                        CalculatedAt = cached.CalculatedAt,
                        FromCache = true
                    };
                }
            }

            var evaluation = await EvaluateUserCareerPlanAsync(userId);
            var now = DateTime.UtcNow;

            var cacheEntry = new CachedCareerPlanEvaluationDto
            {
                UserId = userId,
                Evaluation = evaluation,
                CalculatedAt = now,
                Ttl = DynamoDbCareerPlanEvaluationCacheRepository.ComputeTtlEpoch(now)
            };

            await _evaluationCacheRepository.SaveAsync(cacheEntry);

            return new MyCareerPlanResponse
            {
                Evaluation = evaluation,
                CalculatedAt = now,
                FromCache = false
            };
        }

        private static CareerPlanLevelSummaryDto MapLevelSummary(ClassificationLevel level)
        {
            return new CareerPlanLevelSummaryDto
            {
                Id = level.Id,
                Name = level.Name,
                Description = level.Description,
                Prize = level.Prize,
                SalesGoal = level.SalesGoal,
                Retention = level.Retention
            };
        }
    }
}
