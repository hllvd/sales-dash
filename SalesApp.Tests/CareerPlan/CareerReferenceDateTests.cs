using System;
using FluentAssertions;
using SalesApp.CareerPlan;
using Xunit;

namespace SalesApp.Tests.CareerPlan
{
    public class CareerReferenceDateTests
    {
        [Fact]
        public void Resolve_WhenTodayIsAfterClosingDay_ReturnsClosingDayOfCurrentMonth()
        {
            var today = new DateTime(2026, 10, 23, 14, 0, 0, DateTimeKind.Utc);
            var refDate = CareerReferenceDate.Resolve(today, 15);

            refDate.Year.Should().Be(2026);
            refDate.Month.Should().Be(10);
            refDate.Day.Should().Be(15);
        }

        [Fact]
        public void Resolve_WhenTodayIsExactlyClosingDay_ReturnsClosingDayOfCurrentMonth()
        {
            var today = new DateTime(2026, 10, 15, 9, 30, 0, DateTimeKind.Utc);
            var refDate = CareerReferenceDate.Resolve(today, 15);

            refDate.Year.Should().Be(2026);
            refDate.Month.Should().Be(10);
            refDate.Day.Should().Be(15);
        }

        [Fact]
        public void Resolve_WhenTodayIsBeforeClosingDay_ReturnsClosingDayOfPreviousMonth()
        {
            var today = new DateTime(2026, 10, 10, 10, 0, 0, DateTimeKind.Utc);
            var refDate = CareerReferenceDate.Resolve(today, 15);

            refDate.Year.Should().Be(2026);
            refDate.Month.Should().Be(9);
            refDate.Day.Should().Be(15);
        }

        [Fact]
        public void Resolve_WhenTodayIsJanuaryBeforeClosingDay_RollsOverToDecemberPreviousYear()
        {
            var today = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc);
            var refDate = CareerReferenceDate.Resolve(today, 15);

            refDate.Year.Should().Be(2025);
            refDate.Month.Should().Be(12);
            refDate.Day.Should().Be(15);
        }

        [Fact]
        public void GetClosedMonthWindow_15MonthsFromOctober15_CoversJuly2025ThroughSeptember2026()
        {
            var referenceDate = new DateTime(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc);
            var (start, end) = CareerReferenceDate.GetClosedMonthWindow(referenceDate, 15);

            // Closed months end strictly before 2026-10-01
            end.Should().Be(new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc));
            // 15 months back: 2026-10 - 15 months = 2025-07
            start.Should().Be(new DateTime(2025, 7, 1, 0, 0, 0, DateTimeKind.Utc));
        }

        [Fact]
        public void GetClosedMonthWindow_12MonthsFromOctober15_CoversOctober2025ThroughSeptember2026()
        {
            var referenceDate = new DateTime(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc);
            var (start, end) = CareerReferenceDate.GetClosedMonthWindow(referenceDate, 12);

            end.Should().Be(new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc));
            start.Should().Be(new DateTime(2025, 10, 1, 0, 0, 0, DateTimeKind.Utc));
        }

        [Theory]
        [InlineData("2026-08-10", "2026-10-15", 2)] // 2 full calendar months (Aug to Oct)
        [InlineData("2026-08-20", "2026-10-15", 2)] // Entire month of Aug counts
        [InlineData("2026-09-15", "2026-10-15", 1)] // 1 month (Sep to Oct)
        [InlineData("2026-07-01", "2026-10-15", 3)] // 3 full months (Jul to Oct)
        [InlineData("2026-10-20", "2026-10-15", 0)] // Same month
        public void CalculateAgeMonths_ComputesFullMonthsAccurately(string saleStr, string refStr, int expectedMonths)
        {
            var sale = DateTime.Parse(saleStr).ToUniversalTime();
            var reference = DateTime.Parse(refStr).ToUniversalTime();

            var age = CareerReferenceDate.CalculateAgeMonths(sale, reference);
            age.Should().Be(expectedMonths);
        }

        [Theory]
        [InlineData("2026-10-08", "2026-11-15", 2)] // Oct sale on Nov 15 -> 2 installments (RT1, RT2)
        [InlineData("2026-10-15", "2026-11-15", 2)] // Oct sale on Nov 15 -> 2 installments (RT1, RT2)
        [InlineData("2026-10-31", "2026-11-15", 2)] // Oct sale on Nov 15 -> 2 installments (RT1, RT2)
        [InlineData("2026-11-05", "2026-11-15", 1)] // Nov sale on Nov 15 -> 1 installment (RT1)
        [InlineData("2026-09-10", "2026-11-15", 3)] // Sep sale on Nov 15 -> 3 installments (RT1..3)
        public void CalculateDueInstallments_ComputesInstallmentsAccurately(string saleStr, string refStr, int expected)
        {
            var sale = DateTime.Parse(saleStr).ToUniversalTime();
            var reference = DateTime.Parse(refStr).ToUniversalTime();

            var due = CareerReferenceDate.CalculateDueInstallments(sale, reference);
            due.Should().Be(expected);
        }
    }
}
