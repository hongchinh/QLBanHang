using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrderMgmt.Application.Identity.Interfaces;
using OrderMgmt.Application.Inventory.Numbering;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Entities.Catalog;
using OrderMgmt.Domain.Entities.Identity;
using OrderMgmt.Domain.Entities.Payments;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Infrastructure.Persistence.Seed;

public static class DbSeeder
{
    // Stable arbitrary key so concurrent application instances serialize migration + seed.
    private const long MigrationAdvisoryLockKey = 7426091732641_5L;

    public static async Task SeedAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder");
        var seedOptions = scope.ServiceProvider.GetRequiredService<IOptions<SeedOptions>>().Value;

        // Open a single connection for the whole migrate + seed cycle so the
        // session-level advisory lock survives across operations.
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            await db.Database.ExecuteSqlRawAsync(
                "SELECT pg_advisory_lock({0})", new object[] { MigrationAdvisoryLockKey }, ct);
            try
            {
                await db.Database.MigrateAsync(ct);

                // Permission rows and their grants commit together: if the process died between them,
                // the "newly introduced codes" signal would be lost and the grants never made.
                await using (var tx = await db.Database.BeginTransactionAsync(ct))
                {
                    var newCodes = await SeedPermissionsAsync(db, ct);
                    await SeedRolesAsync(db, newCodes, ct);
                    await tx.CommitAsync(ct);
                }
                await SeedAdminUserAsync(db, hasher, seedOptions, logger, ct);
                await SeedReferenceDataAsync(db, ct);
                await SeedInventoryReferenceDataAsync(db, ct);

                await db.SaveChangesAsync(ct);
                logger.LogInformation("Database seeding completed.");
            }
            finally
            {
                await db.Database.ExecuteSqlRawAsync(
                    "SELECT pg_advisory_unlock({0})", new object[] { MigrationAdvisoryLockKey }, CancellationToken.None);
            }
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    /// <returns>The permission codes inserted by this run (codes introduced by a new release).</returns>
    private static async Task<IReadOnlySet<string>> SeedPermissionsAsync(AppDbContext db, CancellationToken ct)
    {
        var existing = await db.Permissions.Select(p => p.Code).ToListAsync(ct);
        var permissionDefs = new (string Code, string Module, string Name)[]
        {
            (Permissions.Users.View, Permissions.SystemModule, "Xem người dùng"),
            (Permissions.Users.Create, Permissions.SystemModule, "Tạo người dùng"),
            (Permissions.Users.Update, Permissions.SystemModule, "Cập nhật người dùng"),
            (Permissions.Users.Delete, Permissions.SystemModule, "Xóa người dùng"),
            (Permissions.Roles.View, Permissions.SystemModule, "Xem vai trò"),
            (Permissions.Roles.Manage, Permissions.SystemModule, "Quản lý vai trò"),

            (Permissions.Customers.View, Permissions.CatalogModule, "Xem khách hàng"),
            (Permissions.Customers.Create, Permissions.CatalogModule, "Tạo khách hàng"),
            (Permissions.Customers.Update, Permissions.CatalogModule, "Cập nhật khách hàng"),
            (Permissions.Customers.Delete, Permissions.CatalogModule, "Xóa khách hàng"),

            (Permissions.Products.View, Permissions.CatalogModule, "Xem hàng hóa"),
            (Permissions.Products.Create, Permissions.CatalogModule, "Tạo hàng hóa"),
            (Permissions.Products.Update, Permissions.CatalogModule, "Cập nhật hàng hóa"),
            (Permissions.Products.Delete, Permissions.CatalogModule, "Xóa hàng hóa"),

            (Permissions.Quotations.View, Permissions.SalesModule, "Xem báo giá"),
            (Permissions.Quotations.Create, Permissions.SalesModule, "Tạo báo giá"),
            (Permissions.Quotations.Update, Permissions.SalesModule, "Cập nhật báo giá"),
            (Permissions.Quotations.Delete, Permissions.SalesModule, "Xóa báo giá"),
            (Permissions.Quotations.Print, Permissions.SalesModule, "In báo giá"),
            (Permissions.Quotations.CancelConfirmed, Permissions.SalesModule, "Huỷ báo giá đã xác nhận"),
            (Permissions.Quotations.ViewCost, Permissions.SalesModule, "Xem giá vốn/lợi nhuận báo giá"),
            (Permissions.Quotations.ViewAll, Permissions.SalesModule, "Xem mọi báo giá (bypass owner)"),
            (Permissions.Quotations.TransferOwn, Permissions.SalesModule, "Chuyển báo giá của mình cho user khác"),
            (Permissions.Quotations.TransferAny, Permissions.SalesModule, "Chuyển báo giá của bất kỳ user nào"),
            (Permissions.Quotations.CloneOrphan, Permissions.SalesModule, "Clone báo giá của user đã nghỉ"),
            (Permissions.Quotations.BypassLock, Permissions.SalesModule, "Bypass khoá trạng thái báo giá"),
            (Permissions.Quotations.AccountingConfirm, Permissions.SalesModule, "Kế toán xác nhận đã nhận tiền"),
            (Permissions.Quotations.CancelAccountingConfirmed, Permissions.SalesModule, "Huỷ báo giá đã kế toán xác nhận"),
            (Permissions.UserSettings.Manage, Permissions.SystemModule, "Cấu hình thiết lập của user khác"),
            (Permissions.System.ManageSettings, Permissions.SystemModule, "Quản trị cấu hình hệ thống"),

            (Permissions.Reports.Revenue, Permissions.ReportModule, "Báo cáo doanh thu"),
            (Permissions.Reports.Profit, Permissions.ReportModule, "Báo cáo lợi nhuận"),
            (Permissions.Reports.Debt, Permissions.ReportModule, "Báo cáo công nợ"),
            (Permissions.Reports.Delivery, Permissions.ReportModule, "Báo cáo giao hàng"),
            (Permissions.Reports.Inventory, Permissions.ReportModule, "Báo cáo tồn kho, thẻ kho"),

            (Permissions.Branches.Manage, Permissions.SystemModule, "Quản lý chi nhánh"),
            (Permissions.Branches.AccessAll, Permissions.SystemModule, "Làm việc ở mọi chi nhánh"),
            (Permissions.PeriodLock.Manage, Permissions.SystemModule, "Khóa sổ"),

            (Permissions.Suppliers.View, Permissions.CatalogModule, "Xem nhà cung cấp"),
            (Permissions.Suppliers.Create, Permissions.CatalogModule, "Tạo nhà cung cấp"),
            (Permissions.Suppliers.Update, Permissions.CatalogModule, "Cập nhật nhà cung cấp"),
            (Permissions.Suppliers.Delete, Permissions.CatalogModule, "Xóa nhà cung cấp"),

            (Permissions.StockIn.View, Permissions.InventoryModule, "Xem phiếu nhập kho"),
            (Permissions.StockIn.Create, Permissions.InventoryModule, "Tạo phiếu nhập kho"),
            (Permissions.StockIn.Edit, Permissions.InventoryModule, "Sửa phiếu nhập kho"),
            (Permissions.StockIn.Delete, Permissions.InventoryModule, "Xóa phiếu nhập kho"),
            (Permissions.StockIn.Cancel, Permissions.InventoryModule, "Hủy / khôi phục phiếu nhập kho"),
            (Permissions.StockIn.EditAll, Permissions.InventoryModule, "Sửa, xóa, hủy phiếu nhập kho của người khác"),
            (Permissions.StockOut.View, Permissions.InventoryModule, "Xem phiếu xuất kho"),
            (Permissions.StockOut.Create, Permissions.InventoryModule, "Tạo phiếu xuất kho"),
            (Permissions.StockOut.Edit, Permissions.InventoryModule, "Sửa phiếu xuất kho"),
            (Permissions.StockOut.Delete, Permissions.InventoryModule, "Xóa phiếu xuất kho"),
            (Permissions.StockOut.Cancel, Permissions.InventoryModule, "Hủy / khôi phục phiếu xuất kho"),
            (Permissions.StockOut.EditAll, Permissions.InventoryModule, "Sửa, xóa, hủy phiếu xuất kho của người khác"),
            (Permissions.Inventory.OpeningStock, Permissions.InventoryModule, "Nhập tồn đầu kỳ"),
            (Permissions.Inventory.ViewCost, Permissions.InventoryModule, "Xem giá vốn, giá trị tồn kho"),
            (Permissions.Inventory.ManageCatalogs, Permissions.InventoryModule, "Quản lý kho, lý do nhập xuất, hình thức thanh toán"),
            (Permissions.Inventory.Settings, Permissions.InventoryModule, "Cấu hình kho, đánh số chứng từ"),
            (Permissions.Inventory.RecalcCost, Permissions.InventoryModule, "Tính lại giá vốn"),
        };

        var inserted = new HashSet<string>();
        foreach (var (code, module, name) in permissionDefs)
        {
            if (existing.Contains(code)) continue;
            db.Permissions.Add(new Permission { Code = code, Module = module, Name = name });
            inserted.Add(code);
        }
        await db.SaveChangesAsync(ct);
        return inserted;
    }

