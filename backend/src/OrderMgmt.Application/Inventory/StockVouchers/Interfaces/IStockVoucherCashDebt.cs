using OrderMgmt.Application.CashDebt.Guards;
using OrderMgmt.Domain.Entities.Inventory;

namespace OrderMgmt.Application.Inventory.StockVouchers.Interfaces;

/// The customer's receivable balance before the save (save step 7), read after the partner keys. Captured only when
/// the reason raises the receivable and a partner is set; null otherwise, and the credit check is skipped.
public sealed record StockVoucherCreditCapture(Guid BranchId, Guid PartnerId, decimal Before);

/// The cash and debt steps of a stock voucher save: the cash/debt keys, the debt entry and automatic voucher sync,
/// and the credit-limit and fund-balance guards.
public interface IStockVoucherCashDebt
{
    /// Save step 3 (P14): after the branch gate and the product keys, the partner and fund keys of the voucher's old
    /// and new state, then the shared key of each stock reason it is saved with.
    Task AcquireLocksAsync(Guid branchId, IEnumerable<Guid?> partnerIds, IEnumerable<Guid?> fundIds,
        IEnumerable<Guid> reasonIds, CancellationToken ct);

    /// Save steps 11–13 (cancel / delete step 7): the voucher's debt entry and its automatic voucher follow the
    /// voucher, or are removed (`remove`).
    Task SyncDebtAsync(StockVoucher voucher, StockReason reason, bool remove, bool confirmRemoveSettlements,
        CancellationToken ct);

    /// The customer's receivable balance before the save (save step 7); null when the credit check does not apply.
    Task<StockVoucherCreditCapture?> CaptureCreditAsync(Guid branchId, StockReason reason, Guid? partnerId,
        CancellationToken ct);

    /// Save step 15 (C12, P10): after the debt entry and the automatic voucher's entry are posted.
    Task CheckCreditAsync(StockVoucherCreditCapture? capture, bool acknowledge, CancellationToken ct);

    /// Save step 8: the funds the automatic voucher leaves or enters, held under their keys since step 3.
    Task<FundCapture> CaptureFundsAsync(IEnumerable<(Guid? FundId, DateTimeOffset From)> funds, CancellationToken ct);

    /// The fund-balance check after the automatic voucher is posted, against the balances captured at step 8.
    Task CheckFundsAsync(FundCapture capture, bool acknowledge, CancellationToken ct);
}
