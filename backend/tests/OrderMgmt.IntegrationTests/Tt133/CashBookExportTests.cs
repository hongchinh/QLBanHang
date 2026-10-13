using System.Net;
using ClosedXML.Excel;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.CashDebt.Reports.Models;
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

    private HttpClient MainBranchClient(ReportIds ids)
    {
        var client = CloneAdminClient();
        UseBranch(client, ids.MainBranchId);
        return client;
    }
}
