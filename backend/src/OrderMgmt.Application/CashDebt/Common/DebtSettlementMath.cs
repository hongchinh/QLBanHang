namespace OrderMgmt.Application.CashDebt.Common;

/// Pure settlement amounts shared by the debt engine and the open-item views.
public static class DebtSettlementMath
{
    /// P20: min(paid, stock entry open excluding Auto, auto entry open excluding Auto), never negative.
    public static decimal AutoAmount(decimal paidAmount, decimal stockEntryOpenExcludingAuto, decimal autoEntryOpenExcludingAuto)
        => Math.Max(0m, Math.Min(paidAmount, Math.Min(stockEntryOpenExcludingAuto, autoEntryOpenExcludingAuto)));

    /// Amount − Σ settled, never below 0 (used for display only; the engine never lets Σ exceed Amount).
    public static decimal Open(decimal amount, decimal settled) => Math.Max(0m, amount - settled);
}
