using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Organization.SampleData.Models;
using OrderMgmt.Domain.Entities.Catalog;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Organization.SampleData.Services;

/// Ids of the plan's sample products and partners, by code.
internal sealed record SampleCatalogIds(IReadOnlyDictionary<string, Guid> Products, IReadOnlyDictionary<string, Guid> Partners);

/// The shared sample catalog (S13): group DM, products and partners written straight through IAppDbContext, because
/// the create requests carry no IsSample. Rows that already exist are reused.
internal static class SampleCatalogWriter
{
    private const string GroupCode = "DM";

    public static async Task<SampleCatalogIds> WriteAsync(IAppDbContext db, SampleDataPlan plan, CancellationToken ct)
    {
        var groupId = await GroupIdAsync(db, ct);
        var products = await WriteProductsAsync(db, plan.Products, groupId, ct);
        var partners = await WritePartnersAsync(db, plan.Partners, ct);
        await db.SaveChangesAsync(ct);
        return new SampleCatalogIds(products, partners);
    }

    private static async Task<Guid> GroupIdAsync(IAppDbContext db, CancellationToken ct)
    {
        var group = await db.ProductGroups.IgnoreQueryFilters()
            .Where(g => g.Code == GroupCode)
            .OrderBy(g => g.IsDeleted)
            .FirstOrDefaultAsync(ct);
        if (group is null)
        {
            group = new ProductGroup { Code = GroupCode, Name = "Dữ liệu mẫu", SortOrder = 98, IsActive = true };
            db.ProductGroups.Add(group);
        }
        return group.Id;
    }

    private static async Task<Dictionary<string, Guid>> WriteProductsAsync(IAppDbContext db,
        IReadOnlyCollection<SampleProductSpec> specs, Guid groupId, CancellationToken ct)
    {
        var codes = specs.Select(s => s.Code).ToList();
        var existing = await ExistingAsync(db.Products.IgnoreQueryFilters().Where(p => codes.Contains(p.Code)), p => p.Code,
            p => p.IsDeleted, ct);
        var unitCodes = specs.Select(s => s.UnitCode).Distinct().ToList();
        var units = await db.Units.Where(u => unitCodes.Contains(u.Code)).ToDictionaryAsync(u => u.Code, u => u.Id, ct);

        var ids = new Dictionary<string, Guid>();
        foreach (var spec in specs)
        {
            if (existing.TryGetValue(spec.Code, out var product))
            {
                ids[spec.Code] = product.Id;
                continue;
            }

            product = new Product
            {
                Code = spec.Code,
                Name = spec.Name,
                IsSample = true,
                ProductGroupId = groupId,
                UnitId = units.TryGetValue(spec.UnitCode, out var unitId)
                    ? unitId
                    : throw new InvalidOperationException($"Không tìm thấy đơn vị tính {spec.UnitCode}."),
                Length = spec.Length,
                Width = spec.Width,
                Thickness = spec.Thickness,
                Density = spec.Density,
                Specification = spec.Specification,
                DefaultPrice = spec.DefaultPrice,
                CostPrice = spec.CostPrice,
                DefaultTaxRate = 10,
                Status = ProductStatus.Active,
                PricingMode = PricingMode.PerUnit,
                TrackInventory = spec.TrackInventory,
            };
            db.Products.Add(product);
            ids[spec.Code] = product.Id;
        }
        return ids;
    }

    private static async Task<Dictionary<string, Guid>> WritePartnersAsync(IAppDbContext db,
        IReadOnlyCollection<SamplePartnerSpec> specs, CancellationToken ct)
    {
        var codes = specs.Select(s => s.Code).ToList();
        var existing = await ExistingAsync(db.Customers.IgnoreQueryFilters().Where(c => codes.Contains(c.Code)), c => c.Code,
            c => c.IsDeleted, ct);

        var ids = new Dictionary<string, Guid>();
        foreach (var spec in specs)
        {
            if (existing.TryGetValue(spec.Code, out var partner))
            {
                ids[spec.Code] = partner.Id;
                continue;
            }

            partner = new Customer
            {
                Code = spec.Code,
                Name = spec.Name,
                IsSample = true,
                Status = CustomerStatus.Active,
                Group = spec.Group,
                IsCustomer = spec.IsCustomer,
                IsSupplier = spec.IsSupplier,
                CreditDays = spec.CreditDays,
                TaxCode = spec.TaxCode,
                CompanyAddress = spec.Address,
                PhoneNumber = spec.PhoneNumber,
                ContactPerson = spec.ContactPerson,
            };
            db.Customers.Add(partner);
            ids[spec.Code] = partner.Id;
        }
        return ids;
    }

    /// One tracked row per code, soft-deleted rows included; a live row wins over soft-deleted ones.
    private static async Task<Dictionary<string, T>> ExistingAsync<T>(IQueryable<T> rows, Func<T, string> code,
        Func<T, bool> isDeleted, CancellationToken ct)
        => (await rows.ToListAsync(ct))
            .GroupBy(code)
            .ToDictionary(g => g.Key, g => g.OrderBy(isDeleted).First());
}
