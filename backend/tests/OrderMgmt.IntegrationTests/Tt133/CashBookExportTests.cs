using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ClosedXML.Excel;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using OrderMgmt.Application.CashDebt.Funds.Models;
using OrderMgmt.Application.CashDebt.Reports.Models;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Application.Identity.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.CashDebt;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.Tt133;

/// S04a-DNN and S05-DNN: `GET api/reports/cash-book/export`.
[Collection(nameof(PostgresCollection))]
public class CashBookExportTests : CashDebtTestBase
{
    private const string October = "from=2026-10-01&to=2026-10-31";
    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public CashBookExportTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task S04a_for_the_cash_fund()
    {
        var ids = await ReportDataset.SeedAsync(this);
        await InDbAsync(db => db.CompanyProfiles.Where(p => p.Id == 1).ExecuteUpdateAsync(s => s
            .SetProperty(p => p.Name, "Công ty ABC")
            .SetProperty(p => p.TaxCode, "0101234567")
            .SetProperty(p => p.ChiefAccountantName, "Trần B")
            .SetProperty(p => p.LegalRepresentativeName, "Nguyễn A")));
        var branchName = await InDbAsync(db => db.Branches.Where(b => b.Id == ids.MainBranchId).Select(b => b.Name).SingleAsync());
        var adminName = await InDbAsync(db => db.Users.Where(u => u.Username == "admin").Select(u => u.FullName).SingleAsync());
        var client = MainBranchClient(ids);

        var response = await client.GetAsync($"/api/reports/cash-book/export?kind=Cash&{October}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be(XlsxContentType);
        response.Content.Headers.ContentDisposition!.FileName.Should().Be("S04a-DNN_20261001_20261031.xlsx");

        using var wb = await Tt133Sheet.OpenAsync(response);
        var ws = wb.Worksheets.Should().ContainSingle().Subject;
        ws.Name.Should().Be("QTM");
        Tt133Sheet.Contains(ws, "Mẫu số S04a-DNN").Should().BeTrue();
        Tt133Sheet.Contains(ws, "SỔ QUỸ TIỀN MẶT").Should().BeTrue();
        Tt133Sheet.Contains(ws, "Tháng 10 năm 2026").Should().BeTrue();
        Tt133Sheet.Contains(ws, "Loại quỹ: Quỹ tiền mặt").Should().BeTrue();
        Tt133Sheet.Contains(ws, $"Đơn vị: Công ty ABC – {branchName}").Should().BeTrue();

        var opening = Tt133Sheet.RowOf(ws, "Số tồn đầu kỳ");
        Tt133Sheet.Number(ws, opening, 9).Should().Be(10_000m);

        var first = opening + 1;
        ws.Cell(first, 3).GetString().Should().Be("PT00001");
        ws.Cell(first + 1, 3).GetString().Should().Be("PT00002");
        ws.Cell(first + 2, 3).GetString().Should().Be("CQ00001");
        ws.Cell(first + 3, 4).GetString().Should().Be("PC00003");
        ws.Cell(first, 1).GetDateTime().Should().Be(new DateTime(2026, 10, 3));
        ws.Cell(first + 2, 6).GetString().Should().Be("Chuyển quỹ");
        ws.Cell(first + 2, 5).GetString().Should().NotBeEmpty();
        ws.Cell(first + 3, 5).GetString().Should().Be("Nguyễn A");
        ws.Cell(first + 3, 6).GetString().Should().Be("Chi khác");

        (decimal? In, decimal? Out, decimal Running)[] expected =
        [
            (1_000m, null, 11_000m), (4_000m, null, 15_000m), (5_000m, null, 20_000m), (null, 700m, 19_300m),
        ];
        for (var i = 0; i < expected.Length; i++)
        {
            var row = first + i;
            if (expected[i].In is { } received) Tt133Sheet.Number(ws, row, 7).Should().Be(received);
            else ws.Cell(row, 7).IsEmpty().Should().BeTrue();
            if (expected[i].Out is { } paid) Tt133Sheet.Number(ws, row, 8).Should().Be(paid);
            else ws.Cell(row, 8).IsEmpty().Should().BeTrue();
            Tt133Sheet.Number(ws, row, 9).Should().Be(expected[i].Running);
        }

        var total = Tt133Sheet.RowOf(ws, "Cộng số phát sinh trong kỳ");
        Tt133Sheet.Number(ws, total, 7).Should().Be(10_000m);
        Tt133Sheet.Number(ws, total, 8).Should().Be(700m);
        Tt133Sheet.Number(ws, Tt133Sheet.RowOf(ws, "Số tồn cuối kỳ"), 9).Should().Be(19_300m);

        Tt133Sheet.Contains(ws, "Kế toán trưởng").Should().BeTrue();
        Tt133Sheet.Contains(ws, "Trần B").Should().BeTrue();
        Tt133Sheet.Contains(ws, "Nguyễn A").Should().BeTrue();
        Tt133Sheet.Contains(ws, adminName).Should().BeTrue();
        Tt133Sheet.Contains(ws, "- Ngày mở sổ: 01/10/2026").Should().BeTrue();

        ws.PageSetup.PageOrientation.Should().Be(XLPageOrientation.Portrait);
        ws.PageSetup.PaperSize.Should().Be(XLPaperSize.A4Paper);
    }

    [Fact]
    public async Task File_equals_the_screen_report()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var client = MainBranchClient(ids);

        var screen = await ReadDataAsync<CashBookReportDto>(
            await client.GetAsync($"/api/reports/cash-book?kind=Cash&{October}"));
        var response = await client.GetAsync($"/api/reports/cash-book/export?kind=Cash&{October}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var wb = await Tt133Sheet.OpenAsync(response);
        var ws = wb.Worksheets.Should().ContainSingle().Subject;
        var group = screen.Groups.Should().ContainSingle().Subject;
        var opening = Tt133Sheet.RowOf(ws, "Số tồn đầu kỳ");
        var total = Tt133Sheet.RowOf(ws, "Cộng số phát sinh trong kỳ");

        Tt133Sheet.Number(ws, Tt133Sheet.RowOf(ws, "Số tồn cuối kỳ"), 9).Should().Be(group.Closing);
        (total - opening - 1).Should().Be(group.Rows.Count);
    }

    [Fact]
    public async Task S05_for_the_bank_funds()
    {
        var ids = await ReportDataset.SeedAsync(this);
        await InDbAsync(db => db.Funds.Where(f => f.Code == "NH01" && f.BranchId == ids.MainBranchId).ExecuteUpdateAsync(s => s
            .SetProperty(f => f.BankName, "Vietcombank")
            .SetProperty(f => f.AccountNumber, "0123456789")));
        await CreateBankFundAsync("NH02", ids.MainBranchId, opening: 7_000m);
        var client = MainBranchClient(ids);

        var response = await client.GetAsync($"/api/reports/cash-book/export?kind=Bank&{October}&format=xlsx");

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        response.Content.Headers.ContentDisposition!.FileName.Should().Be("S05-DNN_20261001_20261031.xlsx");
        using var wb = await Tt133Sheet.OpenAsync(response);
        wb.Worksheets.Select(w => w.Name).Should().Equal("NH01", "NH02");

        var ws = wb.Worksheet("NH01");
        Tt133Sheet.Contains(ws, "Mẫu số S05-DNN").Should().BeTrue();
        Tt133Sheet.Contains(ws, "SỔ TIỀN GỬI NGÂN HÀNG").Should().BeTrue();
        Tt133Sheet.Contains(ws, "Nơi mở tài khoản giao dịch: Vietcombank").Should().BeTrue();
        Tt133Sheet.Contains(ws, "Số hiệu tài khoản tại nơi gửi: 0123456789").Should().BeTrue();

        var opening = Tt133Sheet.RowOf(ws, "Số dư đầu kỳ");
        Tt133Sheet.Number(ws, opening, 8).Should().Be(20_000m);
        string[] codes = ["PC00001", "PC00002", "CQ00001"];
        decimal[] paid = [2_000m, 3_000m, 5_000m];
        decimal[] running = [18_000m, 15_000m, 10_000m];
        for (var i = 0; i < codes.Length; i++)
        {
            var row = opening + 1 + i;
            ws.Cell(row, 2).GetString().Should().Be(codes[i]);
            ws.Cell(row, 6).IsEmpty().Should().BeTrue();
            Tt133Sheet.Number(ws, row, 7).Should().Be(paid[i]);
            Tt133Sheet.Number(ws, row, 8).Should().Be(running[i]);
        }
        var total = Tt133Sheet.RowOf(ws, "Cộng số phát sinh trong kỳ");
        total.Should().Be(opening + 1 + codes.Length);
        Tt133Sheet.Number(ws, total, 6).Should().Be(0m);
        Tt133Sheet.Number(ws, total, 7).Should().Be(10_000m);
        Tt133Sheet.Number(ws, Tt133Sheet.RowOf(ws, "Số dư cuối kỳ"), 8).Should().Be(10_000m);

        var empty = wb.Worksheet("NH02");
        var emptyOpening = Tt133Sheet.RowOf(empty, "Số dư đầu kỳ");
        Tt133Sheet.Number(empty, emptyOpening, 8).Should().Be(7_000m);
        Tt133Sheet.RowOf(empty, "Cộng số phát sinh trong kỳ").Should().Be(emptyOpening + 1);
        Tt133Sheet.Number(empty, Tt133Sheet.RowOf(empty, "Số dư cuối kỳ"), 8).Should().Be(7_000m);
    }

    [Fact]
    public async Task One_fund_only()
    {
        var ids = await ReportDataset.SeedAsync(this);
        await CreateBankFundAsync("NH02", ids.MainBranchId, opening: 7_000m);
        var nh01 = await InDbAsync(db => db.Funds.Where(f => f.Code == "NH01" && f.BranchId == ids.MainBranchId).Select(f => f.Id).SingleAsync());
        var client = MainBranchClient(ids);

        var response = await client.GetAsync($"/api/reports/cash-book/export?kind=Bank&fundId={nh01}&{October}");

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        using var wb = await Tt133Sheet.OpenAsync(response);
        wb.Worksheets.Should().ContainSingle().Which.Name.Should().Be("NH01");
    }

    [Fact]
    public async Task Validation_permission_and_limits()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var client = MainBranchClient(ids);
        var nh01 = await InDbAsync(db => db.Funds.Where(f => f.Code == "NH01" && f.BranchId == ids.MainBranchId).Select(f => f.Id).SingleAsync());

        // The form needs one kind: All (the default) is refused.
        foreach (var query in new[] { $"kind=All&{October}", October })
        {
            var all = await client.GetAsync($"/api/reports/cash-book/export?{query}");
            all.StatusCode.Should().Be(HttpStatusCode.BadRequest, await all.Content.ReadAsStringAsync());
            (await all.Content.ReadFromJsonAsync<ApiResponse>(TestJson.Options))!.Error!.Details.Should().ContainKey("kind");
        }

        var inverted = await client.GetAsync("/api/reports/cash-book/export?kind=Cash&from=2026-10-31&to=2026-10-01");
        inverted.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await inverted.Content.ReadFromJsonAsync<ApiResponse>(TestJson.Options))!.Error!.Details.Should().ContainKey("to");

