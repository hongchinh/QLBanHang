namespace OrderMgmt.Application.CashDebt.Common;

/// One fund movement. `Order`: 0 = inflow, 1 = outflow, so inflows sort first at the same instant (P17).
public sealed record BalanceMovement(DateTimeOffset At, int Order, string Code, decimal Delta);

public sealed record RunningBalanceResult(decimal Min, DateTimeOffset? FirstNegativeAt, decimal Closing);

/// Fund running balance and the before/after comparison of the negative-fund check (P17, D31).
public static class RunningBalance
{
    /// Sorts by (At, Order, Code). Start = opening + Σ deltas with At < from.
    /// Min = min(start, balance after each movement with At ≥ from).
    /// FirstNegativeAt = from if start < 0, else the At of the first movement ≥ from that leaves the balance < 0.
    public static RunningBalanceResult Evaluate(decimal opening, IEnumerable<BalanceMovement> movements, DateTimeOffset from)
    {
        var ordered = movements
            .OrderBy(m => m.At)
            .ThenBy(m => m.Order)
            .ThenBy(m => m.Code, StringComparer.Ordinal);

        var balance = opening;
        var started = false;
        var min = 0m;
        DateTimeOffset? firstNegativeAt = null;

        foreach (var movement in ordered)
        {
            if (!started && movement.At >= from)
            {
                Start();
            }

            balance += movement.Delta;

            if (started)
            {
                min = Math.Min(min, balance);
                if (firstNegativeAt is null && balance < 0)
                {
                    firstNegativeAt = movement.At;
                }
            }
        }

        if (!started)
        {
            Start();
        }

        return new RunningBalanceResult(min, firstNegativeAt, balance);

        void Start()
        {
            started = true;
            min = balance;
            if (balance < 0)
            {
                firstNegativeAt = from;
            }
        }
    }

    /// The change is worse when the new minimum is negative and either lower than before,
    /// or the balance first goes negative earlier than before (or did not go negative before).
    public static bool IsWorse(RunningBalanceResult before, RunningBalanceResult after) =>
        after.Min < 0
        && (after.Min < before.Min
            || before.FirstNegativeAt is null
            || after.FirstNegativeAt < before.FirstNegativeAt);
}
