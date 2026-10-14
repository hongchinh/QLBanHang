using System.Net;
using System.Net.Http.Json;
using ClosedXML.Excel;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Application.Inventory.OpeningStocks.Models;
using OrderMgmt.Application.Inventory.Reports.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using OrderMgmt.IntegrationTests.Inventory;
using Xunit;

namespace OrderMgmt.IntegrationTests.Tt133;

/// S06-DNN: `POST api/reports/stock-detail/export` with a JSON body.
[Collection(nameof(PostgresCollection))]
public class StockDetailExportTests : InventoryTestBase
{
    public StockDetailExportTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task S06_for_one_product_and_warehouse()
    {
        var ids = await SeedAsync();
        var warehouseName = await InDbAsync(db => db.Warehouses.Where(w => w.Id == DefaultWarehouseId).Select(w => w.Name).SingleAsync());

        var response = await PostAsync(_client, Body(warehouseId: DefaultWarehouseId, productIds: [ids.Sm01]));

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        response.Content.Headers.ContentDisposition!.FileName.Should().Be("S06-DNN_20261010_20261031.xlsx");
        using var wb = await Tt133Sheet.OpenAsync(response);
        var ws = wb.Worksheets.Should().ContainSingle().Subject;
        ws.Name.Should().Be("SM01-KHO01");
        Tt133Sheet.Contains(ws, "Mẫu số S06-DNN").Should().BeTrue();
        Tt133Sheet.Contains(ws, "SỔ CHI TIẾT VẬT LIỆU, DỤNG CỤ, SẢN PHẨM, HÀNG HÓA").Should().BeTrue();
        Tt133Sheet.Contains(ws, $"Kho: KHO01 – {warehouseName}").Should().BeTrue();
        Tt133Sheet.Contains(ws, "Tên, quy cách: SM01 – Hàng SM01").Should().BeTrue();

        var card = await GetCardAsync(ids.Sm01, DefaultWarehouseId);
        card.Rows.Should().HaveCount(2);
        var issue = card.Rows[0];
        var cost = issue.CostAmount!.Value;

        var opening = Tt133Sheet.RowOf(ws, "Số dư đầu kỳ");
        Tt133Sheet.Number(ws, opening, 10).Should().Be(20m);
        Tt133Sheet.Number(ws, opening, 11).Should().Be(2_200_000m);

        var issueRow = opening + 1;
        ws.Cell(issueRow, 1).GetString().Should().Be(issue.SourceCode);
        ws.Cell(issueRow, 2).GetDateTime().Should().Be(new DateTime(2026, 10, 12));
        ws.Cell(issueRow, 3).GetString().Should().EndWith("Phiếu xuất kho");
        ws.Cell(issueRow, 4).GetString().Should().Be("Xuất khác");
        Tt133Sheet.Number(ws, issueRow, 5).Should().Be(issue.UnitCost!.Value);
        ws.Cell(issueRow, 6).IsEmpty().Should().BeTrue();
        ws.Cell(issueRow, 7).IsEmpty().Should().BeTrue();
        Tt133Sheet.Number(ws, issueRow, 8).Should().Be(5m);
        Tt133Sheet.Number(ws, issueRow, 9).Should().Be(cost);
        Tt133Sheet.Number(ws, issueRow, 10).Should().Be(15m);
        Tt133Sheet.Number(ws, issueRow, 11).Should().Be(2_200_000m - cost);

        var receiptRow = opening + 2;
        ws.Cell(receiptRow, 3).GetString().Should().EndWith("Phiếu nhập kho");
        Tt133Sheet.Number(ws, receiptRow, 6).Should().Be(4m);
        Tt133Sheet.Number(ws, receiptRow, 7).Should().Be(400_000m);
        ws.Cell(receiptRow, 8).IsEmpty().Should().BeTrue();
        ws.Cell(receiptRow, 9).IsEmpty().Should().BeTrue();
        Tt133Sheet.Number(ws, receiptRow, 10).Should().Be(19m);
        Tt133Sheet.Number(ws, receiptRow, 11).Should().Be(2_600_000m - cost);

        var movement = Tt133Sheet.RowOf(ws, "Cộng phát sinh");
        movement.Should().Be(opening + 3);
        Tt133Sheet.Number(ws, movement, 6).Should().Be(4m);
        Tt133Sheet.Number(ws, movement, 7).Should().Be(400_000m);
        Tt133Sheet.Number(ws, movement, 8).Should().Be(5m);
        Tt133Sheet.Number(ws, movement, 9).Should().Be(cost);

        var closing = Tt133Sheet.RowOf(ws, "Số dư cuối kỳ");
        closing.Should().Be(opening + 4);
        Tt133Sheet.Number(ws, closing, 10).Should().Be(19m);
        Tt133Sheet.Number(ws, closing, 11).Should().Be(2_600_000m - cost);

        ws.PageSetup.PageOrientation.Should().Be(XLPageOrientation.Landscape);
    }

