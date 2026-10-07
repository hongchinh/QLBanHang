namespace OrderMgmt.Application.Inventory.Costing;

public interface IInventoryRecalcService
{
    // Manual "Tính lại giá vốn" for the working branch; holds only the exclusive branch gate (D30).
    Task<RecalculateCostResult> RecalculateAsync(RecalculateCostRequest request, CancellationToken ct = default);
}
