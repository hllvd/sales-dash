using System;
using System.Collections.Generic;

namespace SalesApp.CareerPlan
{
    public enum MetricKind
    {
        Retention,
        StrictRetention,
        Production,
        IsTeamOwner,
        ActiveTeamMembers,
        DirectSubTeams
    }

    public enum ComparisonOp
    {
        GreaterThan,
        GreaterThanOrEqual,
        LessThan,
        LessThanOrEqual,
        Equal
    }

    public enum RuleNodeType
    {
        All,
        Any,
        Condition
    }

    public record EligibilityFilter(
        int? MinAgeMonths = null,
        int? MinInferredPaidMonths = null
    );

    public record RuleValue(
        decimal? ConstantValue = null,
        string? Reference = null
    );

    public record RuleNode(
        RuleNodeType Type,
        IReadOnlyList<RuleNode>? Children = null,
        string? Label = null,
        MetricKind? Metric = null,
        int? WindowMonths = null,
        EligibilityFilter? Eligibility = null,
        ComparisonOp? Op = null,
        RuleValue? Value = null
    );

    public record CareerLevelRule(
        RuleNode Requirements
    );

    public record CareerPlanConfig(
        int ClosingDay,
        IReadOnlyDictionary<string, CareerLevelRule> Levels
    );
}
