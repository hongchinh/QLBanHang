using System.Net;
using System.Net.Http.Json;
using ClosedXML.Excel;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.CashDebt;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.Tt133;

/// Biên bản đối chiếu công nợ: `GET api/reports/debt-reconciliation/export`.
[Collection(nameof(PostgresCollection))]
public class DebtReconciliationExportTests : CashDebtTestBase
{
    private const string October = "from=2026-10-01&to=2026-10-31";

    public DebtReconciliationExportTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Customer_only_partner()
    {
        var ids = await ReportDataset.SeedAsync(this);
        await SetCompanyProfileAsync();
        var client = MainBranchClient(ids);

        var response = await client.GetAsync($"/api/reports/debt-reconciliation/export?partnerId={ids.Kh1Id}&{October}");

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        response.Content.Headers.ContentDisposition!.FileName.Should().Be($"DoiChieuCongNo_{ids.Kh1Code}_20261001_20261031.xlsx");
        using var wb = await Tt133Sheet.OpenAsync(response);
        var ws = wb.Worksheets.Should().ContainSingle().Subject;
        ws.Name.Should().Be("DoiChieu");
        Tt133Sheet.Contains(ws, "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM").Should().BeTrue();
        Tt133Sheet.Contains(ws, "BIÊN BẢN ĐỐI CHIẾU CÔNG NỢ").Should().BeTrue();
        Tt133Sheet.Contains(ws, "Từ ngày 01/10/2026 đến ngày 31/10/2026").Should().BeTrue();
        Tt133Sheet.Contains(ws, "BÊN A: Công ty ABC").Should().BeTrue();
        Tt133Sheet.Contains(ws, $"BÊN B: Đối tượng {ids.Kh1Code}").Should().BeTrue();
        Tt133Sheet.Contains(ws, "Đại diện: Nguyễn A").Should().BeTrue();

        Tt133Sheet.Contains(ws, "Công nợ phải thu (Bên B nợ Bên A)").Should().BeTrue();
        Tt133Sheet.Contains(ws, "Công nợ phải trả (Bên A nợ Bên B)").Should().BeFalse();

        var opening = Tt133Sheet.RowOf(ws, "Số dư đầu kỳ");
        Tt133Sheet.Number(ws, opening, 5).Should().Be(4_000m);
        var total = Tt133Sheet.RowOf(ws, "Cộng phát sinh");
        (total - opening - 1).Should().Be(4);
        Tt133Sheet.Number(ws, total, 5).Should().Be(3_000m);
        Tt133Sheet.Number(ws, total, 6).Should().Be(5_500m);
        var closing = Tt133Sheet.RowOf(ws, "Số dư cuối kỳ");
        Tt133Sheet.Number(ws, closing, 5).Should().Be(1_500m);

        ws.CellsUsed().Should().NotContain(c => c.GetString().StartsWith("Bù trừ:"));
        Tt133Sheet.Contains(ws,
            "Tính đến ngày 31/10/2026, Bên B còn nợ Bên A số tiền: 1.500 đồng (Bằng chữ: Một nghìn năm trăm đồng).")
            .Should().BeTrue();
        Tt133Sheet.Contains(ws, "ĐẠI DIỆN BÊN A").Should().BeTrue();
        Tt133Sheet.Contains(ws, "ĐẠI DIỆN BÊN B").Should().BeTrue();
        Tt133Sheet.Contains(ws, "Kế toán trưởng").Should().BeTrue();
        Tt133Sheet.Contains(ws, "Trần B").Should().BeTrue();
        ws.CellsUsed().Should().NotContain(c => c.GetString().Contains("Mẫu số") || c.GetString().Contains("Sổ này có"));
        ws.PageSetup.PageOrientation.Should().Be(XLPageOrientation.Portrait);
    }

    [Fact]
    public async Task Partner_on_both_sides()
    {
        var ids = await ReportDataset.SeedAsync(this);
        await SetCompanyProfileAsync();
        var client = MainBranchClient(ids);
        var both = await CreatePartnerAsync("BOTH" + ids.Kh1Code[3..], isCustomer: true, isSupplier: true);
        await ReportDataset.PostStockAsync(this, client, StockDirection.Out, "XBH", "2026-10-20 09:00",
            ids.MainWarehouseId, both, 1_000m, paymentMethodId: null, paid: 0m);
        await ReportDataset.PostStockAsync(this, client, StockDirection.In, "NMH", "2026-10-21 09:00",
            ids.MainWarehouseId, both, 400m, paymentMethodId: null, paid: 0m);

        var response = await client.GetAsync($"/api/reports/debt-reconciliation/export?partnerId={both}&{October}");

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        using var wb = await Tt133Sheet.OpenAsync(response);
        var ws = wb.Worksheets.Single();
        Tt133Sheet.Contains(ws, "Công nợ phải thu (Bên B nợ Bên A)").Should().BeTrue();
        Tt133Sheet.Contains(ws, "Công nợ phải trả (Bên A nợ Bên B)").Should().BeTrue();
        Tt133Sheet.Contains(ws, "Bù trừ: Bên B còn nợ Bên A 600 đồng").Should().BeTrue();
        Tt133Sheet.Contains(ws,
            "Tính đến ngày 31/10/2026, Bên B còn nợ Bên A số tiền: 600 đồng (Bằng chữ: Sáu trăm đồng).")
            .Should().BeTrue();
    }

