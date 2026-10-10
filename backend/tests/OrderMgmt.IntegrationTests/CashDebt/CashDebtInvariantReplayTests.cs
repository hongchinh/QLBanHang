using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderMgmt.Application.CashDebt.CashVouchers.Models;
using OrderMgmt.Application.CashDebt.Debt.Models;
using OrderMgmt.Application.CashDebt.Ledger;
using OrderMgmt.Application.CashDebt.Reports.Models;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.CashDebt;

/// Back-dated edits must leave the same debt and fund state as entering the final data directly (Phase 09, Task 9.5).
[Collection(nameof(PostgresCollection))]
public class CashDebtInvariantReplayTests : CashDebtTestBase
{
    public CashDebtInvariantReplayTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Back_dated_edits_equal_entering_final_data()
    {
        // Scenario A: the report dataset in its fresh main branch, then the back-dated edits.
        var a = await ReportDataset.SeedAsync(this);
        var clientA = BranchClient(a.MainBranchId);

        // Edit #4: total 3,500, paid 1,500, date 10-02 (the automatic receipt follows, Auto is recomputed).
        var sale = await GetVoucherAsync(clientA, a.SaleId);
        var saleUpdate = UpdateRequestFrom(sale);
        saleUpdate.VoucherAt = Vn("2026-10-02 09:00");
        saleUpdate.Lines.Single().UnitPrice = 3_500m;
        saleUpdate.PaidAmount = 1_500m;
        var (saleStatus, _, saleError) = await PutVoucherAsync(clientA, a.SaleId, saleUpdate);
        saleStatus.Should().Be(HttpStatusCode.OK, saleError?.Message);

        // Cancel #5 (its allocation goes), restore it (the allocation is not restored, C7), re-allocate 4,000 to HD-OLD.
        var (_, receipt5, _) = await GetCashAsync(clientA, a.Receipt5Id);
        var (cancelStatus, cancelled5, cancelError) = await CancelCashAsync(clientA, a.Receipt5Id,
            new CashDocumentActionRequest { Version = receipt5!.Version, ConfirmRemoveSettlements = true });
        cancelStatus.Should().Be(HttpStatusCode.OK, cancelError?.Message);
        var (restoreStatus, _, restoreError) = await RestoreCashAsync(clientA, a.Receipt5Id,
            new CashDocumentActionRequest { Version = cancelled5!.Version });
        restoreStatus.Should().Be(HttpStatusCode.OK, restoreError?.Message);
        var receipt5Entry = (await EntriesOfAsync(DebtSourceType.Receipt, a.Receipt5Id)).Single();
        (await SettlementsOfAsync(receipt5Entry.Id)).Should().BeEmpty("a restored voucher does not get its allocation back");
        var matchResponse = await clientA.PostAsJsonAsync("/api/debt/settlements", new CreateSettlementBatchRequest
        {
            PartnerId = a.Kh1Id,
            Side = DebtSide.Receivable,
            Items = new List<SettlementItemRequest>
            {
                new() { IncreaseEntryId = a.OpeningHdOldEntryId, DecreaseEntryId = receipt5Entry.Id, Amount = 4_000m },
            },
        }, TestJson.Options);
        var (matchStatus, _, matchError) = await ReadDocumentResponseAsync<SettlementBatchDto>(matchResponse);
        matchStatus.Should().Be(HttpStatusCode.OK, matchError?.Message);

        // Move #9 to 10-04.
        var (_, transfer9, _) = await GetTransferAsync(clientA, a.Transfer9Id);
        var transferUpdate = TransferRequest(transfer9!.FromFundId, transfer9.ToFundId, transfer9.Amount, "2026-10-04 09:00");
        transferUpdate.Description = transfer9.Description;
        transferUpdate.Note = transfer9.Note;
        transferUpdate.Version = transfer9.Version;
        var (transferStatus, _, transferError) = await PutTransferAsync(clientA, a.Transfer9Id, transferUpdate);
        transferStatus.Should().Be(HttpStatusCode.OK, transferError?.Message);

        // Delete #10.
        var (_, payment10, _) = await GetCashAsync(clientA, a.Payment10Id);
        var (deleteStatus, _, deleteError) = await DeleteCashAsync(clientA, a.Payment10Id,
            new CashDocumentActionRequest { Version = payment10!.Version });
        deleteStatus.Should().Be(HttpStatusCode.OK, deleteError?.Message);

        // Edit #8's fund to QTM.
        var (_, payment8, _) = await GetCashAsync(clientA, a.Payment8Id);
        var payment8Update = CashUpdateFrom(payment8!);
        payment8Update.FundId = a.QtmId;
        var (payment8Status, _, payment8Error) = await PutCashAsync(clientA, a.Payment8Id, payment8Update);
        payment8Status.Should().Be(HttpStatusCode.OK, payment8Error?.Message);

        var autoReceiptA = (await GetVoucherAsync(clientA, a.SaleId)).AutoCashVoucherId;
        var snapshotA = await SnapshotAsync(clientA, a.MainBranchId,
            partners: new() { [a.Kh1Id] = "KH1", [a.Ncc1Id] = "NCC1" },
            funds: new() { [a.QtmId] = "QTM", [a.Nh01Id] = "NH01" },
            sources: new()
            {
                [a.OpeningHdOldId] = "#1 HD-OLD",
                [a.OpeningTtOldId] = "#2 TT-OLD",
                [a.SaleId] = "#4 sale",
                [autoReceiptA!.Value] = "#4 auto receipt",
                [a.Receipt5Id] = "#5 receipt",
                [a.Return6Id] = "#6 return",
                [a.Purchase7Id] = "#7 purchase",
                [a.AutoPaymentId] = "#7 auto payment",
                [a.Payment8Id] = "#8 payment",
                [a.Payment10Id] = "#10 payment",
                [a.Receipt11Id] = "#11 receipt",
            });

        // Scenario B: a fresh branch C with the same opening data; the final state entered directly, in date order.
        var c = await EnterFinalStateAsync();
        var clientC = BranchClient(c.BranchId);
        var snapshotB = await SnapshotAsync(clientC, c.BranchId,
            partners: new() { [c.Kh1Id] = "KH1", [c.Ncc1Id] = "NCC1" },
            funds: new() { [c.QtmId] = "QTM", [c.Nh01Id] = "NH01" },
            sources: c.Sources);

        snapshotA.Should().BeEquivalentTo(snapshotB);

        // Hand-computed final state, so a shared engine error cannot hide behind A == B.
        snapshotA.Balances.Should().BeEquivalentTo(new Dictionary<string, decimal>
        {
            ["KH1:Receivable"] = 5_000m - 1_000m + 3_500m - 1_500m - 4_000m - 500m,
            ["KH1:Payable"] = 0m,
            ["NCC1:Receivable"] = 0m,
            ["NCC1:Payable"] = 6_000m - 2_000m - 3_000m,
        });
        snapshotA.FundClosings.Should().BeEquivalentTo(new Dictionary<string, decimal>
        {
            ["QTM"] = 10_000m + 1_500m + 5_000m + 4_000m - 3_000m,
            ["NH01"] = 20_000m - 5_000m - 2_000m,
        });
        snapshotA.Entries["#1 HD-OLD:Receivable"].Should().Be((5_000m, 4_000m));
        snapshotA.Entries["#4 sale:Receivable"].Should().Be((3_500m, 1_500m));
        snapshotA.Entries["#4 auto receipt:Receivable"].Should().Be((1_500m, 1_500m));
        snapshotA.Entries["#5 receipt:Receivable"].Should().Be((4_000m, 4_000m));

        await AssertCashDebtInvariantsAsync();
    }

