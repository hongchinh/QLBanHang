using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.CashDebt.CashVouchers.Models;
using OrderMgmt.Application.CashDebt.Reports.Models;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.CashDebt.Reports;

/// Sổ quỹ (Phase 09, P25).
[Collection(nameof(PostgresCollection))]
public class CashBookReportTests : CashDebtTestBase
{
    private const string October = "from=2026-10-01&to=2026-10-31";

    public CashBookReportTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Cash_fund_book()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var client = MainBranchClient(ids);
        var kh1Name = $"Đối tượng {ids.Kh1Code}";

        var report = await GetCashBookAsync(client, $"kind=Cash&{October}");

        report.Kind.Should().Be(FundKindFilter.Cash);
        report.From.Should().Be(new DateOnly(2026, 10, 1));
        report.To.Should().Be(new DateOnly(2026, 10, 31));
        var qtm = report.Groups.Should().ContainSingle().Subject;
        qtm.FundId.Should().Be(ids.QtmId);
        qtm.FundCode.Should().Be("QTM");
        qtm.FundName.Should().Be("Quỹ tiền mặt");
        qtm.FundKind.Should().Be(FundKind.Cash);
        qtm.Opening.Should().Be(10_000m);

        // The cancelled receipt PT00003 is absent; the transfer NH01 → QTM is an inflow of QTM.
        qtm.Rows.Select(r => (r.SourceType, r.SourceId, r.ReceiptCode, r.PaymentCode, r.TransferCode, r.In, r.Out, r.Running))
            .Should().Equal(
                (CashBookSourceType.Receipt, ids.AutoReceiptId, "PT00001", null, null, 1_000m, 0m, 11_000m),
                (CashBookSourceType.Receipt, ids.Receipt5Id, "PT00002", null, null, 4_000m, 0m, 15_000m),
                (CashBookSourceType.TransferIn, ids.Transfer9Id, null, null, "CQ00001", 5_000m, 0m, 20_000m),
                (CashBookSourceType.Payment, ids.Payment10Id, null, "PC00003", null, 0m, 700m, 19_300m));
        qtm.Rows.Select(r => r.At).Should().Equal(
            Vn("2026-10-03 09:00"), Vn("2026-10-05 09:00"), Vn("2026-10-15 09:00"), Vn("2026-10-16 09:00"));

        // Partner snapshot, else the person; Description, else the reason name; transfers name their two funds.
        qtm.Rows.Select(r => (r.PartnerOrPerson, r.Description)).Should().Equal(
            (kh1Name, "Thu tiền theo phiếu xuất PX00001"),
            (kh1Name, "Thu tiền khách hàng"),
            (null, "Chuyển quỹ NH01 → QTM"),
            ("Nguyễn A", "Chi khác"));

        qtm.TotalIn.Should().Be(10_000m);
        qtm.TotalOut.Should().Be(700m);
        qtm.Closing.Should().Be(19_300m);

