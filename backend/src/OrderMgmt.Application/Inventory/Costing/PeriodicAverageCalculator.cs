namespace OrderMgmt.Application.Inventory.Costing;

public sealed record CostMovement(Guid EntryId, decimal QtyIn, decimal QtyOut, decimal InValue); // posting order
public sealed record CostPeriodInput(DateOnly Start, DateOnly End, IReadOnlyList<CostMovement> Movements);
public sealed record CostPeriodResult(
    DateOnly Start, DateOnly End,
    decimal OpeningQty, decimal OpeningValue, decimal InQty, decimal InValue,
    decimal OutQty, decimal OutValue, decimal AvgCost, decimal ClosingQty, decimal ClosingValue);
public sealed record OutCost(Guid EntryId, decimal UnitCost, decimal CostAmount);
public sealed record CostRunResult(IReadOnlyList<CostPeriodResult> Periods, IReadOnlyList<OutCost> OutCosts);

/// Periodic weighted-average cost over consecutive periods (D8, D9, D37).
public static class PeriodicAverageCalculator
{
    public static CostRunResult Run(decimal openingQty, decimal openingValue, decimal fallbackAvgCost,
        IReadOnlyList<CostPeriodInput> periods)
    {
        var periodResults = new List<CostPeriodResult>(periods.Count);
        var outCosts = new List<OutCost>();
        var qty = openingQty;
        var value = openingValue;
        var fallback = fallbackAvgCost;

        foreach (var period in periods)
        {
            var inQty = period.Movements.Sum(m => m.QtyIn);
            var inValue = period.Movements.Sum(m => m.InValue);
            var outQty = period.Movements.Sum(m => m.QtyOut);
            var denom = qty + inQty;
            var numer = value + inValue;
            // A negative numerator is only reachable after negative stock and would give a negative average (D37).
            var avgCost = denom > 0m && numer >= 0m ? R4(numer / denom) : fallback;

            var periodOutCosts = period.Movements
                .Where(m => m.QtyOut > 0m)
                .Select(m => new OutCost(m.EntryId, avgCost, R0(m.QtyOut * avgCost)))
                .ToList();
            var outValue = periodOutCosts.Sum(c => c.CostAmount);
            var closingQty = qty + inQty - outQty;
            var closingValue = value + inValue - outValue;

            // Stock fully issued: the rounding residual goes to the last outbound movement.
            if (closingQty == 0m && periodOutCosts.Count > 0)
            {
                var last = periodOutCosts[^1];
                periodOutCosts[^1] = last with { CostAmount = last.CostAmount + closingValue };
                outValue += closingValue;
                closingValue = 0m;
            }

            periodResults.Add(new CostPeriodResult(period.Start, period.End, qty, value, inQty, inValue,
                outQty, outValue, avgCost, closingQty, closingValue));
            outCosts.AddRange(periodOutCosts);

            qty = closingQty;
            value = closingValue;
            fallback = avgCost;
        }

        return new CostRunResult(periodResults, outCosts);
    }

    private static decimal R0(decimal x) => Math.Round(x, 0, MidpointRounding.AwayFromZero);

    private static decimal R4(decimal x) => Math.Round(x, 4, MidpointRounding.AwayFromZero);
}
