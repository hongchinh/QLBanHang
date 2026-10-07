using Mapster;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Catalog.Products.Interfaces;
using OrderMgmt.Application.Catalog.Products.Models;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Application.Inventory.Interfaces;
using OrderMgmt.Domain.Common;
using OrderMgmt.Domain.Entities.Catalog;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Catalog.Products.Services;

public class ProductService : IProductService
{
    private const string CodePrefix = "HH";
    private const int MaxCreateAttempts = 5;

    private readonly IAppDbContext _db;
    private readonly IDateTime _clock;
    private readonly ICurrentUser _currentUser;

    private readonly ITransactionRunner _transaction;
    private readonly IInventoryLock _inventoryLock;

    public ProductService(IAppDbContext db, IDateTime clock, ICurrentUser currentUser,
        ITransactionRunner transaction, IInventoryLock inventoryLock)
    {
        _db = db;
        _clock = clock;
        _currentUser = currentUser;
        _transaction = transaction;
        _inventoryLock = inventoryLock;
    }

    public async Task<PagedResult<ProductListItemDto>> ListAsync(ProductListRequest request, CancellationToken ct = default)
    {
        var query = _db.Products
            .AsNoTracking()
            .Include(p => p.ProductGroup)
            .Include(p => p.Unit)
            .Where(p => !p.IsDeleted);

        if (request.ProductGroupId.HasValue)
            query = query.Where(p => p.ProductGroupId == request.ProductGroupId.Value);

        if (request.UnitId.HasValue)
            query = query.Where(p => p.UnitId == request.UnitId.Value);

        if (request.Status.HasValue)
            query = query.Where(p => p.Status == request.Status.Value);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var pattern = $"%{EscapeLike(request.Search.Trim())}%";
            query = query.Where(p =>
                EF.Functions.ILike(p.Name, pattern)
                || EF.Functions.ILike(p.Code, pattern)
                || (p.Specification != null && EF.Functions.ILike(p.Specification, pattern)));
        }

        query = (request.SortBy?.ToLowerInvariant(), request.SortDirection?.ToLowerInvariant()) switch
        {
            ("name", "desc") => query.OrderByDescending(p => p.Name),
            ("name", _) => query.OrderBy(p => p.Name),
            ("code", "desc") => query.OrderByDescending(p => p.Code),
            ("code", _) => query.OrderBy(p => p.Code),
            ("price", "desc") => query.OrderByDescending(p => p.DefaultPrice),
            ("price", _) => query.OrderBy(p => p.DefaultPrice),
            _ => query.OrderByDescending(p => p.CreatedAt),
        };

        var totalItems = await query.CountAsync(ct);

        var items = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(p => new ProductListItemDto
            {
                Id = p.Id,
                Code = p.Code,
                Name = p.Name,
                ProductGroupName = p.ProductGroup != null ? p.ProductGroup.Name : null,
                UnitName = p.Unit != null ? p.Unit.Name : null,
                Specification = p.Specification,
                DefaultPrice = p.DefaultPrice,
                CostPrice = p.CostPrice,
                Status = p.Status,
                PricingMode = p.PricingMode,
                TrackInventory = p.TrackInventory,
                DefaultTaxRate = p.DefaultTaxRate,
                Length = p.Length,
                Width = p.Width,
                Thickness = p.Thickness,
                PurchaseDiscountRate = p.PurchaseDiscountRate,
                SalesDiscountRate = p.SalesDiscountRate,
                PriceIncludesVat = p.PriceIncludesVat,
            })
            .ToListAsync(ct);

