using System;
using System.Collections.Generic;

namespace SalesApp.DTOs
{
    public class CareerPlanRequirementDto
    {
        public string Label { get; set; } = string.Empty;
        public string Metric { get; set; } = string.Empty;
        public string? RawMetricKey { get; set; }
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
        public decimal ActiveProduction12mMinPaid2 { get; set; }
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

    public class ContractRtRowDto
    {
        public string ContractNumber { get; set; } = string.Empty;
        public string? CustomerName { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime SaleStartDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public List<string> Rts { get; set; } = new();
        public string Current { get; set; } = string.Empty;
        public int AgeMonths { get; set; }
        public int OverdueMonths { get; set; }
        public int PaidMonths { get; set; }
    }

    public class ContractRtPageDto
    {
        public bool IsApplicable { get; set; }
        public string? CurrentLevelName { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
        public List<ContractRtRowDto> Items { get; set; } = new();
    }

    public class CareerPlanConsultantSnapshotDto
    {
        public Guid UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
        public string CurrentLevelName { get; set; } = string.Empty;
        public string TargetLevelName { get; set; } = string.Empty;
        public decimal? SalesGoal { get; set; }
        public int? RetentionTarget { get; set; }
        public decimal ActiveProduction12m { get; set; }
        public decimal ProductionProgressPct { get; set; }
        public decimal StrictRetention15m { get; set; }
        public DateTime CalculatedAt { get; set; }
    }

    public class CareerPlanConsultantsListResponse
    {
        public List<CareerPlanConsultantSnapshotDto> Consultants { get; set; } = new();
        public int TotalCount { get; set; }
        public DateTime? LastCalculatedAt { get; set; }
    }

    public class RecalculateCareerPlanResponse
    {
        public bool Success { get; set; }
        public int ProcessedCount { get; set; }
        public DateTime CalculatedAt { get; set; }
    }

    public class MyCareerPlanResponse
    {
        public CareerPlanEvaluationResponse Evaluation { get; set; } = null!;
        public DateTime CalculatedAt { get; set; }
        public bool FromCache { get; set; }
    }

    public class CachedCareerPlanEvaluationDto
    {
        public Guid UserId { get; set; }
        public CareerPlanEvaluationResponse Evaluation { get; set; } = null!;
        public DateTime CalculatedAt { get; set; }
        public long Ttl { get; set; }
    }

    public class SimulateCareerPlanRequest
    {
        public List<ContractRtOverrideDto> Overrides { get; set; } = new();
    }

    public class ContractRtOverrideDto
    {
        public string ContractNumber { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty; // "upToDate" or "overdue"
        public string? SimulatedCurrentStatus
        {
            get => State;
            set => State = value ?? string.Empty;
        }
    }
}

