using OrderMgmt.Application.Inventory.Common;

namespace OrderMgmt.Application.Dashboards.Executive.Common;

/// The period of a dashboard request and the one before it, which has the same number of days and ends the day
/// before From. Dates are VN days; Start/End are the UTC instants of their bounds, End and PrevEnd exclusive.
public sealed record ExecutivePeriod(DateOnly From, DateOnly To, DateOnly PrevFrom, DateOnly PrevTo)
{
    /// To is the given `to`; without it VN today, or `from` when that is after today. From is the given `from`;
    /// without it day 1 of To's month.
    public static ExecutivePeriod Create(DateOnly? from, DateOnly? to, DateOnly today)
    {
        var end = to ?? (from is { } requested && requested > today ? requested : today);
        var start = from ?? new DateOnly(end.Year, end.Month, 1);
        var prevTo = start.AddDays(-1);
        var prevFrom = prevTo.AddDays(-(end.DayNumber - start.DayNumber));
        return new ExecutivePeriod(start, end, prevFrom, prevTo);
    }

    public int Days => To.DayNumber - From.DayNumber + 1;

    public DateTimeOffset Start => VnTime.ToUtc(VnTime.StartOfDay(From));

    public DateTimeOffset End => VnTime.ToUtc(VnTime.StartOfNextDay(To));

    public DateTimeOffset PrevStart => VnTime.ToUtc(VnTime.StartOfDay(PrevFrom));

    public DateTimeOffset PrevEnd => VnTime.ToUtc(VnTime.StartOfNextDay(PrevTo));
}
