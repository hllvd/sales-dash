using System;
using System.Collections.Generic;
using System.Text.Json;

namespace SalesApp.CareerPlan
{
    public record ConfigParseResult(
        CareerPlanConfig? Config,
        IReadOnlyList<string> Errors
    )
    {
        public bool IsSuccess => Errors.Count == 0 && Config != null;
    }

    /// <summary>
    /// Pure parser for career plan JSON configuration.
    /// Produces strongly-typed immutable records or structured error messages.
    /// </summary>
    public static class CareerPlanConfigParser
    {
        public static ConfigParseResult Parse(string json)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(json))
            {
                errors.Add("Configuration JSON cannot be empty.");
                return new ConfigParseResult(null, errors);
            }

            try
            {
                using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true });
                var root = doc.RootElement;

                if (root.ValueKind != JsonValueKind.Object)
                {
                    errors.Add("Root element must be a JSON object.");
                    return new ConfigParseResult(null, errors);
                }

                int closingDay = 15;
                if (root.TryGetProperty("closingDay", out var closingDayProp))
                {
                    if (closingDayProp.ValueKind == JsonValueKind.Number && closingDayProp.TryGetInt32(out var parsedDay))
                    {
                        if (parsedDay < 1 || parsedDay > 28)
                        {
                            errors.Add($"closingDay must be between 1 and 28, but got {parsedDay}.");
                        }
                        else
                        {
                            closingDay = parsedDay;
                        }
                    }
                    else
                    {
                        errors.Add("closingDay must be an integer.");
                    }
                }

                var levels = new Dictionary<string, CareerLevelRule>(StringComparer.OrdinalIgnoreCase);

                if (root.TryGetProperty("levels", out var levelsProp))
                {
                    if (levelsProp.ValueKind != JsonValueKind.Object)
                    {
                        errors.Add("levels property must be an object.");
                    }
                    else
                    {
                        foreach (var levelProp in levelsProp.EnumerateObject())
                        {
                            var levelName = levelProp.Name.Trim();
                            if (string.IsNullOrEmpty(levelName))
                            {
                                errors.Add("Level name cannot be empty.");
                                continue;
                            }

                            if (levelProp.Value.ValueKind != JsonValueKind.Object)
                            {
                                errors.Add($"Level '{levelName}' must be an object.");
                                continue;
                            }

                            if (!levelProp.Value.TryGetProperty("requirements", out var reqProp))
                            {
                                errors.Add($"Level '{levelName}' is missing 'requirements'.");
                                continue;
                            }

                            var ruleNode = ParseRuleNode(reqProp, $"levels.{levelName}.requirements", errors);
                            if (ruleNode != null)
                            {
                                levels[levelName] = new CareerLevelRule(ruleNode);
                            }
                        }
                    }
                }
                else
                {
                    errors.Add("Missing 'levels' property.");
                }

                if (errors.Count > 0)
                {
                    return new ConfigParseResult(null, errors);
                }

                return new ConfigParseResult(new CareerPlanConfig(closingDay, levels), errors);
            }
            catch (JsonException ex)
            {
                errors.Add($"Malformed JSON: {ex.Message}");
                return new ConfigParseResult(null, errors);
            }
        }

        private static RuleNode? ParseRuleNode(JsonElement element, string path, List<string> errors)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                errors.Add($"{path} must be an object.");
                return null;
            }

            if (element.TryGetProperty("all", out var allProp))
            {
                if (allProp.ValueKind != JsonValueKind.Array)
                {
                    errors.Add($"{path}.all must be an array.");
                    return null;
                }

                var children = new List<RuleNode>();
                int idx = 0;
                foreach (var child in allProp.EnumerateArray())
                {
                    var parsed = ParseRuleNode(child, $"{path}.all[{idx}]", errors);
                    if (parsed != null) children.Add(parsed);
                    idx++;
                }

                return new RuleNode(RuleNodeType.All, Children: children);
            }

            if (element.TryGetProperty("any", out var anyProp))
            {
                if (anyProp.ValueKind != JsonValueKind.Array)
                {
                    errors.Add($"{path}.any must be an array.");
                    return null;
                }

                var children = new List<RuleNode>();
                int idx = 0;
                foreach (var child in anyProp.EnumerateArray())
                {
                    var parsed = ParseRuleNode(child, $"{path}.any[{idx}]", errors);
                    if (parsed != null) children.Add(parsed);
                    idx++;
                }

                return new RuleNode(RuleNodeType.Any, Children: children);
            }

            // It's a leaf condition
            string label = string.Empty;
            if (element.TryGetProperty("label", out var labelProp) && labelProp.ValueKind == JsonValueKind.String)
            {
                label = labelProp.GetString() ?? string.Empty;
            }

            if (!element.TryGetProperty("metric", out var metricProp) || metricProp.ValueKind != JsonValueKind.String)
            {
                errors.Add($"{path} missing required string property 'metric'.");
                return null;
            }

            var metricStr = metricProp.GetString()?.Trim().ToLowerInvariant() ?? "";
            MetricKind? metric = metricStr switch
            {
                "retention" => MetricKind.Retention,
                "strictretention" or "strict_retention" => MetricKind.StrictRetention,
                "production" => MetricKind.Production,
                "isteamowner" or "is_team_owner" => MetricKind.IsTeamOwner,
                "activeteammembers" or "active_team_members" => MetricKind.ActiveTeamMembers,
                "directsubteams" or "direct_sub_teams" => MetricKind.DirectSubTeams,
                _ => null
            };

            if (metric == null)
            {
                errors.Add($"{path}.metric '{metricStr}' is not recognized.");
            }

            int? windowMonths = null;
            if (element.TryGetProperty("windowMonths", out var windowProp))
            {
                if (windowProp.ValueKind == JsonValueKind.Number && windowProp.TryGetInt32(out var wm))
                {
                    if (wm <= 0)
                    {
                        errors.Add($"{path}.windowMonths must be positive, got {wm}.");
                    }
                    else
                    {
                        windowMonths = wm;
                    }
                }
                else
                {
                    errors.Add($"{path}.windowMonths must be an integer.");
                }
            }

            EligibilityFilter? eligibility = null;
            if (element.TryGetProperty("eligibility", out var eligProp))
            {
                if (eligProp.ValueKind != JsonValueKind.Object)
                {
                    errors.Add($"{path}.eligibility must be an object.");
                }
                else
                {
                    int? minAge = null;
                    int? minPaid = null;

                    if (eligProp.TryGetProperty("minAgeMonths", out var ageProp))
                    {
                        if (ageProp.ValueKind == JsonValueKind.Number && ageProp.TryGetInt32(out var a))
                            minAge = a;
                        else
                            errors.Add($"{path}.eligibility.minAgeMonths must be an integer.");
                    }

                    if (eligProp.TryGetProperty("minInferredPaidMonths", out var paidProp))
                    {
                        if (paidProp.ValueKind == JsonValueKind.Number && paidProp.TryGetInt32(out var p))
                            minPaid = p;
                        else
                            errors.Add($"{path}.eligibility.minInferredPaidMonths must be an integer.");
                    }

                    eligibility = new EligibilityFilter(minAge, minPaid);
                }
            }

            if (!element.TryGetProperty("op", out var opProp) || opProp.ValueKind != JsonValueKind.String)
            {
                errors.Add($"{path} missing required string property 'op'.");
                return null;
            }

            var opStr = opProp.GetString()?.Trim().ToLowerInvariant() ?? "";
            ComparisonOp? op = opStr switch
            {
                ">=" or "gte" => ComparisonOp.GreaterThanOrEqual,
                ">" or "gt" => ComparisonOp.GreaterThan,
                "<=" or "lte" => ComparisonOp.LessThanOrEqual,
                "<" or "lt" => ComparisonOp.LessThan,
                "==" or "=" or "eq" => ComparisonOp.Equal,
                _ => null
            };

            if (op == null)
            {
                errors.Add($"{path}.op '{opStr}' is not a valid comparison operator (>=, >, <=, <, ==).");
            }

            if (!element.TryGetProperty("value", out var valProp))
            {
                errors.Add($"{path} missing required property 'value'.");
                return null;
            }

            RuleValue? value = null;
            if (valProp.ValueKind == JsonValueKind.Number && valProp.TryGetDecimal(out var decVal))
            {
                value = new RuleValue(ConstantValue: decVal);
            }
            else if (valProp.ValueKind == JsonValueKind.Object)
            {
                if (valProp.TryGetProperty("ref", out var refProp) && refProp.ValueKind == JsonValueKind.String)
                {
                    var refStr = refProp.GetString()?.Trim() ?? "";
                    if (refStr != "targetLevel.salesGoal" && refStr != "targetLevel.retention")
                    {
                        errors.Add($"{path}.value.ref '{refStr}' is not recognized. Allowed references: targetLevel.salesGoal, targetLevel.retention.");
                    }
                    else
                    {
                        value = new RuleValue(Reference: refStr);
                    }
                }
                else
                {
                    errors.Add($"{path}.value object must contain a string 'ref' property.");
                }
            }
            else
            {
                errors.Add($"{path}.value must be either a number or an object with 'ref'.");
            }

            if (errors.Count > 0 && (metric == null || op == null || value == null))
            {
                return null;
            }

            return new RuleNode(
                Type: RuleNodeType.Condition,
                Label: label,
                Metric: metric,
                WindowMonths: windowMonths,
                Eligibility: eligibility,
                Op: op,
                Value: value
            );
        }
    }
}
