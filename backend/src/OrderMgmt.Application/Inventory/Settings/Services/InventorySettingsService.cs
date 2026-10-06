using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Inventory.Settings.Interfaces;
using OrderMgmt.Application.Inventory.Settings.Models;
using OrderMgmt.Domain.Entities.Inventory;

namespace OrderMgmt.Application.Inventory.Settings.Services;

public class InventorySettingsService : IInventorySettingsService
{
    private readonly IAppDbContext _db;
    private readonly IDateTime _clock;
    private readonly ICurrentUser _currentUser;

    public InventorySettingsService(IAppDbContext db, IDateTime clock, ICurrentUser currentUser)
    {
        _db = db;
        _clock = clock;
        _currentUser = currentUser;
    }

    public async Task<InventorySettingsDto> GetAsync(CancellationToken ct = default)
        => ToDto(await _db.InventorySettings.AsNoTracking().SingleAsync(s => s.Id == 1, ct));

    public async Task<InventorySettingsDto> UpdateAsync(UpdateInventorySettingsRequest request, CancellationToken ct = default)
    {
        var settings = await _db.InventorySettings.SingleAsync(s => s.Id == 1, ct);

        settings.CostingMethod = request.CostingMethod;
        settings.CostingPeriod = request.CostingPeriod;
        settings.CostingScope = request.CostingScope;
        settings.PurchaseCostIncludesVat = request.PurchaseCostIncludesVat;
        settings.NegativeStockPolicy = request.NegativeStockPolicy;
        settings.NetExcludesVat = request.NetExcludesVat;
        settings.DefaultDateMode = request.DefaultDateMode;
        settings.UpdatedAt = _clock.UtcNow;
        settings.UpdatedBy = _currentUser.UserId;

        await _db.SaveChangesAsync(ct);
        return ToDto(settings);
    }

    private static InventorySettingsDto ToDto(InventorySettings s) => new()
    {
        CostingMethod = s.CostingMethod,
        CostingPeriod = s.CostingPeriod,
        CostingScope = s.CostingScope,
        PurchaseCostIncludesVat = s.PurchaseCostIncludesVat,
        NegativeStockPolicy = s.NegativeStockPolicy,
        NetExcludesVat = s.NetExcludesVat,
        DefaultDateMode = s.DefaultDateMode,
        UpdatedAt = s.UpdatedAt,
    };
}
