using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Dashboards.Common;
using OrderMgmt.Application.Inventory.StockVouchers.Models;
using OrderMgmt.Domain.Entities.Identity;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.ExecutiveDashboard;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.Dashboard;

/// The counted sales vouchers shared by the dashboards: XBH positive, NTL negative, Active only, by branch and owner.
[Collection(nameof(PostgresCollection))]
public class SalesRevenueSourceTests : ExecutiveDashboardTestBase
{
    private static readonly DateTimeOffset From = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.FromHours(7)).ToUniversalTime();
    private static readonly DateTimeOffset To = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.FromHours(7)).ToUniversalTime();

    public SalesRevenueSourceTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Counts_sales_positive_and_returns_negative()
    {
        var (branch, _) = await SeedSalesAndReturnAsync();

        (await RevenueAsync(branch.BranchId, null)).Should().Be(450m);
    }

    [Fact]
    public async Task Owner_filter_keeps_only_that_owner()
    {
        var (branch, product) = await SeedSalesAndReturnAsync();
        var second = await InsertUserAsync();
        var sale = await PostLineAsync(branch, StockDirection.Out, "XBH", "2026-10-06 09:00", branch.CustomerId,
            product, 2, 100);
        await InDbAsync(db => db.StockVouchers.Where(v => v.Id == sale.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(v => v.OwnerUserId, second)));

        (await RevenueAsync(branch.BranchId, second)).Should().Be(200m);
        (await RevenueAsync(branch.BranchId, null)).Should().Be(650m);
    }

    [Fact]
    public async Task Other_branch_and_cancelled_are_left_out()
    {
        var (branch, product) = await SeedSalesAndReturnAsync();
        var other = await CreateFreshBranchAsync();
        var otherProduct = await CreateInventoryProductAsync("SRC-" + Guid.NewGuid().ToString("N")[..8]);
        await PostLineAsync(other, StockDirection.In, "NMH", "2026-10-02 09:00", other.SupplierId, otherProduct, 5, 100);
        await PostLineAsync(other, StockDirection.Out, "XBH", "2026-10-04 09:00", other.CustomerId, otherProduct, 2, 100);
        var cancelled = await PostLineAsync(branch, StockDirection.Out, "XBH", "2026-10-07 09:00", branch.CustomerId,
            product, 1, 300);
        var cancel = await branch.Client.PostAsJsonAsync($"/api/stock-vouchers/{cancelled.Id}/cancel",
            new StockVoucherActionRequest { Version = cancelled.Version });
        cancel.StatusCode.Should().Be(HttpStatusCode.OK, await cancel.Content.ReadAsStringAsync());

        (await RevenueAsync(branch.BranchId, null)).Should().Be(450m);
    }

    [Fact]
    public async Task LineRevenues_sign_quantity_of_returns()
    {
        var (branch, product) = await SeedSalesAndReturnAsync();

        using var scope = _factory.Services.CreateScope();
        var counted = await scope.ServiceProvider.GetRequiredService<SalesRevenueSource>()
            .BuildAsync([branch.BranchId], null, From, To, CancellationToken.None);
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

        var quantity = await counted.LineRevenues(db.StockVoucherLines)
            .Where(l => l.ProductId == product).SumAsync(l => l.Quantity);

        quantity.Should().Be(3m);
    }

    [Fact]
    public async Task Multi_line_voucher_nets_discount_and_inclusive_vat()
    {
        var branch = await CreateFreshBranchAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var inclusive = await CreateInventoryProductAsync("SRI-" + suffix, track: false, taxRate: 10, priceIncludesVat: true);
        var plain = await CreateInventoryProductAsync("SRP-" + suffix, track: false);
        var request = VoucherRequest(StockDirection.Out, await ReasonIdAsync("XBH"), "2026-10-04 09:00",
            LineRequest(inclusive, 2, 110), LineRequest(plain, 3, 100));
        request.WarehouseId = branch.WarehouseId;
        request.PartnerId = branch.CustomerId;
        request.Lines[0].VatRate = 10m;
        request.Lines[1].DiscountRate = 10m;
        var (status, voucher, error) = await PostVoucherAsync(branch.Client, request);
        status.Should().Be(HttpStatusCode.OK, error?.Message);

        // Line 1: amount 220, VAT 20 is inside the price, so revenue 220 - 20 = 200.
        // Line 2: amount 300, discount 10% = 30, revenue 270. The voucher: 470.
        (await RevenueAsync(branch.BranchId, null)).Should().Be(470m);
        using var scope = _factory.Services.CreateScope();
        var counted = await scope.ServiceProvider.GetRequiredService<SalesRevenueSource>()
            .BuildAsync([branch.BranchId], null, From, To, CancellationToken.None);
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var lines = await counted.LineRevenues(db.StockVoucherLines).Where(l => l.VoucherId == voucher!.Id)
            .OrderBy(l => l.Revenue).ToListAsync();
        lines.Select(l => l.Revenue).Should().Equal(200m, 270m);
    }

    /// NMH 10 × 100 on 10-02, XBH 4 × 150 on 10-04, NTL 1 × 150 on 10-05 in a fresh branch: 600 − 150 = 450.
    private async Task<(FreshBranch Branch, Guid ProductId)> SeedSalesAndReturnAsync()
    {
        var branch = await CreateFreshBranchAsync();
        var product = await CreateInventoryProductAsync("SRC-" + Guid.NewGuid().ToString("N")[..8]);
        await PostLineAsync(branch, StockDirection.In, "NMH", "2026-10-02 09:00", branch.SupplierId, product, 10, 100);
        await PostLineAsync(branch, StockDirection.Out, "XBH", "2026-10-04 09:00", branch.CustomerId, product, 4, 150);
        await PostLineAsync(branch, StockDirection.In, "NTL", "2026-10-05 09:00", branch.CustomerId, product, 1, 150);
        return (branch, product);
    }

    /// The signed revenue of October for the branch; the query runs inside the scope that built it.
    private async Task<decimal> RevenueAsync(Guid branchId, Guid? ownerUserId)
    {
        using var scope = _factory.Services.CreateScope();
        var counted = await scope.ServiceProvider.GetRequiredService<SalesRevenueSource>()
            .BuildAsync([branchId], ownerUserId, From, To, CancellationToken.None);
        return await counted.VoucherRevenues().SumAsync(r => r.Revenue);
    }

    private Task<Guid> InsertUserAsync() => InDbAsync(async db =>
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var user = new User
        {
            Username = "src_" + suffix,
            Email = $"src_{suffix}@test.local",
            FullName = "Second owner",
            PasswordHash = "x",
            Status = UserStatus.Active,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    });
}
