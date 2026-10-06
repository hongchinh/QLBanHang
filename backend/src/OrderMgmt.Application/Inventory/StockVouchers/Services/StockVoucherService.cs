using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Catalog.Customers.Models;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Inventory.Common;
using OrderMgmt.Application.Inventory.Interfaces;
using OrderMgmt.Application.Inventory.Ledger;
using OrderMgmt.Application.Inventory.Numbering;
using OrderMgmt.Application.Inventory.Posting;
using OrderMgmt.Application.Inventory.StockVouchers.Interfaces;
using OrderMgmt.Application.Inventory.StockVouchers.Models;
using OrderMgmt.Domain.Common;
using OrderMgmt.Domain.Entities.Catalog;
using OrderMgmt.Domain.Entities.Inventory;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Inventory.StockVouchers.Services;

/// Stock-in / stock-out vouchers. Every write follows the section-03 §5 save rules in one transaction.
public class StockVoucherService : IStockVoucherService
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentBranch _currentBranch;
    private readonly IDateTime _clock;
    private readonly ITransactionRunner _transaction;
    private readonly IInventoryPostingService _posting;
    private readonly IDocumentCounter _counter;

    public StockVoucherService(IAppDbContext db, ICurrentUser currentUser, ICurrentBranch currentBranch,
        IDateTime clock, ITransactionRunner transaction, IInventoryPostingService posting, IDocumentCounter counter)
    {
        _db = db;
        _currentUser = currentUser;
        _currentBranch = currentBranch;
        _clock = clock;
        _transaction = transaction;
        _posting = posting;
        _counter = counter;
    }

    public Task<StockVoucherListResult> ListAsync(StockVoucherListRequest request, CancellationToken ct = default) =>
        throw new NotImplementedException();

    public Task<IReadOnlyList<StockVoucherOwnerDto>> ListOwnersAsync(StockDirection type, CancellationToken ct = default) =>
        throw new NotImplementedException();

    public Task<StockVoucherDefaultsDto> GetDefaultsAsync(StockDirection type, DateTimeOffset? voucherAt, CancellationToken ct = default) =>
        throw new NotImplementedException();

    public Task<IReadOnlyList<StockAtResult>> GetStockAtAsync(StockAtRequest request, CancellationToken ct = default) =>
        throw new NotImplementedException();

    public Task<List<CustomerSearchItemDto>> SearchPartnersAsync(StockDirection type, string? keyword, int limit,
        Guid? reasonId, CancellationToken ct = default) =>
        throw new NotImplementedException();

    public async Task<StockVoucherDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var voucher = await _db.StockVouchers.AsNoTracking()
            .Include(v => v.Warehouse)
            .Include(v => v.Partner)
            .Include(v => v.Reason)
            .Include(v => v.PaymentMethod)
            .Include(v => v.Lines).ThenInclude(l => l.Warehouse)
            .FirstOrDefaultAsync(v => v.Id == id, ct)
            ?? throw new NotFoundException(nameof(StockVoucher), id);

        EnsurePermission(StockVoucherPermissions.View(voucher.Type));
        await EnsureWorkingBranchAsync(voucher.BranchId, ct);

        var ownerName = await _db.Users.IgnoreQueryFilters()
            .Where(u => u.Id == voucher.OwnerUserId)
            .Select(u => u.FullName)
            .FirstOrDefaultAsync(ct);
        return ToDto(voucher, ownerName);
    }

    public Task<IReadOnlyList<StockVoucherActivityDto>> ListActivitiesAsync(Guid id, CancellationToken ct = default) =>
        throw new NotImplementedException();

    public async Task<StockVoucherDto> CreateAsync(UpsertStockVoucherRequest request, CancellationToken ct = default)
    {
        EnsurePermission(StockVoucherPermissions.Create(request.Type));
        var branchId = await _currentBranch.GetIdAsync(ct);
        var voucherAt = VnTime.ToUtc(request.VoucherAt);

        var id = await _transaction.RunAsync(async c =>
        {
            var productIds = request.Lines.Select(l => l.ProductId).Distinct().ToList();
            await _posting.AcquireLocksAsync(branchId, productIds, c);
            var settings = await _db.InventorySettings.AsNoTracking().SingleAsync(s => s.Id == 1, c);

            var voucher = new StockVoucher
            {
                Type = request.Type,
                Code = await NextCodeAsync(request.Type, branchId, voucherAt, c),
                BranchId = branchId,
                OwnerUserId = _currentUser.UserId ?? throw new UnauthorizedAccessException(),
            };
            _db.StockVouchers.Add(voucher);
            await ApplyRequestAsync(voucher, request, voucherAt, settings, c);
            await _db.SaveChangesAsync(c);

            await PostLinesAsync(voucher, settings, request.AcknowledgeNegativeStock, c);
            if (voucher.Type == StockDirection.In)
                await RecomputeCostPricesAsync(productIds, c);
            AddActivity(voucher, StockVoucherActivityAction.Created, "Tạo phiếu");
            await _db.SaveChangesAsync(c);
            return voucher.Id;
        }, ct);

        return await GetAsync(id, ct);
    }

    public Task<StockVoucherDto> UpdateAsync(Guid id, UpsertStockVoucherRequest request, CancellationToken ct = default) =>
        throw new NotImplementedException();

    public Task<StockVoucherDto> CancelAsync(Guid id, StockVoucherActionRequest request, CancellationToken ct = default) =>
        throw new NotImplementedException();

    public Task<StockVoucherDto> RestoreAsync(Guid id, StockVoucherActionRequest request, CancellationToken ct = default) =>
        throw new NotImplementedException();

    public Task DeleteAsync(Guid id, StockVoucherActionRequest request, CancellationToken ct = default) =>
        throw new NotImplementedException();

    // ---- Writes -----------------------------------------------------------------------------

    private async Task<string> NextCodeAsync(StockDirection type, Guid branchId, DateTimeOffset voucherAt, CancellationToken ct)
    {
        var docType = DocTypeOf(type);
        var numbering = await _db.DocumentNumberings.AsNoTracking()
            .SingleOrDefaultAsync(n => n.DocType == docType && n.BranchId == branchId, ct)
            ?? throw new DomainException("NUMBERING_NOT_CONFIGURED", "Chi nhánh chưa cấu hình đánh số chứng từ.");
        var date = VnTime.ToVnDate(voucherAt);
        var counter = await _counter.NextAsync(docType, branchId,
            DocumentNumberFormatter.PeriodKey(numbering.ResetPolicy, date), ct);
        return DocumentNumberFormatter.Format(numbering.Pattern, numbering.Prefix, numbering.Length, counter, date);
    }

    /// Header fields, line upsert and totals from StockVoucherCalculator (never from the client).
    private async Task ApplyRequestAsync(StockVoucher voucher, UpsertStockVoucherRequest request,
        DateTimeOffset voucherAt, InventorySettings settings, CancellationToken ct)
    {
        var partner = request.PartnerId.HasValue
            ? await _db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == request.PartnerId.Value, ct)
            : null;

        voucher.VoucherAt = voucherAt;
        voucher.WarehouseId = request.WarehouseId;
        voucher.ReasonId = request.ReasonId;
        voucher.PartnerId = request.PartnerId;
        voucher.PartnerName = request.PartnerName ?? partner?.Name;
        voucher.PartnerAddress = request.PartnerAddress ?? partner?.CompanyAddress;
        voucher.PartnerTaxCode = request.PartnerTaxCode ?? partner?.TaxCode;
        voucher.HandlerName = request.HandlerName;
        voucher.PaymentMethodId = request.PaymentMethodId;
        voucher.Note = request.Note;
        voucher.Freight = request.Freight;
        voucher.OrderDiscount = request.OrderDiscount;

        var requested = request.Lines.OrderBy(l => l.SortOrder).ToList();
        var products = await LoadProductsAsync(requested.Select(l => l.ProductId), ct);
        var computation = StockVoucherCalculator.Compute(
            new StockHeaderInput(request.Type, request.Freight, request.OrderDiscount, settings.NetExcludesVat),
            requested.Select(l => ToCalculatorInput(l, products[l.ProductId])).ToList());

        var existing = voucher.Lines.Where(l => !l.IsDeleted).ToDictionary(l => l.Id);
        var kept = new HashSet<Guid>();
        for (var i = 0; i < requested.Count; i++)
        {
            var line = requested[i];
            if (line.Id is { } lineId && existing.TryGetValue(lineId, out var entity))
            {
                kept.Add(lineId);
            }
            else
            {
                // New child rows go through the DbSet (see plan "Child rows").
                entity = new StockVoucherLine { StockVoucherId = voucher.Id };
                _db.StockVoucherLines.Add(entity);
            }

            var product = products[line.ProductId];
            var result = computation.Lines[i];
            entity.SortOrder = line.SortOrder;
            entity.ProductId = product.Id;
            entity.ProductCode = product.Code;
            entity.ProductName = product.Name;
            entity.WarehouseId = line.WarehouseId ?? request.WarehouseId;
            entity.TrackInventory = product.TrackInventory;
            entity.PricingMode = product.PricingMode;
            entity.UnitName = StockUnit.NameFor(product.PricingMode, product.Unit?.Name);
            entity.PriceIncludesVat = product.PriceIncludesVat;
            entity.SheetCount = line.SheetCount;
            entity.Length = line.Length;
            entity.Width = line.Width;
            entity.Thickness = line.Thickness;
            entity.Quantity = result.Quantity;
            entity.UnitPrice = line.UnitPrice;
            entity.Amount = result.Amount;
            entity.DiscountRate = line.DiscountRate;
            entity.DiscountAmount = result.DiscountAmount;
            entity.DiscountManual = line.DiscountManual;
            entity.OrderDiscountAllocated = result.OrderDiscountAllocated;
            entity.FreightAllocated = result.FreightAllocated;
            entity.VatRate = line.VatRate;
            entity.VatAmount = result.VatAmount;
            entity.NetAmount = result.NetAmount;
            entity.InboundValue = result.InboundValue;
            entity.Note = line.Note;
        }

        foreach (var removed in existing.Values.Where(l => !kept.Contains(l.Id)))
        {
            removed.IsDeleted = true;
            removed.DeletedAt = _clock.UtcNow;
            removed.DeletedBy = _currentUser.UserId;
        }

        voucher.GoodsAmount = computation.Totals.GoodsAmount;
        voucher.LineDiscountTotal = computation.Totals.LineDiscountTotal;
        voucher.VatTotal = computation.Totals.VatTotal;
        voucher.Total = computation.Totals.Total;
        voucher.PaidAmount = request.PaidAmount ?? computation.Totals.Total;
    }

    private async Task<Dictionary<Guid, Product>> LoadProductsAsync(IEnumerable<Guid> productIds, CancellationToken ct)
    {
        var ids = productIds.Distinct().ToList();
        return await _db.Products.AsNoTracking()
            .Include(p => p.Unit)
            .Where(p => ids.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);
    }

    private static StockLineInput ToCalculatorInput(UpsertStockVoucherLineRequest line, Product product) =>
        new(product.TrackInventory, product.PricingMode, product.PriceIncludesVat,
            line.SheetCount, line.Length, line.Width, line.Thickness, line.Quantity,
            line.UnitPrice, line.DiscountRate, line.DiscountAmount, line.DiscountManual, line.VatRate);

    /// Ledger rows of the voucher's tracked lines (an empty list when cancelled or deleted), then costing.
    private async Task PostLinesAsync(StockVoucher voucher, InventorySettings settings, bool acknowledge,
        CancellationToken ct, bool removeAll = false)
    {
        var drafts = removeAll
            ? new List<LedgerEntryDraft>()
            : voucher.Lines
                .Where(l => !l.IsDeleted && l.TrackInventory)
                .Select(l => new LedgerEntryDraft(
                    l.ProductId, l.WarehouseId, voucher.BranchId, voucher.VoucherAt,
                    SourceTypeOf(voucher.Type), voucher.Id, l.Id, voucher.Code, l.SortOrder,
                    voucher.Type == StockDirection.In ? l.Quantity : 0m,
                    voucher.Type == StockDirection.Out ? l.Quantity : 0m,
                    voucher.Type == StockDirection.In
                        ? l.InboundValue + (settings.PurchaseCostIncludesVat ? l.VatAmount : 0m)
                        : 0m))
                .ToList();
        await _posting.PostAsync(SourceTypeOf(voucher.Type), voucher.Id, drafts, acknowledge, ct);
    }

    /// D35: CostPrice follows the latest Active, non-deleted stock-in line (by VoucherAt, then SortOrder).
    private async Task RecomputeCostPricesAsync(IEnumerable<Guid> productIds, CancellationToken ct)
    {
        foreach (var productId in productIds.Distinct())
        {
            var latest = await _db.StockVoucherLines
                .Where(l => l.ProductId == productId
                    && l.StockVoucher!.Type == StockDirection.In
                    && l.StockVoucher.Status == StockVoucherStatus.Active)
                .OrderByDescending(l => l.StockVoucher!.VoucherAt)
                .ThenByDescending(l => l.SortOrder)
                .Select(l => new { l.UnitPrice, l.StockVoucher!.VoucherAt })
                .FirstOrDefaultAsync(ct);
            var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == productId, ct);
            if (product is null)
                continue;

            if (latest is null)
            {
                product.CostPriceUpdatedOn = null;
            }
            else
            {
                product.CostPrice = latest.UnitPrice;
                product.CostPriceUpdatedOn = latest.VoucherAt;
            }
        }
    }

    private void AddActivity(StockVoucher voucher, StockVoucherActivityAction action, string description) =>
        _db.StockVoucherActivities.Add(new StockVoucherActivity
        {
            StockVoucherId = voucher.Id,
            Action = action,
            ActorUserId = _currentUser.UserId,
            OccurredAt = _clock.UtcNow,
            Description = description,
        });

    // ---- Guards and mapping -----------------------------------------------------------------

    private void EnsurePermission(string permission)
    {
        if (!_currentUser.HasPermission(permission))
            throw new ForbiddenException();
    }

    private async Task EnsureWorkingBranchAsync(Guid branchId, CancellationToken ct)
    {
        if (branchId != await _currentBranch.GetIdAsync(ct))
            throw new ForbiddenException("Phiếu thuộc chi nhánh khác.");
    }

    private static DocumentType DocTypeOf(StockDirection type) =>
        type == StockDirection.In ? DocumentType.StockIn : DocumentType.StockOut;

    private static LedgerSourceType SourceTypeOf(StockDirection type) =>
        type == StockDirection.In ? LedgerSourceType.StockIn : LedgerSourceType.StockOut;

    private static StockVoucherDto ToDto(StockVoucher v, string? ownerName) => new()
    {
        Id = v.Id,
        Type = v.Type,
        Code = v.Code,
        VoucherAt = v.VoucherAt,
        BranchId = v.BranchId,
        WarehouseId = v.WarehouseId,
        WarehouseCode = v.Warehouse?.Code,
        WarehouseName = v.Warehouse?.Name,
        PartnerId = v.PartnerId,
        PartnerCode = v.Partner?.Code,
        PartnerName = v.PartnerName,
        PartnerAddress = v.PartnerAddress,
        PartnerTaxCode = v.PartnerTaxCode,
        HandlerName = v.HandlerName,
        ReasonId = v.ReasonId,
        ReasonName = v.Reason?.Name,
        PaymentMethodId = v.PaymentMethodId,
        PaymentMethodName = v.PaymentMethod?.Name,
        Note = v.Note,
        Freight = v.Freight,
        OrderDiscount = v.OrderDiscount,
        GoodsAmount = v.GoodsAmount,
        LineDiscountTotal = v.LineDiscountTotal,
        DiscountTotal = v.LineDiscountTotal + v.OrderDiscount,
        VatTotal = v.VatTotal,
        Total = v.Total,
        PaidAmount = v.PaidAmount,
        Status = v.Status,
        CancelledAt = v.CancelledAt,
        CancelledBy = v.CancelledBy,
        OwnerUserId = v.OwnerUserId,
        OwnerName = ownerName,
        CreatedAt = v.CreatedAt,
        Version = v.Version,
        Lines = v.Lines.OrderBy(l => l.SortOrder).Select(l => new StockVoucherLineDto
        {
            Id = l.Id,
            SortOrder = l.SortOrder,
            ProductId = l.ProductId,
            ProductCode = l.ProductCode,
            ProductName = l.ProductName,
            WarehouseId = l.WarehouseId,
            WarehouseCode = l.Warehouse?.Code,
            TrackInventory = l.TrackInventory,
            PricingMode = l.PricingMode,
            UnitName = l.UnitName,
            PriceIncludesVat = l.PriceIncludesVat,
            SheetCount = l.SheetCount,
            Length = l.Length,
            Width = l.Width,
            Thickness = l.Thickness,
            Quantity = l.Quantity,
            UnitPrice = l.UnitPrice,
            Amount = l.Amount,
            DiscountRate = l.DiscountRate,
            DiscountAmount = l.DiscountAmount,
            DiscountManual = l.DiscountManual,
            OrderDiscountAllocated = l.OrderDiscountAllocated,
            FreightAllocated = l.FreightAllocated,
            VatRate = l.VatRate,
            VatAmount = l.VatAmount,
            NetAmount = l.NetAmount,
            Note = l.Note,
        }).ToList(),
    };
}
