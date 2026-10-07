using OrderMgmt.Application.Inventory.Reports.Models;

namespace OrderMgmt.Application.Inventory.Reports.Interfaces;

public interface IInventoryReportService
{
    // Quantity and value per (product, warehouse) of the working branch; values follow the costing scope (D32).
    Task<StockOnHandReportDto> GetStockOnHandAsync(StockOnHandReportRequest request, CancellationToken ct = default);

    // Movements of one product (optionally one warehouse) between two VN dates, with opening, running and closing.
    Task<StockCardDto> GetStockCardAsync(StockCardRequest request, CancellationToken ct = default);
}
