using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Inventory.Costing;
using OrderMgmt.Application.Inventory.Interfaces;
using OrderMgmt.Application.Inventory.Numbering;
using OrderMgmt.Application.Inventory.Settings.Interfaces;
using OrderMgmt.Application.Inventory.Settings.Models;
using OrderMgmt.Domain.Common;
using OrderMgmt.Domain.Entities.Inventory;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Inventory.Settings.Services;

public class InventorySettingsService : IInventorySettingsService
{
    private readonly IAppDbContext _db;
    private readonly IDateTime _clock;
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentBranch _currentBranch;
    private readonly ITransactionRunner _transaction;
    private readonly IInventoryRecalcService _recalc;
    private readonly IInventoryLock _lock;

    public InventorySettingsService(IAppDbContext db, IDateTime clock, ICurrentUser currentUser, ICurrentBranch currentBranch,
        ITransactionRunner transaction, IInventoryRecalcService recalc, IInventoryLock inventoryLock)
    {
        _lock = inventoryLock;
        _db = db;
        _clock = clock;
        _currentUser = currentUser;
        _currentBranch = currentBranch;
        _transaction = transaction;
        _recalc = recalc;
    }

    public async Task<InventorySettingsDto> GetAsync(CancellationToken ct = default)
        => ToDto(await _db.InventorySettings.AsNoTracking().SingleAsync(s => s.Id == 1, ct));

    public Task<InventorySettingsDto> UpdateAsync(UpdateInventorySettingsRequest request, CancellationToken ct = default) =>
        _transaction.RunAsync(async c =>
        {
            // Settings writers wait for each other and for in-flight postings before reading the row (D30, review
            // finding), so `before` is never stale. ApplySettingsChangeAsync re-takes the same gates (re-entrant).
            var allBranchIds = await _db.Branches.IgnoreQueryFilters().Select(b => b.Id).ToListAsync(c);
            await _lock.AcquireBranchGateAsync(allBranchIds, exclusive: true, c);
            var settings = await _db.InventorySettings.SingleAsync(s => s.Id == 1, c);
            var before = new InventorySettings
            {
                CostingPeriod = settings.CostingPeriod,
                CostingScope = settings.CostingScope,
                PurchaseCostIncludesVat = settings.PurchaseCostIncludesVat,
            };

            settings.CostingMethod = request.CostingMethod;
            settings.CostingPeriod = request.CostingPeriod;
            settings.CostingScope = request.CostingScope;
            settings.PurchaseCostIncludesVat = request.PurchaseCostIncludesVat;
            settings.NegativeStockPolicy = request.NegativeStockPolicy;
            settings.NetExcludesVat = request.NetExcludesVat;
            settings.DefaultDateMode = request.DefaultDateMode;
            settings.UpdatedAt = _clock.UtcNow;
            settings.UpdatedBy = _currentUser.UserId;

            await _db.SaveChangesAsync(c);
            // D11: recalculation reads the saved settings; a lock-date rejection rolls the save back.
            await _recalc.ApplySettingsChangeAsync(before, settings, c);
            return ToDto(settings);
        }, ct);

    public async Task<IReadOnlyList<DocumentNumberingDto>> ListNumberingAsync(CancellationToken ct = default)
    {
        var branchId = await _currentBranch.GetIdAsync(ct);
        return await _db.DocumentNumberings.AsNoTracking()
            .Where(n => n.BranchId == branchId)
            .OrderBy(n => n.DocType)
            .Select(n => ToDto(n))
            .ToListAsync(ct);
    }

    public async Task<DocumentNumberingDto> UpdateNumberingAsync(
        DocumentType docType, UpdateNumberingRequest request, CancellationToken ct = default)
    {
        if (!Enum.IsDefined(docType))
            throw new ValidationDomainException(
                new Dictionary<string, string[]> { ["docType"] = new[] { "Loại chứng từ không hợp lệ." } }, null);

        var errors = DocumentNumberFormatter.Validate(request.Pattern, request.Length, request.ResetPolicy);
        if (errors.Count > 0)
            throw new ValidationDomainException(
                new Dictionary<string, string[]> { ["pattern"] = errors.ToArray() }, errors[0]);

        var branchId = await _currentBranch.GetIdAsync(ct);
        var numbering = await _db.DocumentNumberings.FirstOrDefaultAsync(n => n.BranchId == branchId && n.DocType == docType, ct);
        if (numbering is null)
        {
            numbering = DocumentNumberingDefaults.Create(docType, branchId, _clock.UtcNow);
            _db.DocumentNumberings.Add(numbering);
        }

        numbering.Prefix = request.Prefix.Trim();
        numbering.Length = request.Length;
        numbering.ResetPolicy = request.ResetPolicy;
        numbering.Pattern = request.Pattern.Trim();
        numbering.UpdatedAt = _clock.UtcNow;
        numbering.UpdatedBy = _currentUser.UserId;

        await _db.SaveChangesAsync(ct);
        return ToDto(numbering);
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

    private static DocumentNumberingDto ToDto(DocumentNumbering n) => new()
    {
        DocType = n.DocType,
        Prefix = n.Prefix,
        Length = n.Length,
        ResetPolicy = n.ResetPolicy,
        Pattern = n.Pattern,
    };
}
