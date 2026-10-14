using System.Net;
using System.Net.Http.Json;
using ClosedXML.Excel;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Inventory.Common;
using OrderMgmt.Application.Inventory.OpeningStocks.Models;
using OrderMgmt.Application.Inventory.Reports.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using OrderMgmt.IntegrationTests.Inventory;
using Xunit;

namespace OrderMgmt.IntegrationTests.Tt133;

/// S07-DNN: `GET api/reports/stock-summary/export`.
[Collection(nameof(PostgresCollection))]
public class StockSummaryExportTests : InventoryTestBase
{
    private const string Window = "from=2026-10-10&to=2026-10-31";

    public StockSummaryExportTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task S07_with_values()
    {
        var cost = await SeedAsync();

        var response = await _client.GetAsync($"/api/reports/stock-summary/export?{Window}");

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        response.Content.Headers.ContentDisposition!.FileName.Should().Be("S07-DNN_20261010_20261031.xlsx");
        using var wb = await Tt133Sheet.OpenAsync(response);
        var ws = wb.Worksheets.Should().ContainSingle().Subject;
        ws.Name.Should().Be("S07");
        Tt133Sheet.Contains(ws, "Mẫu số S07-DNN").Should().BeTrue();
        Tt133Sheet.Contains(ws, "BẢNG TỔNG HỢP CHI TIẾT VẬT LIỆU, DỤNG CỤ, SẢN PHẨM, HÀNG HÓA").Should().BeTrue();
        Tt133Sheet.Contains(ws, "Từ ngày 10/10/2026 đến ngày 31/10/2026").Should().BeTrue();
        Tt133Sheet.Contains(ws, "Kho: Tất cả kho").Should().BeTrue();
        ws.CellsUsed().Should().NotContain(c => c.GetString().StartsWith("Nhóm hàng:"));

        var row = Tt133Sheet.RowOf(ws, "SM01");
        ws.Cell(row, 1).GetValue<int>().Should().Be(1);
        ws.Cell(row, 3).GetString().Should().Be("Hàng SM01");
        var expected = new[] { 20m, 2_200_000m, 10m, 1_000_000m, 5m, cost, 25m, 3_200_000m - cost };
        for (var i = 0; i < expected.Length; i++)
            Tt133Sheet.Number(ws, row, 5 + i).Should().Be(expected[i], $"column {5 + i}");

        var report = await ReadDataAsync<StockSummaryReportDto>(
            await _client.GetAsync($"/api/reports/stock-summary?{Window}"));
        var total = Tt133Sheet.RowOf(ws, "Cộng");
        Tt133Sheet.Number(ws, total, 6).Should().Be(report.TotalOpeningValue!.Value);
        Tt133Sheet.Number(ws, total, 8).Should().Be(report.TotalInValue!.Value);
        Tt133Sheet.Number(ws, total, 10).Should().Be(report.TotalOutValue!.Value);
        Tt133Sheet.Number(ws, total, 12).Should().Be(report.TotalClosingValue!.Value);
        foreach (var column in new[] { 5, 7, 9, 11 })
            ws.Cell(total, column).IsEmpty().Should().BeTrue($"column {column}");

        ws.PageSetup.PageOrientation.Should().Be(XLPageOrientation.Landscape);
    }

