using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderMgmt.Application.Inventory.Common;
using OrderMgmt.Application.Organization.SampleData.Interfaces;
using OrderMgmt.Application.Organization.SampleData.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Enums;
using OrderMgmt.Infrastructure.Persistence;
using OrderMgmt.IntegrationTests.Fixtures;

namespace OrderMgmt.IntegrationTests.SampleData;

/// Hand-built plans for the runner tests (S19), so Lane B does not depend on the generator.
public static class TinyPlans
{
    public static readonly DateOnly OpeningDate = new(2026, 7, 1);

    /// Catalog, warehouse K1, funds QTM and VCB, opening stock, two opening debts and one credit limit; no steps.
    public static SampleDataPlan Setup(DateOnly openingDate) => Build(openingDate, Array.Empty<SampleStep>());

    public static SampleDataPlan WithSteps(DateOnly openingDate, params SampleStep[] steps) => Build(openingDate, steps);

    private static SampleDataPlan Build(DateOnly openingDate, IReadOnlyList<SampleStep> steps)
    {
        var od01Date = openingDate.AddDays(-20);
        return new SampleDataPlan
        {
            OpeningDate = openingDate,
            LastDocumentDate = steps.Count == 0 ? openingDate : steps.Max(s => VnTime.ToVnDate(s.At)),
            Products =
            {
                new("DM-SP01", "Tấm EPS 1000x2000x50 (mẫu)", "TAM", 60_000m, 40_000m, TrackInventory: true,
                    Length: 2000m, Width: 1000m, Thickness: 50m, Density: 10m, Specification: "1000x2000x50"),
                new("DM-SP30", "Cước vận chuyển (mẫu)", "CHUYEN", 350_000m, 0m, TrackInventory: false,
                    Length: null, Width: null, Thickness: null, Density: null, Specification: null),
            },
            Partners =
            {
                new("DM-KH01", "Đại lý Minh An (mẫu)", CustomerGroup.Agent, IsCustomer: true, IsSupplier: false,
                    CreditDays: 30, TaxCode: "0100000001", Address: "Hà Nội", PhoneNumber: "0900000101",
                    ContactPerson: "Anh An", OwnerIndex: 0),
                new("DM-NCC01", "Công ty Nhựa Bình Minh (mẫu)", CustomerGroup.Company, IsCustomer: false, IsSupplier: true,
                    CreditDays: 15, TaxCode: "0100000002", Address: "Bắc Ninh", PhoneNumber: "0900000102",
                    ContactPerson: "Chị Bình", OwnerIndex: null),
                new("DM-DT01", "Công ty Xây dựng Đông Tiến (mẫu)", CustomerGroup.Company, IsCustomer: true, IsSupplier: true,
                    CreditDays: 30, TaxCode: "0100000003", Address: "Hải Phòng", PhoneNumber: "0900000103",
                    ContactPerson: "Anh Tiến", OwnerIndex: null),
            },
            Warehouses = { new("K1", "Kho chính") },
            Funds =
            {
                new("QTM", "Quỹ tiền mặt", FundKind.Cash, null, null, 10_000_000m),
                new("VCB", "Ngân hàng VCB", FundKind.Bank, "Vietcombank", "0011000000001", 100_000_000m),
            },
            CreditLimits = { new("DM-KH01", 50_000_000m) },
            OpeningStocks = { new("K1", new[] { new SampleOpeningStockLine("DM-SP01", 100m, 5_000_000m) }) },
            OpeningDebts =
            {
                new("OD-01", "DM-KH01", DebtSide.Receivable, OpeningDebtKind.Debt, "HD-DK-01", od01Date, od01Date.AddDays(30),
                    3_000_000m, OwnerIndex: 0),
                new("OD-02", "DM-NCC01", DebtSide.Payable, OpeningDebtKind.Debt, "HDM-DK-01", openingDate.AddDays(-10), null,
                    2_000_000m, OwnerIndex: null),
            },
            Steps = steps.ToList(),
        };
    }

    public static Task<Guid> AdminIdAsync(WebAppFactory factory) =>
        InDbAsync(factory, db => db.Users.Where(u => u.Username == "admin").Select(u => u.Id).SingleAsync());

    /// S5, computed independently of the runner: active non-deleted SALES users by username, take 3.
    public static Task<List<Guid>> SalesOwnersAsync(WebAppFactory factory) =>
        InDbAsync(factory, db => db.Users
            .Where(u => !u.IsDeleted && u.Status == UserStatus.Active && u.UserRoles.Any(r => r.Role.Code == RoleCodes.Sales))
            .OrderBy(u => u.Username)
            .Select(u => u.Id)
            .Take(3)
            .ToListAsync());

    /// Runs `plan` for the branch as requested by admin, through a scope of the factory.
    public static async Task<SampleDataRunResult> RunAsync(WebAppFactory factory, Guid branchId, SampleDataPlan plan)
    {
        var adminId = await AdminIdAsync(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISampleDataRunner>()
            .RunPlanAsync(new SampleDataJob(branchId, adminId), plan);
    }

    private static async Task<T> InDbAsync<T>(WebAppFactory factory, Func<AppDbContext, Task<T>> work)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await work(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }
}
