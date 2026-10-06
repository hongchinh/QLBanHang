using OrderMgmt.Application.Inventory.Settings.Models;

namespace OrderMgmt.Application.Inventory.Settings.Interfaces;

public interface IInventorySettingsService
{
    Task<InventorySettingsDto> GetAsync(CancellationToken ct = default);
    Task<InventorySettingsDto> UpdateAsync(UpdateInventorySettingsRequest request, CancellationToken ct = default);
}
