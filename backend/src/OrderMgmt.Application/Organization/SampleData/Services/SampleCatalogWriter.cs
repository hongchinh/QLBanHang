using OrderMgmt.Application.Common;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Inventory.Interfaces;
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

    /// Must run inside the caller's transaction, which holds the product keys until it commits.
    public static async Task<SampleCatalogIds> WriteAsync(IAppDbContext db, IInventoryLock inventoryLock,
        SampleDataPlan plan, CancellationToken ct)
    {
        await LockSampleProductsAsync(db, inventoryLock, plan.Products, ct);
        var groupId = await GroupIdAsync(db, ct);
        var products = await WriteProductsAsync(db, plan.Products, groupId, ct);
        var partners = await WritePartnersAsync(db, plan.Partners, ct);
        await db.SaveChangesAsync(ct);
        return new SampleCatalogIds(products, partners);
    }

    /// The keys of the plan's existing sample products (soft-deleted ones too), taken before anything of the catalog is
    /// read: (1) their settings are then read under the key ProductService.UpdateAsync takes to change them; (2) the
    /// catalog cleanup of the last demo branch holds the same keys while it deletes the catalog, so the two never
    /// interleave. One sorted statement takes all the keys, so this cannot deadlock.
    private static async Task LockSampleProductsAsync(IAppDbContext db, IInventoryLock inventoryLock,
        IReadOnlyCollection<SampleProductSpec> specs, CancellationToken ct)
    {
        var codes = specs.Select(s => s.Code).ToList();
        var ids = await db.Products.IgnoreQueryFilters([QueryFilters.SoftDelete]).Where(p => p.IsSample && codes.Contains(p.Code))
            .Select(p => p.Id).ToListAsync(ct);
        if (ids.Count > 0)
            await inventoryLock.AcquireProductsAsync(ids, ct);
    }

    /// A DM group is the sample group only when it carries the sample name; any other DM group is real data.
    private static async Task<Guid> GroupIdAsync(IAppDbContext db, CancellationToken ct)
    {
        var rows = await db.ProductGroups.IgnoreQueryFilters([QueryFilters.SoftDelete]).Where(g => g.Code == GroupCode).ToListAsync(ct);
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
        var existing = (await db.Products.IgnoreQueryFilters([QueryFilters.SoftDelete]).Where(p => codes.Contains(p.Code)).ToListAsync(ct))
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
                await RestoreSettingsAsync(db, product, spec, ct);
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

    /// The plan's quantities, totals and stock assume the spec's settings: priced per unit, the price without VAT,
    /// stock tracked as the spec says. A reused product may have been edited since. Read under its product key (see
    /// LockSampleProductsAsync), as ProductService.UpdateAsync guards the change: without stock postings its settings
    /// go back to the spec; with them it keeps its settings, as the service would, and the run fails.
    private static async Task RestoreSettingsAsync(IAppDbContext db, Product product, SampleProductSpec spec,
        CancellationToken ct)
    {
        var changed = ChangedSettings(product, spec);
        if (changed.Count == 0)
            return;
        if (await db.InventoryLedger.AnyAsync(e => e.ProductId == product.Id, ct))
            throw new ConflictException(
                $"Hàng mẫu {spec.Code} đã bị đổi {string.Join(", ", changed)}. Hãy xóa các chi nhánh demo rồi tạo lại.");
        product.PricingMode = PricingMode.PerUnit;
        product.TrackInventory = spec.TrackInventory;
        product.PriceIncludesVat = false;
    }

    /// The settings of a reused product that differ from the spec, as the user calls them.
    private static List<string> ChangedSettings(Product product, SampleProductSpec spec)
    {
        var changed = new List<string>();
        if (product.PricingMode != PricingMode.PerUnit)
            changed.Add("cách tính giá");
        if (product.TrackInventory != spec.TrackInventory)
            changed.Add("theo dõi tồn kho");
        if (product.PriceIncludesVat)
            changed.Add("giá gồm VAT");
        return changed;
    }

    private static async Task<Dictionary<string, Guid>> WritePartnersAsync(IAppDbContext db,
        IReadOnlyCollection<SamplePartnerSpec> specs, CancellationToken ct)
    {
        var codes = specs.Select(s => s.Code).ToList();
        var existing = (await db.Customers.IgnoreQueryFilters([QueryFilters.SoftDelete]).Where(c => codes.Contains(c.Code)).ToListAsync(ct))
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
