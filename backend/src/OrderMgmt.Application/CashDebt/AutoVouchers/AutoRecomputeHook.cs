using OrderMgmt.Application.CashDebt.Ledger;
using OrderMgmt.Application.Common.Interfaces;

namespace OrderMgmt.Application.CashDebt.AutoVouchers;

/// Just before the commit, recomputes the Auto settlements of the stock vouchers whose entries joined a settlement
/// that the request removed (recorded by the ledger in SettlementChangeCollector): the freed room goes back to their
/// paid amount (Phase 07 rule, 2026-10-09). Recomputing an entry that needs nothing changes nothing.
public sealed class AutoRecomputeHook : IBeforeCommitHook
{
    private readonly SettlementChangeCollector _collector;
    private readonly IAutoCashVoucherService _auto;

    public AutoRecomputeHook(SettlementChangeCollector collector, IAutoCashVoucherService auto)
    {
        _collector = collector;
        _auto = auto;
    }

    public async Task BeforeCommitAsync(CancellationToken ct)
    {
        var entryIds = _collector.Drain();
        if (entryIds.Count > 0)
            await _auto.RecomputeAutoAsync(entryIds, ct);
    }
}
