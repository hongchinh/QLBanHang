using OrderMgmt.Application.Inventory.Ledger;

namespace OrderMgmt.Application.Inventory.Costing;

/// ScopeKey is the branch id (Branch scope) or the warehouse id (Warehouse scope).
public sealed record CostScopeChange(Guid ProductId, Guid BranchId, Guid ScopeKey, DateOnly FromDate);

public interface IInventoryCostingService
{
    // Branch scope → group pairs by (ProductId, BranchId); Warehouse scope → (ProductId, WarehouseId).
    // FromDate = VnTime.ToVnDate(min From) of the group.
    Task<IReadOnlyList<CostScopeChange>> ScopesForAsync(IReadOnlyList<PairChange> pairs, CancellationToken ct = default);

    // Recomputes the periodic average cost of each scope from the first period that is not fully locked.
    // Callers hold the D30 locks.
    Task RecalculateAsync(IReadOnlyCollection<CostScopeChange> changes, CancellationToken ct = default);
}
