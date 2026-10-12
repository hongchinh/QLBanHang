using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Application.Inventory.Common;
using OrderMgmt.Application.Inventory.Ledger;
using OrderMgmt.Application.Inventory.OpeningStocks.Models;
using OrderMgmt.Application.Inventory.StockVouchers.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.Inventory;

[Collection(nameof(PostgresCollection))]
public class OpeningStockTests : InventoryTestBase
{
    public OpeningStockTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Save_load_posting_order_and_costing()
    {
        var unit = await CreateInventoryProductAsync("OS01");
        var area = await CreateInventoryProductAsync("OS02", PricingMode.PerSquareMeter);

        await SetBranchOpeningDateAsync(MainBranchId, new DateOnly(2026, 10, 1));
        var saved = await SaveAsync(_client, Request(Line(unit, 10, 1_000_000), Line(area, 100, 5_000_000)));
        saved.Status.Should().Be(HttpStatusCode.OK, saved.Error?.Message);

        var grid = await LoadAsync(_client, DefaultWarehouseId);
        grid.WarehouseId.Should().Be(DefaultWarehouseId);
        grid.OpeningDate.Should().Be(new DateOnly(2026, 10, 1));
        grid.Lines.Should().BeEquivalentTo(new object[]
        {
            new { ProductId = unit, ProductCode = "OS01", Quantity = 10m, Amount = 1_000_000m },
            new { ProductId = area, ProductCode = "OS02", UnitName = "m²", Quantity = 100m, Amount = 5_000_000m },
        }, o => o.WithStrictOrdering());
        saved.Grid.Should().BeEquivalentTo(grid);

        // The opening row (00:00 VN) comes before a voucher of the same day.
        await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-01 08:00", LineRequest(unit, 5, 100_000));
        var order = await InDbAsync(db => db.InventoryLedger.AsNoTracking()
            .Where(e => e.ProductId == unit).InPostingOrder().Select(e => e.SourceType).ToListAsync());
        order.Should().Equal(LedgerSourceType.Opening, LedgerSourceType.StockIn);
        var opening = await InDbAsync(db => db.InventoryLedger.AsNoTracking()
            .SingleAsync(e => e.ProductId == unit && e.SourceType == LedgerSourceType.Opening));
        opening.Should().BeEquivalentTo(new
        {
            SourceId = DefaultWarehouseId,
            SourceCode = "TDK",
            PostedAt = Vn("2026-10-01 00:00"),
            QtyIn = 10m,
            InValue = 1_000_000m,
        });

        // 100 m² worth 5,000,000 → 10 m² out in the same month costs 500,000.
        var outbound = await CreateVoucherAsync(_client, StockDirection.Out, "XKH", "2026-10-10 08:00",
            new UpsertStockVoucherLineRequest { ProductId = area, SheetCount = 5, Length = 2000, Width = 1000, UnitPrice = 0 });
        (await LedgerOfAsync(outbound.Id)).Single().CostAmount.Should().Be(500_000m);
        await AssertInvariantsAsync();
    }

    [Fact]
    public async Task Policy_and_period_lock()
    {
        var p = await CreateInventoryProductAsync("OS03");
        await SetBranchOpeningDateAsync(MainBranchId, new DateOnly(2026, 9, 1));
        (await SaveAsync(_client, Request(Line(p, 10, 1_000_000)))).Status.Should().Be(HttpStatusCode.OK);
        await CreateVoucherAsync(_client, StockDirection.Out, "XKH", "2026-10-05 08:00", LineRequest(p, 8, 0));
        await UpdateInventorySettingsAsync(s => s.NegativeStockPolicy = NegativeStockPolicy.Block);

        var reduced = await SaveAsync(_client, Request(Line(p, 5, 500_000)));
        reduced.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        reduced.Error!.Code.Should().Be("NEGATIVE_STOCK_BLOCKED");
        (await StockOfAsync(p, DefaultWarehouseId)).Should().Be(2m);

        await SetPeriodLockAsync(new DateOnly(2026, 8, 31));
        await SetBranchOpeningDateAsync(MainBranchId, new DateOnly(2026, 8, 31));
        var onLock = await SaveAsync(_client, Request(Line(p, 10, 1_000_000)));
        onLock.Status.Should().Be(HttpStatusCode.BadRequest);
        onLock.Error!.Code.Should().Be("PERIOD_LOCKED");

        await SetPeriodLockAsync(new DateOnly(2026, 9, 30));
        await SetBranchOpeningDateAsync(MainBranchId, new DateOnly(2026, 10, 1));
        var oldDateLocked = await SaveAsync(_client, Request(Line(p, 10, 1_000_000)));
        oldDateLocked.Status.Should().Be(HttpStatusCode.BadRequest);
        oldDateLocked.Error!.Code.Should().Be("PERIOD_LOCKED");

        // The rejected saves left the stored rows on their original date.
        (await InDbAsync(db => db.OpeningStocks.AsNoTracking().Where(o => o.ProductId == p).Select(o => o.OpeningDate).SingleAsync()))
            .Should().Be(new DateOnly(2026, 9, 1));
        await AssertInvariantsAsync();
    }

