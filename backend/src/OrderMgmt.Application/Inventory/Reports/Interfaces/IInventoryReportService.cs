using OrderMgmt.Application.Inventory.Reports.Models;

namespace OrderMgmt.Application.Inventory.Reports.Interfaces;

public interface IInventoryReportService
{
    // Quantity and value per (product, warehouse) of the working branch; values follow the costing scope (D32).
    Task<StockOnHandReportDto> GetStockOnHandAsync(StockOnHandReportRequest request, CancellationToken ct = default);

    // Movements of one product (optionally one warehouse) between two VN dates, with opening, running and closing.
    Task<StockCardDto> GetStockCardAsync(StockCardRequest request, CancellationToken ct = default);

    // Opening / in / out / closing per product between two VN dates (S07), quantities and values.
    Task<StockSummaryReportDto> GetStockSummaryAsync(StockSummaryRequest request, CancellationToken ct = default);

    // Product × warehouse pairs of the working branch with a non-zero opening quantity or a ledger row in the period (T20).
    Task<List<StockCardTargetDto>> ListStockCardTargetsAsync(StockCardTargetsRequest request, CancellationToken ct = default);
}
