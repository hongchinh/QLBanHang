using OrderMgmt.Application.Inventory.OpeningStocks.Models;

namespace OrderMgmt.Application.Inventory.OpeningStocks.Interfaces;

public interface IOpeningStockService
{
    Task<OpeningStockGridDto> GetAsync(Guid warehouseId, CancellationToken ct = default);

    // Replaces the warehouse's opening stock and posts it to the ledger (D20); an empty list removes it.
    Task<OpeningStockGridDto> SaveAsync(SaveOpeningStockRequest request, CancellationToken ct = default);

    // Moves every opening stock row of the branch to newDate and re-posts the Opening ledger rows (P8).
    // Caller holds the exclusive branch gate inside a transaction; the branch has no documents, so no shortage can arise.
    Task RepostBranchAsync(Guid branchId, DateOnly newDate, CancellationToken ct = default);
}