    private sealed record ReplaySnapshot(
        Dictionary<string, decimal> Balances,
        Dictionary<string, (decimal Amount, decimal Settled)> Entries,
        Dictionary<string, decimal> FundClosings);

    private sealed record FinalStateIds(Guid BranchId, Guid Kh1Id, Guid Ncc1Id, Guid QtmId, Guid Nh01Id,
        Dictionary<Guid, string> Sources);

    /// Per (partner, side): the balance; per debt entry (keyed by document role and side): Amount and Σ settled;
    /// per fund: the closing of the cash book (kind=All, 10-01..10-31). Every entry of the branch must have a role.
    private async Task<ReplaySnapshot> SnapshotAsync(HttpClient client, Guid branchId, Dictionary<Guid, string> partners,
        Dictionary<Guid, string> funds, Dictionary<Guid, string> sources)
    {
        var balances = new Dictionary<string, decimal>();
        foreach (var (partnerId, role) in partners)
        {
            foreach (var side in new[] { DebtSide.Receivable, DebtSide.Payable })
            {
                balances[$"{role}:{side}"] = await InTransactionAsync((sp, ct) =>
                    sp.GetRequiredService<IDebtQueryService>().BalanceAsync(branchId, partnerId, side, null, ct));
            }
        }

        var (entries, settlements) = await InDbAsync(async db =>
        {
            var rows = await db.DebtEntries.AsNoTracking().Where(e => e.BranchId == branchId).ToListAsync();
            var ids = rows.Select(e => e.Id).ToList();
            var links = await db.DebtSettlements.AsNoTracking()
                .Where(s => ids.Contains(s.IncreaseEntryId) || ids.Contains(s.DecreaseEntryId))
                .ToListAsync();
            return (rows, links);
        });
        entries.Select(e => e.SourceId).Should().OnlyContain(id => sources.ContainsKey(id),
            "every debt entry of the branch belongs to a document of the dataset");
        var entrySnapshot = entries.ToDictionary(
            e => $"{sources[e.SourceId]}:{e.Side}",
            e => (e.Amount, settlements.Where(s => s.IncreaseEntryId == e.Id || s.DecreaseEntryId == e.Id)
                .Sum(s => s.Amount)));

        var cashBook = await ReadDataAsync<CashBookReportDto>(
            await client.GetAsync("/api/reports/cash-book?kind=All&from=2026-10-01&to=2026-10-31"));
        cashBook.Groups.Select(g => g.FundId).Should().BeEquivalentTo(funds.Keys);
        var fundClosings = cashBook.Groups.ToDictionary(g => funds[g.FundId], g => g.Closing);

        return new ReplaySnapshot(balances, entrySnapshot, fundClosings);
    }

