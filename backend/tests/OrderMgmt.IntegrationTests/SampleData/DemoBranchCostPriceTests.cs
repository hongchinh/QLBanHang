using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.SampleData;

[Collection(nameof(PostgresCollection))]
public class DemoBranchCostPriceTests : DemoBranchFixture
{
    public DemoBranchCostPriceTests(PostgresFixture pg) : base(pg) { }

    private Task<(decimal? CostPrice, DateTimeOffset? UpdatedOn)> CostOfAsync(Guid productId) =>
        InDbAsync(async db =>
        {
            var p = await db.Products.IgnoreQueryFilters().Where(x => x.Id == productId).SingleAsync();
            return (p.CostPrice, p.CostPriceUpdatedOn);
        });

    private async Task PostPurchaseAsync(DemoBranch branch, Guid supplier, Guid product, string at, decimal unitPrice) =>
        await PostStockLineAsync(branch, StockDirection.In, "NMH", at, supplier, product, 1, unitPrice);

    [Fact]
    public async Task Delete_recomputes_cost_price_of_real_products()
    {
        var product = await CreateInventoryProductAsync("CP-REAL");
        var supplier = await CreatePartnerAsync("SUPM", isCustomer: false, isSupplier: true);
        var main = new DemoBranch(MainBranchId, DefaultWarehouseId, await CashFundIdAsync(MainBranchId), _client);
        await PostPurchaseAsync(main, supplier, product, "2026-09-10 09:00", 40_000m);

        var branch = await CreateDemoBranchAsync("DC01", openingDate: new DateOnly(2026, 9, 1));
        await PostPurchaseAsync(branch, supplier, product, "2026-09-20 09:00", 55_000m);
        (await CostOfAsync(product)).CostPrice.Should().Be(55_000m);

        await DeleteDemoBranchAsync(branch.BranchId);

        var (cost, updatedOn) = await CostOfAsync(product);
        cost.Should().Be(40_000m);
        updatedOn.Should().Be(Vn("2026-09-10 09:00"));
    }

    [Fact]
    public async Task Delete_recomputes_cost_price_of_sample_products()
    {
        var product = await CreateInventoryProductAsync("CP-SAMPLE");
        await InDbAsync(db => db.Products.Where(p => p.Id == product)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.IsSample, true)));
        var supplier = await CreatePartnerAsync("SUPS", isCustomer: false, isSupplier: true);
        var main = new DemoBranch(MainBranchId, DefaultWarehouseId, await CashFundIdAsync(MainBranchId), _client);
        await PostPurchaseAsync(main, supplier, product, "2026-09-10 09:00", 40_000m);

        var branch = await CreateDemoBranchAsync("DC03", openingDate: new DateOnly(2026, 9, 1));
        await PostPurchaseAsync(branch, supplier, product, "2026-09-20 09:00", 55_000m);
        (await CostOfAsync(product)).CostPrice.Should().Be(55_000m);

        await DeleteDemoBranchAsync(branch.BranchId);

        (await CostOfAsync(product)).CostPrice.Should().Be(40_000m);
    }

    [Fact]
    public async Task Delete_keeps_cost_price_of_a_product_whose_only_stock_in_was_in_the_demo_branch()
    {
        var product = await CreateInventoryProductAsync("CP-ONLY");
        var supplier = await CreatePartnerAsync("SUP" + product.ToString("N")[..6], isCustomer: false, isSupplier: true);
        var branch = await CreateDemoBranchAsync("DC02", openingDate: new DateOnly(2026, 9, 1));
        await PostPurchaseAsync(branch, supplier, product, "2026-09-20 09:00", 55_000m);

        await DeleteDemoBranchAsync(branch.BranchId);

        // Known limitation (plan S17): the price stays, only its date is cleared.
        var (cost, updatedOn) = await CostOfAsync(product);
        cost.Should().Be(55_000m);
        updatedOn.Should().BeNull();
    }
}
