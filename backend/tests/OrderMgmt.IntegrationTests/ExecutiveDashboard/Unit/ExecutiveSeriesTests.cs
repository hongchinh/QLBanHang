using FluentAssertions;
using OrderMgmt.Application.Dashboards.Executive.Common;
using OrderMgmt.Application.Dashboards.Executive.Models;
using Xunit;

namespace OrderMgmt.IntegrationTests.ExecutiveDashboard.Unit;

/// 2026-10-12 is a Monday.
public class ExecutiveSeriesTests
{
    private static DateOnly D(int month, int day) => new(2026, month, day);

    [Fact]
    public void Week_bucket_is_the_monday_on_or_before_the_date()
    {
        ExecutiveSeries.BucketOf(D(10, 14), SeriesGranularity.Week).Should().Be(D(10, 12));
        ExecutiveSeries.BucketOf(D(10, 12), SeriesGranularity.Week).Should().Be(D(10, 12));
        ExecutiveSeries.BucketOf(D(10, 18), SeriesGranularity.Week).Should().Be(D(10, 12));
        ExecutiveSeries.BucketOf(D(10, 19), SeriesGranularity.Week).Should().Be(D(10, 19));
    }

    [Fact]
    public void Month_bucket_is_day_one()
        => ExecutiveSeries.BucketOf(D(10, 14), SeriesGranularity.Month).Should().Be(D(10, 1));

    [Fact]
    public void Day_bucket_is_the_date()
        => ExecutiveSeries.BucketOf(D(10, 14), SeriesGranularity.Day).Should().Be(D(10, 14));

    [Fact]
    public void Daily_buckets()
        => ExecutiveSeries.Buckets(D(10, 1), D(10, 3), SeriesGranularity.Day)
            .Should().Equal(D(10, 1), D(10, 2), D(10, 3));

    [Fact]
    public void Weekly_buckets_start_with_the_week_of_the_first_day()
        => ExecutiveSeries.Buckets(D(9, 30), D(10, 13), SeriesGranularity.Week)
            .Should().Equal(D(9, 28), D(10, 5), D(10, 12));

    [Fact]
    public void Monthly_buckets_start_with_the_month_of_the_first_day()
        => ExecutiveSeries.Buckets(D(8, 15), D(10, 13), SeriesGranularity.Month)
            .Should().Equal(D(8, 1), D(9, 1), D(10, 1));

    [Fact]
    public void Undefined_granularity_is_refused()
    {
        var undefined = (SeriesGranularity)7;

        Action bucketOf = () => ExecutiveSeries.BucketOf(D(10, 14), undefined);
        Action buckets = () => ExecutiveSeries.Buckets(D(10, 1), D(10, 3), undefined);

        bucketOf.Should().Throw<ArgumentOutOfRangeException>();
        buckets.Should().Throw<ArgumentOutOfRangeException>();
    }
}
