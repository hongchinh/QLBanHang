using FluentAssertions;
using OrderMgmt.Application.Dashboards.Executive.Common;
using Xunit;

namespace OrderMgmt.IntegrationTests.ExecutiveDashboard.Unit;

public class ExecutivePeriodTests
{
    private static DateOnly D(int month, int day) => new(2026, month, day);

    private static readonly DateOnly Today = D(10, 13);

    [Fact]
    public void Without_dates_the_period_is_the_month_so_far()
    {
        var period = ExecutivePeriod.Create(null, null, Today);

        period.From.Should().Be(D(10, 1));
        period.To.Should().Be(D(10, 13));
        period.PrevFrom.Should().Be(D(9, 18));
        period.PrevTo.Should().Be(D(9, 30));
        period.Days.Should().Be(13);
    }

    [Fact]
    public void Previous_period_has_the_same_length_and_ends_the_day_before()
    {
        var period = ExecutivePeriod.Create(D(10, 5), D(10, 8), Today);

        period.From.Should().Be(D(10, 5));
        period.To.Should().Be(D(10, 8));
        period.PrevFrom.Should().Be(D(10, 1));
        period.PrevTo.Should().Be(D(10, 4));
        period.Days.Should().Be(4);
    }

    [Fact]
    public void Only_to_starts_on_day_one_of_its_month()
    {
        var period = ExecutivePeriod.Create(null, D(9, 20), Today);

        period.From.Should().Be(D(9, 1));
        period.To.Should().Be(D(9, 20));
    }

    [Fact]
    public void Only_from_in_the_future_ends_on_that_day()
    {
        var period = ExecutivePeriod.Create(new DateOnly(2026, 11, 2), null, Today);

        period.From.Should().Be(new DateOnly(2026, 11, 2));
        period.To.Should().Be(new DateOnly(2026, 11, 2));
    }

    [Fact]
    public void Only_from_in_the_past_ends_today()
    {
        var period = ExecutivePeriod.Create(D(10, 5), null, Today);

        period.From.Should().Be(D(10, 5));
        period.To.Should().Be(Today);
    }

    [Fact]
    public void A_single_day_is_compared_with_the_day_before()
    {
        var period = ExecutivePeriod.Create(D(10, 13), D(10, 13), Today);

        period.PrevFrom.Should().Be(D(10, 12));
        period.PrevTo.Should().Be(D(10, 12));
        period.Days.Should().Be(1);
    }

    [Fact]
    public void Instants_are_vn_midnights_in_utc_and_the_end_is_exclusive()
    {
        var period = ExecutivePeriod.Create(D(10, 1), D(10, 13), Today);

        period.Start.Should().Be(new DateTimeOffset(2026, 9, 30, 17, 0, 0, TimeSpan.Zero));
        period.End.Should().Be(new DateTimeOffset(2026, 10, 13, 17, 0, 0, TimeSpan.Zero));
        period.PrevStart.Should().Be(new DateTimeOffset(2026, 9, 17, 17, 0, 0, TimeSpan.Zero));
        period.PrevEnd.Should().Be(period.Start);
        period.Start.Offset.Should().Be(TimeSpan.Zero);
        period.End.Offset.Should().Be(TimeSpan.Zero);
    }
}
