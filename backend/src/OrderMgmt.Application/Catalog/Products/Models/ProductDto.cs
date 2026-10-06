using OrderMgmt.Application.Common.Models;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Catalog.Products.Models;

public class ProductDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;

    public Guid? ProductGroupId { get; set; }
    public string? ProductGroupCode { get; set; }
    public string? ProductGroupName { get; set; }

    public Guid? UnitId { get; set; }
    public string? UnitCode { get; set; }
    public string? UnitName { get; set; }

    public decimal? Length { get; set; }
    public decimal? Width { get; set; }
    public decimal? Thickness { get; set; }
    public decimal? Density { get; set; }
    public string? Specification { get; set; }

    public decimal? DefaultPrice { get; set; }
    public decimal? CostPrice { get; set; }
    public decimal? DefaultTaxRate { get; set; }

    public string? Note { get; set; }
    public ProductStatus Status { get; set; }
    public PricingMode PricingMode { get; set; }
    public bool TrackInventory { get; set; }
    public decimal PurchaseDiscountRate { get; set; }
    public decimal SalesDiscountRate { get; set; }
    public bool PriceIncludesVat { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class ProductListItemDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? ProductGroupName { get; set; }
    public string? UnitName { get; set; }
    public string? Specification { get; set; }
    public decimal? DefaultPrice { get; set; }
    public decimal? CostPrice { get; set; }
    public ProductStatus Status { get; set; }
    public PricingMode PricingMode { get; set; }
    public bool TrackInventory { get; set; }
    public decimal? DefaultTaxRate { get; set; }
    public decimal? Length { get; set; }
    public decimal? Width { get; set; }
    public decimal? Thickness { get; set; }
    public decimal PurchaseDiscountRate { get; set; }
    public decimal SalesDiscountRate { get; set; }
    public bool PriceIncludesVat { get; set; }
}

public class CreateProductRequest
{
    public string? Code { get; set; }
    public string Name { get; set; } = default!;
    public Guid ProductGroupId { get; set; }
    public Guid UnitId { get; set; }
    public decimal? Length { get; set; }
    public decimal? Width { get; set; }
    public decimal? Thickness { get; set; }
    public decimal? Density { get; set; }
    public string? Specification { get; set; }
    public decimal? DefaultPrice { get; set; }
    public decimal? CostPrice { get; set; }
    public decimal? DefaultTaxRate { get; set; }
    public string? Note { get; set; }
    public PricingMode PricingMode { get; set; } = PricingMode.PerUnit;
    public bool TrackInventory { get; set; } = true;
    public decimal PurchaseDiscountRate { get; set; }
    public decimal SalesDiscountRate { get; set; }
    public bool PriceIncludesVat { get; set; }
}

public class UpdateProductRequest
{
    public string Name { get; set; } = default!;
    public Guid ProductGroupId { get; set; }
    public Guid UnitId { get; set; }
    public decimal? Length { get; set; }
    public decimal? Width { get; set; }
    public decimal? Thickness { get; set; }
    public decimal? Density { get; set; }
    public string? Specification { get; set; }
    public decimal? DefaultPrice { get; set; }
    public decimal? CostPrice { get; set; }
    public decimal? DefaultTaxRate { get; set; }
    public string? Note { get; set; }
    public ProductStatus Status { get; set; }
    public PricingMode PricingMode { get; set; } = PricingMode.PerUnit;
    /// Null keeps the current value (older clients do not send the inventory fields).
    public bool? TrackInventory { get; set; }
    public decimal? PurchaseDiscountRate { get; set; }
    public decimal? SalesDiscountRate { get; set; }
    public bool? PriceIncludesVat { get; set; }
}

public class ProductListRequest : PageRequest
{
    public Guid? ProductGroupId { get; set; }
    public Guid? UnitId { get; set; }
    public ProductStatus? Status { get; set; }
}

public class ProductSuggestionDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? Specification { get; set; }
    public string? UnitName { get; set; }
    public PricingMode PricingMode { get; set; }
    public decimal? DefaultPrice { get; set; }
    public decimal? CostPrice { get; set; }
    public decimal? DefaultTaxRate { get; set; }
    public decimal? Length { get; set; }
    public decimal? Width { get; set; }
    public decimal? Thickness { get; set; }
    public bool TrackInventory { get; set; }
    public decimal PurchaseDiscountRate { get; set; }
    public decimal SalesDiscountRate { get; set; }
    public bool PriceIncludesVat { get; set; }
}