    [Fact]
    public async Task We_owe_the_partner()
    {
        var ids = await ReportDataset.SeedAsync(this);
        await SetCompanyProfileAsync();
        var client = MainBranchClient(ids);

        var response = await client.GetAsync($"/api/reports/debt-reconciliation/export?partnerId={ids.Ncc1Id}&{October}");

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        using var wb = await Tt133Sheet.OpenAsync(response);
        var ws = wb.Worksheets.Single();
        Tt133Sheet.Contains(ws, "Công nợ phải trả (Bên A nợ Bên B)").Should().BeTrue();
        Tt133Sheet.Contains(ws, "Công nợ phải thu (Bên B nợ Bên A)").Should().BeFalse();
        Tt133Sheet.Contains(ws,
            "Tính đến ngày 31/10/2026, Bên A còn nợ Bên B số tiền: 1.000 đồng (Bằng chữ: Một nghìn đồng).")
            .Should().BeTrue();
    }

    [Fact]
    public async Task Nothing_owed()
    {
        var ids = await ReportDataset.SeedAsync(this);
        await SetCompanyProfileAsync();
        var client = MainBranchClient(ids);

        var response = await client.GetAsync(
            $"/api/reports/debt-reconciliation/export?partnerId={ids.Ncc1Id}&from=2026-10-01&to=2026-10-04");

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        using var wb = await Tt133Sheet.OpenAsync(response);
        Tt133Sheet.Contains(wb.Worksheets.Single(), "Tính đến ngày 04/10/2026, hai bên không còn công nợ.").Should().BeTrue();
    }

    [Fact]
    public async Task Company_scope()
    {
        var ids = await ReportDataset.SeedAsync(this);
        await SetCompanyProfileAsync();
        var codes = await InDbAsync(db => db.Branches.IgnoreQueryFilters().AsNoTracking()
            .Where(b => b.Id == ids.MainBranchId || b.Id == ids.BranchBId)
            .ToDictionaryAsync(b => b.Id, b => b.Code));
        var client = MainBranchClient(ids);

        var response = await client.GetAsync(
            $"/api/reports/debt-reconciliation/export?partnerId={ids.Kh1Id}&{October}&scope=Company");

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        using var wb = await Tt133Sheet.OpenAsync(response);
        var ws = wb.Worksheets.Single();
        var opening = Tt133Sheet.RowOf(ws, "Số dư đầu kỳ");
        var total = Tt133Sheet.RowOf(ws, "Cộng phát sinh");
        var sales = Enumerable.Range(opening + 1, total - opening - 1)
            .Where(row => ws.Cell(row, 3).GetString() == "PX00001")
            .Select(row => ws.Cell(row, 4).GetString())
            .ToList();
        sales.Should().HaveCount(2);
        sales.Should().Contain(text => text.StartsWith($"[{codes[ids.MainBranchId]}] "));
        sales.Should().Contain(text => text.StartsWith($"[{codes[ids.BranchBId]}] "));
        Tt133Sheet.Number(ws, Tt133Sheet.RowOf(ws, "Số dư cuối kỳ"), 5).Should().Be(11_499m);
        Tt133Sheet.Contains(ws,
            "Tính đến ngày 31/10/2026, Bên B còn nợ Bên A số tiền: 11.499 đồng (Bằng chữ: Mười một nghìn bốn trăm chín mươi chín đồng).")
            .Should().BeTrue();
    }

    [Fact]
    public async Task Validation_and_permission()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var client = MainBranchClient(ids);

        var missing = await client.GetAsync($"/api/reports/debt-reconciliation/export?{October}");
        missing.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await missing.Content.ReadFromJsonAsync<ApiResponse>(TestJson.Options))!.Error!.Details.Should().ContainKey("partnerId");

        (await client.GetAsync($"/api/reports/debt-reconciliation/export?partnerId={Guid.NewGuid()}&{October}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        var cashOnly = await CreateClientWithPermissionsAsync("tt133_rec_" + Guid.NewGuid().ToString("N")[..8], ids.MainBranchId,
            Permissions.Reports.Cash);
        (await cashOnly.GetAsync($"/api/reports/debt-reconciliation/export?partnerId={ids.Kh1Id}&{October}"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private Task SetCompanyProfileAsync() =>
        InDbAsync(db => db.CompanyProfiles.Where(p => p.Id == 1).ExecuteUpdateAsync(s => s
            .SetProperty(p => p.Name, "Công ty ABC")
            .SetProperty(p => p.Address, "12 Lê Lợi")
            .SetProperty(p => p.TaxCode, "0101234567")
            .SetProperty(p => p.LegalRepresentativeName, "Nguyễn A")
            .SetProperty(p => p.ChiefAccountantName, "Trần B")));

    private HttpClient MainBranchClient(ReportIds ids)
    {
        var client = CloneAdminClient();
        UseBranch(client, ids.MainBranchId);
        return client;
    }
}
