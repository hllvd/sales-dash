using System;

namespace SalesApp.CareerPlan
{
    public static class CareerReferenceDate
    {
        /// <summary>
        /// Pure function to determine the most recent reference closing date (e.g. day 15) that is &lt;= today.
        /// </summary>
        public static DateTime Resolve(DateTime today, int closingDay = 15)
        {
            var dateOnly = DateTime.SpecifyKind(today.Date, DateTimeKind.Utc);

            if (dateOnly.Day >= closingDay)
            {
                return new DateTime(dateOnly.Year, dateOnly.Month, closingDay, 0, 0, 0, DateTimeKind.Utc);
            }
            else
            {
                var prevMonth = dateOnly.AddMonths(-1);
                int daysInPrevMonth = DateTime.DaysInMonth(prevMonth.Year, prevMonth.Month);
                int safeDay = Math.Min(closingDay, daysInPrevMonth);
                return new DateTime(prevMonth.Year, prevMonth.Month, safeDay, 0, 0, 0, DateTimeKind.Utc);
            }
        }

        /// <summary>
        /// Pure function returning the half-open interval [Start, End) of full closed months
        /// ending strictly before the reference date's month.
        /// Example: reference 15/10/2026, window 15 months -&gt; Start = 2025-07-01 00:00:00, End = 2026-10-01 00:00:00.
        /// </summary>
        public static (DateTime Start, DateTime End) GetClosedMonthWindow(DateTime referenceDate, int windowMonths)
        {
            if (windowMonths <= 0)
                throw new ArgumentOutOfRangeException(nameof(windowMonths), "Window months must be positive.");

            var end = new DateTime(referenceDate.Year, referenceDate.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var start = end.AddMonths(-windowMonths);

            return (start, end);
        }

        /// <summary>
        /// Pure function to compute the number of full months elapsed between contract SaleStartDate and referenceDate.
        /// </summary>
        public static int CalculateAgeMonths(DateTime saleDate, DateTime referenceDate)
        {
            if (saleDate > referenceDate) return 0;

            int months = (referenceDate.Year - saleDate.Year) * 12 + referenceDate.Month - saleDate.Month;
            if (saleDate.Day > referenceDate.Day)
            {
                months--;
            }

            return Math.Max(0, months);
        }
    }
}
