namespace OrderMgmt.Application.CashDebt.Ledger;

/// The entries joined by settlements that a request removed (other than Auto ones), so that AutoRecomputeHook can
/// recompute the Auto settlements they free room for just before the commit. Scoped: one per request.
public sealed class SettlementChangeCollector
{
    private readonly HashSet<Guid> _entryIds = new();

    public void Record(Guid increaseEntryId, Guid decreaseEntryId)
    {
        _entryIds.Add(increaseEntryId);
        _entryIds.Add(decreaseEntryId);
    }

    /// The distinct recorded entry ids; the collector is empty afterwards.
    public IReadOnlyList<Guid> Drain()
    {
        var ids = _entryIds.ToList();
        _entryIds.Clear();
        return ids;
    }
}
