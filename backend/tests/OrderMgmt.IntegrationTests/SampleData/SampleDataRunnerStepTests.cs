using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Inventory.Common;
using OrderMgmt.Application.Organization.SampleData.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Entities.Inventory;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.CashDebt;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.SampleData;

[Collection(nameof(PostgresCollection))]
public class SampleDataRunnerStepTests : CashDebtTestBase
{
    public SampleDataRunnerStepTests(PostgresFixture pg) : base(pg) { }

    /// 09:00 + `minutes` (VN time) on the `day`-th day after the opening date.
    private static DateTimeOffset At(int day, int minutes = 0) =>
        VnTime.StartOfDay(TinyPlans.OpeningDate.AddDays(day)).AddHours(9).AddMinutes(minutes);

    /// A stock voucher step whose note is its key, so the test finds the voucher by its key.
    private static SampleStockVoucherStep Stock(string key, DateTimeOffset at, int? ownerIndex, StockDirection type,
        string reasonCode, string? partnerCode, string? paymentMethodCode, string? fundCode, params SampleStockLine[] lines) =>
        new(key, at, ownerIndex, type, reasonCode, "K1", partnerCode, paymentMethodCode, fundCode, key, lines);

    /// The branch's stock vouchers by note (= the step key).
    private Task<Dictionary<string, StockVoucher>> VouchersByKeyAsync(Guid branchId) =>
        InDbAsync(db => db.StockVouchers.AsNoTracking().Where(v => v.BranchId == branchId).ToDictionaryAsync(v => v.Note!));

    [Fact]
    public async Task Stock_steps_post_vouchers_debt_and_automatic_receipts()
    {
        var branchId = await CreateBranchAsync("RT01");
        var adminId = await TinyPlans.AdminIdAsync(_factory);
        await CreateTestUserAsync("sd_sales_a", "Pass@123", RoleCodes.Sales);
        await CreateTestUserAsync("sd_sales_b", "Pass@123", RoleCodes.Sales);
        // The clone may hold other SALES users, so the owners come from the S5 query.
        var owners = await TinyPlans.SalesOwnersAsync(_factory);
        owners.Should().HaveCountGreaterThanOrEqualTo(2);

        var plan = TinyPlans.WithSteps(TinyPlans.OpeningDate,
            Stock("NMH-0001", At(1), null, StockDirection.In, "NMH", "DM-NCC01", null, null,
                new SampleStockLine("DM-SP01", 50m, 40_000m, 0m, 10m)),
            Stock("XBH-0001", At(2), 0, StockDirection.Out, "XBH", "DM-KH01", "TM", "QTM",
                new SampleStockLine("DM-SP01", 10m, 60_000m, 0m, 10m),
                new SampleStockLine("DM-SP30", 1m, 350_000m, 0m, 0m)),
            Stock("XBH-0002", At(3), 1, StockDirection.Out, "XBH", "DM-KH01", null, null,
                new SampleStockLine("DM-SP01", 20m, 60_000m, 0m, 0m)),
            Stock("XKH-0001", At(4), null, StockDirection.Out, "XKH", null, null, null,
                new SampleStockLine("DM-SP01", 1m, 0m, 0m, 0m)));

        var result = await TinyPlans.RunAsync(_factory, branchId, plan);

        result.Succeeded.Should().BeTrue(result.Error);
        result.StepsExecuted.Should().Be(4);
        result.StepsSkipped.Should().Be(0);

        var vouchers = await VouchersByKeyAsync(branchId);
        vouchers.Keys.Should().BeEquivalentTo("NMH-0001", "XBH-0001", "XBH-0002", "XKH-0001");
        vouchers["NMH-0001"].Code.Should().StartWith("PN");
        vouchers["NMH-0001"].Type.Should().Be(StockDirection.In);
        foreach (var key in new[] { "XBH-0001", "XBH-0002", "XKH-0001" })
        {
            vouchers[key].Code.Should().StartWith("PX", key);
            vouchers[key].Type.Should().Be(StockDirection.Out, key);
        }

        // XBH-0001 is paid in cash: its automatic receipt goes into QTM.
        var qtmId = await FundIdAsync("QTM", branchId);
        var xbh1 = vouchers["XBH-0001"];
        xbh1.Total.Should().Be(1_010_000m);
        var receipt = await InDbAsync(db => db.CashVouchers.AsNoTracking().SingleAsync(c => c.SourceStockVoucherId == xbh1.Id));
        receipt.Type.Should().Be(CashVoucherType.Receipt);
        receipt.FundId.Should().Be(qtmId);
        receipt.Total.Should().Be(1_010_000m);

        // Credit documents post their debt.
        var xbh2 = vouchers["XBH-0002"];
        var receivable = (await EntriesOfAsync(DebtSourceType.StockOut, xbh2.Id)).Should().ContainSingle().Subject;
        receivable.Side.Should().Be(DebtSide.Receivable);
        receivable.Direction.Should().Be(DebtDirection.Increase);
        receivable.Amount.Should().Be(1_200_000m);
        var nmh = vouchers["NMH-0001"];
        var payable = (await EntriesOfAsync(DebtSourceType.StockIn, nmh.Id)).Should().ContainSingle().Subject;
        payable.Side.Should().Be(DebtSide.Payable);
        payable.Direction.Should().Be(DebtDirection.Increase);
        payable.Amount.Should().Be(2_200_000m);
        (await EntriesOfAsync(DebtSourceType.StockOut, vouchers["XKH-0001"].Id)).Should().BeEmpty();

        // Each step acts as its owner; OwnerIndex null is the requesting admin.
        (xbh1.OwnerUserId, xbh1.CreatedBy).Should().Be((owners[0], owners[0]));
        (xbh2.OwnerUserId, xbh2.CreatedBy).Should().Be((owners[1], owners[1]));
        (nmh.OwnerUserId, nmh.CreatedBy).Should().Be((adminId, adminId));
        vouchers["XKH-0001"].OwnerUserId.Should().Be(adminId);
        receipt.OwnerUserId.Should().Be(owners[0]);

        var warehouseId = await InDbAsync(db => db.Warehouses.Where(w => w.Code == "RT01-K1").Select(w => w.Id).SingleAsync());
        var productId = await InDbAsync(db => db.Products.Where(p => p.Code == "DM-SP01").Select(p => p.Id).SingleAsync());
        var balance = await InDbAsync(db => db.StockBalances.AsNoTracking()
            .SingleAsync(b => b.WarehouseId == warehouseId && b.ProductId == productId));
        balance.Quantity.Should().Be(100m + 50m - 10m - 20m - 1m);

        await AssertInvariantsAsync();
        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Owners_are_active_sales_users_by_username()
    {
        var branchId = await CreateBranchAsync("RT02");
        foreach (var username in new[] { "sd_o3", "sd_o1", "sd_o2", "sd_o4" })
            await CreateTestUserAsync(username, "Pass@123", RoleCodes.Sales);
        await InDbAsync(db => db.Users.Where(u => u.Username == "sd_o2")
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.Status, UserStatus.Disabled)));
        var ids = await InDbAsync(db => db.Users.Where(u => u.Username.StartsWith("sd_o"))
            .ToDictionaryAsync(u => u.Username, u => u.Id));

