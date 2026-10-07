using OrderMgmt.Application.Inventory.Settings.Models;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Inventory.Settings.Interfaces;

public interface IInventorySettingsService
{
    Task<InventorySettingsDto> GetAsync(CancellationToken ct = default);
    Task<InventorySettingsDto> UpdateAsync(UpdateInventorySettingsRequest request, CancellationToken ct = default);

    /// Numbering rules of the working branch.
    Task<IReadOnlyList<DocumentNumberingDto>> ListNumberingAsync(CancellationToken ct = default);
    Task<DocumentNumberingDto> UpdateNumberingAsync(DocumentType docType, UpdateNumberingRequest request, CancellationToken ct = default);
}
