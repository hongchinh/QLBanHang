using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using OrderMgmt.Application.CashDebt.Ledger;
using OrderMgmt.Domain.Entities.CashDebt;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.CashDebt;

[Collection(nameof(PostgresCollection))]
public class DebtQueryTests : CashDebtTestBase
{
    private const DebtSide R = DebtSide.Receivable;
    private const DebtSide P = DebtSide.Payable;
    private const DebtDirection Inc = DebtDirection.Increase;
    private const DebtDirection Dec = DebtDirection.Decrease;

    private Guid _partnerId;
    private string _suffix = default!;

    public DebtQueryTests(PostgresFixture pg) : base(pg) { }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _suffix = Guid.NewGuid().ToString("N")[..8];
        _partnerId = await CreatePartnerAsync("QP" + _suffix, isCustomer: true, isSupplier: true);
    }

    [Fact]
    public async Task Balance_at_date_and_all_time()
    {
        await EntryAsync(Draft(_partnerId, R, Inc, 1_000m, "2026-10-01 08:00", "A" + _suffix));
        await EntryAsync(Draft(_partnerId, R, Dec, 300m, "2026-10-03 08:00", "B" + _suffix));
        await EntryAsync(Draft(_partnerId, R, Inc, 500m, "2026-10-05 08:00", "C" + _suffix));

        (await BalanceAsync(R, Vn("2026-10-04 23:59"))).Should().Be(700m);
        // PostedAt <= at: an entry posted exactly at `at` counts.
        (await BalanceAsync(R, Vn("2026-10-03 08:00"))).Should().Be(700m);
        (await BalanceAsync(R, Vn("2026-10-03 07:59"))).Should().Be(1_000m);
        (await BalanceAsync(R, null)).Should().Be(1_200m);
        (await BalanceAsync(P, null)).Should().Be(0m);

        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Open_items_order_and_direction_filter()
    {
        var due20 = await EntryAsync(Draft(_partnerId, R, Inc, 1_000m, "2026-10-01 08:00", "A" + _suffix,
            dueDate: new DateOnly(2026, 10, 20)));
        var due10 = await EntryAsync(Draft(_partnerId, R, Inc, 800m, "2026-10-01 09:00", "B" + _suffix,
            dueDate: new DateOnly(2026, 10, 10)));
        var noTerm = await EntryAsync(Draft(_partnerId, R, Inc, 600m, "2026-10-15 08:00", "C" + _suffix));
        var paid = await EntryAsync(Draft(_partnerId, R, Dec, 200m, "2026-10-02 08:00", "D" + _suffix));
        var openDecrease = await EntryAsync(Draft(_partnerId, R, Dec, 100m, "2026-10-03 08:00", "E" + _suffix));
        await SettleAsync(new SettlementDraft(due10, paid, 200m));

        var increases = await OpenItemsAsync(new OpenItemsQuery
        {
            BranchId = MainBranchId, PartnerId = _partnerId, Side = R, Direction = Inc,
        });

        increases.Select(i => i.EntryId).Should().Equal(due10, noTerm, due20);
        var first = increases[0];
        first.SourceType.Should().Be(DebtSourceType.Opening);
        first.DocCode.Should().Be("B" + _suffix);
        first.DocDate.Should().Be(new DateOnly(2026, 10, 1));
        first.DueDate.Should().Be(new DateOnly(2026, 10, 10));
        first.PostedAt.Should().Be(Vn("2026-10-01 09:00"));
        first.Direction.Should().Be(Inc);
        first.Amount.Should().Be(800m);
        first.Settled.Should().Be(200m);
        first.Open.Should().Be(600m);
        increases[1].DueDate.Should().Be(new DateOnly(2026, 10, 15));
        increases[1].Settled.Should().Be(0m);
        increases[1].Open.Should().Be(600m);

        // The fully settled Decrease entry is not open.
        var decreases = await OpenItemsAsync(new OpenItemsQuery
        {
            BranchId = MainBranchId, PartnerId = _partnerId, Side = R, Direction = Dec,
        });
        decreases.Should().ContainSingle().Which.Should().Match<OpenItemDto>(i =>
            i.EntryId == openDecrease && i.Direction == Dec && i.Open == 100m && i.DueDate == null);

        // Without a direction filter both directions are listed; the Payable side has nothing.
        (await OpenItemsAsync(new OpenItemsQuery { BranchId = MainBranchId, PartnerId = _partnerId, Side = R }))
            .Select(i => i.EntryId).Should().BeEquivalentTo(new[] { due10, noTerm, due20, openDecrease });
        (await OpenItemsAsync(new OpenItemsQuery { BranchId = MainBranchId, PartnerId = _partnerId, Side = P }))
            .Should().BeEmpty();

        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task For_source_adds_back_only_its_own_at_voucher_settlements()
    {
        var receiptId = Guid.NewGuid();
        var x = await EntryAsync(Draft(_partnerId, R, Inc, 1_000m, "2026-10-01 08:00", "X" + _suffix));
        var w = await EntryAsync(Draft(_partnerId, R, Inc, 300m, "2026-10-01 08:00", "W" + _suffix));
        var v = await EntryAsync(Draft(_partnerId, R, Inc, 400m, "2026-10-01 08:00", "V" + _suffix));
        var s = await EntryAsync(Draft(_partnerId, R, Dec, 850m, "2026-10-05 08:00", "S" + _suffix),
            DebtSourceType.Receipt, receiptId);
        var y = await EntryAsync(Draft(_partnerId, R, Dec, 100m, "2026-10-05 08:00", "Y" + _suffix),
            DebtSourceType.Receipt, Guid.NewGuid());
        await SettleAsync(SettlementOrigin.AtVoucher, receiptId, new SettlementDraft(x, s, 500m));
        await SettleAsync(new SettlementDraft(x, s, 200m));
        await SettleAsync(SettlementOrigin.AtVoucher, Guid.NewGuid(), new SettlementDraft(v, s, 150m));
        await SettleAsync(new SettlementDraft(w, y, 100m));

        var plain = await OpenItemsAsync(new OpenItemsQuery { BranchId = MainBranchId, PartnerId = _partnerId, Side = R });
        plain.Select(i => i.EntryId).Should().BeEquivalentTo(new[] { x, w, v });
        plain.Single(i => i.EntryId == x).Open.Should().Be(300m);
        plain.Single(i => i.EntryId == w).Open.Should().Be(200m);
        plain.Single(i => i.EntryId == v).Open.Should().Be(250m);

        var forSource = await OpenItemsAsync(new OpenItemsQuery
        {
            BranchId = MainBranchId, PartnerId = _partnerId, Side = R,
            ForSourceType = DebtSourceType.Receipt, ForSourceId = receiptId,
        });
        // S itself is excluded. An edit replaces only S's own AtVoucher batch, so X gets back those 500 and keeps the
        // Manual 200; V keeps the AtVoucher 150 another voucher made against S; W keeps its settlement with Y.
        forSource.Select(i => i.EntryId).Should().BeEquivalentTo(new[] { x, w, v });
        var xItem = forSource.Single(i => i.EntryId == x);
        xItem.Settled.Should().Be(200m);
        xItem.Open.Should().Be(800m);
        forSource.Single(i => i.EntryId == w).Open.Should().Be(200m);
        forSource.Single(i => i.EntryId == v).Open.Should().Be(250m);

        await AssertCashDebtInvariantsAsync();
    }

    /// Posts one draft for a source (a new Opening source by default) and returns the entry id.
    private async Task<Guid> EntryAsync(DebtEntryDraft draft, DebtSourceType sourceType = DebtSourceType.Opening,
        Guid? sourceId = null)
    {
        var source = sourceId ?? Guid.NewGuid();
        if (sourceType == DebtSourceType.Opening) LedgerOnlyOpeningSource(source);
        var entries = await InTransactionAsync((sp, ct) => sp.GetRequiredService<IDebtLedgerService>()
            .PostAsync(sourceType, source, new[] { draft }, false, ct: ct));
        return entries.Single().Id;
    }

    private Task<IReadOnlyList<DebtSettlement>> SettleAsync(params SettlementDraft[] drafts) =>
        SettleAsync(SettlementOrigin.Manual, Guid.NewGuid(), drafts);

    private Task<IReadOnlyList<DebtSettlement>> SettleAsync(SettlementOrigin origin, Guid batchId,
        params SettlementDraft[] drafts) =>
        InTransactionAsync((sp, ct) => sp.GetRequiredService<IDebtLedgerService>()
            .AddSettlementsAsync(drafts, origin, batchId, "items", ct));

    private Task<decimal> BalanceAsync(DebtSide side, DateTimeOffset? at) =>
        InTransactionAsync((sp, ct) => sp.GetRequiredService<IDebtQueryService>()
            .BalanceAsync(MainBranchId, _partnerId, side, at, ct));

    private Task<IReadOnlyList<OpenItemDto>> OpenItemsAsync(OpenItemsQuery query) =>
        InTransactionAsync((sp, ct) => sp.GetRequiredService<IDebtQueryService>().OpenItemsAsync(query, ct));
}
