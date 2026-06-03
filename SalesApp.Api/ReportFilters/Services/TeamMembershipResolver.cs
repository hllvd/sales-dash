using System;

namespace SalesApp.ReportFilters.Services
{
    /// <summary>
    /// Pure function helper for determining team membership activity relative to sale and report dates.
    /// Follows strict pure, deterministic logic with isolated side effects.
    /// </summary>
    public static class TeamMembershipResolver
    {
        /// <summary>
        /// Pure function to determine if a team membership was active at the time of a contract sale,
        /// and if the sale date is within the report filter's resolved start/end dates.
        /// </summary>
        /// <param name="membershipStart">The start date of the user in the team.</param>
        /// <param name="membershipEnd">The end date of the user in the team (nullable if ongoing).</param>
        /// <param name="saleStartDate">The start date of the contract sale.</param>
        /// <param name="reportStartDate">The resolved start date of the report filter (nullable if open-ended).</param>
        /// <param name="reportEndDate">The resolved end date of the report filter (nullable if open-ended).</param>
        /// <returns>True if the membership was active for the sale and the sale is within the report range; otherwise false.</returns>
        public static bool IsMembershipActiveForSale(
            DateTime membershipStart,
            DateTime? membershipEnd,
            DateTime saleStartDate,
            DateTime? reportStartDate,
            DateTime? reportEndDate)
        {
            // 1. Validate contract sale date is within the report filter range
            if (reportStartDate.HasValue && saleStartDate < reportStartDate.Value)
            {
                return false;
            }

            if (reportEndDate.HasValue && saleStartDate > reportEndDate.Value)
            {
                return false;
            }

            // 2. Validate team membership is active at the time of the sale
            if (membershipStart > saleStartDate)
            {
                return false;
            }

            if (membershipEnd.HasValue && membershipEnd.Value <= saleStartDate)
            {
                return false;
            }

            return true;
        }
    }
}
