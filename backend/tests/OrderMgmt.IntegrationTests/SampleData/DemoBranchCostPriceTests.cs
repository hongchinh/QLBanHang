using System.Net;
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

    private async Task DeleteAsync(Guid branchId)
    {
        var response = await _client.DeleteAsync($"/api/branches/{branchId}/demo");
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    private Task<(decimal? CostPrice, DateTimeOffset? UpdatedOn)> CostOfAsync(Guid productId) =>
        InDbAsync(async db =>
        {
            var p = await db.Products.IgnoreQueryFilters().Where(x => x.Id == productId).SingleAsync();
            return (p.CostPrice, p.CostPriceUpdatedOn);
        });

    [Fact]
    public async Task Delete_recomputes_cost_price_of_real_products()
    {
        var product = await CreateInventoryProductAsync("CP-REAL");
        var supplier = await CreatePartnerAsync("SUPM", isCustomer: false, isSupplier: true);
        var main = VoucherRequest(StockDirection.In, await ReasonIdAsync("NMH"), "2026-09-10 09:00",
            LineRequest(product, 1, 40_000m));
        main.PartnerId = supplier;
        var (mainStatus, _, mainError) = await PostVoucherAsync(_client, main);
        mainStatus.Should().Be(HttpStatusCode.OK, mainError?.Message);

        var branch = await CreateDemoBranchAsync("DC01", openingDate: new DateOnly(2026, 9, 1));
        await PostStockLineAsync(branch, StockDirection.In, "NMH", "2026-09-20 09:00", await CreatePartnerAsync("SUP" + product.ToString("N")[..6], isCustomer: false, isSupplier: true), product, 1, 55_000m);
        (await CostOfAsync(product)).CostPrice.Should().Be(55_000m);

        await DeleteAsync(branch.BranchId);

        var (cost, updatedOn) = await CostOfAsync(product);
        cost.Should().Be(40_000m);
        updatedOn.Should().Be(Vn("2026-09-10 09:00"));
    }

    [Fact]
    public async Task Delete_keeps_cost_price_of_a_product_whose_only_stock_in_was_in_the_demo_branch()
    {
        var product = await CreateInventoryProductAsync("CP-ONLY");
        var branch = await CreateDemoBranchAsync("DC02", openingDate: new DateOnly(2026, 9, 1));
        await PostStockLineAsync(branch, StockDirection.In, "NMH", "2026-09-20 09:00", await CreatePartnerAsync("SUP" + product.ToString("N")[..6], isCustomer: false, isSupplier: true), product, 1, 55_000m);

        await DeleteAsync(branch.BranchId);

        // Known limitation (plan S17): the price stays, only its date is cleared.
        var (cost, updatedOn) = await CostOfAsync(product);
        cost.Should().Be(55_000m);
        updatedOn.Should().BeNull();
    }
}
