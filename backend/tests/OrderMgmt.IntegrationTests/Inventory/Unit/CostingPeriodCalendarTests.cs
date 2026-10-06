using FluentAssertions;
using OrderMgmt.Application.Inventory.Common;
using OrderMgmt.Domain.Enums;
using Xunit;

namespace OrderMgmt.IntegrationTests.Inventory.Unit;

public class CostingPeriodCalendarTests
{
    private static DateOnly D(int y, int m, int d) => new(y, m, d);

    [Fact]
    public void Month_period() =>
        CostingPeriodCalendar.PeriodOf(D(2026, 2, 15), CostingPeriod.Month)
            .Should().Be(new CostingPeriodRange(D(2026, 2, 1), D(2026, 2, 28)));

    [Fact]
    public void Month_period_in_leap_year() =>
        CostingPeriodCalendar.PeriodOf(D(2024, 2, 10), CostingPeriod.Month)
            .Should().Be(new CostingPeriodRange(D(2024, 2, 1), D(2024, 2, 29)));

    [Fact]
    public void Quarter_period() =>
        CostingPeriodCalendar.PeriodOf(D(2026, 11, 30), CostingPeriod.Quarter)
            .Should().Be(new CostingPeriodRange(D(2026, 10, 1), D(2026, 12, 31)));

    [Fact]
    public void Year_period() =>
        CostingPeriodCalendar.PeriodOf(D(2026, 6, 15), CostingPeriod.Year)
            .Should().Be(new CostingPeriodRange(D(2026, 1, 1), D(2026, 12, 31)));

    [Fact]
    public void Next_month_crosses_year() =>
        CostingPeriodCalendar.Next(new CostingPeriodRange(D(2026, 12, 1), D(2026, 12, 31)), CostingPeriod.Month)
            .Should().Be(new CostingPeriodRange(D(2027, 1, 1), D(2027, 1, 31)));

    [Fact]
    public void Range_lists_covering_periods() =>
        CostingPeriodCalendar.Range(D(2026, 1, 15), D(2026, 3, 2), CostingPeriod.Month).Should().Equal(
            new CostingPeriodRange(D(2026, 1, 1), D(2026, 1, 31)),
            new CostingPeriodRange(D(2026, 2, 1), D(2026, 2, 28)),
            new CostingPeriodRange(D(2026, 3, 1), D(2026, 3, 31)));

    [Fact]
    public void IsPeriodEnd()
    {
        CostingPeriodCalendar.IsPeriodEnd(D(2026, 3, 31), CostingPeriod.Quarter).Should().BeTrue();
        CostingPeriodCalendar.IsPeriodEnd(D(2026, 3, 30), CostingPeriod.Quarter).Should().BeFalse();
    }

    [Fact]
    public void FirstUnlockedFrom_skips_fully_locked_periods() =>
        CostingPeriodCalendar.FirstUnlockedFrom(D(2026, 3, 10), D(2026, 3, 31), CostingPeriod.Month)
            .Should().Be(new CostingPeriodRange(D(2026, 4, 1), D(2026, 4, 30)));

    [Fact]
    public void FirstUnlockedFrom_keeps_partially_locked_period() =>
        CostingPeriodCalendar.FirstUnlockedFrom(D(2026, 3, 10), D(2026, 3, 15), CostingPeriod.Month)
            .Should().Be(new CostingPeriodRange(D(2026, 3, 1), D(2026, 3, 31)));

    [Fact]
    public void FirstUnlockedFrom_without_lock_or_with_older_lock()
    {
        CostingPeriodCalendar.FirstUnlockedFrom(D(2026, 3, 10), null, CostingPeriod.Month)
            .Should().Be(new CostingPeriodRange(D(2026, 3, 1), D(2026, 3, 31)));
        CostingPeriodCalendar.FirstUnlockedFrom(D(2026, 5, 10), D(2026, 3, 31), CostingPeriod.Month)
            .Should().Be(new CostingPeriodRange(D(2026, 5, 1), D(2026, 5, 31)));
    }
}
