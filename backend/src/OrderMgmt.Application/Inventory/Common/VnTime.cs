namespace OrderMgmt.Application.Inventory.Common;

/// Business dates are Vietnam calendar days; instants handed to EF are always UTC (D27).
public static class VnTime
{
    public static readonly TimeSpan Offset = TimeSpan.FromHours(7);

    public static DateOnly ToVnDate(DateTimeOffset at) => DateOnly.FromDateTime(at.ToOffset(Offset).DateTime);

    /// The UTC instant of 00:00 in Vietnam on `date`.
    public static DateTimeOffset StartOfDay(DateOnly date) =>
        new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), Offset).ToUniversalTime();

    /// Exclusive upper bound of `date`, UTC.
    public static DateTimeOffset StartOfNextDay(DateOnly date) => StartOfDay(date.AddDays(1));

    /// Normalizes any input instant before it reaches EF (Npgsql rejects non-zero offsets for timestamptz).
    public static DateTimeOffset ToUtc(DateTimeOffset at) => at.ToUniversalTime();
}
