using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Organization.SampleData.Models;
using OrderMgmt.Domain.Common;
using OrderMgmt.Domain.Entities.Catalog;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Organization.SampleData.Services;

/// Ids of the plan's sample products and partners, by code.
internal sealed record SampleCatalogIds(IReadOnlyDictionary<string, Guid> Products, IReadOnlyDictionary<string, Guid> Partners);

/// The shared sample catalog (S13): group DM, products and partners written straight through IAppDbContext, because
/// the create requests carry no IsSample. Sample rows that already exist are reused and made usable again; a code
/// held by real data fails the run.
internal static class SampleCatalogWriter
{
    private const string GroupCode = "DM";
    private const string GroupName = "Dữ liệu mẫu";

    public static async Task<SampleCatalogIds> WriteAsync(IAppDbContext db, SampleDataPlan plan, CancellationToken ct)
    {
        var groupId = await GroupIdAsync(db, ct);
        var products = await WriteProductsAsync(db, plan.Products, groupId, ct);
        var partners = await WritePartnersAsync(db, plan.Partners, ct);
        await db.SaveChangesAsync(ct);
        return new SampleCatalogIds(products, partners);
    }

    /// A DM group is the sample group only when it carries the sample name; any other DM group is real data.
    private static async Task<Guid> GroupIdAsync(IAppDbContext db, CancellationToken ct)
    {
        var rows = await db.ProductGroups.IgnoreQueryFilters().Where(g => g.Code == GroupCode).ToListAsync(ct);
        var group = ReuseSample(rows, g => g.Name == GroupName, $"Mã nhóm {GroupCode} đã được dùng cho dữ liệu thật.");
        if (group is null)
        {
            group = new ProductGroup { Code = GroupCode, Name = GroupName, SortOrder = 98 };
            db.ProductGroups.Add(group);
        }
        group.IsActive = true;
        return group.Id;
    }

    private static async Task<Dictionary<string, Guid>> WriteProductsAsync(IAppDbContext db,
        IReadOnlyCollection<SampleProductSpec> specs, Guid groupId, CancellationToken ct)
    {
        var codes = specs.Select(s => s.Code).ToList();
        var existing = (await db.Products.IgnoreQueryFilters().Where(p => codes.Contains(p.Code)).ToListAsync(ct))
            .ToLookup(p => p.Code);
        var unitCodes = specs.Select(s => s.UnitCode).Distinct().ToList();
        var units = await db.Units.Where(u => unitCodes.Contains(u.Code)).ToDictionaryAsync(u => u.Code, u => u.Id, ct);

        var ids = new Dictionary<string, Guid>();
        foreach (var spec in specs)
        {
            var product = ReuseSample(existing[spec.Code], p => p.IsSample, RealDataMessage(spec.Code));
            if (product is not null)
            {
                // A user may have stopped using it or deleted its group; the plan's documents need it active.
                product.Status = ProductStatus.Active;
                product.ProductGroupId ??= groupId;
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
                    : throw new SampleDataPlanException($"Không tìm thấy đơn vị tính {spec.UnitCode}."),
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
        var existing = (await db.Customers.IgnoreQueryFilters().Where(c => codes.Contains(c.Code)).ToListAsync(ct))
            .ToLookup(c => c.Code);

        var ids = new Dictionary<string, Guid>();
        foreach (var spec in specs)
        {
            var partner = ReuseSample(existing[spec.Code], c => c.IsSample, RealDataMessage(spec.Code));
            if (partner is not null)
            {
                // Active again, and at least the roles the plan's documents need.
                partner.Status = CustomerStatus.Active;
                partner.IsCustomer |= spec.IsCustomer;
                partner.IsSupplier |= spec.IsSupplier;
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

    /// The tracked row a sample code maps to, among every row with that code: the live row decides (sample → reused,
    /// real → ConflictException); without one, the latest soft-deleted sample row is restored. Null (insert a new row)
    /// when there is none or only soft-deleted real rows, which no longer hold the code (filtered unique index).
    private static T? ReuseSample<T>(IEnumerable<T> rows, Func<T, bool> isSample, string conflictMessage) where T : BaseEntity
    {
        var candidates = rows.ToList();
        var live = candidates.FirstOrDefault(r => !r.IsDeleted);
        if (live is not null)
            return isSample(live) ? live : throw new ConflictException(conflictMessage);

        var deleted = candidates.Where(isSample).OrderByDescending(r => r.DeletedAt).FirstOrDefault();
        if (deleted is not null)
        {
            deleted.IsDeleted = false;
            deleted.DeletedAt = null;
            deleted.DeletedBy = null;
        }
        return deleted;
    }

    private static string RealDataMessage(string code) => $"Mã {code} đã được dùng cho dữ liệu thật.";
}
