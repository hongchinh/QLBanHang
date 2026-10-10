using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderMgmt.Application.CashDebt.Guards;
using OrderMgmt.Application.CashDebt.Ledger;
using OrderMgmt.Application.Inventory.Common;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.CashDebt.Reports;

/// Số dư công nợ (Phase 09).
[Collection(nameof(PostgresCollection))]
public class DebtBalanceReportTests : CashDebtTestBase
{
    public DebtBalanceReportTests(PostgresFixture pg) : base(pg) { }

    /// The dataset builds through the API, keeps the invariants and matches the plan's hand-computed numbers.
    [Fact]
    public async Task Dataset_builds_with_invariants()
    {
        var ids = await ReportDataset.SeedAsync(this);

        // Codes.
        var cashCodes = await InDbAsync(db => db.CashVouchers.IgnoreQueryFilters().AsNoTracking()
            .Where(v => v.BranchId == ids.MainBranchId)
            .ToDictionaryAsync(v => v.Id, v => v.Code));
        cashCodes.Should().BeEquivalentTo(new Dictionary<Guid, string>
        {
            [ids.AutoReceiptId] = "PT00001",
            [ids.Receipt5Id] = "PT00002",
            [ids.Receipt11Id] = "PT00003",
            [ids.AutoPaymentId] = "PC00001",
            [ids.Payment8Id] = "PC00002",
            [ids.Payment10Id] = "PC00003",
        });
        (await InDbAsync(db => db.CashVouchers.Where(v => v.Id == ids.Receipt11Id).Select(v => v.Status).SingleAsync()))
            .Should().Be(DocumentStatus.Cancelled);
        (await InDbAsync(db => db.StockVouchers.AsNoTracking()
                .Where(v => v.Id == ids.Return6Id || v.Id == ids.Purchase7Id)
                .ToDictionaryAsync(v => v.Id, v => v.Code)))
            .Should().BeEquivalentTo(new Dictionary<Guid, string> { [ids.Return6Id] = "PN00001", [ids.Purchase7Id] = "PN00002" });
        (await InDbAsync(db => db.FundTransfers.Where(f => f.Id == ids.Transfer9Id).Select(f => f.Code).SingleAsync()))
            .Should().Be("CQ00001");

        // KH1 receivable movements in the main branch: (PostedAt, signed amount) in ledger order.
        var kh1Movements = await InDbAsync(db => db.DebtEntries.AsNoTracking()
            .Where(e => e.BranchId == ids.MainBranchId && e.PartnerId == ids.Kh1Id && e.Side == DebtSide.Receivable)
            .OrderBy(e => e.PostedAt).ThenBy(e => e.SourceType).ThenBy(e => e.DocCode)
            .Select(e => new { e.PostedAt, Signed = e.Direction == DebtDirection.Increase ? e.Amount : -e.Amount })
            .ToListAsync());
        kh1Movements.Select(m => (m.PostedAt, m.Signed)).Should().Equal(
            (Vn("2026-10-01 00:00"), 5_000m),   // HD-OLD
            (Vn("2026-10-01 00:00"), -1_000m),  // TT-OLD
            (Vn("2026-10-03 09:00"), 3_000m),   // XBH
            (Vn("2026-10-03 09:00"), -1_000m),  // auto receipt PT00001
            (Vn("2026-10-05 09:00"), -4_000m),  // PT00002
            (Vn("2026-10-08 09:00"), -500m));   // NTL PN00001

        // Balances via the query service: KH1 opening 4,000 → closing 1,500, NCC1 payable 1,000.
        var opening = VnTime.StartOfDay(ReportDataset.OpeningDate);
        (await BalanceAsync(ids.MainBranchId, ids.Kh1Id, DebtSide.Receivable, opening)).Should().Be(4_000m);
        (await BalanceAsync(ids.MainBranchId, ids.Kh1Id, DebtSide.Receivable, null)).Should().Be(1_500m);
        (await BalanceAsync(ids.MainBranchId, ids.Ncc1Id, DebtSide.Payable, null)).Should().Be(1_000m);
        (await BalanceAsync(ids.MainBranchId, ids.Kh1Id, DebtSide.Payable, null)).Should().Be(0m);
        // Branch B keeps its own sale.
        (await BalanceAsync(ids.BranchBId, ids.Kh1Id, DebtSide.Receivable, null)).Should().Be(9_999m);

        // Funds via the P17 movements: QTM 19,300, NH01 10,000.
        var qtm = await MovementsAsync(ids.QtmId);
        qtm.Should().Equal(10_000m, 1_000m, 4_000m, 5_000m, -700m);
        qtm.Sum().Should().Be(19_300m);
        var nh01 = await MovementsAsync(ids.Nh01Id);
        nh01.Should().Equal(20_000m, -2_000m, -3_000m, -5_000m);
        nh01.Sum().Should().Be(10_000m);

        await AssertCashDebtInvariantsAsync();
    }

    private Task<decimal> BalanceAsync(Guid branchId, Guid partnerId, DebtSide side, DateTimeOffset? at) =>
        InTransactionAsync((sp, ct) => sp.GetRequiredService<IDebtQueryService>()
            .BalanceAsync(branchId, partnerId, side, at, ct));

    /// Deltas of the fund's movements in P17 order (OpeningAmount first).
    private async Task<List<decimal>> MovementsAsync(Guid fundId) =>
        (await InTransactionAsync((sp, ct) => sp.GetRequiredService<IFundBalanceGuard>().MovementsAsync(fundId, ct)))
        .Select(m => m.Delta).ToList();
}
