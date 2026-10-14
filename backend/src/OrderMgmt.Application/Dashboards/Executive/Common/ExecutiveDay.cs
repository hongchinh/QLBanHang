using OrderMgmt.Application.Inventory.Common;

namespace OrderMgmt.Application.Dashboards.Executive.Common;

/// The instants of a VN day that the dashboard hands to the database (always UTC).
public static class ExecutiveDay
{
    /// The instant the day ends, exclusive: a figure "as at" the day counts everything before it.
    public static DateTimeOffset End(DateOnly day) => VnTime.ToUtc(VnTime.StartOfNextDay(day));
}