    [Fact]
    public async Task S06_file_equals_the_stock_card()
    {
        var ids = await SeedAsync();

        var response = await PostAsync(_client, Body(warehouseId: DefaultWarehouseId, productIds: [ids.Sm01]));

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        using var wb = await Tt133Sheet.OpenAsync(response);
        var ws = wb.Worksheets.Single();
        var card = await GetCardAsync(ids.Sm01, DefaultWarehouseId);

        var opening = Tt133Sheet.RowOf(ws, "Số dư đầu kỳ");
        Tt133Sheet.Number(ws, opening, 10).Should().Be(card.OpeningQty);
        Tt133Sheet.Number(ws, opening, 11).Should().Be(card.OpeningValue!.Value);
        var movement = Tt133Sheet.RowOf(ws, "Cộng phát sinh");
        Tt133Sheet.Number(ws, movement, 6).Should().Be(card.InQty);
        Tt133Sheet.Number(ws, movement, 7).Should().Be(card.InValue!.Value);
        Tt133Sheet.Number(ws, movement, 8).Should().Be(card.OutQty);
        Tt133Sheet.Number(ws, movement, 9).Should().Be(card.OutValue!.Value);
        var closing = Tt133Sheet.RowOf(ws, "Số dư cuối kỳ");
        Tt133Sheet.Number(ws, closing, 10).Should().Be(card.ClosingQty);
        Tt133Sheet.Number(ws, closing, 11).Should().Be(card.ClosingValue!.Value);
    }

    [Fact]
    public async Task S06_all_pairs()
    {
        await SeedAsync();

        var response = await PostAsync(_client, Body(all: true));

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        using var wb = await Tt133Sheet.OpenAsync(response);
        wb.Worksheets.Select(w => w.Name).Should().Equal("SM01-KHO01", "SM01-KHO02");
    }

    [Fact]
    public async Task S06_under_branch_costing_has_quantities_only()
    {
        var ids = await SeedAsync(CostingScope.Branch);

        var response = await PostAsync(_client, Body(warehouseId: DefaultWarehouseId, productIds: [ids.Sm01]));

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        using var wb = await Tt133Sheet.OpenAsync(response);
        var ws = wb.Worksheets.Single();
        Tt133Sheet.Contains(ws, "Giá trị tính theo chi nhánh").Should().BeTrue();
        ws.CellsUsed().Should().NotContain(c => c.GetString() == "Đơn giá");
        ws.CellsUsed().Should().NotContain(c => c.GetString() == "Thành tiền");
        ws.LastColumnUsed()!.ColumnNumber().Should().Be(8);

        var closing = Tt133Sheet.RowOf(ws, "Số dư cuối kỳ");
        Tt133Sheet.Number(ws, closing, 7).Should().Be(19m);
        foreach (var column in new[] { 1, 2, 4, 5, 6, 8 })
            ws.Cell(closing, column).IsEmpty().Should().BeTrue($"column {column}");
    }

    [Fact]
    public async Task S06_needs_the_cost_permission()
    {
        var ids = await SeedAsync();
        var client = await CreateClientWithPermissionsAsync("s06_nocost", null, Permissions.Reports.Inventory);
        var body = Body(warehouseId: DefaultWarehouseId, productIds: [ids.Sm01]);

        (await PostAsync(client, body)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var cards = await client.PostAsJsonAsync("/api/reports/stock-card/export", body, TestJson.Options);
        cards.StatusCode.Should().Be(HttpStatusCode.OK, await cards.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Validation()
    {
        await SeedAsync();

        var neither = await PostAsync(_client, Body());

        neither.StatusCode.Should().Be(HttpStatusCode.BadRequest, await neither.Content.ReadAsStringAsync());
        (await neither.Content.ReadFromJsonAsync<ApiResponse>(TestJson.Options))!.Error!.Details.Should().ContainKey("productIds");
    }

    private static Dictionary<string, object?> Body(Guid? warehouseId = null, Guid[]? productIds = null, bool all = false)
    {
        var body = new Dictionary<string, object?> { ["from"] = "2026-10-10", ["to"] = "2026-10-31" };
        if (warehouseId is { } id) body["warehouseId"] = id;
        if (productIds is not null) body["productIds"] = productIds;
        if (all) body["all"] = true;
        return body;
    }

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, object body) =>
        client.PostAsJsonAsync("/api/reports/stock-detail/export", body, TestJson.Options);

    private async Task<StockCardDto> GetCardAsync(Guid productId, Guid warehouseId) =>
        await ReadDataAsync<StockCardDto>(await _client.GetAsync(
            $"/api/reports/stock-card?productId={productId}&warehouseId={warehouseId}&from=2026-10-10&to=2026-10-31"));

    private record Seeded(Guid Sm01, Guid Kho02);

    /// Product SM01 with the data of the Phase 04 test table.
    private async Task<Seeded> SeedAsync(CostingScope scope = CostingScope.Warehouse)
    {
        await UpdateInventorySettingsAsync(s => s.CostingScope = scope);
        var p = await CreateInventoryProductAsync("SM01");
        var second = await CreateWarehouseAsync(MainBranchId, "KHO02");
        await SaveOpeningAsync(p, new DateOnly(2026, 10, 1), 10, 1_000_000);
        await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-03 08:00",
            r => r.PartnerName = "NCC A", LineRequest(p, 10, 120_000));
        await CreateVoucherAsync(_client, StockDirection.Out, "XKH", "2026-10-12 08:00", LineRequest(p, 5, 0));
        await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-15 08:00", r => r.WarehouseId = second,
            LineRequest(p, 6, 100_000));
        await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-20 08:00", LineRequest(p, 4, 100_000));
        return new Seeded(p, second);
    }

    private async Task SaveOpeningAsync(Guid productId, DateOnly date, decimal quantity, decimal amount)
    {
        await SetBranchOpeningDateAsync(MainBranchId, date);
        var response = await _client.PutAsJsonAsync("/api/inventory/opening-stock", new SaveOpeningStockRequest
        {
            WarehouseId = DefaultWarehouseId,
            Lines = { new SaveOpeningStockLine { ProductId = productId, Quantity = quantity, Amount = amount } },
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }
}
