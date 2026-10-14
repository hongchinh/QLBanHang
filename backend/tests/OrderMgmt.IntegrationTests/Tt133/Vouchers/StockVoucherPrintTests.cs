using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using ClosedXML.Excel;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Common;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Application.Inventory.Common;
using OrderMgmt.Application.Inventory.Reports.Models;
using OrderMgmt.Application.Inventory.StockVouchers.Models;
using OrderMgmt.Application.Tt133.Vouchers;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using OrderMgmt.IntegrationTests.Inventory;
using Xunit;

namespace OrderMgmt.IntegrationTests.Tt133.Vouchers;

/// `GET api/stock-vouchers/{id}/print`: the 01-VT stock-in and the 02-VT stock-out.
[Collection(nameof(PostgresCollection))]
public class StockVoucherPrintTests : InventoryTestBase
{
    private static readonly string Dots = VoucherPrintRules.Dots;

    public StockVoucherPrintTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Prints_a_stock_in_with_its_totals()
    {
        await SetCompanyAsync();
        var (v, supplierCode, aCode) = await StockInWithTotalsAsync();
        var kho01 = await WarehouseNameAsync(DefaultWarehouseId);

        var response = await PrintAsync(_client, v.Id);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentDisposition!.FileName.Should().Be($"{v.Code}.xlsx");
        using var wb = await Tt133Sheet.OpenAsync(response);
        var ws = wb.Worksheets.Should().ContainSingle().Subject;
        Texts(ws).Should().Contain(new[]
        {
            "Mẫu số 01-VT", "PHIẾU NHẬP KHO", "- Họ và tên người giao: Trần Giao",
            $"- Theo {Dots} số {Dots} ngày {Dots} của Đối tượng {supplierCode}",
            $"- Nhập tại kho: {kho01} Địa điểm: {Dots}",
            $"- Tổng số tiền (viết bằng chữ): {VndAmountInWords.Read(v.Total)}",
            v.OwnerName!,
        });
        Tt133Sheet.RowOf(ws, v.OwnerName!).Should().BeGreaterThan(Tt133Sheet.RowOf(ws, "Người lập phiếu"));

        var row = ws.Row(Tt133Sheet.RowOf(ws, $"Hàng {aCode}"));
        row.Cell(4).GetString().Should().Be("Tấm");
        row.Cell(5).GetValue<decimal>().Should().Be(10);
        row.Cell(6).GetValue<decimal>().Should().Be(10);
        row.Cell(7).GetValue<decimal>().Should().Be(100_000);
        row.Cell(8).GetValue<decimal>().Should().Be(1_000_000);

        var vat = v.Total - v.Freight - (v.GoodsAmount - v.LineDiscountTotal - v.OrderDiscount);
        Amount(ws, "Cộng tiền hàng").Should().Be(v.GoodsAmount).And.Be(1_500_000);
        Amount(ws, "Chiết khấu").Should().Be(v.LineDiscountTotal + v.OrderDiscount).And.Be(60_000);
        Amount(ws, "Thuế GTGT").Should().Be(vat).And.Be(144_000);
        Amount(ws, "Cước vận chuyển").Should().Be(v.Freight).And.Be(20_000);
        Amount(ws, "Tổng cộng").Should().Be(v.Total).And.Be(1_604_000);
    }

    [Fact]
    public async Task Prints_a_stock_out_as_02_VT()
    {
        await SetCompanyAsync();
        var a = await CreateInventoryProductAsync(NewCode("A"));
        await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-01 08:00", LineRequest(a, 10, 100_000));
        var customerCode = NewCode("KH");
        var customer = await CreatePartnerAsync(customerCode, true, false);
        var v = await CreateVoucherAsync(_client, StockDirection.Out, "XBH", "2026-10-05 09:00", r =>
        {
            r.PartnerId = customer;
            r.HandlerName = null;
        }, LineRequest(a, 2, 150_000));

        var response = await PrintAsync(_client, v.Id);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var wb = await Tt133Sheet.OpenAsync(response);
        var ws = wb.Worksheets.Single();
        Texts(ws).Should().Contain(new[]
        {
            "Mẫu số 02-VT", "PHIẾU XUẤT KHO", $"- Họ và tên người nhận hàng: Đối tượng {customerCode}",
            "Địa chỉ (bộ phận): Hà Nội", "- Lý do xuất kho: Xuất bán hàng",
        });
        Texts(ws).Should().ContainSingle(t => t == "Cộng");
        Amount(ws, "Cộng").Should().Be(300_000);
    }

