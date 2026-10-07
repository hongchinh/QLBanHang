using OrderMgmt.Domain.Entities.Inventory;

namespace OrderMgmt.Application.Inventory.Costing;

public interface IInventoryRecalcService
{
    // Manual "Tính lại giá vốn" for the working branch; holds only the exclusive branch gate (D30).
    Task<RecalculateCostResult> RecalculateAsync(RecalculateCostRequest request, CancellationToken ct = default);

    // D11/D33: re-derives inbound values and costs of every branch after a costing settings change. Runs inside the
    // caller's transaction, after `after` has been saved; throws ValidationDomainException when a lock date blocks it.
    Task ApplySettingsChangeAsync(InventorySettings before, InventorySettings after, CancellationToken ct = default);
}