    /// Branch C: the dataset's opening data (rows 1–3), then the final state of scenario A entered in date order.
    private async Task<FinalStateIds> EnterFinalStateAsync()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var branchId = await CreateBranchAsync("RC" + suffix);
        var warehouseId = await CreateWarehouseAsync(branchId, "RC" + suffix);
        var client = BranchClient(branchId);
        var kh1 = await CreatePartnerAsync("KH1" + suffix, isCustomer: true, isSupplier: false);
        var ncc1 = await CreatePartnerAsync("NCC1" + suffix, isCustomer: false, isSupplier: true);

        // Opening data, as in the dataset.
        await ReportDataset.SetOpeningDateAsync(this, branchId, ReportDataset.OpeningDate);
        var qtm = await CashFundIdAsync(branchId);
        var nh01 = await CreateBankFundAsync("NH01", branchId);
        await ReportDataset.SetFundOpeningAsync(client, qtm, 10_000m);
        await ReportDataset.SetFundOpeningAsync(client, nh01, 20_000m);
        var (hdOld, hdOldEntry) = await CreateOpeningDebtAsync(client, kh1, DebtSide.Receivable,
            OpeningDebtKind.Debt, "HD-OLD", new DateOnly(2026, 9, 5), 5_000m, dueDate: new DateOnly(2026, 9, 20));
        var (ttOld, _) = await CreateOpeningDebtAsync(client, kh1, DebtSide.Receivable,
            OpeningDebtKind.Prepayment, "TT-OLD", new DateOnly(2026, 9, 15), 1_000m);
        var cashMethod = await ReportDataset.PaymentMethodIdAsync(this, "TM");
        var bankMethod = await ReportDataset.PaymentMethodIdAsync(this, "CK");

