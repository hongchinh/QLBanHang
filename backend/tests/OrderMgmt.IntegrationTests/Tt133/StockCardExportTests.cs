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

/// S08-DNN: `POST api/reports/stock-card/export` with a JSON body.
[Collection(nameof(PostgresCollection))]
public class StockCardExportTests : InventoryTestBase
{
    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public StockCardExportTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task S08_for_one_product_and_warehouse()
    {
        var ids = await SeedAsync();
        var warehouseName = await InDbAsync(db => db.Warehouses.Where(w => w.Id == DefaultWarehouseId).Select(w => w.Name).SingleAsync());

        var response = await PostAsync(_client, Body(warehouseId: DefaultWarehouseId, productIds: [ids.Sm01]));

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        response.Content.Headers.ContentType!.MediaType.Should().Be(XlsxContentType);
        response.Content.Headers.ContentDisposition!.FileName.Should().Be("S08-DNN_20261010_20261031.xlsx");
        using var wb = await Tt133Sheet.OpenAsync(response);
        var ws = wb.Worksheets.Should().ContainSingle().Subject;
        ws.Name.Should().Be("SM01-KHO01");
        Tt133Sheet.Contains(ws, "Mẫu số S08-DNN").Should().BeTrue();
        Tt133Sheet.Contains(ws, "THẺ KHO (SỔ KHO)").Should().BeTrue();
        Tt133Sheet.Contains(ws, "Tờ số: 1").Should().BeTrue();
        Tt133Sheet.Contains(ws, "Mã số: SM01").Should().BeTrue();
        Tt133Sheet.Contains(ws, "Tên, nhãn hiệu, quy cách vật tư: Hàng SM01").Should().BeTrue();
        Tt133Sheet.Contains(ws, $"Kho: KHO01 – {warehouseName}").Should().BeTrue();

        var card = await GetCardAsync(ids.Sm01, DefaultWarehouseId);
        card.Rows.Should().HaveCount(2);
        var issueCode = card.Rows[0].SourceCode;
        var receiptCode = card.Rows[1].SourceCode;

        var opening = Tt133Sheet.RowOf(ws, "Tồn đầu kỳ");
        Tt133Sheet.Number(ws, opening, 9).Should().Be(20m);

        var issue = opening + 1;
        ws.Cell(issue, 1).GetValue<int>().Should().Be(1);
        ws.Cell(issue, 3).IsEmpty().Should().BeTrue();
        ws.Cell(issue, 4).GetString().Should().Be(issueCode);
        ws.Cell(issue, 7).IsEmpty().Should().BeTrue();
        Tt133Sheet.Number(ws, issue, 8).Should().Be(5m);
        Tt133Sheet.Number(ws, issue, 9).Should().Be(15m);
        ws.Cell(issue, 2).GetDateTime().Should().Be(new DateTime(2026, 10, 12));

        var receipt = opening + 2;
        ws.Cell(receipt, 1).GetValue<int>().Should().Be(2);
        ws.Cell(receipt, 3).GetString().Should().Be(receiptCode);
        ws.Cell(receipt, 4).IsEmpty().Should().BeTrue();
        Tt133Sheet.Number(ws, receipt, 7).Should().Be(4m);
        ws.Cell(receipt, 8).IsEmpty().Should().BeTrue();
        Tt133Sheet.Number(ws, receipt, 9).Should().Be(19m);

        var total = Tt133Sheet.RowOf(ws, "Cộng cuối kỳ");
        total.Should().Be(opening + 3);
        Tt133Sheet.Number(ws, total, 7).Should().Be(4m);
        Tt133Sheet.Number(ws, total, 8).Should().Be(5m);
        var closing = Tt133Sheet.RowOf(ws, "Tồn cuối kỳ");
        closing.Should().Be(opening + 4);
        Tt133Sheet.Number(ws, closing, 9).Should().Be(19m);

        ws.PageSetup.PageOrientation.Should().Be(XLPageOrientation.Portrait);
    }

    [Fact]
    public async Task S08_all_pairs_with_activity()
    {
        var ids = await SeedAsync();

        var response = await PostAsync(_client, Body(all: true));

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        using var wb = await Tt133Sheet.OpenAsync(response);
        wb.Worksheets.Select(w => w.Name).Should().Equal("SM01-KHO01", "SM01-KHO02");
        Tt133Sheet.Contains(wb.Worksheet(1), "Tờ số: 1").Should().BeTrue();
        Tt133Sheet.Contains(wb.Worksheet(2), "Tờ số: 2").Should().BeTrue();

        var warehouses = new[] { DefaultWarehouseId, ids.Kho02 };
        for (var i = 0; i < 2; i++)
        {
            var ws = wb.Worksheet(i + 1);
            var card = await GetCardAsync(ids.Sm01, warehouses[i]);
            Tt133Sheet.Number(ws, Tt133Sheet.RowOf(ws, "Tồn cuối kỳ"), 9).Should().Be(card.ClosingQty, ws.Name);
        }
    }

