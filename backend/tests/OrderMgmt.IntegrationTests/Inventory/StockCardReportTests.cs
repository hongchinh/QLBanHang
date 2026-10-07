using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using OrderMgmt.Application.Inventory.OpeningStocks.Models;
using OrderMgmt.Application.Inventory.Reports.Models;
using OrderMgmt.Application.Inventory.StockVouchers.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.Inventory;

[Collection(nameof(PostgresCollection))]
public class StockCardReportTests : InventoryTestBase
{
    public StockCardReportTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Card_shows_opening_rows_running_and_closing()
    {
        // Warehouse scope, so per-warehouse running values are meaningful (D32). No ledger rows yet: nothing to recalculate.
        await UpdateInventorySettingsAsync(s => s.CostingScope = CostingScope.Warehouse);
        var p = await CreateInventoryProductAsync("SC01");
        var second = await CreateWarehouseAsync(MainBranchId, "KHO02");
        await SaveOpeningAsync(p, new DateOnly(2026, 10, 1), 10, 1_000_000);
        var supplied = await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-03 08:00",
            r => r.PartnerName = "NCC A", LineRequest(p, 10, 120_000));
        var issued = await CreateVoucherAsync(_client, StockDirection.Out, "XKH", "2026-10-12 08:00", LineRequest(p, 5, 0));
        var other = await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-15 08:00", r => r.WarehouseId = second,
            LineRequest(p, 6, 100_000));
        var late = await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-20 08:00", LineRequest(p, 4, 100_000));
        var outRow = (await LedgerOfAsync(issued.Id)).Single();
        var cost = outRow.CostAmount!.Value;

        // Mid-month: the opening includes the opening stock and the 10-03 stock-in.
        var card = await GetCardAsync(_client, $"productId={p}&warehouseId={DefaultWarehouseId}&from=2026-10-10&to=2026-10-31");
        card.Should().BeEquivalentTo(new
        {
            ProductId = p,
            ProductCode = "SC01",
            From = new DateOnly(2026, 10, 10),
            To = new DateOnly(2026, 10, 31),
            CanViewCost = true,
            ValuesAtScopeOnly = false,
            OpeningQty = 20m,
            OpeningValue = 2_200_000m,
            InQty = 4m,
            OutQty = 5m,
            InValue = 400_000m,
            OutValue = cost,
            ClosingQty = 19m,
            ClosingValue = 2_600_000m - cost,
        });
        card.Rows.Should().BeEquivalentTo(new object[]
        {
            new
            {
                PostedAt = Vn("2026-10-12 08:00"), SourceType = LedgerSourceType.StockOut, SourceId = issued.Id,
                SourceCode = issued.Code, ReasonName = "Xuất khác", WarehouseCode = "KHO01",
                QtyIn = 0m, QtyOut = 5m, UnitCost = outRow.UnitCost, CostAmount = cost,
                RunningQty = 15m, RunningValue = 2_200_000m - cost,
            },
            new
            {
                SourceId = late.Id, SourceCode = late.Code, ReasonName = "Nhập khác",
                QtyIn = 4m, UnitCost = 100_000m, InValue = 400_000m, RunningQty = 19m, RunningValue = 2_600_000m - cost,
            },
        }, o => o.WithStrictOrdering());

        // From the first day: the opening-stock row and the partner of the stock-in.
        var month = await GetCardAsync(_client, $"productId={p}&warehouseId={DefaultWarehouseId}&from=2026-10-01&to=2026-10-31");
        month.OpeningQty.Should().Be(0m);
        month.Rows.Take(2).Should().BeEquivalentTo(new object[]
        {
            new { SourceType = LedgerSourceType.Opening, SourceCode = "TDK", ReasonName = "Tồn đầu kỳ", PartnerName = (string?)null,
                QtyIn = 10m, UnitCost = 100_000m, InValue = 1_000_000m, RunningQty = 10m, RunningValue = 1_000_000m },
            new { SourceId = supplied.Id, PartnerName = "NCC A", UnitCost = 120_000m, RunningQty = 20m, RunningValue = 2_200_000m },
        }, o => o.WithStrictOrdering());
        month.ClosingValue.Should().Be(card.ClosingValue);

        // Without a warehouse: every warehouse of the branch accumulates.
        var all = await GetCardAsync(_client, $"productId={p}&from=2026-10-10&to=2026-10-31");
        all.ValuesAtScopeOnly.Should().BeFalse();
        all.OpeningQty.Should().Be(20m);
        all.Rows.Select(r => (r.SourceId, r.WarehouseCode, r.RunningQty)).Should().Equal(
            (issued.Id, "KHO01", 15m), (other.Id, "KHO02", 21m), (late.Id, "KHO01", 25m));
        all.ClosingQty.Should().Be(25m);
        all.ClosingValue.Should().Be(3_200_000m - cost);

        (await GetCardAsync(_client, $"productId={p}&from=2026-01-01&to=2026-01-31")).IsProvisional.Should().BeFalse();
        await AssertInvariantsAsync();
    }

    [Fact]
    public async Task Excludes_cancelled_and_deleted_and_validates_range()
    {
        var p = await CreateInventoryProductAsync("SC10");
        var kept = await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-02 08:00", LineRequest(p, 10, 100_000));
        var cancelled = await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-03 08:00", LineRequest(p, 5, 100_000));
        var deleted = await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-04 08:00", LineRequest(p, 3, 100_000));
        (await _client.PostAsJsonAsync($"/api/stock-vouchers/{cancelled.Id}/cancel",
            new StockVoucherActionRequest { Version = cancelled.Version })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await _client.DeleteAsync($"/api/stock-vouchers/{deleted.Id}?version={deleted.Version}"))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var card = await GetCardAsync(_client, $"productId={p}&from=2026-10-01&to=2026-10-31");
        card.Rows.Should().ContainSingle().Which.SourceId.Should().Be(kept.Id);
        card.ClosingQty.Should().Be(10m);

        (await _client.GetAsync($"/api/reports/stock-card?productId={p}&from=2026-10-31&to=2026-10-01"))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await _client.GetAsync($"/api/reports/stock-card?productId={p}&to=2026-10-31"))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await _client.GetAsync($"/api/reports/stock-card?productId={Guid.NewGuid()}&from=2026-10-01&to=2026-10-31"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        await AssertInvariantsAsync();
    }

    [Fact]
    public async Task Values_hidden_without_view_cost_and_permission_required()
    {
        var p = await CreateInventoryProductAsync("SC20");
        await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-02 08:00", LineRequest(p, 10, 100_000));
        await CreateVoucherAsync(_client, StockDirection.Out, "XKH", "2026-10-05 08:00", LineRequest(p, 4, 0));
        var query = $"productId={p}&from=2026-10-03&to=2026-10-31";

        var warehouse = await CreateClientForRoleAsync("kho_sc", RoleCodes.Warehouse);
        var card = await GetCardAsync(warehouse, query);
        card.Should().BeEquivalentTo(new
        {
            CanViewCost = false,
            OpeningQty = 10m,
            OpeningValue = (decimal?)null,
            InValue = (decimal?)null,
            OutValue = (decimal?)null,
            ClosingQty = 6m,
            ClosingValue = (decimal?)null,
        });
        card.Rows.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            QtyOut = 4m,
            RunningQty = 6m,
            UnitCost = (decimal?)null,
            InValue = (decimal?)null,
            CostAmount = (decimal?)null,
            RunningValue = (decimal?)null,
        });

        var sales = await CreateClientForRoleAsync("sales_sc", RoleCodes.Sales);
        (await sales.GetAsync($"/api/reports/stock-card?{query}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await AssertInvariantsAsync();
    }

    [Fact]
    public async Task Warehouse_filter_under_branch_scope_hides_running_values()
    {
        var p = await CreateInventoryProductAsync("SC30");
        var second = await CreateWarehouseAsync(MainBranchId, "KHO02");
        await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-02 08:00", LineRequest(p, 10, 100_000));
        await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-02 09:00", r => r.WarehouseId = second,
            LineRequest(p, 10, 200_000));
        await CreateVoucherAsync(_client, StockDirection.Out, "XKH", "2026-10-05 08:00", LineRequest(p, 5, 0));

        var one = await GetCardAsync(_client, $"productId={p}&warehouseId={DefaultWarehouseId}&from=2026-10-01&to=2026-10-31");
        one.Should().BeEquivalentTo(new
        {
            ValuesAtScopeOnly = true,
            OpeningValue = (decimal?)null,
            ClosingQty = 5m,
            ClosingValue = (decimal?)null,
        });
        one.Rows.Should().OnlyContain(r => r.RunningValue == null);
        one.Rows.Should().BeEquivalentTo(new object[]
        {
            new { QtyIn = 10m, UnitCost = 100_000m, InValue = 1_000_000m, RunningQty = 10m },
            new { QtyOut = 5m, UnitCost = 150_000m, CostAmount = 750_000m, RunningQty = 5m },
        }, o => o.WithStrictOrdering());

        var all = await GetCardAsync(_client, $"productId={p}&from=2026-10-01&to=2026-10-31");
        all.ValuesAtScopeOnly.Should().BeFalse();
        all.Rows.Select(r => r.RunningValue).Should().Equal(1_000_000m, 3_000_000m, 2_250_000m);
        all.ClosingValue.Should().Be(2_250_000m);
        var onHand = await ReadDataAsync<StockOnHandReportDto>(await _client.GetAsync("/api/reports/stock-on-hand"));
        all.ClosingValue.Should().Be(onHand.TotalValue);
        await AssertInvariantsAsync();
    }

    private static async Task<StockCardDto> GetCardAsync(HttpClient client, string query) =>
        await ReadDataAsync<StockCardDto>(await client.GetAsync($"/api/reports/stock-card?{query}"));

    private async Task SaveOpeningAsync(Guid productId, DateOnly date, decimal quantity, decimal amount)
    {
        var response = await _client.PutAsJsonAsync("/api/inventory/opening-stock", new SaveOpeningStockRequest
        {
            WarehouseId = DefaultWarehouseId,
            OpeningDate = date,
            Lines = { new SaveOpeningStockLine { ProductId = productId, Quantity = quantity, Amount = amount } },
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }
}