    private static async Task SeedRolesAsync(AppDbContext db, IReadOnlySet<string> newCodes, CancellationToken ct)
    {
        var allPermissions = await db.Permissions.ToListAsync(ct);
        var existingRoles = await db.Roles.Include(r => r.RolePermissions).ToListAsync(ct);

        var roleDefs = new (string Code, string Name, string[] Permissions)[]
        {
            (RoleCodes.Admin, "Quản trị hệ thống", allPermissions.Select(p => p.Code).ToArray()),
            (RoleCodes.Sales, "Nhân viên kinh doanh", new[]
            {
                Permissions.Customers.View, Permissions.Customers.Create, Permissions.Customers.Update,
                Permissions.Products.View,
                Permissions.Quotations.View, Permissions.Quotations.Create, Permissions.Quotations.Update,
                Permissions.Quotations.Print,
                Permissions.Quotations.TransferOwn,
            }),
            (RoleCodes.Accountant, "Kế toán", new[]
            {
                Permissions.Customers.View, Permissions.Products.View,
                Permissions.Quotations.View,
                Permissions.Quotations.ViewAll,
                // AccountingConfirm predates the "grant new codes once" step below, so roles created before
                // it still need it granted manually via the UI.
                Permissions.Quotations.AccountingConfirm,
                Permissions.Reports.Revenue, Permissions.Reports.Debt,
            }),
            (RoleCodes.Warehouse, "Kho / giao hàng", new[]
            {
                Permissions.Customers.View, Permissions.Products.View,
                Permissions.StockIn.View, Permissions.StockIn.Create, Permissions.StockIn.Edit,
                Permissions.StockIn.Delete, Permissions.StockIn.Cancel,
                Permissions.StockOut.View, Permissions.StockOut.Create, Permissions.StockOut.Edit,
                Permissions.StockOut.Delete, Permissions.StockOut.Cancel,
                Permissions.Inventory.OpeningStock, Permissions.Reports.Inventory, Permissions.Suppliers.View,
            }),
            (RoleCodes.Manager, "Quản lý", allPermissions.Select(p => p.Code).ToArray()),
        };

        foreach (var (code, name, permCodes) in roleDefs)
        {
            var role = existingRoles.FirstOrDefault(r => r.Code == code);
            if (role is null)
            {
                role = new Role { Code = code, Name = name, IsSystem = true };
                db.Roles.Add(role);
                AssignPermissions(role, permCodes, allPermissions);
                continue;
            }

            if (code == RoleCodes.Admin)
            {
                // ADMIN: always re-apply full permissions so new permission codes added in later
                // releases are auto-granted. Mutations on ADMIN are server-rejected, so admin
                // edits cannot be silently undone here.
                AssignPermissions(role, permCodes, allPermissions);
                continue;
            }

            // Defensive fallback: if a non-Admin system role lost all its permissions (manual DB
            // edit / failed migration), restore the defaults. In the normal flow admins edit via
            // the UI and the role always retains ≥1 permission, so this branch is dormant.
            if (role.RolePermissions.Count == 0)
            {
                AssignPermissions(role, permCodes, allPermissions);
                continue;
            }

            // Existing non-Admin system role with permissions → keep admin customisations made via the UI,
            // but grant the codes introduced by this release (inserted in this run) that the role's defaults
            // include. This runs once per code — the permission row exists afterwards — so an admin who later
            // removes such a code keeps it removed across restarts. It applies to every future code too.
            AssignPermissions(role, permCodes.Where(newCodes.Contains).ToArray(), allPermissions);
        }
        await db.SaveChangesAsync(ct);
    }

