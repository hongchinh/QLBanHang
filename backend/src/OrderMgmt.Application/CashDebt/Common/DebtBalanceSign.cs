using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.CashDebt.Common;

/// Splits a debt balance (Σ Increase − Σ Decrease) into the Dư Nợ / Dư Có report columns (P24, MISA convention).
public static class DebtBalanceSign
{
    /// P24: Receivable +x → (x, 0); Receivable −x → (0, x); Payable +x → (0, x); Payable −x → (x, 0).
    public static (decimal Debit, decimal Credit) Split(DebtSide side, decimal balance)
    {
        var positive = Math.Max(balance, 0m);
        var negative = Math.Max(-balance, 0m);
        return side == DebtSide.Receivable ? (positive, negative) : (negative, positive);
    }

    /// Movement columns of S12 and the debt summary (P24): Receivable (increase, decrease); Payable (decrease, increase).
    public static (decimal Debit, decimal Credit) SplitMovement(DebtSide side, decimal increase, decimal decrease)
        => side == DebtSide.Receivable ? (increase, decrease) : (decrease, increase);
}
