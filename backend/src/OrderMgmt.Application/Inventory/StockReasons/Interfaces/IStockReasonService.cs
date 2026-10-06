using OrderMgmt.Application.Inventory.StockReasons.Models;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Inventory.StockReasons.Interfaces;

public interface IStockReasonService
{
    Task<IReadOnlyList<StockReasonDto>> ListAsync(StockDirection? direction, CancellationToken ct = default);
    Task<StockReasonDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<StockReasonDto> CreateAsync(CreateStockReasonRequest request, CancellationToken ct = default);
    Task<StockReasonDto> UpdateAsync(Guid id, UpdateStockReasonRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