    private static void AssignPermissions(Role role, string[] permCodes, List<Permission> allPermissions)
    {
        foreach (var pcode in permCodes)
        {
            var perm = allPermissions.FirstOrDefault(p => p.Code == pcode);
            if (perm is null) continue;
            if (role.RolePermissions.Any(rp => rp.PermissionId == perm.Id)) continue;
            role.RolePermissions.Add(new RolePermission { Role = role, Permission = perm });
        }
    }

    private static async Task SeedAdminUserAsync(
        AppDbContext db,
        IPasswordHasher hasher,
        SeedOptions seedOptions,
        ILogger logger,
        CancellationToken ct)
    {
        if (await db.Users.AnyAsync(u => u.Username == "admin", ct)) return;

        if (string.IsNullOrWhiteSpace(seedOptions.AdminPassword))
        {
            logger.LogWarning(
                "Skipping admin user seed: Seed:AdminPassword is not configured. " +
                "Provide it via environment variable Seed__AdminPassword to enable seeding.");
            return;
        }

        var adminRole = await db.Roles.FirstAsync(r => r.Code == RoleCodes.Admin, ct);

        var admin = new User
        {
            Username = "admin",
            Email = "admin@qldh.local",
            FullName = "Quản trị hệ thống",
            PasswordHash = hasher.Hash(seedOptions.AdminPassword),
            Status = UserStatus.Active,
            UserRoles = new List<UserRole> { new() { RoleId = adminRole.Id } },
        };
        db.Users.Add(admin);
        await db.SaveChangesAsync(ct);
    }

