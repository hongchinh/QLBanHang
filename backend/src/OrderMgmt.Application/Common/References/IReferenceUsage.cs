using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Common.References;

/// What references a partner, payment method, branch or fund, so each owner can refuse a delete or a change that
/// would orphan the references. Every check skips soft-deleted rows through the query filters.
public interface IReferenceUsage
{
    /// Non-deleted cash vouchers, opening debts and offsets of any status (a cancelled one can be restored), and any
    /// debt entry.
    Task<bool> PartnerHasCashOrDebtDataAsync(Guid partnerId, CancellationToken ct);

    Task<bool> PartnerHasDebtOnSideAsync(Guid partnerId, DebtSide side, CancellationToken ct);

    Task<bool> PartnerHasStockVouchersAsync(Guid partnerId, CancellationToken ct);

    Task<bool> PaymentMethodUsedByStockVouchersAsync(Guid methodId, CancellationToken ct);

    /// Only stock vouchers with a paid amount: they store a fund of the method's kind.
    Task<bool> PaymentMethodUsedByPaidStockVouchersAsync(Guid methodId, CancellationToken ct);

    /// Non-deleted documents of any status: stock vouchers, cash vouchers, fund transfers and debt offsets.
    Task<bool> BranchHasDocumentsAsync(Guid branchId, CancellationToken ct);

    /// Opening stock, opening debts or a fund with an opening amount.
    Task<bool> BranchHasOpeningDataAsync(Guid branchId, CancellationToken ct);

    /// The VN date of the branch's earliest non-deleted document, of any status. Call it only when one exists.
    Task<DateOnly> EarliestDocumentDateAsync(Guid branchId, CancellationToken ct);

    /// The first kind of document that uses the fund, in the order cash vouchers, fund transfers, stock vouchers.
    Task<FundUsage> FundUsageAsync(Guid fundId, CancellationToken ct);
}

public enum FundUsage
{
    None,
    CashVouchers,
    FundTransfers,
    StockVouchers,
}
