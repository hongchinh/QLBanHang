using OrderMgmt.Application.Dashboards.Executive.Models;

namespace OrderMgmt.Application.Dashboards.Executive.Common;

public static class ExecutiveSeries
{
    /// Day: the date; Week: its Monday; Month: day 1.
    public static DateOnly BucketOf(DateOnly date, SeriesGranularity granularity) => granularity switch
    {
        SeriesGranularity.Day => date,
        SeriesGranularity.Week => date.AddDays(-(((int)date.DayOfWeek + 6) % 7)),
        _ => new DateOnly(date.Year, date.Month, 1),
    };

    /// Every bucket that holds a day of [from, to]: distinct, ascending.
    public static IReadOnlyList<DateOnly> Buckets(DateOnly from, DateOnly to, SeriesGranularity granularity)
    {
        var buckets = new List<DateOnly>();
        for (var bucket = BucketOf(from, granularity); bucket <= to; bucket = Next(bucket, granularity))
            buckets.Add(bucket);
        return buckets;
    }

    private static DateOnly Next(DateOnly bucket, SeriesGranularity granularity) => granularity switch
    {
        SeriesGranularity.Day => bucket.AddDays(1),
        SeriesGranularity.Week => bucket.AddDays(7),
        _ => bucket.AddMonths(1),
    };
}
