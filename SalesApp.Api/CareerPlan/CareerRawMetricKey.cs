namespace SalesApp.CareerPlan
{
    /// <summary>
    /// Maps a rule condition to the camelCase key of the matching consolidated raw metric
    /// (property of CareerRawMetricsDto), so the UI can highlight metrics used as promotion requirements.
    /// </summary>
    public static class CareerRawMetricKey
    {
        private static readonly int[] RetentionWindows = { 6, 12, 15 };
        private static readonly int[] ProductionWindows = { 12, 15 };

        /// <summary>Returns the raw metric key, or null when no consolidated card represents the condition.</summary>
        public static string? From(MetricKind metric, int? windowMonths, EligibilityFilter? eligibility)
        {
            return metric switch
            {
                MetricKind.IsTeamOwner => "isTeamOwner",
                MetricKind.ActiveTeamMembers => "activeTeamMembers",
                MetricKind.DirectSubTeams => "directSubTeams",
                MetricKind.Retention => Windowed("retention", windowMonths, RetentionWindows, eligibility),
                MetricKind.StrictRetention => Windowed("strictRetention", windowMonths, RetentionWindows, eligibility),
                MetricKind.Production => Windowed("production", windowMonths, ProductionWindows, eligibility),
                MetricKind.ActiveProduction => ActiveProduction(windowMonths, eligibility),
                MetricKind.CommercializedContracts => CommercializedContracts(windowMonths, eligibility),
                _ => null
            };
        }

        private static string? CommercializedContracts(int? windowMonths, EligibilityFilter? eligibility)
        {
            int wm = windowMonths.GetValueOrDefault(12);
            int minAge = eligibility?.MinAgeMonths ?? 2;
            return wm == 12 && minAge == 2 && eligibility?.MinInferredPaidMonths == null ? "commercializedContracts12mMinAge2" : null;
        }

        private static string? Windowed(string prefix, int? windowMonths, int[] allowed, EligibilityFilter? eligibility)
        {
            int wm = windowMonths.GetValueOrDefault(12);
            bool hasFilter = eligibility?.MinAgeMonths != null || eligibility?.MinInferredPaidMonths != null;
            return !hasFilter && System.Array.IndexOf(allowed, wm) >= 0 ? $"{prefix}{wm}m" : null;
        }

        private static string? ActiveProduction(int? windowMonths, EligibilityFilter? eligibility)
        {
            int wm = windowMonths.GetValueOrDefault(12);
            int minPaid = eligibility?.MinInferredPaidMonths ?? 2;
            return wm == 12 && minPaid == 2 && eligibility?.MinAgeMonths == null ? "activeProduction12mMinPaid2" : null;
        }
    }
}
