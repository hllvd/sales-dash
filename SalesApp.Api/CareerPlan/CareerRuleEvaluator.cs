using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SalesApp.Models;

namespace SalesApp.CareerPlan
{
    public record RequirementEvaluationResult(
        string Label,
        string Metric,
        decimal CurrentValue,
        decimal? TargetValue,
        string FormattedCurrent,
        string FormattedTarget,
        string Op,
        bool Passed,
        decimal Progress
    );

    public record RuleEvaluationNode(
        RuleNodeType Type,
        bool Passed,
        decimal Progress,
        RequirementEvaluationResult? Requirement = null,
        IReadOnlyList<RuleEvaluationNode>? Children = null
    );

    public record CareerPlanEvaluationResult(
        RuleEvaluationNode Root,
        IReadOnlyList<RequirementEvaluationResult> Requirements,
        bool AllPassed
    );

    public static class CareerRuleEvaluator
    {
        private static readonly CultureInfo PtBr = new CultureInfo("pt-BR");

        public static CareerPlanEvaluationResult Evaluate(
            RuleNode rootRule,
            CareerMetricInputs inputs,
            ClassificationLevel? targetLevel)
        {
            var flatRequirements = new List<RequirementEvaluationResult>();
            var rootEval = EvaluateNode(rootRule, inputs, targetLevel, flatRequirements);
            return new CareerPlanEvaluationResult(rootEval, flatRequirements, rootEval.Passed);
        }

        private static RuleEvaluationNode EvaluateNode(
            RuleNode node,
            CareerMetricInputs inputs,
            ClassificationLevel? targetLevel,
            List<RequirementEvaluationResult> flatRequirements)
        {
            if (node.Type == RuleNodeType.Condition)
            {
                var req = EvaluateCondition(node, inputs, targetLevel);
                flatRequirements.Add(req);
                return new RuleEvaluationNode(
                    Type: RuleNodeType.Condition,
                    Passed: req.Passed,
                    Progress: req.Progress,
                    Requirement: req
                );
            }

            var children = new List<RuleEvaluationNode>();
            if (node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    children.Add(EvaluateNode(child, inputs, targetLevel, flatRequirements));
                }
            }

            bool passed;
            decimal progress;

            if (children.Count == 0)
            {
                passed = true;
                progress = 1m;
            }
            else if (node.Type == RuleNodeType.All)
            {
                passed = children.All(c => c.Passed);
                progress = children.Average(c => c.Progress);
            }
            else // Any
            {
                passed = children.Any(c => c.Passed);
                progress = children.Max(c => c.Progress);
            }

            return new RuleEvaluationNode(
                Type: node.Type,
                Passed: passed,
                Progress: progress,
                Children: children
            );
        }

        private static RequirementEvaluationResult EvaluateCondition(
            RuleNode node,
            CareerMetricInputs inputs,
            ClassificationLevel? targetLevel)
        {
            var metricKind = node.Metric!.Value;
            var current = CareerMetricsCalculator.ComputeMetric(metricKind, node.WindowMonths, node.Eligibility, inputs);

            decimal? target = ResolveTargetValue(node.Value, targetLevel);

            var op = node.Op ?? ComparisonOp.GreaterThanOrEqual;
            var opStr = FormatOp(op);

            bool passed = false;
            decimal progress = 0m;

            if (target.HasValue)
            {
                passed = op switch
                {
                    ComparisonOp.GreaterThan => current > target.Value,
                    ComparisonOp.GreaterThanOrEqual => current >= target.Value,
                    ComparisonOp.LessThan => current < target.Value,
                    ComparisonOp.LessThanOrEqual => current <= target.Value,
                    ComparisonOp.Equal => current == target.Value,
                    _ => false
                };

                if (target.Value <= 0m)
                {
                    progress = passed ? 1m : 0m;
                }
                else if (op is ComparisonOp.GreaterThan or ComparisonOp.GreaterThanOrEqual)
                {
                    progress = Math.Min(1m, Math.Max(0m, current / target.Value));
                }
                else if (op is ComparisonOp.LessThan or ComparisonOp.LessThanOrEqual)
                {
                    progress = current <= target.Value
                        ? 1m
                        : Math.Max(0m, 1m - ((current - target.Value) / target.Value));
                }
                else
                {
                    progress = passed ? 1m : 0m;
                }
            }

            string formattedCurrent = FormatValue(metricKind, current);
            string formattedTarget = target.HasValue ? FormatValue(metricKind, target.Value) : "Meta não configurada";

            string label = !string.IsNullOrWhiteSpace(node.Label)
                ? node.Label
                : $"{FormatMetricName(metricKind)} {opStr} {formattedTarget}";

            return new RequirementEvaluationResult(
                Label: label,
                Metric: metricKind.ToString(),
                CurrentValue: current,
                TargetValue: target,
                FormattedCurrent: formattedCurrent,
                FormattedTarget: formattedTarget,
                Op: opStr,
                Passed: passed,
                Progress: progress
            );
        }

        private static decimal? ResolveTargetValue(RuleValue? ruleVal, ClassificationLevel? targetLevel)
        {
            if (ruleVal == null) return null;

            if (ruleVal.ConstantValue.HasValue)
            {
                return ruleVal.ConstantValue.Value;
            }

            if (!string.IsNullOrEmpty(ruleVal.Reference))
            {
                if (targetLevel == null) return null;

                switch (ruleVal.Reference)
                {
                    case "targetLevel.salesGoal":
                        return targetLevel.SalesGoal;

                    case "targetLevel.retention":
                        return targetLevel.Retention.HasValue
                            ? (decimal)targetLevel.Retention.Value / 100m
                            : null;
                }
            }

            return null;
        }

        private static string FormatOp(ComparisonOp op) => op switch
        {
            ComparisonOp.GreaterThan => ">",
            ComparisonOp.GreaterThanOrEqual => "≥",
            ComparisonOp.LessThan => "<",
            ComparisonOp.LessThanOrEqual => "≤",
            ComparisonOp.Equal => "=",
            _ => ">="
        };

        private static string FormatMetricName(MetricKind metric) => metric switch
        {
            MetricKind.Retention => "Retenção",
            MetricKind.StrictRetention => "Retenção Estrita",
            MetricKind.Production => "Produção",
            MetricKind.IsTeamOwner => "Líder de Equipe",
            MetricKind.ActiveTeamMembers => "Membros Ativos",
            MetricKind.DirectSubTeams => "Equipes Diretas",
            _ => metric.ToString()
        };

        private static string FormatValue(MetricKind metric, decimal value)
        {
            switch (metric)
            {
                case MetricKind.Retention:
                case MetricKind.StrictRetention:
                    return value.ToString("P1", PtBr);

                case MetricKind.Production:
                    return value.ToString("C2", PtBr);

                case MetricKind.IsTeamOwner:
                    return value >= 1m ? "Sim" : "Não";

                case MetricKind.ActiveTeamMembers:
                case MetricKind.DirectSubTeams:
                    return ((int)value).ToString();

                default:
                    return value.ToString("N2", PtBr);
            }
        }
    }
}
