using System.Net;
using System.Net.Http.Json;
using ClosedXML.Excel;
using FluentAssertions;
using OrderMgmt.Application.CashDebt.Common;
using OrderMgmt.Application.CashDebt.Reports.Models;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.CashDebt;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.Tt133;

/// Bảng tổng hợp chi tiết thanh toán: `GET api/reports/debt-balance/export`.
[Collection(nameof(PostgresCollection))]
public class DebtSummaryExportTests : CashDebtTestBase
{
    private const string October = "from=2026-10-01&to=2026-10-31";

    public DebtSummaryExportTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Summary_receivable()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var client = MainBranchClient(ids);

        var response = await client.GetAsync($"/api/reports/debt-balance/export?side=Receivable&{October}");

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        response.Content.Headers.ContentDisposition!.FileName.Should().Be("TongHopCongNo_20261001_20261031.xlsx");
        using var wb = await Tt133Sheet.OpenAsync(response);
        var ws = wb.Worksheets.Should().ContainSingle().Subject;
        ws.Name.Should().Be("TongHop");
        ws.CellsUsed().Should().NotContain(c => c.GetString().Contains("Mẫu số"));
        Tt133Sheet.Contains(ws, "BẢNG TỔNG HỢP CHI TIẾT THANH TOÁN VỚI NGƯỜI MUA").Should().BeTrue();

        var row = Tt133Sheet.RowOf(ws, ids.Kh1Code);
        ws.Cell(row, 1).GetValue<int>().Should().Be(1);
        AssertAmounts(ws, row, 4_000m, null, 3_000m, 5_500m, 1_500m, null);
        var total = Tt133Sheet.RowOf(ws, "Cộng");
        total.Should().Be(row + 1);
        AssertAmounts(ws, total, 4_000m, 0m, 3_000m, 5_500m, 1_500m, 0m);