    [Fact]
    public async Task S08_explicit_product_without_warehouse()
    {
        var ids = await SeedAsync();

        var response = await PostAsync(_client, Body(productIds: [ids.Sm01]));
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        using (var wb = await Tt133Sheet.OpenAsync(response))
            wb.Worksheets.Select(w => w.Name).Should().Equal("SM01-KHO01", "SM01-KHO02");

        var none = await PostAsync(_client, Body(productIds: [ids.Sm03]));
        none.StatusCode.Should().Be((HttpStatusCode)422, await none.Content.ReadAsStringAsync());
        (await none.Content.ReadFromJsonAsync<ApiResponse>(TestJson.Options))!.Error!.Code.Should().Be("EXPORT_NO_TARGETS");
    }

    [Fact]
    public async Task S08_explicit_product_with_warehouse_is_always_a_sheet()
    {
        var ids = await SeedAsync();

        var response = await PostAsync(_client, Body(warehouseId: ids.Kho02, productIds: [ids.Sm03]));

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        using var wb = await Tt133Sheet.OpenAsync(response);
        var ws = wb.Worksheets.Should().ContainSingle().Subject;
        ws.Name.Should().Be("SM03-KHO02");
        var opening = Tt133Sheet.RowOf(ws, "Tồn đầu kỳ");
        Tt133Sheet.RowOf(ws, "Cộng cuối kỳ").Should().Be(opening + 1);
        Tt133Sheet.Number(ws, Tt133Sheet.RowOf(ws, "Tồn cuối kỳ"), 9).Should().Be(0m);
    }

    [Fact]
    public async Task S08_needs_no_cost_permission()
    {
        var ids = await SeedAsync();
        var client = await CreateClientWithPermissionsAsync("s08_nocost", null, Permissions.Reports.Inventory);

        var response = await PostAsync(client, Body(warehouseId: DefaultWarehouseId, productIds: [ids.Sm01]));

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Validation_and_permission()
    {
        var ids = await SeedAsync();

        var neither = await PostAsync(_client, Body());
        neither.StatusCode.Should().Be(HttpStatusCode.BadRequest, await neither.Content.ReadAsStringAsync());
        (await neither.Content.ReadFromJsonAsync<ApiResponse>(TestJson.Options))!.Error!.Details.Should().ContainKey("productIds");

        var noTo = Body(productIds: [ids.Sm01]);
        noTo.Remove("to");
        var missing = await PostAsync(_client, noTo);
        missing.StatusCode.Should().Be(HttpStatusCode.BadRequest, await missing.Content.ReadAsStringAsync());
        (await missing.Content.ReadFromJsonAsync<ApiResponse>(TestJson.Options))!.Error!.Details.Should().ContainKey("to");

        (await _client.GetAsync("/api/reports/stock-card/export?from=2026-10-10&to=2026-10-31"))
            .StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);

        (await PostAsync(_client, Body(warehouseId: DefaultWarehouseId, productIds: [Guid.NewGuid()])))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        var branchB = await CreateBranchAsync("S08B");
        var warehouseB = await CreateWarehouseAsync(branchB, "KHOB1");
        (await PostAsync(_client, Body(warehouseId: warehouseB, productIds: [ids.Sm01])))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var debtOnly = await CreateClientWithPermissionsAsync("s08_debt", null, Permissions.Reports.Debt);
        (await PostAsync(debtOnly, Body(warehouseId: DefaultWarehouseId, productIds: [ids.Sm01])))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
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
        client.PostAsJsonAsync("/api/reports/stock-card/export", body, TestJson.Options);

    private async Task<StockCardDto> GetCardAsync(Guid productId, Guid warehouseId) =>
        await ReadDataAsync<StockCardDto>(await _client.GetAsync(
            $"/api/reports/stock-card?productId={productId}&warehouseId={warehouseId}&from=2026-10-10&to=2026-10-31"));

    private record Seeded(Guid Sm01, Guid Sm03, Guid Kho02);

    /// Product SM01 with the data of the Phase 04 test table, a product SM03 without ledger rows and a second warehouse.
    private async Task<Seeded> SeedAsync()
    {
        await UpdateInventorySettingsAsync(s => s.CostingScope = CostingScope.Warehouse);
        var p = await CreateInventoryProductAsync("SM01");
        var sm03 = await CreateInventoryProductAsync("SM03");
        var second = await CreateWarehouseAsync(MainBranchId, "KHO02");
        await SaveOpeningAsync(p, new DateOnly(2026, 10, 1), 10, 1_000_000);
        await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-03 08:00",
            r => r.PartnerName = "NCC A", LineRequest(p, 10, 120_000));
        await CreateVoucherAsync(_client, StockDirection.Out, "XKH", "2026-10-12 08:00", LineRequest(p, 5, 0));
        await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-15 08:00", r => r.WarehouseId = second,
            LineRequest(p, 6, 100_000));
        await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-20 08:00", LineRequest(p, 4, 100_000));
        return new Seeded(p, sm03, second);
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
