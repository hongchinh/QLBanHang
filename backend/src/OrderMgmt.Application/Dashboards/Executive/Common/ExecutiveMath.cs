namespace OrderMgmt.Application.Dashboards.Executive.Common;

public static class ExecutiveMath
{
    /// Change against the previous value in percent, divided by its absolute value; null when there is no previous value.
    public static decimal? DeltaPct(decimal current, decimal previous) =>
        previous == 0m ? null : Round2((current - previous) / Math.Abs(previous) * 100m);

    /// Gross profit as a percentage of revenue; null without revenue.
    public static decimal? MarginPct(decimal revenue, decimal grossProfit) =>
        revenue == 0m ? null : Round2(grossProfit / revenue * 100m);

    private static decimal Round2(decimal x) => Math.Round(x, 2, MidpointRounding.AwayFromZero);
}