    [Fact]
    public async Task Spec_and_several_warehouses()
    {
        await SetCompanyAsync();
        var secondCode = NewCode("KH");
        var second = await CreateWarehouseAsync(MainBranchId, secondCode);
        var aCode = NewCode("A");
        var areaCode = NewCode("K");
        var area = await CreateInventoryProductAsync(areaCode, PricingMode.PerSquareMeter);
        var a = await CreateInventoryProductAsync(aCode);
        var la = LineRequest(a, 3, 100_000);
        la.WarehouseId = second;
        var v = await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-05 09:00",
            new UpsertStockVoucherLineRequest { ProductId = area, SheetCount = 10, Length = 1200, Width = 2400, UnitPrice = 50_000 },
            la);
        var kho01 = await WarehouseNameAsync(DefaultWarehouseId);
        var secondName = await WarehouseNameAsync(second);

        var response = await PrintAsync(_client, v.Id);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var wb = await Tt133Sheet.OpenAsync(response);
        var ws = wb.Worksheets.Single();
        Texts(ws).Should().Contain(new[] { "Kho", $"- Nhập tại kho: {kho01}, {secondName} Địa điểm: {Dots}" });
        var row = ws.Row(Tt133Sheet.RowOf(ws, $"Hàng {areaCode} (1200×2400 mm, 10 tấm)"));
        row.Cell(4).GetString().Should().Be("m²");
        row.Cell(5).GetString().Should().Be("KHO01");
        row.Cell(6).GetValue<decimal>().Should().Be(28.8m);
        row.Cell(7).GetValue<decimal>().Should().Be(28.8m);
        ws.Row(Tt133Sheet.RowOf(ws, $"Hàng {aCode}")).Cell(5).GetString().Should().Be(secondCode);
        var headerRow = Tt133Sheet.RowOf(ws, "Kho");
        Tt133Sheet.ColumnOf(ws, headerRow, "Kho").Should().Be(5);
        Tt133Sheet.ColumnOf(ws, headerRow, "Đơn vị tính").Should().Be(4);
    }

    [Fact]
    public async Task Removed_lines_and_deleted_names()
    {
        await SetCompanyAsync();
        var whCode = NewCode("KW");
        var w = await CreateWarehouseAsync(MainBranchId, whCode);
        var aCode = NewCode("A");
        var bCode = NewCode("B");
        var a = await CreateInventoryProductAsync(aCode);
        var b = await CreateInventoryProductAsync(bCode);
        var v = await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-05 09:00", r => r.WarehouseId = w,
            LineRequest(a, 1, 100_000), LineRequest(b, 2, 100_000));
        var update = UpdateRequestFrom(v);
        update.Lines.RemoveAt(1);
        var (status, _, error) = await PutVoucherAsync(_client, v.Id, update);
        status.Should().Be(HttpStatusCode.OK, error?.Message);
        var wName = await WarehouseNameAsync(w);
        wName.Should().Be($"Kho {whCode}");
        await InDbAsync(db => db.Warehouses.Where(x => x.Id == w)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsDeleted, true)));

        var response = await PrintAsync(_client, v.Id);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var wb = await Tt133Sheet.OpenAsync(response);
        var ws = wb.Worksheets.Single();
        Tt133Sheet.Contains(ws, $"Hàng {aCode}").Should().BeTrue();
        Tt133Sheet.Contains(ws, $"Hàng {bCode}").Should().BeFalse();
        Texts(ws).Should().Contain($"- Nhập tại kho: {wName} Địa điểm: {Dots}");
    }

    [Fact]
    public async Task Cancelled_voucher_prints_ĐÃ_HỦY()
    {
        await SetCompanyAsync();
        var aCode = NewCode("A");
        var a = await CreateInventoryProductAsync(aCode);
        var v = await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-05 09:00", LineRequest(a, 4, 120_000));
        await ActionAsync(v.Id, "cancel", v.Version);

        var response = await PrintAsync(_client, v.Id);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var wb = await Tt133Sheet.OpenAsync(response);
        var ws = wb.Worksheets.Single();
        Texts(ws).Should().Contain("ĐÃ HỦY");
        var row = ws.Row(Tt133Sheet.RowOf(ws, $"Hàng {aCode}"));
        row.Cell(7).GetValue<decimal>().Should().Be(120_000);
        row.Cell(8).GetValue<decimal>().Should().Be(480_000);
        Amount(ws, "Cộng").Should().Be(480_000);
    }

    [Fact]
    public async Task Vat_inside_the_price_of_a_sale()
    {
        await SetCompanyAsync();
        var p = await CreateInventoryProductAsync(NewCode("V"), priceIncludesVat: true);
        await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-01 08:00", LineRequest(p, 10, 500_000));
        var customer = await CreatePartnerAsync(NewCode("KH"), true, false);
        var line = LineRequest(p, 2, 1_100_000);
        line.VatRate = 10;
        var v = await CreateVoucherAsync(_client, StockDirection.Out, "XBH", "2026-10-05 09:00",
            r => r.PartnerId = customer, line);
        v.Lines.Single().VatAmount.Should().Be(200_000);

        var response = await PrintAsync(_client, v.Id);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var wb = await Tt133Sheet.OpenAsync(response);
        var ws = wb.Worksheets.Single();
        Texts(ws).Should().ContainSingle(t => t == "Cộng");
        Amount(ws, "Cộng").Should().Be(v.Total).And.Be(2_200_000);
        Tt133Sheet.Contains(ws, "Thuế GTGT").Should().BeFalse();
        Tt133Sheet.RowOf(ws, "Trong đó thuế GTGT đã gồm trong giá: 200.000")
            .Should().BeGreaterThan(Tt133Sheet.RowOf(ws, "Cộng"));
    }

    [Fact]
    public async Task Vat_left_out_of_the_total()
    {
        await SetCompanyAsync();
        var a = await CreateInventoryProductAsync(NewCode("A"));
        await UpdateInventorySettingsAsync(s => s.NetExcludesVat = true);
        StockVoucherDto v;
        try
        {
            var line = LineRequest(a, 10, 100_000);
            line.VatRate = 10;
            v = await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-05 09:00", line);
            v.VatTotal.Should().Be(100_000);
            v.Total.Should().Be(1_000_000);
            await AssertOneTotalRowAsync(v.Id, 1_000_000);
        }
        finally
        {
            await UpdateInventorySettingsAsync(s => s.NetExcludesVat = false);
        }

        // The print reads the stored totals, not the current setting.
        await AssertOneTotalRowAsync(v.Id, 1_000_000);
    }

    [Fact]
    public async Task Rows_add_up()
    {
        await SetCompanyAsync();
        var (stockIn, _, _) = await StockInWithTotalsAsync();

        var p = await CreateInventoryProductAsync(NewCode("P"));
        await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-01 08:00", LineRequest(p, 10, 100_000));
        var customer = await CreatePartnerAsync(NewCode("KH"), true, false);
        var line = LineRequest(p, 3, 150_000);
        line.DiscountRate = 10;
        line.VatRate = 8;
        var stockOut = await CreateVoucherAsync(_client, StockDirection.Out, "XBH", "2026-10-05 09:00", r =>
        {
            r.PartnerId = customer;
            r.OrderDiscount = 5_000;
            r.Freight = 30_000;
        }, line);

        foreach (var v in new[] { stockIn, stockOut })
        {
            var response = await PrintAsync(_client, v.Id);
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            using var wb = await Tt133Sheet.OpenAsync(response);
            var ws = wb.Worksheets.Single();
            decimal Row(string label) => Tt133Sheet.Contains(ws, label) ? Amount(ws, label) : 0;
            var sum = Row("Cộng tiền hàng") - Row("Chiết khấu") + Row("Thuế GTGT") + Row("Cước vận chuyển");
            sum.Should().Be(Amount(ws, "Tổng cộng"), v.Code);
            Amount(ws, "Tổng cộng").Should().Be(v.Total, v.Code);
        }
    }

    private const string ProvisionalNote = "Giá vốn tạm tính (kỳ tính giá vốn chưa kết thúc).";

    [Fact]
    public async Task Cost_of_a_stock_in_is_its_ledger_value()
    {
        await SetCompanyAsync();
        var supplier = await CreatePartnerAsync(NewCode("NCC"), false, true);
        var aCode = NewCode("A");
        var a = await CreateInventoryProductAsync(aCode);
        var uCode = NewCode("U");
        var u = await CreateInventoryProductAsync(uCode, track: false);
        var la = LineRequest(a, 10, 100_000);
        la.VatRate = 10;
        var v = await CreateVoucherAsync(_client, StockDirection.In, "NMH", "2026-10-05 09:00",
            r => r.PartnerId = supplier, la, LineRequest(u, 1, 50_000));
        var ledger = (await LedgerOfAsync(v.Id)).Single();
        ledger.InValue.Should().Be(1_100_000);
        var inbound = await InDbAsync(db => db.StockVoucherLines
            .Where(l => l.StockVoucherId == v.Id && l.ProductId == a).Select(l => l.InboundValue).SingleAsync());
        inbound.Should().Be(1_000_000);

        var response = await PrintAsync(_client, v.Id, "format=xlsx&priceMode=Cost");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var wb = await Tt133Sheet.OpenAsync(response);
        var ws = wb.Worksheets.Single();
        Tt133Sheet.Contains(ws, $"Hàng {uCode}").Should().BeFalse();
        var row = ws.Row(Tt133Sheet.RowOf(ws, $"Hàng {aCode}"));
        row.Cell(1).GetValue<decimal>().Should().Be(1);
        row.Cell(7).GetValue<decimal>().Should().Be(110_000);
        row.Cell(8).GetValue<decimal>().Should().Be(ledger.InValue);
        Texts(ws).Should().ContainSingle(t => t == "Cộng");
        Amount(ws, "Cộng").Should().Be(1_100_000);
        Texts(ws).Should().Contain($"- Tổng số tiền (viết bằng chữ): {VndAmountInWords.Read(1_100_000)}");
        Tt133Sheet.Contains(ws, ProvisionalNote).Should().BeFalse();
    }

    [Fact]
    public async Task Cost_of_a_stock_out_is_its_ledger_cost()
    {
        await SetCompanyAsync();
        var aCode = NewCode("A");
        var a = await CreateInventoryProductAsync(aCode);
        await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-09-02 08:00", LineRequest(a, 10, 100_000));
        var stockOut = await CreateVoucherAsync(_client, StockDirection.Out, "XKH", "2026-09-10 09:00",
            LineRequest(a, 4, 150_000));
        var ledger = (await LedgerOfAsync(stockOut.Id)).Single();
        var card = await ReadDataAsync<StockCardDto>(await _client.GetAsync(
            $"/api/reports/stock-card?productId={a}&warehouseId={DefaultWarehouseId}&from=2026-09-01&to=2026-09-30"));
        var cardRow = card.Rows.Single(r => r.SourceId == stockOut.Id);

        var response = await PrintAsync(_client, stockOut.Id, "format=xlsx&priceMode=Cost");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var wb = await Tt133Sheet.OpenAsync(response);
        var ws = wb.Worksheets.Single();
        var row = ws.Row(Tt133Sheet.RowOf(ws, $"Hàng {aCode}"));
        row.Cell(7).GetValue<decimal>().Should().Be(ledger.UnitCost!.Value).And.Be(cardRow.UnitCost!.Value);
        row.Cell(8).GetValue<decimal>().Should().Be(ledger.CostAmount!.Value).And.Be(cardRow.CostAmount!.Value);
        Amount(ws, "Cộng").Should().Be(ledger.CostAmount!.Value);
        Tt133Sheet.Contains(ws, ProvisionalNote).Should().BeFalse();
    }

    [Fact]
    public async Task Current_period_cost_is_provisional()
    {
        await SetCompanyAsync();
        var today = VnTime.ToVnDate(DateTimeOffset.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var a = await CreateInventoryProductAsync(NewCode("A"));
        var stockIn = await CreateVoucherAsync(_client, StockDirection.In, "NKH", $"{today} 00:01", LineRequest(a, 10, 100_000));
        var stockOut = await CreateVoucherAsync(_client, StockDirection.Out, "XKH", $"{today} 00:02", LineRequest(a, 2, 0));

        var outResponse = await PrintAsync(_client, stockOut.Id, "format=xlsx&priceMode=Cost");
        var inResponse = await PrintAsync(_client, stockIn.Id, "format=xlsx&priceMode=Cost");

        outResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        using (var wb = await Tt133Sheet.OpenAsync(outResponse))
            Tt133Sheet.Contains(wb.Worksheets.Single(), ProvisionalNote).Should().BeTrue();
        inResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        using (var wb = await Tt133Sheet.OpenAsync(inResponse))
            Tt133Sheet.Contains(wb.Worksheets.Single(), ProvisionalNote).Should().BeFalse();
    }

    [Fact]
    public async Task Cost_needs_the_cost_permission()
    {
        var a = await CreateInventoryProductAsync(NewCode("A"));
        await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-01 08:00", LineRequest(a, 10, 100_000));
        var stockOut = await CreateVoucherAsync(_client, StockDirection.Out, "XKH", "2026-10-05 09:00", LineRequest(a, 2, 0));
        var client = await CreateClientWithPermissionsAsync("vpc_" + Guid.NewGuid().ToString("N")[..8], MainBranchId,
            Permissions.StockOut.View);

        var cost = await PrintAsync(client, stockOut.Id, "format=xlsx&priceMode=Cost");
        var voucher = await PrintAsync(client, stockOut.Id, "format=xlsx&priceMode=Voucher");

        cost.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var error = await cost.Content.ReadFromJsonAsync<ApiResponse>(TestJson.Options);
        error!.Error!.Message.Should().Be("Cần quyền xem giá vốn để in phiếu theo giá vốn.");
        voucher.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Vat_included_line_only_for_a_stock_out_at_voucher_prices()
    {
        await SetCompanyAsync();
        var p = await CreateInventoryProductAsync(NewCode("V"), priceIncludesVat: true);
        var inLine = LineRequest(p, 10, 550_000);
        inLine.VatRate = 10;
        var stockIn = await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-01 08:00", inLine);
        stockIn.Lines.Single().VatAmount.Should().BeGreaterThan(0);
        var customer = await CreatePartnerAsync(NewCode("KH"), true, false);
        var outLine = LineRequest(p, 2, 1_100_000);
        outLine.VatRate = 10;
        var stockOut = await CreateVoucherAsync(_client, StockDirection.Out, "XBH", "2026-10-05 09:00",
            r => r.PartnerId = customer, outLine);

        foreach (var (id, query) in new[]
                 {
                     (stockIn.Id, "format=xlsx"),               // a stock-in never gets the line
                     (stockOut.Id, "format=xlsx&priceMode=Cost"), // nor a print at cost
                 })
        {
            var response = await PrintAsync(_client, id, query);
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            using var wb = await Tt133Sheet.OpenAsync(response);
            Texts(wb.Worksheets.Single()).Should().NotContain(t => t.StartsWith("Trong đó thuế GTGT"), query);
        }
    }

    /// The stock-in of `Prints_a_stock_in_with_its_totals`: line discount, order discount, VAT and freight.
    private async Task<(StockVoucherDto Voucher, string SupplierCode, string ACode)> StockInWithTotalsAsync()
    {
        var supplierCode = NewCode("NCC");
        var supplier = await CreatePartnerAsync(supplierCode, false, true);
        var aCode = NewCode("A");
        var a = await CreateInventoryProductAsync(aCode);
        var b = await CreateInventoryProductAsync(NewCode("B"));
        var la = LineRequest(a, 10, 100_000);
        la.DiscountRate = 5;
        la.VatRate = 10;
        var lb = LineRequest(b, 5, 100_000);
        lb.VatRate = 10;
        var v = await CreateVoucherAsync(_client, StockDirection.In, "NMH", "2026-10-05 09:00", r =>
        {
            r.PartnerId = supplier;
            r.HandlerName = "Trần Giao";
            r.OrderDiscount = 10_000;
            r.Freight = 20_000;
        }, la, lb);
        return (v, supplierCode, aCode);
    }

    /// One "Cộng" row with the given amount, no VAT row and no VAT-included line.
    private async Task AssertOneTotalRowAsync(Guid id, decimal amount)
    {
        var response = await PrintAsync(_client, id);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var wb = await Tt133Sheet.OpenAsync(response);
        var ws = wb.Worksheets.Single();
        Texts(ws).Should().ContainSingle(t => t == "Cộng");
        Amount(ws, "Cộng").Should().Be(amount);
        Tt133Sheet.Contains(ws, "Thuế GTGT").Should().BeFalse();
        Texts(ws).Should().NotContain(t => t.StartsWith("Trong đó thuế GTGT"));
    }

    private Task SetCompanyAsync() =>
        InDbAsync(db => db.CompanyProfiles.Where(p => p.Id == 1).ExecuteUpdateAsync(s => s
            .SetProperty(p => p.Name, "Công ty ABC")
            .SetProperty(p => p.Address, "1 Láng Hạ")
            .SetProperty(p => p.ChiefAccountantName, "Trần B")
            .SetProperty(p => p.LegalRepresentativeName, "Nguyễn A")));

    private static Task<HttpResponseMessage> PrintAsync(HttpClient client, Guid id, string query = "format=xlsx") =>
        client.GetAsync($"/api/stock-vouchers/{id}/print?{query}");

    private Task<string> WarehouseNameAsync(Guid id) =>
        InDbAsync(db => db.Warehouses.IgnoreQueryFilters().Where(w => w.Id == id).Select(w => w.Name).SingleAsync());

    private async Task<StockVoucherDto> ActionAsync(Guid id, string action, uint version)
    {
        var (status, voucher, error) = await ReadVoucherResponseAsync(await _client.PostAsJsonAsync(
            $"/api/stock-vouchers/{id}/{action}", new StockVoucherActionRequest { Version = version }));
        status.Should().Be(HttpStatusCode.OK, error?.Message);
        return voucher!;
    }

    private static string NewCode(string prefix) => prefix + Guid.NewGuid().ToString("N")[..8];

    private static List<string> Texts(IXLWorksheet ws) => ws.CellsUsed().Select(c => c.GetString()).ToList();

    /// The amount of a row under the table: the last column of the form.
    private static decimal Amount(IXLWorksheet ws, string label) =>
        Tt133Sheet.Number(ws, Tt133Sheet.RowOf(ws, label), ws.LastColumnUsed()!.ColumnNumber());
}