    [Fact]
    public async Task S07_parameters()
    {
        await SeedAsync();
        var group = await InDbAsync(db => db.ProductGroups.Where(g => g.Code == "EPS").Select(g => new { g.Id, g.Name }).SingleAsync());
        var warehouseName = await InDbAsync(db => db.Warehouses.Where(w => w.Id == DefaultWarehouseId).Select(w => w.Name).SingleAsync());

        var filtered = await _client.GetAsync(
            $"/api/reports/stock-summary/export?{Window}&warehouseId={DefaultWarehouseId}&productGroupId={group.Id}");
        filtered.StatusCode.Should().Be(HttpStatusCode.OK, await filtered.Content.ReadAsStringAsync());
        using (var wb = await Tt133Sheet.OpenAsync(filtered))
        {
            var ws = wb.Worksheets.Single();
            Tt133Sheet.Contains(ws, $"Kho: KHO01 – {warehouseName}").Should().BeTrue();
            Tt133Sheet.Contains(ws, $"Nhóm hàng: {group.Name}").Should().BeTrue();
            ws.CellsUsed().Should().NotContain(c => c.GetString().StartsWith("Điều kiện lọc:"));
        }

        var searched = await _client.GetAsync($"/api/reports/stock-summary/export?{Window}&search=sm0");
        searched.StatusCode.Should().Be(HttpStatusCode.OK, await searched.Content.ReadAsStringAsync());
        using (var wb = await Tt133Sheet.OpenAsync(searched))
        {
            var ws = wb.Worksheets.Single();
            Tt133Sheet.Contains(ws, "Điều kiện lọc: mã hoặc tên chứa \"sm0\"").Should().BeTrue();
            Tt133Sheet.RowOf(ws, "SM01").Should().BeGreaterThan(0);
        }
    }

    [Fact]
    public async Task S07_without_cost_permission_has_quantities_only()
    {
        await SeedAsync();
        var client = await CreateClientWithPermissionsAsync("s07_nocost", null, Permissions.Reports.Inventory);

        var response = await client.GetAsync($"/api/reports/stock-summary/export?{Window}");

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        using var wb = await Tt133Sheet.OpenAsync(response);
        var ws = wb.Worksheets.Single();
        Tt133Sheet.Contains(ws, "Mẫu số S07-DNN").Should().BeTrue();
        ws.CellsUsed().Should().NotContain(c => c.GetString() == "Thành tiền");
        ws.CellsUsed().Should().NotContain(c => c.GetString() == "Cộng");

        var row = Tt133Sheet.RowOf(ws, "SM01");
        ws.Cell(row, 1).GetValue<int>().Should().Be(1);
        Tt133Sheet.Number(ws, row, 5).Should().Be(20m);
        Tt133Sheet.Number(ws, row, 6).Should().Be(10m);
        Tt133Sheet.Number(ws, row, 7).Should().Be(5m);
        Tt133Sheet.Number(ws, row, 8).Should().Be(25m);
        ws.LastColumnUsed()!.ColumnNumber().Should().Be(8);
    }

    [Fact]
    public async Task S07_note_for_provisional_cost()
    {
        await SeedAsync();

        var today = VnTime.ToVnDate(DateTimeOffset.UtcNow);
        var todayQuery = $"from={today.AddDays(-5):yyyy-MM-dd}&to={today:yyyy-MM-dd}";
        var response = await _client.GetAsync($"/api/reports/stock-summary/export?{todayQuery}");
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        using (var wb = await Tt133Sheet.OpenAsync(response))
            Tt133Sheet.Contains(wb.Worksheets.Single(), "Giá vốn tạm tính").Should().BeTrue();

        var report = await ReadDataAsync<StockSummaryReportDto>(
            await _client.GetAsync($"/api/reports/stock-summary?{Window}"));
        var october = await _client.GetAsync($"/api/reports/stock-summary/export?{Window}");
        using var octoberBook = await Tt133Sheet.OpenAsync(october);
        Tt133Sheet.Contains(octoberBook.Worksheets.Single(), "Giá vốn tạm tính").Should().Be(report.IsProvisional);
    }

    [Fact]
    public async Task Validation_and_permission()
    {
        await SeedAsync();

        (await _client.GetAsync("/api/reports/stock-summary/export?from=2026-10-31&to=2026-10-10"))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var noAccessAll = await CreateClientWithPermissionsAsync("s07_scope", null, Permissions.Reports.Inventory);
        (await noAccessAll.GetAsync($"/api/reports/stock-summary/export?{Window}&scope=Company"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var debtOnly = await CreateClientWithPermissionsAsync("s07_debt", null, Permissions.Reports.Debt);
        (await debtOnly.GetAsync($"/api/reports/stock-summary/export?{Window}"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// Product SM01 with the data of the Phase 04 test table; returns the cost of the stock-out.
    private async Task<decimal> SeedAsync()
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
        return (await LedgerOfAsync(issued.Id)).Single().CostAmount!.Value;
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