        // One fund selected: the grand totals are the group's.
        report.GrandOpening.Should().Be(10_000m);
        report.GrandIn.Should().Be(10_000m);
        report.GrandOut.Should().Be(700m);
        report.GrandClosing.Should().Be(19_300m);
    }

    [Fact]
    public async Task Bank_book_all_accounts()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var client = MainBranchClient(ids);
        var nh02Id = await CreateBankFundAsync("NH02", ids.MainBranchId, opening: 7_000m);
        await InDbAsync(db => db.Funds.Where(f => f.Id == nh02Id).ExecuteUpdateAsync(s => s
            .SetProperty(f => f.BankName, "Vietcombank")
            .SetProperty(f => f.AccountNumber, "0123456789")));

        // Deleted documents leave no trace: a transfer NH01 → NH02 and a payment from NH02, both deleted.
        var (_, deletedTransfer, transferError) =
            await PostTransferAsync(client, TransferRequest(ids.Nh01Id, nh02Id, 1_000m, "2026-10-20 09:00"));
        deletedTransfer.Should().NotBeNull(transferError?.Message);
        var deletedPayment = await CreateCashAsync(client, CashRequest(CashVoucherType.Payment,
            await CashReasonIdAsync("CK"), nh02Id, "2026-10-21 09:00", partnerId: null, 500m));
        (await DeleteCashAsync(client, deletedPayment.Id, new CashDocumentActionRequest { Version = deletedPayment.Version }))
            .Status.Should().Be(HttpStatusCode.OK);
        (await DeleteTransferAsync(client, deletedTransfer!.Id,
                new CashDocumentActionRequest { Version = deletedTransfer.Version }))
            .Status.Should().Be(HttpStatusCode.OK);

        var report = await GetCashBookAsync(client, $"kind=Bank&{October}");

        report.Kind.Should().Be(FundKindFilter.Bank);
        report.Groups.Select(g => (g.FundId, g.FundCode, g.FundKind)).Should().Equal(
            (ids.Nh01Id, "NH01", FundKind.Bank),
            (nh02Id, "NH02", FundKind.Bank));

        var nh01 = report.Groups[0];
        nh01.Opening.Should().Be(20_000m);
        nh01.Rows.Select(r => (r.SourceType, r.SourceId, r.PaymentCode, r.TransferCode, r.In, r.Out, r.Running))
            .Should().Equal(
                (CashBookSourceType.Payment, ids.AutoPaymentId, "PC00001", null, 0m, 2_000m, 18_000m),
                (CashBookSourceType.Payment, ids.Payment8Id, "PC00002", null, 0m, 3_000m, 15_000m),
                (CashBookSourceType.TransferOut, ids.Transfer9Id, null, "CQ00001", 0m, 5_000m, 10_000m));
        nh01.Rows[0].Description.Should().Be("Chi tiền theo phiếu nhập PN00002");
        nh01.Rows[2].Description.Should().Be("Chuyển quỹ NH01 → QTM");
        nh01.TotalIn.Should().Be(0m);
        nh01.TotalOut.Should().Be(10_000m);
        nh01.Closing.Should().Be(10_000m);

        // A fund without movements still has its group: opening = closing = its opening amount.
        var nh02 = report.Groups[1];
        nh02.FundName.Should().Be("Ngân hàng NH02");
        nh02.BankName.Should().Be("Vietcombank");
        nh02.AccountNumber.Should().Be("0123456789");
        nh02.Opening.Should().Be(7_000m);
        nh02.Rows.Should().BeEmpty();
        nh02.TotalIn.Should().Be(0m);
        nh02.TotalOut.Should().Be(0m);
        nh02.Closing.Should().Be(7_000m);

        // QTM is not selected, so the transfer out of NH01 counts in the grand total (P25).
        report.GrandOpening.Should().Be(27_000m);
        report.GrandIn.Should().Be(0m);
        report.GrandOut.Should().Be(10_000m);
        report.GrandClosing.Should().Be(17_000m);
    }

    [Fact]
    public async Task All_funds_exclude_internal_transfers_from_grand_totals()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var client = MainBranchClient(ids);

        var report = await GetCashBookAsync(client, $"kind=All&{October}");

        // Ordered by Kind (Cash before Bank), then Code; each group shows its half of the transfer.
        report.Kind.Should().Be(FundKindFilter.All);
        report.Groups.Select(g => g.FundId).Should().Equal(ids.QtmId, ids.Nh01Id);
        report.Groups[0].Rows.Should().ContainSingle(r => r.SourceType == CashBookSourceType.TransferIn)
            .Which.In.Should().Be(5_000m);
        report.Groups[1].Rows.Should().ContainSingle(r => r.SourceType == CashBookSourceType.TransferOut)
            .Which.Out.Should().Be(5_000m);
        report.Groups.Select(g => (g.Opening, g.TotalIn, g.TotalOut, g.Closing)).Should().Equal(
            (10_000m, 10_000m, 700m, 19_300m),
            (20_000m, 0m, 10_000m, 10_000m));

        // Both funds of the transfer are selected, so its two halves leave the grand totals (P25).
        report.GrandOpening.Should().Be(30_000m);
        report.GrandIn.Should().Be(5_000m);
        report.GrandOut.Should().Be(5_700m);
        report.GrandClosing.Should().Be(29_300m);
        (report.GrandOpening + report.GrandIn - report.GrandOut).Should().Be(report.GrandClosing);

        // Kind defaults to All.
        var byDefault = await GetCashBookAsync(client, October);
        byDefault.Kind.Should().Be(FundKindFilter.All);
        byDefault.Groups.Select(g => g.FundId).Should().Equal(ids.QtmId, ids.Nh01Id);
        byDefault.GrandClosing.Should().Be(29_300m);
    }

    [Fact]
    public async Task Opening_window_and_fund_filter()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var client = MainBranchClient(ids);
        var suffix = ids.Kh1Code[3..];

        // From 10-11: QTM's receipts of 10-03 and 10-05 move into the opening balance.
        var later = await GetCashBookAsync(client, "kind=Cash&from=2026-10-11&to=2026-10-31");
        var qtm = later.Groups.Should().ContainSingle().Subject;
        qtm.Opening.Should().Be(15_000m);
        qtm.Rows.Select(r => (r.SourceId, r.Running)).Should().Equal(
            (ids.Transfer9Id, 20_000m), (ids.Payment10Id, 19_300m));
        qtm.Closing.Should().Be(19_300m);

        // P29: the fund's opening amount is opening balance even in a window before the opening date.
        var september = await GetCashBookAsync(client, "kind=Cash&from=2026-09-01&to=2026-09-30");
        september.Groups.Single().Opening.Should().Be(10_000m);
        september.Groups.Single().Rows.Should().BeEmpty();
        september.Groups.Single().Closing.Should().Be(10_000m);

        // fundId selects that fund only; with Bank selected alone, the transfer counts in the grand total.
        var nh01Only = await GetCashBookAsync(client, $"kind=Bank&fundId={ids.Nh01Id}&{October}");
        nh01Only.Groups.Select(g => g.FundId).Should().Equal(ids.Nh01Id);
        nh01Only.GrandOut.Should().Be(10_000m);
        (await GetCashBookAsync(client, $"fundId={ids.Nh01Id}&{October}"))
            .Groups.Select(g => g.FundId).Should().Equal(ids.Nh01Id);

        // fundId must match the kind.
        var mismatch = await client.GetAsync($"/api/reports/cash-book?kind=Cash&fundId={ids.Nh01Id}&{October}");
        mismatch.StatusCode.Should().Be(HttpStatusCode.BadRequest, await mismatch.Content.ReadAsStringAsync());
        (await mismatch.Content.ReadFromJsonAsync<ApiResponse>(TestJson.Options))!.Error!.Details
            .Should().ContainKey("fundId");

        // A fund of another branch is not found in the working branch.
        var branchBFund = await CashFundIdAsync(ids.BranchBId);
        (await client.GetAsync($"/api/reports/cash-book?fundId={branchBFund}&{October}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Validation: from > to.
        (await client.GetAsync("/api/reports/cash-book?kind=Cash&from=2026-10-31&to=2026-10-01"))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Permission: reports.debt alone does not open the cash book.
        var debtOnly = await CreateClientWithPermissionsAsync("cbr_nc_" + suffix, ids.MainBranchId,
            Permissions.Reports.Debt);
        (await debtOnly.GetAsync($"/api/reports/cash-book?kind=Cash&{October}"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var cashOnly = await CreateClientWithPermissionsAsync("cbr_c_" + suffix, ids.MainBranchId,
            Permissions.Reports.Cash);
        (await cashOnly.GetAsync($"/api/reports/cash-book?kind=Cash&{October}"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private HttpClient MainBranchClient(ReportIds ids)
    {
        var client = CloneAdminClient();
        UseBranch(client, ids.MainBranchId);
        return client;
    }

    private static async Task<CashBookReportDto> GetCashBookAsync(HttpClient client, string query) =>
        await ReadDataAsync<CashBookReportDto>(await client.GetAsync($"/api/reports/cash-book?{query}"));
}
