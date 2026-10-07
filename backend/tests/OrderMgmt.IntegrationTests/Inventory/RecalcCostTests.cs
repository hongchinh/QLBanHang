using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Application.Inventory.Costing;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.Inventory;

[Collection(nameof(PostgresCollection))]
public class RecalcCostTests : InventoryTestBase
{
    public RecalcCostTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Recalc_is_idempotent_and_repairs_tampered_costs()
    {
        var p = await CreateInventoryProductAsync("RC01");
        await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-02 08:00", LineRequest(p, 10, 100_000));
        var octOut = await CreateVoucherAsync(_client, StockDirection.Out, "XKH", "2026-10-05 08:00", LineRequest(p, 4, 0));
        await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-11-03 08:00", LineRequest(p, 10, 200_000));
        await CreateVoucherAsync(_client, StockDirection.Out, "XKH", "2026-11-10 08:00", LineRequest(p, 5, 0));
        var before = await SnapshotAsync();

        var result = await RecalcAsync(_client, new RecalculateCostRequest { FromPeriodStart = new DateOnly(2026, 10, 1) });
        result.Status.Should().Be(HttpStatusCode.OK, result.Error?.Message);
        result.Result!.Should().BeEquivalentTo(new RecalculateCostResult { FromPeriodStart = new DateOnly(2026, 10, 1), ScopeCount = 1 });
        (await SnapshotAsync()).Should().BeEquivalentTo(before);

        await InDbAsync(async db =>
        {
            await db.InventoryLedger.Where(e => e.SourceId == octOut.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.CostAmount, 0m).SetProperty(e => e.UnitCost, 0m));
            await db.InventoryCostPeriods.Where(c => c.ProductId == p && c.PeriodStart == new DateOnly(2026, 10, 1))
                .ExecuteDeleteAsync();
        });
        (await SnapshotAsync()).Should().NotBeEquivalentTo(before);

        (await RecalcAsync(_client, new RecalculateCostRequest { FromPeriodStart = new DateOnly(2026, 10, 1) }))
            .Status.Should().Be(HttpStatusCode.OK);
        (await SnapshotAsync()).Should().BeEquivalentTo(before);
        (await LedgerOfAsync(octOut.Id)).Single().CostAmount.Should().Be(400_000m);
        await AssertInvariantsAsync();
    }

    [Fact]
    public async Task Recalc_filters_by_product_and_warehouse()
    {
        var p1 = await CreateInventoryProductAsync("RC02");
        var p2 = await CreateInventoryProductAsync("RC03");
        var secondWarehouse = await CreateWarehouseAsync(MainBranchId, "KHO02");
        await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-02 08:00",
            LineRequest(p1, 10, 100_000), LineRequest(p2, 10, 50_000));
        var outbound = await CreateVoucherAsync(_client, StockDirection.Out, "XKH", "2026-10-05 08:00",
            LineRequest(p1, 2, 0), LineRequest(p2, 3, 0));
        await InDbAsync(db => db.InventoryLedger.Where(e => e.SourceId == outbound.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.CostAmount, 0m)));

        var october = new DateOnly(2026, 10, 1);
        (await RecalcAsync(_client, new RecalculateCostRequest { FromPeriodStart = october, ProductId = p1 }))
            .Result!.ScopeCount.Should().Be(1);
        (await CostsAsync(outbound.Id)).Should().Equal(200_000m, 0m);

        // No rows of p2 in the second warehouse: nothing to recalculate.
        (await RecalcAsync(_client, new RecalculateCostRequest { FromPeriodStart = october, WarehouseId = secondWarehouse }))
            .Result!.ScopeCount.Should().Be(0);
        (await CostsAsync(outbound.Id)).Should().Equal(200_000m, 0m);

        (await RecalcAsync(_client, new RecalculateCostRequest { FromPeriodStart = october, WarehouseId = DefaultWarehouseId }))
            .Result!.ScopeCount.Should().Be(2);
        (await CostsAsync(outbound.Id)).Should().Equal(200_000m, 150_000m);
        await AssertInvariantsAsync();
    }

    [Fact]
    public async Task Recalc_validation_and_permission()
    {
        var p = await CreateInventoryProductAsync("RC04");
        await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-09-02 08:00", LineRequest(p, 10, 100_000));

        var midPeriod = await RecalcAsync(_client, new RecalculateCostRequest { FromPeriodStart = new DateOnly(2026, 10, 2) });
        midPeriod.Status.Should().Be(HttpStatusCode.BadRequest);
        midPeriod.Error!.Details.Should().ContainKey("fromPeriodStart");

        await SetPeriodLockAsync(new DateOnly(2026, 9, 30));
        var locked = await RecalcAsync(_client, new RecalculateCostRequest { FromPeriodStart = new DateOnly(2026, 9, 1) });
        locked.Status.Should().Be(HttpStatusCode.BadRequest);
        locked.Error!.Code.Should().Be("PERIOD_LOCKED");
        (await RecalcAsync(_client, new RecalculateCostRequest { FromPeriodStart = new DateOnly(2026, 10, 1) }))
            .Status.Should().Be(HttpStatusCode.OK);

        var sales = await CreateClientForRoleAsync("sales_rc", RoleCodes.Sales);
        (await RecalcAsync(sales, new RecalculateCostRequest { FromPeriodStart = new DateOnly(2026, 10, 1) }))
            .Status.Should().Be(HttpStatusCode.Forbidden);
        await AssertInvariantsAsync();
    }

    /// Cost periods without ids, and the cost of every outbound row.
    private Task<object> SnapshotAsync() =>
        InDbAsync<object>(async db => new
        {
            Periods = await db.InventoryCostPeriods.AsNoTracking()
                .OrderBy(c => c.ProductId).ThenBy(c => c.ScopeKey).ThenBy(c => c.PeriodStart)
                .Select(c => new
                {
                    c.ProductId, c.BranchId, c.ScopeKey, c.PeriodStart, c.PeriodEnd, c.OpeningQty, c.OpeningValue,
                    c.InQty, c.InValue, c.OutQty, c.OutValue, c.AvgCost, c.ClosingQty, c.ClosingValue,
                })
                .ToListAsync(),
            Outbound = await db.InventoryLedger.AsNoTracking()
                .Where(e => e.QtyOut > 0)
                .OrderBy(e => e.Id)
                .Select(e => new { e.Id, e.UnitCost, e.CostAmount })
                .ToListAsync(),
        });

    private Task<List<decimal?>> CostsAsync(Guid voucherId) =>
        InDbAsync(db => db.InventoryLedger.AsNoTracking()
            .Where(e => e.SourceId == voucherId)
            .OrderBy(e => e.LineSortOrder)
            .Select(e => e.CostAmount)
            .ToListAsync());

    private static async Task<(HttpStatusCode Status, RecalculateCostResult? Result, ApiError? Error)> RecalcAsync(
        HttpClient client, RecalculateCostRequest request)
    {
        var response = await client.PostAsJsonAsync("/api/inventory/recalc-cost", request);
        var body = await response.Content.ReadAsStringAsync();
        var parsed = string.IsNullOrEmpty(body)
            ? null
            : System.Text.Json.JsonSerializer.Deserialize<ApiResponse<RecalculateCostResult>>(body, TestJson.Options);
        return (response.StatusCode, parsed?.Data, parsed?.Error);
    }
}
