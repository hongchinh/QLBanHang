using OrderMgmt.Application.Inventory.Warehouses.Models;

namespace OrderMgmt.Application.Inventory.Warehouses.Interfaces;

public interface IWarehouseService
{
    Task<IReadOnlyList<WarehouseDto>> ListAsync(WarehouseListRequest request, CancellationToken ct = default);
    Task<WarehouseDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<WarehouseDto> CreateAsync(CreateWarehouseRequest request, CancellationToken ct = default);
    Task<WarehouseDto> UpdateAsync(Guid id, UpdateWarehouseRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