        var expected = await TinyPlans.SalesOwnersAsync(_factory);
        expected.Should().HaveCount(3);
        expected.Should().NotContain(ids["sd_o2"]);
        var otherSales = await InDbAsync(db => db.Users.AnyAsync(u => !u.IsDeleted && u.Status == UserStatus.Active
            && !u.Username.StartsWith("sd_o") && u.UserRoles.Any(r => r.Role.Code == RoleCodes.Sales)));
        if (!otherSales)
            expected.Should().Equal(ids["sd_o1"], ids["sd_o3"], ids["sd_o4"]);

        var steps = Enumerable.Range(0, 4)
            .Select(i => (SampleStep)Stock($"XBH-000{i + 1}", At(1, i), i, StockDirection.Out, "XBH", "DM-KH01", null, null,
                new SampleStockLine("DM-SP01", 1m, 60_000m, 0m, 0m)))
            .ToArray();

        var result = await TinyPlans.RunAsync(_factory, branchId, TinyPlans.WithSteps(TinyPlans.OpeningDate, steps));

        result.Succeeded.Should().BeTrue(result.Error);
        result.StepsExecuted.Should().Be(4);
        var vouchers = await VouchersByKeyAsync(branchId);
        new[] { "XBH-0001", "XBH-0002", "XBH-0003", "XBH-0004" }.Select(k => vouchers[k].OwnerUserId)
            .Should().Equal(expected[0], expected[1], expected[2], expected[0]);
        vouchers.Values.Should().NotContain(v => v.OwnerUserId == ids["sd_o2"]);

        await AssertInvariantsAsync();
        await AssertCashDebtInvariantsAsync();
    }
}