    [Fact]
    public async Task Validation_and_permissions()
    {
        var p = await CreateInventoryProductAsync("OS04");
        var untracked = await CreateInventoryProductAsync("OS05", track: false);
        await SetBranchOpeningDateAsync(MainBranchId, new DateOnly(2026, 10, 1));

        var notTracked = await SaveAsync(_client, Request(Line(p, 1, 1_000), Line(untracked, 1, 1_000)));
        notTracked.Status.Should().Be(HttpStatusCode.BadRequest);
        notTracked.Error!.Details.Should().ContainKey("lines[1].productId");

        var duplicate = await SaveAsync(_client, Request(Line(p, 1, 1_000), Line(p, 2, 2_000)));
        duplicate.Status.Should().Be(HttpStatusCode.BadRequest);
        duplicate.Error!.Details.Should().ContainKey("lines[1].productId");

        var invalid = await SaveAsync(_client, Request(Line(p, 0, -1)));
        invalid.Status.Should().Be(HttpStatusCode.BadRequest);
        invalid.Error!.Details.Should().ContainKeys("lines[0].quantity", "lines[0].amount");

        (await SaveAsync(_client, Request(Line(p, 3, 300_000)))).Status.Should().Be(HttpStatusCode.OK);

        var sales = await CreateClientForRoleAsync("sales_os", RoleCodes.Sales);
        (await sales.GetAsync($"/api/inventory/opening-stock?warehouseId={DefaultWarehouseId}"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await SaveAsync(sales, Request(Line(p, 3, 300_000)))).Status.Should().Be(HttpStatusCode.Forbidden);

        // D36: opening values are part of inventory.opening_stock.
        var warehouse = await CreateClientForRoleAsync("kho_os", RoleCodes.Warehouse);
        var grid = await LoadAsync(warehouse, DefaultWarehouseId);
        grid.Lines.Should().ContainSingle().Which.Amount.Should().Be(300_000m);
        (await SaveAsync(warehouse, Request(Line(p, 4, 400_000)))).Status.Should().Be(HttpStatusCode.OK);

        var otherBranch = await CreateBranchAsync("CN02");
        var otherWarehouse = await CreateWarehouseAsync(otherBranch, "KHO-B");
        (await _client.GetAsync($"/api/inventory/opening-stock?warehouseId={otherWarehouse}"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var foreign = Request(Line(p, 1, 1_000));
        foreign.WarehouseId = otherWarehouse;
        (await SaveAsync(_client, foreign)).Status.Should().Be(HttpStatusCode.Forbidden);
        await AssertInvariantsAsync();
    }

    [Fact]
    public async Task Raising_opening_stock_passes_under_block_even_with_a_later_deficit()
    {
        var p = await CreateInventoryProductAsync("OS06");
        await SetBranchOpeningDateAsync(MainBranchId, new DateOnly(2026, 10, 1));
        await UpdateInventorySettingsAsync(s => s.NegativeStockPolicy = NegativeStockPolicy.Allow);
        (await SaveAsync(_client, Request(Line(p, 5, 500_000)))).Status.Should().Be(HttpStatusCode.OK);
        await CreateVoucherAsync(_client, StockDirection.Out, "XKH", "2026-10-05 08:00", LineRequest(p, 8, 0));
        await UpdateInventorySettingsAsync(s => s.NegativeStockPolicy = NegativeStockPolicy.Block);

        var raised = await SaveAsync(_client, Request(Line(p, 6, 600_000)));

        raised.Status.Should().Be(HttpStatusCode.OK, raised.Error?.Message);
        (await StockOfAsync(p, DefaultWarehouseId)).Should().Be(-2m);
        await AssertInvariantsAsync();
    }

    [Fact]
    public async Task Empty_list_removes_the_opening_stock()
    {
        var p = await CreateInventoryProductAsync("OS07");
        await SetBranchOpeningDateAsync(MainBranchId, new DateOnly(2026, 10, 1));
        (await SaveAsync(_client, Request(Line(p, 5, 500_000)))).Status.Should().Be(HttpStatusCode.OK);

        var cleared = await SaveAsync(_client, Request());

        cleared.Status.Should().Be(HttpStatusCode.OK, cleared.Error?.Message);
        cleared.Grid!.Lines.Should().BeEmpty();
        cleared.Grid.OpeningDate.Should().Be(new DateOnly(2026, 10, 1), "the grid reports the branch opening date");
        (await StockOfAsync(p, DefaultWarehouseId)).Should().Be(0m);
        await AssertInvariantsAsync();
    }

    [Fact]
    public async Task Save_without_branch_opening_date_returns_opening_date_not_set()
    {
        var p = await CreateInventoryProductAsync("OS08");
        var original = await InDbAsync(db => db.Branches.AsNoTracking().Where(b => b.Id == MainBranchId).Select(b => b.OpeningDate).SingleAsync());
        await SetBranchOpeningDateAsync(MainBranchId, null);
        try
        {
            var result = await SaveAsync(_client, Request(Line(p, 1, 1_000)));

            result.Status.Should().Be(HttpStatusCode.BadRequest);
            result.Error!.Code.Should().Be("OPENING_DATE_NOT_SET");
            (await LoadAsync(_client, DefaultWarehouseId)).OpeningDate.Should().BeNull();
        }
        finally
        {
            await SetBranchOpeningDateAsync(MainBranchId, original);
        }
    }

    [Fact]
    public async Task Save_posts_at_branch_opening_date()
    {
        var p = await CreateInventoryProductAsync("OS09");
        await SetBranchOpeningDateAsync(MainBranchId, new DateOnly(2026, 10, 1));

        var saved = await SaveAsync(_client, Request(Line(p, 4, 400_000)));

        saved.Status.Should().Be(HttpStatusCode.OK, saved.Error?.Message);
        saved.Grid!.OpeningDate.Should().Be(new DateOnly(2026, 10, 1));
        (await InDbAsync(db => db.InventoryLedger.AsNoTracking()
                .SingleAsync(e => e.ProductId == p && e.SourceType == LedgerSourceType.Opening))).PostedAt
            .Should().Be(VnTime.StartOfDay(new DateOnly(2026, 10, 1)));
        var json = await (await _client.GetAsync($"/api/inventory/opening-stock?warehouseId={DefaultWarehouseId}")).Content.ReadAsStringAsync();
        json.Should().Contain("\"openingDate\":\"2026-10-01\"");
    }

    private SaveOpeningStockRequest Request(params SaveOpeningStockLine[] lines) => new()
    {
        WarehouseId = DefaultWarehouseId,
        Lines = lines.ToList(),
    };

    private static SaveOpeningStockLine Line(Guid productId, decimal quantity, decimal amount) =>
        new() { ProductId = productId, Quantity = quantity, Amount = amount };

    private static async Task<OpeningStockGridDto> LoadAsync(HttpClient client, Guid warehouseId) =>
        await ReadDataAsync<OpeningStockGridDto>(await client.GetAsync($"/api/inventory/opening-stock?warehouseId={warehouseId}"));

    private static async Task<(HttpStatusCode Status, OpeningStockGridDto? Grid, ApiError? Error)> SaveAsync(
        HttpClient client, SaveOpeningStockRequest request)
    {
        var response = await client.PutAsJsonAsync("/api/inventory/opening-stock", request);
        var body = await response.Content.ReadAsStringAsync();
        var parsed = string.IsNullOrEmpty(body)
            ? null
            : System.Text.Json.JsonSerializer.Deserialize<ApiResponse<OpeningStockGridDto>>(body, TestJson.Options);
        return (response.StatusCode, parsed?.Data, parsed?.Error);
    }
}
