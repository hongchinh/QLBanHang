using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Common;
using OrderMgmt.Application.Inventory.OpeningStocks.Models;
using OrderMgmt.Application.Inventory.Reports.Models;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.Inventory;

[Collection(nameof(PostgresCollection))]
public class StockSummaryReportTests : InventoryTestBase
{
    public StockSummaryReportTests(PostgresFixture pg) : base(pg) { }

    private const string Window = "from=2026-10-10&to=2026-10-31";

    [Fact]
    public async Task Summary_per_product()
    {
        var (p, _, cost) = await SeedAsync();

        var report = await GetSummaryAsync(_client, Window);
        report.Scope.Should().Be(ReportScope.Branch);
        report.CanViewCost.Should().BeTrue();
        report.ValuesAtScopeOnly.Should().BeFalse();
        var row = report.Rows.Should().ContainSingle().Subject;
        row.Should().BeEquivalentTo(new
        {
            ProductId = p,
            ProductCode = "SM01",
            ProductName = "Hàng SM01",
            OpeningQty = 20m,
            OpeningValue = 2_200_000m,
            InQty = 10m,
            InValue = 1_000_000m,
            OutQty = 5m,
            OutValue = cost,
            ClosingQty = 25m,
            ClosingValue = 3_200_000m - cost,
        });
        var onHand = await ReadDataAsync<StockOnHandReportDto>(await _client.GetAsync("/api/reports/stock-on-hand"));
        var onHandRow = onHand.Rows.First(r => r.ProductId == p);
        row.ProductGroupName.Should().Be(onHandRow.ProductGroupName);
        row.UnitName.Should().Be(onHandRow.UnitName);
        report.TotalOpeningValue.Should().Be(row.OpeningValue);
        report.TotalInValue.Should().Be(row.InValue);
        report.TotalOutValue.Should().Be(row.OutValue);
        report.TotalClosingValue.Should().Be(row.ClosingValue);
    }

    [Fact]
    public async Task Warehouse_filter()
    {
        var (_, second, cost) = await SeedAsync();

        var kho01 = (await GetSummaryAsync(_client, $"{Window}&warehouseId={DefaultWarehouseId}")).Rows.Should().ContainSingle().Subject;
        kho01.Should().BeEquivalentTo(new
        {
            OpeningQty = 20m, OpeningValue = 2_200_000m, InQty = 4m, InValue = 400_000m, OutQty = 5m, OutValue = cost,
            ClosingQty = 19m, ClosingValue = 2_600_000m - cost,
        });

        var kho02 = (await GetSummaryAsync(_client, $"{Window}&warehouseId={second}")).Rows.Should().ContainSingle().Subject;
        kho02.Should().BeEquivalentTo(new
        {
            OpeningQty = 0m, OpeningValue = 0m, InQty = 6m, InValue = 600_000m, OutQty = 0m, OutValue = 0m,
            ClosingQty = 6m, ClosingValue = 600_000m,
        });
    }

    [Fact]
    public async Task Invariants()
    {
        var (p, second, _) = await SeedAsync();
        var onHand = await ReadDataAsync<StockOnHandReportDto>(
            await _client.GetAsync("/api/reports/stock-on-hand?at=2026-10-31T23:59:59%2B07:00"));

        foreach (var window in new[] { Window, "from=2026-10-01&to=2026-10-31" })
        {
            foreach (var warehouse in new Guid?[] { null, DefaultWarehouseId, second })
            {
                var query = warehouse is null ? window : $"{window}&warehouseId={warehouse}";
                var row = (await GetSummaryAsync(_client, query)).Rows.Should().ContainSingle().Subject;
                (row.OpeningQty + row.InQty - row.OutQty).Should().Be(row.ClosingQty);
                (row.OpeningValue + row.InValue - row.OutValue).Should().Be(row.ClosingValue);

                if (warehouse is null)
                {
                    var held = onHand.Rows.Where(r => r.ProductId == p).ToList();
                    row.ClosingQty.Should().Be(held.Sum(r => r.Quantity));
                    row.ClosingValue.Should().Be(held.Sum(r => r.Value));
                }
                else
                {
                    var card = await ReadDataAsync<StockCardDto>(await _client.GetAsync(
                        $"/api/reports/stock-card?productId={p}&warehouseId={warehouse}&{window}"));
                    row.Should().BeEquivalentTo(new
                    {
                        card.OpeningQty, card.InQty, card.OutQty, card.ClosingQty,
                        card.OpeningValue, card.InValue, card.OutValue, card.ClosingValue,
                    });
                }
            }
        }
    }

    [Fact]
    public async Task Rows_with_only_zeros_are_dropped()
    {
        await SeedAsync();
        var p2 = await CreateInventoryProductAsync("SM02");
        await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-02 08:00", LineRequest(p2, 3, 100_000));
        await CreateVoucherAsync(_client, StockDirection.Out, "XKH", "2026-10-03 08:00", LineRequest(p2, 3, 0));

        (await GetSummaryAsync(_client, Window)).Rows.Select(r => r.ProductCode).Should().Equal("SM01");
        (await GetSummaryAsync(_client, "from=2026-10-01&to=2026-10-31")).Rows.Select(r => r.ProductCode)
            .Should().Equal("SM01", "SM02");
    }

    [Fact]
    public async Task Only_tracked_products()
    {
        var (p, _, _) = await SeedAsync();
        await InDbAsync(db => db.Products.Where(x => x.Id == p)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.TrackInventory, false)));

        (await GetSummaryAsync(_client, Window)).Rows.Should().BeEmpty();
    }

    /// Product SM01 with the data of the Phase 04 test table; returns (product, KHO02, cost of the stock-out).
    private async Task<(Guid Product, Guid Second, decimal Cost)> SeedAsync()
    {
        await UpdateInventorySettingsAsync(s => s.CostingScope = CostingScope.Warehouse);
        var p = await CreateInventoryProductAsync("SM01");
        var second = await CreateWarehouseAsync(MainBranchId, "KHO02");
        await SaveOpeningAsync(p, new DateOnly(2026, 10, 1), 10, 1_000_000);
        await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-03 08:00",
            r => r.PartnerName = "NCC A", LineRequest(p, 10, 120_000));
        var issued = await CreateVoucherAsync(_client, StockDirection.Out, "XKH", "2026-10-12 08:00", LineRequest(p, 5, 0));
        await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-15 08:00", r => r.WarehouseId = second,
            LineRequest(p, 6, 100_000));
        await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-20 08:00", LineRequest(p, 4, 100_000));
        var cost = (await LedgerOfAsync(issued.Id)).Single().CostAmount!.Value;
        return (p, second, cost);
    }

    private static async Task<StockSummaryReportDto> GetSummaryAsync(HttpClient client, string query) =>
        await ReadDataAsync<StockSummaryReportDto>(await client.GetAsync($"/api/reports/stock-summary?{query}"));

    private async Task SaveOpeningAsync(Guid productId, DateOnly date, decimal quantity, decimal amount)
    {
        await SetBranchOpeningDateAsync(MainBranchId, date);
        var response = await _client.PutAsJsonAsync("/api/inventory/opening-stock", new SaveOpeningStockRequest
        {
            WarehouseId = DefaultWarehouseId,
            Lines = { new SaveOpeningStockLine { ProductId = productId, Quantity = quantity, Amount = amount } },
        });
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }
}
