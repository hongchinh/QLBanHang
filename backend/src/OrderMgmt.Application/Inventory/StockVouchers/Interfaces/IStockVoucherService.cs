using OrderMgmt.Application.Catalog.Customers.Models;
using OrderMgmt.Application.Inventory.StockVouchers.Models;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Inventory.StockVouchers.Interfaces;

public interface IStockVoucherService
{
    Task<StockVoucherListResult> ListAsync(StockVoucherListRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<StockVoucherOwnerDto>> ListOwnersAsync(StockDirection type, CancellationToken ct = default);
    Task<StockVoucherDefaultsDto> GetDefaultsAsync(StockDirection type, DateTimeOffset? voucherAt, CancellationToken ct = default);
    Task<IReadOnlyList<StockAtResult>> GetStockAtAsync(StockAtRequest request, CancellationToken ct = default);
    Task<List<CustomerSearchItemDto>> SearchPartnersAsync(StockDirection type, string? keyword, int limit, Guid? reasonId, CancellationToken ct = default);
    Task<StockVoucherDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<StockVoucherActivityDto>> ListActivitiesAsync(Guid id, CancellationToken ct = default);
    Task<StockVoucherDto> CreateAsync(UpsertStockVoucherRequest request, CancellationToken ct = default);
    Task<StockVoucherDto> UpdateAsync(Guid id, UpsertStockVoucherRequest request, CancellationToken ct = default);
    Task<StockVoucherDto> CancelAsync(Guid id, StockVoucherActionRequest request, CancellationToken ct = default);
    Task<StockVoucherDto> RestoreAsync(Guid id, StockVoucherActionRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, StockVoucherActionRequest request, CancellationToken ct = default);
}
