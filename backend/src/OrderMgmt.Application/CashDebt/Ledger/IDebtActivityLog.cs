using OrderMgmt.Domain.Entities.CashDebt;

namespace OrderMgmt.Application.CashDebt.Ledger;

public interface IDebtActivityLog
{
    /// Adds (no save) a "SettlementRemoved" activity to the source document of `entry`:
    /// StockIn/StockOut → StockVoucherActivity; Opening → nothing. Receipt/Payment (Phase 05) and Offset (Phase 08) extend it.
    Task SettlementRemovedAsync(DebtEntry entry, DebtEntry other, decimal amount, CancellationToken ct = default);
}
