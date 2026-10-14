using System.Net;
using System.Net.Http.Json;
using ClosedXML.Excel;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.CashDebt.Reports.Models;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.CashDebt;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.Tt133;

/// S12-DNN: `POST api/reports/debt-ledger/export` with a JSON body.
[Collection(nameof(PostgresCollection))]
public class DebtLedgerExportTests : CashDebtTestBase
{
    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public DebtLedgerExportTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task S12_receivable_for_one_partner()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var client = MainBranchClient(ids);

        var response = await PostAsync(client, Body("Receivable", ids.Kh1Id));

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        response.Content.Headers.ContentType!.MediaType.Should().Be(XlsxContentType);
        response.Content.Headers.ContentDisposition!.FileName.Should().Be("S12-DNN_20261001_20261031.xlsx");

        using var wb = await Tt133Sheet.OpenAsync(response);
        var ws = wb.Worksheets.Should().ContainSingle().Subject;
        ws.Name.Should().Be(ids.Kh1Code[..Math.Min(31, ids.Kh1Code.Length)]);
        Tt133Sheet.Contains(ws, "Mẫu số S12-DNN").Should().BeTrue();
        Tt133Sheet.Contains(ws, "SỔ CHI TIẾT THANH TOÁN VỚI NGƯỜI MUA").Should().BeTrue();
        Tt133Sheet.Contains(ws, "Loại công nợ: Phải thu khách hàng").Should().BeTrue();
        Tt133Sheet.Contains(ws, $"Đối tượng: {ids.Kh1Code} – Đối tượng {ids.Kh1Code}").Should().BeTrue();
        Tt133Sheet.Contains(ws, "Loại tiền: VNĐ").Should().BeTrue();
        Tt133Sheet.Contains(ws, "- Ngày mở sổ: 01/10/2026").Should().BeTrue();

        var opening = Tt133Sheet.RowOf(ws, "Số dư đầu kỳ");
        Tt133Sheet.Number(ws, opening, 9).Should().Be(4_000m);
        ws.Cell(opening, 10).IsEmpty().Should().BeTrue();

        var first = opening + 1;
        string[] codes = ["PX00001", "PT00001", "PT00002", "PN00001"];
        for (var i = 0; i < codes.Length; i++)
            ws.Cell(first + i, 2).GetString().Should().Be(codes[i]);

        (decimal? Debit, decimal? Credit, decimal? BalanceDebit, decimal? BalanceCredit)[] expected =
        [
            (3_000m, null, 7_000m, null),
            (null, 1_000m, 6_000m, null),
            (null, 4_000m, 2_000m, null),
            (null, 500m, 1_500m, null),
        ];
        for (var i = 0; i < expected.Length; i++)
        {
            var row = first + i;
            AssertAmount(ws, row, 7, expected[i].Debit);
            AssertAmount(ws, row, 8, expected[i].Credit);
            AssertAmount(ws, row, 9, expected[i].BalanceDebit);
            AssertAmount(ws, row, 10, expected[i].BalanceCredit);
            ws.Cell(row, 4).GetString().Should().NotStartWith("[");
        }
        ws.Cell(first, 6).DataType.Should().Be(XLDataType.DateTime);
        ws.Cell(first, 6).IsEmpty().Should().BeFalse();
        for (var i = 1; i < codes.Length; i++)
            ws.Cell(first + i, 6).IsEmpty().Should().BeTrue();
        ws.Cell(first, 3).GetDateTime().Should().Be(new DateTime(2026, 10, 3));

        var total = Tt133Sheet.RowOf(ws, "Cộng số phát sinh");
        total.Should().Be(first + codes.Length);
        Tt133Sheet.Number(ws, total, 7).Should().Be(3_000m);
        Tt133Sheet.Number(ws, total, 8).Should().Be(5_500m);
        Tt133Sheet.Number(ws, Tt133Sheet.RowOf(ws, "Số dư cuối kỳ"), 9).Should().Be(1_500m);

