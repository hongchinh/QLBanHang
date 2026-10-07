using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Inventory.Common;

public readonly record struct CostingPeriodRange(DateOnly Start, DateOnly End);

/// Calendar months, quarters and years used as costing periods.
public static class CostingPeriodCalendar
{
    public static CostingPeriodRange PeriodOf(DateOnly date, CostingPeriod period)
    {
        var startMonth = period switch
        {
            CostingPeriod.Month => date.Month,
            CostingPeriod.Quarter => (date.Month - 1) / 3 * 3 + 1,
            _ => 1,
        };
        var start = new DateOnly(date.Year, startMonth, 1);
        return new CostingPeriodRange(start, start.AddMonths(MonthsIn(period)).AddDays(-1));
    }

    public static CostingPeriodRange Next(CostingPeriodRange current, CostingPeriod period) =>
        PeriodOf(current.End.AddDays(1), period);

    /// The periods covering [from, to].
    public static IReadOnlyList<CostingPeriodRange> Range(DateOnly from, DateOnly to, CostingPeriod period)
    {
        var periods = new List<CostingPeriodRange>();
        for (var p = PeriodOf(from, period); p.Start <= to; p = Next(p, period))
            periods.Add(p);
        return periods;
    }

    public static bool IsPeriodEnd(DateOnly date, CostingPeriod period) => PeriodOf(date, period).End == date;

    /// PeriodOf(date), advanced while its End <= lockedUntil (fully locked periods are frozen).
    public static CostingPeriodRange FirstUnlockedFrom(DateOnly date, DateOnly? lockedUntil, CostingPeriod period)
    {
        var p = PeriodOf(date, period);
        while (lockedUntil.HasValue && p.End <= lockedUntil.Value)
            p = Next(p, period);
        return p;
    }

    private static int MonthsIn(CostingPeriod period) => period switch
    {
        CostingPeriod.Month => 1,
        CostingPeriod.Quarter => 3,
        _ => 12,
    };
}