        return new PagedResult<ProductListItemDto>
        {
            Items = items,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalItems = totalItems,
        };
    }

    public async Task<ProductDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var product = await _db.Products
            .AsNoTracking()
            .Include(p => p.ProductGroup)
            .Include(p => p.Unit)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Product), id);

        var dto = MapToDto(product);
        dto.HasInventoryActivity = await _db.InventoryLedger.AnyAsync(e => e.ProductId == id, ct);
        return dto;
    }

    public async Task<ProductDto> CreateAsync(CreateProductRequest request, CancellationToken ct = default)
    {
        await EnsureReferencesAsync(request.ProductGroupId, request.UnitId, ct);

        var explicitCode = !string.IsNullOrWhiteSpace(request.Code);

        for (var attempt = 1; attempt <= MaxCreateAttempts; attempt++)
        {
            var code = explicitCode ? request.Code!.Trim() : await GenerateCodeAsync(ct);

            if (await _db.Products.AnyAsync(p => p.Code == code, ct))
            {
                if (explicitCode)
                    throw new ConflictException($"Mã hàng '{code}' đã tồn tại.");
                continue;
            }

            var product = new Product
            {
                Code = code,
                Name = request.Name.Trim(),
                ProductGroupId = request.ProductGroupId,
                UnitId = request.UnitId,
                Length = request.Length,
                Width = request.Width,
                Thickness = request.Thickness,
                Density = request.Density,
                Specification = request.Specification?.Trim(),
                DefaultPrice = request.DefaultPrice,
                CostPrice = request.CostPrice,
                DefaultTaxRate = request.DefaultTaxRate,
                Note = request.Note,
                Status = ProductStatus.Active,
                PricingMode = request.PricingMode,
                TrackInventory = request.TrackInventory,
                PurchaseDiscountRate = request.PurchaseDiscountRate,
                SalesDiscountRate = request.SalesDiscountRate,
                PriceIncludesVat = request.PriceIncludesVat,
            };

            _db.Products.Add(product);

            try
            {
                await _db.SaveChangesAsync(ct);
                return await GetAsync(product.Id, ct);
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex) && !explicitCode && attempt < MaxCreateAttempts)
            {
                _db.Entry(product).State = EntityState.Detached;
            }
        }

        throw new ConflictException("Không thể tạo mã hàng tự động sau nhiều lần thử. Vui lòng thử lại.");
    }

    public async Task<ProductDto> UpdateAsync(Guid id, UpdateProductRequest request, CancellationToken ct = default)
    {
        await EnsureReferencesAsync(request.ProductGroupId, request.UnitId, ct);
        await _transaction.RunAsync(c => ApplyUpdateAsync(id, request, c), ct);
        return await GetAsync(id, ct);
    }

    private async Task ApplyUpdateAsync(Guid id, UpdateProductRequest request, CancellationToken ct)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Product), id);

        var stockFieldsChanged = product.PricingMode != request.PricingMode
            || product.UnitId != request.UnitId
            || (request.TrackInventory.HasValue && request.TrackInventory.Value != product.TrackInventory)
            || (request.PriceIncludesVat.HasValue && request.PriceIncludesVat.Value != product.PriceIncludesVat);
        // The product key serializes the check with postings, so a first posting cannot slip in between (review finding).
        if (stockFieldsChanged)
            await _inventoryLock.AcquireProductsAsync(new[] { id }, ct);
        if (stockFieldsChanged && await _db.InventoryLedger.AnyAsync(e => e.ProductId == id, ct))
            throw new ConflictException("Hàng hóa đã phát sinh kho: không được đổi cách tính, ĐVT, theo dõi tồn, giá gồm VAT.");

        product.Name = request.Name.Trim();
        product.ProductGroupId = request.ProductGroupId;
        product.UnitId = request.UnitId;
        product.Length = request.Length;
        product.Width = request.Width;
        product.Thickness = request.Thickness;
        product.Density = request.Density;
        product.Specification = request.Specification?.Trim();
        product.DefaultPrice = request.DefaultPrice;
        product.CostPrice = request.CostPrice;
        product.DefaultTaxRate = request.DefaultTaxRate;
        product.Note = request.Note;
        product.Status = request.Status;
        product.PricingMode = request.PricingMode;
        product.TrackInventory = request.TrackInventory ?? product.TrackInventory;
        product.PurchaseDiscountRate = request.PurchaseDiscountRate ?? product.PurchaseDiscountRate;
        product.SalesDiscountRate = request.SalesDiscountRate ?? product.SalesDiscountRate;
        product.PriceIncludesVat = request.PriceIncludesVat ?? product.PriceIncludesVat;

        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ProductSuggestionDto>> SearchAsync(string? query, int take, CancellationToken ct = default)
    {
        if (take < 1) take = 1;
        if (take > 50) take = 50;

        if (string.IsNullOrWhiteSpace(query))
            return Array.Empty<ProductSuggestionDto>();

        var pattern = $"%{EscapeLike(query.Trim())}%";

        return await _db.Products
            .AsNoTracking()
            .Include(p => p.Unit)
            .Where(p => !p.IsDeleted && p.Status == ProductStatus.Active)
            .Where(p =>
                EF.Functions.ILike(EF.Functions.Unaccent(p.Code), EF.Functions.Unaccent(pattern))
                || EF.Functions.ILike(EF.Functions.Unaccent(p.Name), EF.Functions.Unaccent(pattern))
                || (p.Specification != null
                    && EF.Functions.ILike(EF.Functions.Unaccent(p.Specification), EF.Functions.Unaccent(pattern))))
            .OrderBy(p => p.Code)
            .Take(take)
            .Select(p => new ProductSuggestionDto
            {
                Id = p.Id,
                Code = p.Code,
                Name = p.Name,
                Specification = p.Specification,
                UnitName = p.Unit != null ? p.Unit.Name : null,
                PricingMode = p.PricingMode,
                DefaultPrice = p.DefaultPrice,
                CostPrice = p.CostPrice,
                DefaultTaxRate = p.DefaultTaxRate,
                Length = p.Length,
                Width = p.Width,
                Thickness = p.Thickness,
                TrackInventory = p.TrackInventory,
                PurchaseDiscountRate = p.PurchaseDiscountRate,
                SalesDiscountRate = p.SalesDiscountRate,
                PriceIncludesVat = p.PriceIncludesVat,
            })
            .ToListAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await _transaction.RunAsync(async c =>
        {
            var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, c)
                ?? throw new NotFoundException(nameof(Product), id);

            // As in UpdateAsync: the product key serializes the check with postings and opening-stock saves
            // (review finding).
            await _inventoryLock.AcquireProductsAsync(new[] { id }, c);
            if (await _db.InventoryLedger.AnyAsync(e => e.ProductId == id, c)
                || await _db.StockVoucherLines.AnyAsync(l => l.ProductId == id, c))
                throw new ConflictException("Hàng hóa đã phát sinh kho, không thể xóa.");

            product.IsDeleted = true;
            product.DeletedAt = _clock.UtcNow;
            product.DeletedBy = _currentUser.UserId;

            await _db.SaveChangesAsync(c);
        }, ct);
    }

    private async Task EnsureReferencesAsync(Guid productGroupId, Guid unitId, CancellationToken ct)
    {
        if (!await _db.ProductGroups.AnyAsync(g => g.Id == productGroupId, ct))
            throw new NotFoundException(nameof(ProductGroup), productGroupId);
        if (!await _db.Units.AnyAsync(u => u.Id == unitId, ct))
            throw new NotFoundException(nameof(Unit), unitId);
    }

    private async Task<string> GenerateCodeAsync(CancellationToken ct)
    {
        var date = _clock.Now.ToString("yyMMdd");
        var todayCount = await _db.Products
            .CountAsync(p => p.Code.StartsWith($"{CodePrefix}-{date}"), ct);
        return $"{CodePrefix}-{date}-{todayCount + 1:D4}";
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        var inner = ex.InnerException;
        var sqlState = inner?.GetType().GetProperty("SqlState")?.GetValue(inner) as string;
        return sqlState == "23505";
    }

    private static string EscapeLike(string input) =>
        input.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    private static ProductDto MapToDto(Product p) => new()
    {
        Id = p.Id,
        Code = p.Code,
        Name = p.Name,
        ProductGroupId = p.ProductGroupId,
        ProductGroupCode = p.ProductGroup?.Code,
        ProductGroupName = p.ProductGroup?.Name,
        UnitId = p.UnitId,
        UnitCode = p.Unit?.Code,
        UnitName = p.Unit?.Name,
        Length = p.Length,
        Width = p.Width,
        Thickness = p.Thickness,
        Density = p.Density,
        Specification = p.Specification,
        DefaultPrice = p.DefaultPrice,
        CostPrice = p.CostPrice,
        DefaultTaxRate = p.DefaultTaxRate,
        Note = p.Note,
        Status = p.Status,
        PricingMode = p.PricingMode,
        TrackInventory = p.TrackInventory,
        PurchaseDiscountRate = p.PurchaseDiscountRate,
        SalesDiscountRate = p.SalesDiscountRate,
        PriceIncludesVat = p.PriceIncludesVat,
        CreatedAt = p.CreatedAt,
    };
}
