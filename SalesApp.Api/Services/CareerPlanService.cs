using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SalesApp.CareerPlan;
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
        private readonly ILogger<CareerPlanService> _logger;

        public CareerPlanService(
            AppDbContext context,
            IClassificationLevelRepository classificationLevelRepo,
            ICareerPlanConfigProvider configProvider,
            ILogger<CareerPlanService> logger)
        {
            _context = context;
            _classificationLevelRepo = classificationLevelRepo;
            _configProvider = configProvider;
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
