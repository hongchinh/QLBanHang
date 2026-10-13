using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Inventory.Common;

public static class CostProvisional
{
    /// The costing period containing `date` has not ended on `today` (its last day is today or later).
    public static bool IsProvisional(DateOnly date, CostingPeriod period, DateOnly today) =>
        CostingPeriodCalendar.PeriodOf(date, period).End >= today;
}
