using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.CashDebt.Common;

public enum PolicyOutcome
{
    Pass,
    Warn,
    Block,
}

/// Applies a <see cref="LimitPolicy"/> (credit limit, negative fund) to a detected violation (P10).
public static class LimitPolicyDecision
{
    /// !violated → Pass; Allow → Pass; Warn → acknowledged ? Pass : Warn; Block → Block.
    public static PolicyOutcome Decide(LimitPolicy policy, bool violated, bool acknowledged)
    {
        if (!violated)
        {
            return PolicyOutcome.Pass;
        }

        return policy switch
        {
            LimitPolicy.Warn => acknowledged ? PolicyOutcome.Pass : PolicyOutcome.Warn,
            LimitPolicy.Block => PolicyOutcome.Block,
            _ => PolicyOutcome.Pass,
        };
    }
}

/// Credit-limit breach test (C12): only a save that raises the debt above the limit violates it.
public static class CreditLimitRule
{
    /// C12: limit set && after > limit && after > before.
    public static bool IsViolation(decimal? limit, decimal before, decimal after)
        => limit is { } value && after > value && after > before;
}
