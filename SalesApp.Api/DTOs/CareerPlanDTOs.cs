using System;
using System.Collections.Generic;

namespace SalesApp.DTOs
{
    public class CareerPlanRequirementDto
    {
        public string Label { get; set; } = string.Empty;
        public string Metric { get; set; } = string.Empty;
        public decimal CurrentValue { get; set; }
        public decimal? TargetValue { get; set; }
        public string FormattedCurrent { get; set; } = string.Empty;
        public string FormattedTarget { get; set; } = string.Empty;
        public string Op { get; set; } = string.Empty;
        public bool Passed { get; set; }
        public decimal Progress { get; set; }
    }

    public class CareerPlanLevelSummaryDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Prize { get; set; }
        public decimal? SalesGoal { get; set; }
        public int? Retention { get; set; }
    }

    public class CareerRawMetricsDto
    {
        public decimal Retention6m { get; set; }
        public decimal Retention12m { get; set; }
        public decimal Retention15m { get; set; }
        public decimal StrictRetention6m { get; set; }
        public decimal StrictRetention12m { get; set; }
        public decimal StrictRetention15m { get; set; }
        public decimal StrictRetention15mMinAge2 { get; set; }
        public decimal Production12m { get; set; }
        public decimal Production15m { get; set; }
        public bool IsTeamOwner { get; set; }
        public int ActiveTeamMembers { get; set; }
        public int DirectSubTeams { get; set; }
    }

    public class CareerPlanEvaluationResponse
    {
        public Guid UserId { get; set; }
        public int UserInternalId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
        public DateTime ReferenceDate { get; set; }
        public int ClosingDay { get; set; }
        public CareerPlanLevelSummaryDto? CurrentLevel { get; set; }
        public CareerPlanLevelSummaryDto? TargetLevel { get; set; }
        public bool HasTargetLevel { get; set; }
        public bool HasRulesConfigured { get; set; }
        public List<CareerPlanRequirementDto> Requirements { get; set; } = new();
        public CareerRawMetricsDto RawMetrics { get; set; } = new();
    }

    public class CareerPlanConfigResponse
    {
        public int ClosingDay { get; set; }
        public List<string> ConfiguredLevels { get; set; } = new();
    }
}