        // #4 (10-02): sale 3,500 to KH1, paid 1,500 in cash.
        var sale = await ReportDataset.PostStockAsync(this, client, StockDirection.Out, "XBH", "2026-10-02 09:00",
            warehouseId, kh1, 3_500m, cashMethod, paid: 1_500m);

        // #9 (10-04): transfer 5,000 NH01 → QTM.
        var (transferStatus, transfer9, transferError) =
            await PostTransferAsync(client, TransferRequest(nh01, qtm, 5_000m, "2026-10-04 09:00"));
        transferStatus.Should().Be(HttpStatusCode.OK, transferError?.Message);

        // #5 (10-05): receipt 4,000 from KH1 into QTM, allocated to HD-OLD.
        var receipt5Request = CashRequest(CashVoucherType.Receipt, await CashReasonIdAsync("TKH"), qtm,
            "2026-10-05 09:00", kh1, 4_000m);
        receipt5Request.Allocations = new List<AllocationRequest> { new() { EntryId = hdOldEntry, Amount = 4_000m } };
        var receipt5 = await CreateCashAsync(client, receipt5Request);

        // #6 (10-08): customer return 500, nothing paid.
        var return6 = await ReportDataset.PostStockAsync(this, client, StockDirection.In, "NTL", "2026-10-08 09:00",
            warehouseId, kh1, 500m, paymentMethodId: null, paid: 0m);

        // #7 (10-10): purchase 6,000 from NCC1, paid 2,000 by bank from NH01.
        var purchase7 = await ReportDataset.PostStockAsync(this, client, StockDirection.In, "NMH", "2026-10-10 09:00",
            warehouseId, ncc1, 6_000m, bankMethod, paid: 2_000m);

        // #8 (10-12): payment 3,000 to NCC1, from QTM.
        var payment8 = await CreateCashAsync(client, CashRequest(CashVoucherType.Payment,
            await CashReasonIdAsync("TNCC"), qtm, "2026-10-12 09:00", ncc1, 3_000m));

        // #10 is deleted in the final state: not entered. #11 (10-18): receipt 300 from KH1, cancelled.
        var receipt11 = await CreateCashAsync(client, CashRequest(CashVoucherType.Receipt,
            await CashReasonIdAsync("TKH"), qtm, "2026-10-18 09:00", kh1, 300m));
        var (cancelStatus, _, cancelError) = await CancelCashAsync(client, receipt11.Id,
            new CashDocumentActionRequest { Version = receipt11.Version });
        cancelStatus.Should().Be(HttpStatusCode.OK, cancelError?.Message);

        return new FinalStateIds(branchId, kh1, ncc1, qtm, nh01, new Dictionary<Guid, string>
        {
            [hdOld] = "#1 HD-OLD",
            [ttOld] = "#2 TT-OLD",
            [sale.Id] = "#4 sale",
            [sale.AutoCashVoucherId!.Value] = "#4 auto receipt",
            [receipt5.Id] = "#5 receipt",
            [return6.Id] = "#6 return",
            [purchase7.Id] = "#7 purchase",
            [purchase7.AutoCashVoucherId!.Value] = "#7 auto payment",
            [payment8.Id] = "#8 payment",
            [receipt11.Id] = "#11 receipt",
        });
    }

    private HttpClient BranchClient(Guid branchId)
    {
        var client = CloneAdminClient();
        UseBranch(client, branchId);
        return client;
    }
}
