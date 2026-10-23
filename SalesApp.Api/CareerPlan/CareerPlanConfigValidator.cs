using System;
using System.Collections.Generic;
using System.Linq;

namespace SalesApp.CareerPlan
{
    public static class CareerPlanConfigValidator
    {
        public static IReadOnlyList<string> Validate(CareerPlanConfig config, IEnumerable<string>? existingDbLevelNames = null)
        {
            var errors = new List<string>();

            if (config == null)
            {
                errors.Add("Configuration cannot be null.");
                return errors;
            }

            if (config.ClosingDay < 1 || config.ClosingDay > 28)
            {
                errors.Add($"ClosingDay must be between 1 and 28, but got {config.ClosingDay}.");
            }

            if (config.Levels == null || config.Levels.Count == 0)
            {
                errors.Add("Configuration must define at least one level.");
                return errors;
            }

            HashSet<string>? dbSet = null;
            if (existingDbLevelNames != null)
            {
                dbSet = new HashSet<string>(existingDbLevelNames, StringComparer.OrdinalIgnoreCase);
            }

            foreach (var (levelName, rule) in config.Levels)
            {
                if (string.IsNullOrWhiteSpace(levelName))
                {
                    errors.Add("Level name in configuration cannot be empty.");
                    continue;
                }

                if (dbSet != null && !dbSet.Contains(levelName))
                {
                    errors.Add($"Career plan level '{levelName}' configured in JSON does not exist in database classification levels.");
                }

                ValidateNode(rule.Requirements, $"levels.{levelName}.requirements", errors);
            }

            return errors;
        }

        private static void ValidateNode(RuleNode node, string path, List<string> errors)
        {
            if (node == null)
            {
                errors.Add($"{path} cannot be null.");
                return;
            }

            switch (node.Type)
            {
                case RuleNodeType.All:
                case RuleNodeType.Any:
                    if (node.Children == null || node.Children.Count == 0)
                    {
                        errors.Add($"{path} group ({node.Type}) must have at least one child rule.");
                    }
                    else
                    {
                        for (int i = 0; i < node.Children.Count; i++)
                        {
                            ValidateNode(node.Children[i], $"{path}[{i}]", errors);
                        }
                    }
                    break;

                case RuleNodeType.Condition:
                    if (node.Metric == null)
                    {
                        errors.Add($"{path} condition must specify a Metric.");
                    }
                    else if (node.Metric is MetricKind.Retention or MetricKind.StrictRetention or MetricKind.Production)
                    {
                        if (!node.WindowMonths.HasValue || node.WindowMonths.Value <= 0)
                        {
                            errors.Add($"{path} metric '{node.Metric}' requires a positive windowMonths.");
                        }
                    }

                    if (node.Op == null)
                    {
                        errors.Add($"{path} condition must specify a comparison operator (Op).");
                    }

                    if (node.Value == null || (!node.Value.ConstantValue.HasValue && string.IsNullOrEmpty(node.Value.Reference)))
                    {
                        errors.Add($"{path} condition must specify either a ConstantValue or Reference.");
                    }
                    break;
            }
        }
    }
}
