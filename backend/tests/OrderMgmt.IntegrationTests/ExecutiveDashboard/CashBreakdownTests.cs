using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.CashDebt.CashVouchers.Models;
using OrderMgmt.Application.Dashboards.Executive.Models;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.ExecutiveDashboard;

/// cash/breakdown: the totals by reason and the largest documents of the report dataset (Phase 02, Task 2.3).
[Collection(nameof(PostgresCollection))]
public class CashBreakdownTests : ExecutiveDashboardTestBase
{
    public CashBreakdownTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task By_reason()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var reasons = await InDbAsync(db => db.CashReasons
            .Where(r => new[] { "TKH", "TPX", "TNCC", "CPN", "CK" }.Contains(r.Code))
            .Select(r => new { r.Code, r.Id, r.Name })
            .ToDictionaryAsync(r => r.Code));

        var breakdown = await GetBreakdownAsync(MainClient(ids), $"{October}&branchId={ids.MainBranchId}");

        // Receipts first, then payments; each by total, the largest first. TPX and CPN are the automatic vouchers.
        breakdown.ByReason.Select(r => (r.ReasonCode, r.Type, r.Total, r.Count)).Should().Equal(
            ("TKH", CashVoucherType.Receipt, 4_000m, 1),
            ("TPX", CashVoucherType.Receipt, 1_000m, 1),
            ("TNCC", CashVoucherType.Payment, 3_000m, 1),
            ("CPN", CashVoucherType.Payment, 2_000m, 1),
            ("CK", CashVoucherType.Payment, 700m, 1));
        breakdown.ByReason.Select(r => (r.ReasonId, r.ReasonName)).Should().Equal(
            breakdown.ByReason.Select(r => (reasons[r.ReasonCode!].Id, (string?)reasons[r.ReasonCode!].Name)));
        breakdown.ByReason.Where(r => r.Type == CashVoucherType.Receipt).Sum(r => r.Total).Should().Be(5_000m);
        breakdown.ByReason.Where(r => r.Type == CashVoucherType.Payment).Sum(r => r.Total).Should().Be(5_700m);
    }

    [Fact]
    public async Task Largest_documents()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var branchCode = await InDbAsync(db => db.Branches.Where(b => b.Id == ids.MainBranchId)
            .Select(b => b.Code).SingleAsync());

        var breakdown = await GetBreakdownAsync(MainClient(ids), $"{October}&branchId={ids.MainBranchId}");

        // The transfer of 5,000 and the cancelled receipt PT00003 are not documents of the list.
        breakdown.Largest.Select(d => (d.Code, d.Total)).Should().Equal(
            ("PT00002", 4_000m), ("PC00002", 3_000m), ("PC00001", 2_000m), ("PT00001", 1_000m), ("PC00003", 700m));

        var receipt = breakdown.Largest[0];
        receipt.Id.Should().Be(ids.Receipt5Id);
        receipt.Type.Should().Be(CashVoucherType.Receipt);
        receipt.At.Should().Be(Vn("2026-10-05 09:00"));
        receipt.BranchCode.Should().Be(branchCode);
        receipt.PartnerOrPerson.Should().Be($"Đối tượng {ids.Kh1Code}");
        receipt.Description.Should().Be("Thu tiền khách hàng");

        var payment = breakdown.Largest[4];
        payment.Id.Should().Be(ids.Payment10Id);
        payment.Type.Should().Be(CashVoucherType.Payment);
        payment.At.Should().Be(Vn("2026-10-16 09:00"));
        payment.BranchCode.Should().Be(branchCode);
        payment.PartnerOrPerson.Should().Be("Nguyễn A");
        payment.Description.Should().Be("Chi khác");
    }

    [Fact]
    public async Task Reasons_add_up_their_vouchers_and_equal_totals_are_ordered_by_code()
    {
        var ids = await ReportDataset.SeedAsync(this);
        // The dataset has TKH 4,000 and TPX 1,000 (receipts), TNCC 3,000, CPN 2,000 and CK 700 (payments). Four receipt
        // reasons and four payment reasons end with the same total each, so only the code tells them apart.
        await InsertAsync(ids.MainBranchId, ids.QtmId, CashVoucherType.Receipt, "TK", 1_000m, "2026-10-20 09:00");
        await InsertAsync(ids.MainBranchId, ids.QtmId, CashVoucherType.Receipt, "TK", 3_000m, "2026-10-21 09:00");
        await InsertAsync(ids.MainBranchId, ids.QtmId, CashVoucherType.Receipt, "THNCC", 4_000m, "2026-10-22 09:00");
        await InsertAsync(ids.MainBranchId, ids.QtmId, CashVoucherType.Receipt, "TPX", 3_000m, "2026-10-22 10:00");
        await InsertAsync(ids.MainBranchId, ids.QtmId, CashVoucherType.Payment, "CK", 2_300m, "2026-10-23 09:00");
        await InsertAsync(ids.MainBranchId, ids.QtmId, CashVoucherType.Payment, "CHKH", 3_000m, "2026-10-23 10:00");
        await InsertAsync(ids.MainBranchId, ids.QtmId, CashVoucherType.Payment, "CPN", 1_000m, "2026-10-23 11:00");

        var breakdown = await GetBreakdownAsync(MainClient(ids), $"{October}&branchId={ids.MainBranchId}");

        breakdown.ByReason.Select(r => (r.ReasonCode, r.Total, r.Count)).Should().Equal(
            ("THNCC", 4_000m, 1), ("TK", 4_000m, 2), ("TKH", 4_000m, 1), ("TPX", 4_000m, 2),
            ("CHKH", 3_000m, 1), ("CK", 3_000m, 2), ("CPN", 3_000m, 2), ("TNCC", 3_000m, 1));
    }

    [Fact]
    public async Task Only_the_period_counts_from_its_first_minute_to_its_last()
    {
        var branch = await CreateFreshBranchAsync();
        var fundId = await CashFundIdAsync(branch.BranchId);
        await InsertAsync(branch.BranchId, fundId, CashVoucherType.Receipt, "TK", 5m, "2026-09-30 23:59");
        await InsertAsync(branch.BranchId, fundId, CashVoucherType.Receipt, "TK", 10m, "2026-10-01 00:00");
        await InsertAsync(branch.BranchId, fundId, CashVoucherType.Receipt, "TK", 20m, "2026-10-31 23:59");
        await InsertAsync(branch.BranchId, fundId, CashVoucherType.Receipt, "TK", 40m, "2026-11-01 00:00");
        await InsertAsync(branch.BranchId, fundId, CashVoucherType.Payment, "CK", 7m, "2026-10-01 00:00");
        await InsertAsync(branch.BranchId, fundId, CashVoucherType.Payment, "CK", 70m, "2026-11-01 00:00");

        var breakdown = await GetBreakdownAsync(branch.Client, $"{October}&branchId={branch.BranchId}");

        breakdown.ByReason.Select(r => (r.ReasonCode, r.Total, r.Count)).Should().Equal(("TK", 30m, 2), ("CK", 7m, 1));
        breakdown.Largest.Select(d => d.Total).Should().Equal(20m, 10m, 7m);
    }

    [Fact]
    public async Task Largest_documents_of_all_funds_are_ordered_and_cut_at_ten()
    {
        var branch = await CreateFreshBranchAsync();
        var cash = await CashFundIdAsync(branch.BranchId);
        var bank = await CreateBankFundAsync("NH01", branch.BranchId);
        // Equal amounts, three dates inserted out of order and spread over the two funds: the earlier date first.
        await InsertCashVoucherAsync(branch.BranchId, cash, CashVoucherType.Receipt, 5_000m, Vn("2026-10-22 09:00"));
        await InsertCashVoucherAsync(branch.BranchId, bank, CashVoucherType.Receipt, 5_000m, Vn("2026-10-21 09:00"));
        await InsertCashVoucherAsync(branch.BranchId, cash, CashVoucherType.Receipt, 5_000m, Vn("2026-10-20 09:00"));
        // Equal amounts and dates, three in each fund: by code.
        foreach (var fundId in new[] { cash, bank, cash, bank, cash, bank })
            await InsertCashVoucherAsync(branch.BranchId, fundId, CashVoucherType.Payment, 4_000m, Vn("2026-10-23 09:00"));
        // Twelve more with their own amounts; only the largest of them still fits in the ten.
        foreach (var hundreds in Enumerable.Range(1, 12))
            await InsertCashVoucherAsync(branch.BranchId, cash, CashVoucherType.Receipt, hundreds * 100m, Vn("2026-10-24 09:00"));

        var largest = (await GetBreakdownAsync(branch.Client, $"{October}&branchId={branch.BranchId}")).Largest;

        largest.Select(d => d.Total).Should().Equal(5_000m, 5_000m, 5_000m, 4_000m, 4_000m, 4_000m, 4_000m, 4_000m, 4_000m, 1_200m);
        largest.Take(3).Select(d => d.At).Should().Equal(
            Vn("2026-10-20 09:00"), Vn("2026-10-21 09:00"), Vn("2026-10-22 09:00"));
        largest.Skip(3).Take(6).Select(d => d.Code).Should().BeInAscendingOrder(StringComparer.Ordinal);
        largest.Select(d => d.Type).Should().Equal(Enumerable.Repeat(CashVoucherType.Receipt, 3)
            .Concat(Enumerable.Repeat(CashVoucherType.Payment, 6)).Append(CashVoucherType.Receipt));
    }

    [Fact]
    public async Task Deleted_voucher_is_not_counted()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var client = MainClient(ids);
        var request = CashRequest(CashVoucherType.Payment, await CashReasonIdAsync("CK"), ids.QtmId,
            "2026-10-20 09:00", partnerId: null, 900m);
        request.PersonName = "Nguyễn C";
        var payment = await CreateCashAsync(client, request);
        var query = $"{October}&branchId={ids.MainBranchId}";

        (await GetBreakdownAsync(client, query)).ByReason.Single(r => r.ReasonCode == "CK")
            .Should().BeEquivalentTo(new { Total = 1_600m, Count = 2 });

        (await DeleteCashAsync(client, payment.Id, new CashDocumentActionRequest { Version = payment.Version }))
            .Status.Should().Be(System.Net.HttpStatusCode.OK);

        var breakdown = await GetBreakdownAsync(client, query);
        breakdown.ByReason.Single(r => r.ReasonCode == "CK").Should().BeEquivalentTo(new { Total = 700m, Count = 1 });
        breakdown.Largest.Should().NotContain(d => d.Id == payment.Id);
    }

    [Fact]
    public async Task Deleted_reason_keeps_its_code_and_name()
    {
        var ids = await ReportDataset.SeedAsync(this);
        await InDbAsync(db => db.CashReasons.IgnoreQueryFilters().Where(r => r.Code == "TPX")
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.IsDeleted, true)));

        var breakdown = await GetBreakdownAsync(MainClient(ids), $"{October}&branchId={ids.MainBranchId}");

        var row = breakdown.ByReason.Single(r => r.Type == CashVoucherType.Receipt && r.Total == 1_000m);
        row.ReasonCode.Should().Be("TPX");
        row.ReasonName.Should().Be("Thu tiền theo phiếu xuất");
    }

    [Fact]
    public async Task Company_wide_adds_the_reasons_of_every_branch_and_names_the_branch_of_each_document()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var suffix = ids.Kh1Code[3..];
        await InsertCashVoucherAsync(ids.BranchBId, await CashFundIdAsync(ids.BranchBId), CashVoucherType.Payment, 123m,
            Vn("2026-10-06 09:00"));

        var client = MainClient(ids);

        var breakdown = await GetBreakdownAsync(client, October);

        breakdown.ByReason.Single(r => r.ReasonCode == "CK").Should().BeEquivalentTo(new { Total = 823m, Count = 2 });
        breakdown.Largest.Select(d => (d.Total, d.BranchCode)).Should().Equal(
            (4_000m, "RD" + suffix), (3_000m, "RD" + suffix), (2_000m, "RD" + suffix), (1_000m, "RD" + suffix),
            (700m, "RD" + suffix), (123m, "RB" + suffix));

        // Named to the main branch, the payment of branch B is in neither list.
        var main = await GetBreakdownAsync(client, $"{October}&branchId={ids.MainBranchId}");
        main.ByReason.Single(r => r.ReasonCode == "CK").Should().BeEquivalentTo(new { Total = 700m, Count = 1 });
        main.Largest.Should().HaveCount(5).And.OnlyContain(d => d.BranchCode == "RD" + suffix);
    }

    [Fact]
    public async Task Documents_alike_in_every_way_but_the_branch_keep_one_order()
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        // Numbering is per branch, so four branches can each hold a PT00001 of the same amount at the same minute.
        var voucherIds = new List<Guid>();
        foreach (var number in Enumerable.Range(1, 4))
        {
            var branchId = await CreateBranchAsync($"EQ{number}{suffix}");
            var voucherId = await InsertCashVoucherAsync(branchId, await CashFundIdAsync(branchId),
                CashVoucherType.Receipt, 500m, Vn("2026-10-12 09:00"));
            await InDbAsync(db => db.CashVouchers.Where(v => v.Id == voucherId)
                .ExecuteUpdateAsync(s => s.SetProperty(v => v.Code, "PT00001")));
            voucherIds.Add(voucherId);
        }

        var largest = (await GetBreakdownAsync(CloneAdminClient(), October)).Largest;

        largest.Select(d => d.Id).Should().Equal(voucherIds.Order());
    }

    /// A receipt or payment row straight in the database; its reason is the given one (no debt entry is written).
    private async Task InsertAsync(Guid branchId, Guid fundId, CashVoucherType type, string reasonCode, decimal total, string at)
    {
        var voucherId = await InsertCashVoucherAsync(branchId, fundId, type, total, Vn(at));
        var reasonId = await CashReasonIdAsync(reasonCode);
        await InDbAsync(db => db.CashVouchers.Where(v => v.Id == voucherId)
            .ExecuteUpdateAsync(s => s.SetProperty(v => v.ReasonId, reasonId)));
    }

    private static Task<CashBreakdownDto> GetBreakdownAsync(HttpClient client, string query) =>
        GetExecAsync<CashBreakdownDto>(client, $"cash/breakdown?{query}");
}