        ws.PageSetup.PageOrientation.Should().Be(XLPageOrientation.Landscape);
    }

    [Fact]
    public async Task Summary_payable()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var client = MainBranchClient(ids);

        var response = await client.GetAsync($"/api/reports/debt-balance/export?side=Payable&{October}");

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        using var wb = await Tt133Sheet.OpenAsync(response);
        var ws = wb.Worksheets.Should().ContainSingle().Subject;
        Tt133Sheet.Contains(ws, "BẢNG TỔNG HỢP CHI TIẾT THANH TOÁN VỚI NGƯỜI BÁN").Should().BeTrue();
        var row = Tt133Sheet.RowOf(ws, ids.Ncc1Code);
        AssertAmount(ws, row, 6, 5_000m);
        AssertAmount(ws, row, 7, 6_000m);
        AssertAmount(ws, row, 8, null);
        AssertAmount(ws, row, 9, 1_000m);
    }

    [Fact]
    public async Task Summary_totals_equal_the_balance_report()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var client = MainBranchClient(ids);

        foreach (var side in new[] { DebtSide.Receivable, DebtSide.Payable })
        {
            var balance = await ReadDataAsync<DebtBalanceReportDto>(
                await client.GetAsync($"/api/reports/debt-balance?side={side}&{October}"));
            var response = await client.GetAsync($"/api/reports/debt-balance/export?side={side}&{October}");
            response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
            using var wb = await Tt133Sheet.OpenAsync(response);
            var ws = wb.Worksheets.Single();

            var totals = balance.Totals;
            var (debit, credit) = DebtBalanceSign.SplitMovement(side, totals.Increase, totals.Decrease);
            var total = Tt133Sheet.RowOf(ws, "Cộng");
            Tt133Sheet.Number(ws, total, 4).Should().Be(totals.OpeningDebit, side.ToString());
            Tt133Sheet.Number(ws, total, 5).Should().Be(totals.OpeningCredit, side.ToString());
            Tt133Sheet.Number(ws, total, 6).Should().Be(debit, side.ToString());
            Tt133Sheet.Number(ws, total, 7).Should().Be(credit, side.ToString());
            Tt133Sheet.Number(ws, total, 8).Should().Be(totals.ClosingDebit, side.ToString());
            Tt133Sheet.Number(ws, total, 9).Should().Be(totals.ClosingCredit, side.ToString());
        }
    }

    [Fact]
    public async Task Summary_closing_equals_S12_closing()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var client = MainBranchClient(ids);

        var summary = await client.GetAsync($"/api/reports/debt-balance/export?side=Receivable&{October}");
        summary.StatusCode.Should().Be(HttpStatusCode.OK, await summary.Content.ReadAsStringAsync());
        var ledger = await client.PostAsJsonAsync("/api/reports/debt-ledger/export", new Dictionary<string, object?>
        {
            ["side"] = "Receivable",
            ["from"] = "2026-10-01",
            ["to"] = "2026-10-31",
            ["partnerIds"] = new[] { ids.Kh1Id },
        }, TestJson.Options);
        ledger.StatusCode.Should().Be(HttpStatusCode.OK, await ledger.Content.ReadAsStringAsync());

        using var summaryBook = await Tt133Sheet.OpenAsync(summary);
        using var ledgerBook = await Tt133Sheet.OpenAsync(ledger);
        var summarySheet = summaryBook.Worksheets.Single();
        var ledgerSheet = ledgerBook.Worksheets.Single();
        var row = Tt133Sheet.RowOf(summarySheet, ids.Kh1Code);
        var closing = Tt133Sheet.RowOf(ledgerSheet, "Số dư cuối kỳ");
        Tt133Sheet.Number(summarySheet, row, 8).Should().Be(Tt133Sheet.Number(ledgerSheet, closing, 9));
        ledgerSheet.Cell(closing, 10).IsEmpty().Should().BeTrue();
        summarySheet.Cell(row, 9).IsEmpty().Should().BeTrue();
    }

    [Fact]
    public async Task Summary_company_scope()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var client = MainBranchClient(ids);

        var response = await client.GetAsync($"/api/reports/debt-balance/export?side=Receivable&{October}&scope=Company");

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        using var wb = await Tt133Sheet.OpenAsync(response);
        var ws = wb.Worksheets.Single();
        var row = Tt133Sheet.RowOf(ws, ids.Kh1Code);
        AssertAmount(ws, row, 6, 12_999m);
        AssertAmount(ws, row, 7, 5_500m);
        AssertAmount(ws, row, 8, 11_499m);
    }

    [Fact]
    public async Task Summary_with_no_rows_still_exports()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var client = MainBranchClient(ids);

        var response = await client.GetAsync("/api/reports/debt-balance/export?side=Payable&from=2026-10-01&to=2026-10-04");

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        using var wb = await Tt133Sheet.OpenAsync(response);
        var ws = wb.Worksheets.Single();
        AssertAmounts(ws, Tt133Sheet.RowOf(ws, "Cộng"), 0m, 0m, 0m, 0m, 0m, 0m);
    }

    [Fact]
    public async Task Summary_with_search_prints_the_filter()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var client = MainBranchClient(ids);

        var found = await client.GetAsync($"/api/reports/debt-balance/export?side=Receivable&{October}&search={ids.Kh1Code}");
        found.StatusCode.Should().Be(HttpStatusCode.OK, await found.Content.ReadAsStringAsync());
        using (var wb = await Tt133Sheet.OpenAsync(found))
        {
            var ws = wb.Worksheets.Single();
            Tt133Sheet.Contains(ws, $"Điều kiện lọc: mã hoặc tên chứa \"{ids.Kh1Code}\"").Should().BeTrue();
            Tt133Sheet.Contains(ws, ids.Kh1Code).Should().BeTrue();
        }

        var none = await client.GetAsync($"/api/reports/debt-balance/export?side=Receivable&{October}&search=zzz-none");
        none.StatusCode.Should().Be(HttpStatusCode.OK, await none.Content.ReadAsStringAsync());
        using (var wb = await Tt133Sheet.OpenAsync(none))
        {
            var ws = wb.Worksheets.Single();
            Tt133Sheet.Contains(ws, "Điều kiện lọc: mã hoặc tên chứa \"zzz-none\"").Should().BeTrue();
            ws.CellsUsed().Should().NotContain(c => c.GetString() == ids.Kh1Code);
            AssertAmounts(ws, Tt133Sheet.RowOf(ws, "Cộng"), 0m, 0m, 0m, 0m, 0m, 0m);
        }

        foreach (var query in new[] { "", "&search=%20" })
        {
            var plain = await client.GetAsync($"/api/reports/debt-balance/export?side=Receivable&{October}{query}");
            plain.StatusCode.Should().Be(HttpStatusCode.OK, await plain.Content.ReadAsStringAsync());
            using var wb = await Tt133Sheet.OpenAsync(plain);
            wb.Worksheets.Single().CellsUsed().Should().NotContain(c => c.GetString().StartsWith("Điều kiện lọc:"));
        }
    }

    [Fact]
    public async Task Summary_rejects_a_partner_filter()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var client = MainBranchClient(ids);

        var response = await client.GetAsync($"/api/reports/debt-balance/export?side=Receivable&{October}&partnerId={ids.Kh1Id}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
        (await response.Content.ReadFromJsonAsync<ApiResponse>(TestJson.Options))!.Error!.Details.Should().ContainKey("partnerId");
    }

    [Fact]
    public async Task Permission()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var cashOnly = await CreateClientWithPermissionsAsync("tt133_sum_" + Guid.NewGuid().ToString("N")[..8], ids.MainBranchId,
            Permissions.Reports.Cash);

        (await cashOnly.GetAsync($"/api/reports/debt-balance/export?side=Receivable&{October}"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// Columns 4-9 of a row; null = an empty cell.
    private static void AssertAmounts(IXLWorksheet ws, int row, params decimal?[] expected)
    {
        for (var i = 0; i < expected.Length; i++)
            AssertAmount(ws, row, 4 + i, expected[i]);
    }

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
