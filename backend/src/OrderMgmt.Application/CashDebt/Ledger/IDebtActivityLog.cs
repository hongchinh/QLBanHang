using OrderMgmt.Domain.Entities.CashDebt;

namespace OrderMgmt.Application.CashDebt.Ledger;

public interface IDebtActivityLog
{
    /// Adds (no save) a "SettlementRemoved" activity to the source document of `entry`:
    /// StockIn/StockOut → StockVoucherActivity; Receipt/Payment/Offset → CashDocumentActivity; Opening → nothing.
    Task SettlementRemovedAsync(DebtEntry entry, DebtEntry other, decimal amount, CancellationToken ct = default);
}
