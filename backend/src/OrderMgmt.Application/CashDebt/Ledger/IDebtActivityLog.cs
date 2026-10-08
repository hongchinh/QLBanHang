using OrderMgmt.Domain.Entities.CashDebt;

namespace OrderMgmt.Application.CashDebt.Ledger;

public interface IDebtActivityLog
{
    /// Adds (no save) a "SettlementRemoved" activity to the source document of `entry`:
    /// StockIn/StockOut → StockVoucherActivity; Receipt/Payment → CashDocumentActivity; Opening → nothing.
    /// Offset (Phase 08) extends it.
    Task SettlementRemovedAsync(DebtEntry entry, DebtEntry other, decimal amount, CancellationToken ct = default);
}