    /// Inventory reference data (Round 1). Every block only adds what is missing, so it is safe to re-run.
    private static async Task SeedInventoryReferenceDataAsync(AppDbContext db, CancellationToken ct)
    {
        // Default document numbering for every branch that lacks it (D13).
        var branchIds = await db.Branches.Select(b => b.Id).ToListAsync(ct);
        var existing = await db.DocumentNumberings.Select(n => new { n.BranchId, n.DocType }).ToListAsync(ct);
        foreach (var branchId in branchIds)
        foreach (var docType in DocumentNumberingDefaults.AllTypes)
        {
            if (existing.Any(e => e.BranchId == branchId && e.DocType == docType)) continue;
            db.DocumentNumberings.Add(DocumentNumberingDefaults.Create(docType, branchId, DateTimeOffset.UtcNow));
        }

        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedReferenceDataAsync(AppDbContext db, CancellationToken ct)
    {
        if (!await db.ProductGroups.AnyAsync(ct))
        {
            var groups = new[]
            {
                new ProductGroup { Code = "EPS", Name = "Tấm xốp EPS", SortOrder = 1 },
                new ProductGroup { Code = "XPS", Name = "Tấm xốp XPS", SortOrder = 2 },
                new ProductGroup { Code = "PE", Name = "Tấm xốp PE Foam", SortOrder = 3 },
                new ProductGroup { Code = "CSN", Name = "Cao su non", SortOrder = 4 },
                new ProductGroup { Code = "THUNG", Name = "Thùng xốp", SortOrder = 5 },
                new ProductGroup { Code = "DAGEL", Name = "Da gel", SortOrder = 6 },
                new ProductGroup { Code = "BK", Name = "Bông khoáng", SortOrder = 7 },
                new ProductGroup { Code = "BTT", Name = "Bông thủy tinh", SortOrder = 8 },
                new ProductGroup { Code = "VC", Name = "Vận chuyển", SortOrder = 9 },
                new ProductGroup { Code = "KHAC", Name = "Khác", SortOrder = 99 },
            };
            db.ProductGroups.AddRange(groups);
        }

        if (!await db.Units.AnyAsync(ct))
        {
            var units = new[]
            {
                new Unit { Code = "TAM", Name = "Tấm" },
                new Unit { Code = "M2", Name = "m²" },
                new Unit { Code = "M3", Name = "m³" },
                new Unit { Code = "THUNG", Name = "Thùng" },
                new Unit { Code = "TUI", Name = "Túi" },
                new Unit { Code = "KG", Name = "Kg" },
                new Unit { Code = "CHUYEN", Name = "Chuyến" },
                new Unit { Code = "BO", Name = "Bộ" },
                new Unit { Code = "CAI", Name = "Cái" },
            };
            db.Units.AddRange(units);
        }

        if (!await db.Banks.AnyAsync(ct))
        {
            var banks = new[]
            {
                new Bank { Code = "VCB", Name = "Ngân hàng TMCP Ngoại thương Việt Nam", ShortName = "Vietcombank", Bin = "970436" },
                new Bank { Code = "CTG", Name = "Ngân hàng TMCP Công thương Việt Nam", ShortName = "VietinBank", Bin = "970415" },
                new Bank { Code = "BIDV", Name = "Ngân hàng TMCP Đầu tư và Phát triển Việt Nam", ShortName = "BIDV", Bin = "970418" },
                new Bank { Code = "VBA", Name = "Ngân hàng Nông nghiệp và Phát triển Nông thôn Việt Nam", ShortName = "Agribank", Bin = "970405" },
                new Bank { Code = "TCB", Name = "Ngân hàng TMCP Kỹ thương Việt Nam", ShortName = "Techcombank", Bin = "970407" },
                new Bank { Code = "MB", Name = "Ngân hàng TMCP Quân đội", ShortName = "MB Bank", Bin = "970422" },
                new Bank { Code = "ACB", Name = "Ngân hàng TMCP Á Châu", ShortName = "ACB", Bin = "970416" },
                new Bank { Code = "VPB", Name = "Ngân hàng TMCP Việt Nam Thịnh Vượng", ShortName = "VPBank", Bin = "970432" },
                new Bank { Code = "STB", Name = "Ngân hàng TMCP Sài Gòn Thương Tín", ShortName = "Sacombank", Bin = "970403" },
                new Bank { Code = "TPB", Name = "Ngân hàng TMCP Tiên Phong", ShortName = "TPBank", Bin = "970423" },
                new Bank { Code = "HDB", Name = "Ngân hàng TMCP Phát triển TP.HCM", ShortName = "HDBank", Bin = "970437" },
                new Bank { Code = "VIB", Name = "Ngân hàng TMCP Quốc tế Việt Nam", ShortName = "VIB", Bin = "970441" },
                new Bank { Code = "SHB", Name = "Ngân hàng TMCP Sài Gòn - Hà Nội", ShortName = "SHB", Bin = "970443" },
                new Bank { Code = "EIB", Name = "Ngân hàng TMCP Xuất Nhập khẩu Việt Nam", ShortName = "Eximbank", Bin = "970431" },
                new Bank { Code = "MSB", Name = "Ngân hàng TMCP Hàng hải Việt Nam", ShortName = "MSB", Bin = "970426" },
                new Bank { Code = "SEAB", Name = "Ngân hàng TMCP Đông Nam Á", ShortName = "SeABank", Bin = "970440" },
                new Bank { Code = "OCB", Name = "Ngân hàng TMCP Phương Đông", ShortName = "OCB", Bin = "970448" },
                new Bank { Code = "SCB", Name = "Ngân hàng TMCP Sài Gòn", ShortName = "SCB", Bin = "970429" },
                new Bank { Code = "NAB", Name = "Ngân hàng TMCP Nam Á", ShortName = "Nam A Bank", Bin = "970428" },
                new Bank { Code = "BAB", Name = "Ngân hàng TMCP Bắc Á", ShortName = "Bac A Bank", Bin = "970409" },
                new Bank { Code = "PVCB", Name = "Ngân hàng TMCP Đại Chúng Việt Nam", ShortName = "PVcomBank", Bin = "970412" },
                new Bank { Code = "ABB", Name = "Ngân hàng TMCP An Bình", ShortName = "ABBANK", Bin = "970425" },
                new Bank { Code = "VAB", Name = "Ngân hàng TMCP Việt Á", ShortName = "VietABank", Bin = "970427" },
                new Bank { Code = "KLB", Name = "Ngân hàng TMCP Kiên Long", ShortName = "KienLongBank", Bin = "970452" },
                new Bank { Code = "PGB", Name = "Ngân hàng TMCP Xăng dầu Petrolimex", ShortName = "PGBank", Bin = "970430" },
                new Bank { Code = "SGB", Name = "Ngân hàng TMCP Sài Gòn Công Thương", ShortName = "Saigonbank", Bin = "970400" },
                new Bank { Code = "LPB", Name = "Ngân hàng TMCP Bưu điện Liên Việt", ShortName = "LienVietPostBank", Bin = "970449" },
                new Bank { Code = "NCB", Name = "Ngân hàng TMCP Quốc Dân", ShortName = "NCB", Bin = "970419" },
                new Bank { Code = "DAB", Name = "Ngân hàng TMCP Đông Á", ShortName = "DongA Bank", Bin = "970406" },
                new Bank { Code = "VCCB", Name = "Ngân hàng TMCP Bản Việt", ShortName = "VietCapital Bank", Bin = "970454" },
            };
            db.Banks.AddRange(banks);
        }

        await db.SaveChangesAsync(ct);
    }
}