        ws.PageSetup.PageOrientation.Should().Be(XLPageOrientation.Landscape);
    }

    [Fact]
    public async Task S12_payable_puts_increases_in_credit()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var client = MainBranchClient(ids);

        var response = await PostAsync(client, Body("Payable", ids.Ncc1Id));

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        using var wb = await Tt133Sheet.OpenAsync(response);
        var ws = wb.Worksheets.Should().ContainSingle().Subject;
        Tt133Sheet.Contains(ws, "SỔ CHI TIẾT THANH TOÁN VỚI NGƯỜI BÁN").Should().BeTrue();
        Tt133Sheet.Contains(ws, "Loại công nợ: Phải trả người bán").Should().BeTrue();

        // A zero opening balance is written as 0 in the natural column of the side (Có for a payable).
        var opening = Tt133Sheet.RowOf(ws, "Số dư đầu kỳ");
        ws.Cell(opening, 9).IsEmpty().Should().BeTrue();
        Tt133Sheet.Number(ws, opening, 10).Should().Be(0m);

        var first = opening + 1;
        string[] codes = ["PN00002", "PC00001", "PC00002"];
        (decimal? Debit, decimal? Credit, decimal? BalanceDebit, decimal? BalanceCredit)[] expected =
        [
            (null, 6_000m, null, 6_000m),
            (2_000m, null, null, 4_000m),
            (3_000m, null, null, 1_000m),
        ];
        for (var i = 0; i < codes.Length; i++)
        {
            var row = first + i;
            ws.Cell(row, 2).GetString().Should().Be(codes[i]);
            AssertAmount(ws, row, 7, expected[i].Debit);
            AssertAmount(ws, row, 8, expected[i].Credit);
            AssertAmount(ws, row, 9, expected[i].BalanceDebit);
            AssertAmount(ws, row, 10, expected[i].BalanceCredit);
        }
        Tt133Sheet.Number(ws, Tt133Sheet.RowOf(ws, "Số dư cuối kỳ"), 10).Should().Be(1_000m);
    }

    [Fact]
    public async Task S12_negative_balance_moves_to_the_other_column()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var client = MainBranchClient(ids);
        var kh3 = await CreatePartnerAsync("KH3" + ids.Kh1Code[3..], isCustomer: true, isSupplier: false);
        await CreateOpeningDebtAsync(client, kh3, DebtSide.Receivable, OpeningDebtKind.Prepayment, "TT-KH3",
            new DateOnly(2026, 9, 15), 800m);

        var response = await PostAsync(client, Body("Receivable", kh3));

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        using var wb = await Tt133Sheet.OpenAsync(response);
        var ws = wb.Worksheets.Should().ContainSingle().Subject;
        var opening = Tt133Sheet.RowOf(ws, "Số dư đầu kỳ");
        ws.Cell(opening, 9).IsEmpty().Should().BeTrue();
        Tt133Sheet.Number(ws, opening, 10).Should().Be(800m);
        var closing = Tt133Sheet.RowOf(ws, "Số dư cuối kỳ");
        ws.Cell(closing, 9).IsEmpty().Should().BeTrue();
        Tt133Sheet.Number(ws, closing, 10).Should().Be(800m);
    }

    [Fact]
    public async Task S12_company_scope()
    {
        var ids = await ReportDataset.SeedAsync(this);
        await InDbAsync(db => db.CompanyProfiles.Where(p => p.Id == 1).ExecuteUpdateAsync(s => s
            .SetProperty(p => p.Name, "Công ty ABC")));
        // Two branches with an opening date: the form prints the earliest (T7).
        await ReportDataset.SetOpeningDateAsync(this, ids.BranchBId, new DateOnly(2026, 9, 15));
        var codes = await InDbAsync(db => db.Branches.IgnoreQueryFilters().AsNoTracking()
            .Where(b => b.Id == ids.MainBranchId || b.Id == ids.BranchBId)
            .ToDictionaryAsync(b => b.Id, b => b.Code));
        var client = MainBranchClient(ids);

        var response = await PostAsync(client, Body("Receivable", ids.Kh1Id, scope: "Company"));

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        using var wb = await Tt133Sheet.OpenAsync(response);
        var ws = wb.Worksheets.Should().ContainSingle().Subject;
        Tt133Sheet.Contains(ws, "Đơn vị: Công ty ABC").Should().BeTrue();
        Tt133Sheet.Contains(ws, "- Ngày mở sổ: 15/09/2026").Should().BeTrue();

        var opening = Tt133Sheet.RowOf(ws, "Số dư đầu kỳ");
        var total = Tt133Sheet.RowOf(ws, "Cộng số phát sinh");
        (total - opening - 1).Should().Be(5);
        Tt133Sheet.Number(ws, total, 7).Should().Be(12_999m);
        Tt133Sheet.Number(ws, total, 8).Should().Be(5_500m);
        Tt133Sheet.Number(ws, Tt133Sheet.RowOf(ws, "Số dư cuối kỳ"), 9).Should().Be(11_499m);

        var sales = Enumerable.Range(opening + 1, 5)
            .Where(row => ws.Cell(row, 2).GetString() == "PX00001")
            .Select(row => ws.Cell(row, 4).GetString())
            .ToList();
        sales.Should().HaveCount(2);
        sales.Should().Contain(text => text.StartsWith($"[{codes[ids.MainBranchId]}] "));
        sales.Should().Contain(text => text.StartsWith($"[{codes[ids.BranchBId]}] "));
        Enumerable.Range(opening + 1, 5).Should().OnlyContain(row => ws.Cell(row, 4).GetString().StartsWith("["));
    }

    [Fact]
    public async Task S12_all_partners_with_activity()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var client = MainBranchClient(ids);
        var kh2 = await CreatePartnerAsync("KH2" + ids.Kh1Code[3..], isCustomer: true, isSupplier: false);
        await CreatePartnerAsync("KH9" + ids.Kh1Code[3..], isCustomer: true, isSupplier: false);
        await ReportDataset.PostStockAsync(this, client, StockDirection.Out, "XBH", "2026-10-20 09:00", ids.MainWarehouseId,
            kh2, 700m, paymentMethodId: null, paid: 0m);

        var response = await PostAsync(client, Raw("Receivable", all: true));

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        using var wb = await Tt133Sheet.OpenAsync(response);
        var names = wb.Worksheets.Select(w => w.Name).ToList();
        names.Should().Equal("KH1" + ids.Kh1Code[3..], "KH2" + ids.Kh1Code[3..]);

        var balance = await GetBalanceAsync(client, "Receivable");
        foreach (var ws in wb.Worksheets)
        {
            var row = balance.Rows.Single(r => r.PartnerCode == ws.Name);
            Tt133Sheet.Number(ws, Tt133Sheet.RowOf(ws, "Số dư cuối kỳ"), 9).Should().Be(row.ClosingDebit, ws.Name);
        }
        balance.Rows.Select(r => r.PartnerCode).Should().Equal(names);
    }

    [Fact]
    public async Task S12_several_partners()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var client = MainBranchClient(ids);
        var kh2 = await CreatePartnerAsync("KH2" + ids.Kh1Code[3..], isCustomer: true, isSupplier: false);
        await ReportDataset.PostStockAsync(this, client, StockDirection.Out, "XBH", "2026-10-20 09:00", ids.MainWarehouseId,
            kh2, 700m, paymentMethodId: null, paid: 0m);

        var response = await PostAsync(client, Raw("Receivable", partnerIds: [kh2, ids.Kh1Id]));
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        using (var wb = await Tt133Sheet.OpenAsync(response))
            wb.Worksheets.Select(w => w.Name).Should().Equal("KH1" + ids.Kh1Code[3..], "KH2" + ids.Kh1Code[3..]);

        var repeated = await PostAsync(client, Raw("Receivable", partnerIds: [ids.Kh1Id, ids.Kh1Id]));
        repeated.StatusCode.Should().Be(HttpStatusCode.OK, await repeated.Content.ReadAsStringAsync());
        using var wb2 = await Tt133Sheet.OpenAsync(repeated);
        wb2.Worksheets.Should().ContainSingle();
    }

    [Fact]
    public async Task Validation_permission_and_limits()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var client = MainBranchClient(ids);

        var neither = await PostAsync(client, Raw("Receivable"));
        neither.StatusCode.Should().Be(HttpStatusCode.BadRequest, await neither.Content.ReadAsStringAsync());
        (await neither.Content.ReadFromJsonAsync<ApiResponse>(TestJson.Options))!.Error!.Details.Should().ContainKey("partnerIds");

        // 150 unknown ids pass the validator: the answer is the 404 of the first unknown partner.
        var many = await PostAsync(client, Raw("Receivable", partnerIds: Enumerable.Range(0, 150).Select(_ => Guid.NewGuid()).ToArray()));
        many.StatusCode.Should().Be(HttpStatusCode.NotFound, await many.Content.ReadAsStringAsync());

        (await client.GetAsync("/api/reports/debt-ledger/export?side=Receivable&from=2026-10-01&to=2026-10-31"))
            .StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);

        var reversed = Raw("Receivable", partnerIds: [ids.Kh1Id]);
        reversed["from"] = "2026-10-31";
        reversed["to"] = "2026-10-01";
        var badRange = await PostAsync(client, reversed);
        badRange.StatusCode.Should().Be(HttpStatusCode.BadRequest, await badRange.Content.ReadAsStringAsync());
        (await badRange.Content.ReadFromJsonAsync<ApiResponse>(TestJson.Options))!.Error!.Details.Should().ContainKey("to");

        var unknown = await PostAsync(client, Raw("Receivable", partnerIds: [Guid.NewGuid()]));
        unknown.StatusCode.Should().Be(HttpStatusCode.NotFound, await unknown.Content.ReadAsStringAsync());

        var none = Raw("Payable", all: true);
        none["to"] = "2026-10-04";
        var noTargets = await PostAsync(client, none);
        noTargets.StatusCode.Should().Be((HttpStatusCode)422, await noTargets.Content.ReadAsStringAsync());
        (await noTargets.Content.ReadFromJsonAsync<ApiResponse>(TestJson.Options))!.Error!.Code.Should().Be("EXPORT_NO_TARGETS");

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var cashOnly = await CreateClientWithPermissionsAsync("tt133_s12c_" + suffix, ids.MainBranchId, Permissions.Reports.Cash);
        (await PostAsync(cashOnly, Raw("Receivable", partnerIds: [ids.Kh1Id]))).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var debtOnly = await CreateClientWithPermissionsAsync("tt133_s12d_" + suffix, ids.MainBranchId, Permissions.Reports.Debt);
        (await PostAsync(debtOnly, Raw("Receivable", partnerIds: [ids.Kh1Id], scope: "Company")))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static Dictionary<string, object?> Raw(string side, Guid[]? partnerIds = null, bool all = false, string? scope = null)
    {
        var body = new Dictionary<string, object?>
        {
            ["side"] = side,
            ["from"] = "2026-10-01",
            ["to"] = "2026-10-31",
            ["scope"] = scope ?? "Branch",
        };
        if (partnerIds is not null) body["partnerIds"] = partnerIds;
        if (all) body["all"] = true;
        return body;
    }

    private async Task<DebtBalanceReportDto> GetBalanceAsync(HttpClient client, string side)
    {
        return await ReadDataAsync<DebtBalanceReportDto>(
            await client.GetAsync($"/api/reports/debt-balance?side={side}&from=2026-10-01&to=2026-10-31"));
    }

    private static Dictionary<string, object?> Body(string side, Guid partnerId, string? scope = null) => new()
    {
        ["side"] = side,
        ["from"] = "2026-10-01",
        ["to"] = "2026-10-31",
        ["partnerIds"] = new[] { partnerId },
        ["scope"] = scope ?? "Branch",
    };

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, object body, string? format = null) =>
        client.PostAsJsonAsync("/api/reports/debt-ledger/export" + (format is null ? "" : "?format=" + format), body, TestJson.Options);

    /// null = an empty cell (a zero amount in the body of the book).
    private static void AssertAmount(IXLWorksheet ws, int row, int column, decimal? expected)
    {
        if (expected is { } value) Tt133Sheet.Number(ws, row, column).Should().Be(value, $"row {row} column {column}");
        else ws.Cell(row, column).IsEmpty().Should().BeTrue($"row {row} column {column}");
    }

    private HttpClient MainBranchClient(ReportIds ids)
    {
        var client = CloneAdminClient();
        UseBranch(client, ids.MainBranchId);
        return client;
    }
}
