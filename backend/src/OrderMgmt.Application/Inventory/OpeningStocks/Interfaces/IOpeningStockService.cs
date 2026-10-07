using OrderMgmt.Application.Inventory.OpeningStocks.Models;

namespace OrderMgmt.Application.Inventory.OpeningStocks.Interfaces;

public interface IOpeningStockService
{
    Task<OpeningStockGridDto> GetAsync(Guid warehouseId, CancellationToken ct = default);

    // Replaces the warehouse's opening stock and posts it to the ledger (D20); an empty list removes it.
    Task<OpeningStockGridDto> SaveAsync(SaveOpeningStockRequest request, CancellationToken ct = default);
}