        var mismatch = await client.GetAsync($"/api/reports/cash-book/export?kind=Cash&fundId={nh01}&{October}");
        mismatch.StatusCode.Should().Be(HttpStatusCode.BadRequest, await mismatch.Content.ReadAsStringAsync());
        (await mismatch.Content.ReadFromJsonAsync<ApiResponse>(TestJson.Options))!.Error!.Details.Should().ContainKey("fundId");

        var debtOnly = await CreateClientWithPermissionsAsync("tt133_nc_" + Guid.NewGuid().ToString("N")[..8], ids.MainBranchId,
            Permissions.Reports.Debt);
        (await debtOnly.GetAsync($"/api/reports/cash-book/export?kind=Cash&{October}"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // A branch without a bank fund has nothing to export.
        var freshBranch = await CreateBranchAsync("X" + Guid.NewGuid().ToString("N")[..8]);
        var fresh = CloneAdminClient();
        UseBranch(fresh, freshBranch);
        var none = await fresh.GetAsync($"/api/reports/cash-book/export?kind=Bank&{October}");
        none.StatusCode.Should().Be((HttpStatusCode)422, await none.Content.ReadAsStringAsync());
        (await none.Content.ReadFromJsonAsync<ApiResponse>(TestJson.Options))!.Error!.Code.Should().Be("EXPORT_NO_TARGETS");

        // MaxSheets = 1 and two cash funds.
        var limited = new WebAppFactoryWithOneSheet(_pg);
        await ((IAsyncLifetime)limited).InitializeAsync();
        try
        {
            using var limitedClient = limited.CreateClient();
            var login = await limitedClient.PostAsJsonAsync("/api/auth/login",
                new LoginRequest { Username = "admin", Password = "Admin@123" });
            login.EnsureSuccessStatusCode();
            var body = await login.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(TestJson.Options);
            limitedClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.Data!.AccessToken);

            var create = await limitedClient.PostAsJsonAsync("/api/funds", new CreateFundRequest
            {
                Code = "QTM2", Name = "Quỹ QTM2", Kind = FundKind.Cash, IsDefault = false, IsActive = true,
            });
            create.EnsureSuccessStatusCode();

            var tooMany = await limitedClient.GetAsync($"/api/reports/cash-book/export?kind=Cash&{October}");
            tooMany.StatusCode.Should().Be((HttpStatusCode)422, await tooMany.Content.ReadAsStringAsync());
            (await tooMany.Content.ReadFromJsonAsync<ApiResponse>(TestJson.Options))!.Error!.Code.Should().Be("EXPORT_TOO_MANY_SHEETS");
        }
        finally
        {
            await ((IAsyncLifetime)limited).DisposeAsync();
        }
    }

    private HttpClient MainBranchClient(ReportIds ids)
    {
        var client = CloneAdminClient();
        UseBranch(client, ids.MainBranchId);
        return client;
    }
}

/// Tt133Export:MaxSheets = 1, so a second fund is one sheet too many.
file sealed class WebAppFactoryWithOneSheet : WebAppFactory
{
    public WebAppFactoryWithOneSheet(PostgresFixture pg) : base(pg) { }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { ["Tt133Export:MaxSheets"] = "1" }));
    }
}
