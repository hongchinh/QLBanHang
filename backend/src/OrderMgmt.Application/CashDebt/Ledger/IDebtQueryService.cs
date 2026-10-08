using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.CashDebt.Ledger;

/// Read-only debt balances and open items computed from the ledger.
public interface IDebtQueryService
{
    /// Σ Increase − Σ Decrease with PostedAt <= at (all when null).
    Task<decimal> BalanceAsync(Guid branchId, Guid partnerId, DebtSide side, DateTimeOffset? at = null,
        CancellationToken ct = default);

    /// Entries with Open > 0. The source's own entries are excluded; with ForSource, settlements against the
    /// source's own entries are added back to Open. Ordered by (DueDate ?? DocDate), DocDate, DocCode.
    Task<IReadOnlyList<OpenItemDto>> OpenItemsAsync(OpenItemsQuery query, CancellationToken ct = default);
}
