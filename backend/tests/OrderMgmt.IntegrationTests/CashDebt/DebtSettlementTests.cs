using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderMgmt.Application.CashDebt.Ledger;
using OrderMgmt.Domain.Common;
using OrderMgmt.Domain.Entities.CashDebt;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.CashDebt;

[Collection(nameof(PostgresCollection))]
public class DebtSettlementTests : CashDebtTestBase
{
    private const DebtSide R = DebtSide.Receivable;
    private const DebtSide P = DebtSide.Payable;
    private const DebtDirection Inc = DebtDirection.Increase;
    private const DebtDirection Dec = DebtDirection.Decrease;

    private Guid _partnerId;
    private string _suffix = default!;

    public DebtSettlementTests(PostgresFixture pg) : base(pg) { }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _suffix = Guid.NewGuid().ToString("N")[..8];
        _partnerId = await CreatePartnerAsync("SP" + _suffix, isCustomer: true, isSupplier: true);
    }

    [Fact]
    public async Task Add_validates_pairs_amounts_and_open_balance()
    {
        var otherPartner = await CreatePartnerAsync("SQ" + _suffix, isCustomer: true, isSupplier: true);
        var otherBranch = await CreateBranchAsync("SB" + _suffix[..6]);

        var incA = await EntryAsync(Draft(_partnerId, R, Inc, 1_000m, "2026-10-01 08:00", "A" + _suffix));
        var incB = await EntryAsync(Draft(_partnerId, R, Inc, 500m, "2026-10-01 08:00", "B" + _suffix));
        var decC = await EntryAsync(Draft(_partnerId, R, Dec, 400m, "2026-10-02 08:00", "C" + _suffix));
        var decBig = await EntryAsync(Draft(_partnerId, R, Dec, 2_000m, "2026-10-02 08:00", "D" + _suffix));
        var decOtherPartner = await EntryAsync(Draft(otherPartner, R, Dec, 100m, "2026-10-02 08:00", "E" + _suffix));
        var decPayable = await EntryAsync(Draft(_partnerId, P, Dec, 100m, "2026-10-02 08:00", "F" + _suffix));
        var decOtherBranch = await EntryAsync(Draft(_partnerId, R, Dec, 100m, "2026-10-02 08:00", "G" + _suffix, otherBranch));

        // Two Increase entries.
        await ExpectErrorsAsync(new[] { new SettlementDraft(incA, incB, 100m) }, "items[0].decreaseEntryId");
        // A Decrease entry in the Increase slot.
        await ExpectErrorsAsync(new[] { new SettlementDraft(decC, decBig, 100m) }, "items[0].increaseEntryId");
        // An unknown entry.
        await ExpectErrorsAsync(new[] { new SettlementDraft(incA, Guid.NewGuid(), 100m) }, "items[0].decreaseEntryId");
        // Different partner, side and branch.
        await ExpectErrorsAsync(new[] { new SettlementDraft(incA, decOtherPartner, 100m) }, "items[0].decreaseEntryId");
        await ExpectErrorsAsync(new[] { new SettlementDraft(incA, decPayable, 100m) }, "items[0].decreaseEntryId");
        await ExpectErrorsAsync(new[] { new SettlementDraft(incA, decOtherBranch, 100m) }, "items[0].decreaseEntryId");
        // Amount 0.
        await ExpectErrorsAsync(new[] { new SettlementDraft(incA, decC, 0m) }, "items[0].amount");
        // Σ over the Increase entry's amount (400 + 700 > 1,000).
        await ExpectErrorsAsync(new[]
        {
            new SettlementDraft(incA, decC, 400m),
            new SettlementDraft(incA, decBig, 700m),
        }, "items[1].amount");
        // Σ over the Decrease entry's amount (300 + 200 > 400).
        await ExpectErrorsAsync(new[]
        {
            new SettlementDraft(incA, decC, 300m),
            new SettlementDraft(incB, decC, 200m),
        }, "items[1].amount");
        // Errors are collected over all items.
        await ExpectErrorsAsync(new[]
        {
            new SettlementDraft(incA, incB, 100m),
            new SettlementDraft(incA, decC, 0m),
        }, "items[0].decreaseEntryId", "items[1].amount");

        // Existing settlements count towards the open balance.
        await SettleAsync(SettlementOrigin.Manual, new SettlementDraft(incA, decBig, 900m));
        await ExpectErrorsAsync(new[] { new SettlementDraft(incA, decC, 101m) }, "items[0].amount");
        await SettleAsync(SettlementOrigin.Manual, new SettlementDraft(incA, decC, 100m));

        (await SettlementsOfAsync(incA)).Select(s => s.Amount).Should().Equal(900m, 100m);
        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Add_rejects_effective_at_in_locked_period()
    {
        var x = await EntryAsync(Draft(_partnerId, R, Inc, 1_000m, "2026-10-01 08:00", "X" + _suffix));
        var y = await EntryAsync(Draft(_partnerId, R, Dec, 300m, "2026-10-03 08:00", "Y" + _suffix));
        var later = await EntryAsync(Draft(_partnerId, R, Dec, 300m, "2026-10-05 08:00", "Z" + _suffix));

        await SetPeriodLockAsync(new DateOnly(2026, 10, 4));
        try
        {
            var locked = () => SettleAsync(SettlementOrigin.Manual, new SettlementDraft(x, y, 300m));
            (await locked.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("PERIOD_LOCKED");
            (await SettlementsOfAsync(x)).Should().BeEmpty();

            var added = await SettleAsync(SettlementOrigin.Manual, new SettlementDraft(x, later, 300m));
            added.Should().ContainSingle().Which.EffectiveAt.Should().Be(Vn("2026-10-05 08:00"));
        }
        finally
        {
            await SetPeriodLockAsync(null);
        }

        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Remove_settlements_logs_activity_on_both_stock_vouchers()
    {
        // The stock vouchers post their own entries: a customer return (NTL) R− 150 and a sale (XBH) R+ 200.
        var productId = await CreateInventoryProductAsync("SV" + _suffix);
        var stockIn = await CreateVoucherAsync(_client, StockDirection.In, "NTL", "2026-10-01 08:00",
            r => r.PartnerId = _partnerId, LineRequest(productId, 10, 15));
        var stockOut = await CreateVoucherAsync(_client, StockDirection.Out, "XBH", "2026-10-02 08:00",
            r => r.PartnerId = _partnerId, LineRequest(productId, 2, 100));

        var outEntry = (await EntriesOfAsync(DebtSourceType.StockOut, stockOut.Id)).Single().Id;
        var inEntry = (await EntriesOfAsync(DebtSourceType.StockIn, stockIn.Id)).Single().Id;
        var settlement = (await SettleAsync(SettlementOrigin.Manual, new SettlementDraft(outEntry, inEntry, 150m))).Single();

        var removed = await InTransactionAsync((sp, ct) => sp.GetRequiredService<IDebtLedgerService>()
            .RemoveSettlementsAsync(new[] { settlement.Id }, ct));

        removed.Should().ContainSingle().Which.Should().Be(
            new RemovedSettlement(settlement.Id, outEntry, inEntry, 150m, SettlementOrigin.Manual));
        (await SettlementsOfAsync(outEntry)).Should().BeEmpty();
        (await SettlementRemovedActivitiesAsync(stockOut.Id)).Should().Equal($"Bỏ đối trừ 150 với {stockIn.Code}");
        (await SettlementRemovedActivitiesAsync(stockIn.Id)).Should().Equal($"Bỏ đối trừ 150 với {stockOut.Code}");
        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Remove_own_settlements_only_touches_that_origin()
    {
        var source = Guid.NewGuid();
        var x = await EntryAsync(Draft(_partnerId, R, Inc, 1_000m, "2026-10-01 08:00", "X" + _suffix), sourceId: source);
        var y = await EntryAsync(Draft(_partnerId, R, Dec, 400m, "2026-10-02 08:00", "Y" + _suffix));
        var z = await EntryAsync(Draft(_partnerId, R, Dec, 300m, "2026-10-02 08:00", "Z" + _suffix));
        await SettleAsync(SettlementOrigin.Auto, source, new SettlementDraft(x, y, 400m));
        var manual = (await SettleAsync(SettlementOrigin.Manual, new SettlementDraft(x, z, 300m))).Single();

        await RemoveOwnAsync(source, SettlementOrigin.Auto);

        (await SettlementsOfAsync(x)).Should().ContainSingle().Which.Id.Should().Be(manual.Id);
        (await SettlementsOfAsync(y)).Should().BeEmpty();
        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Remove_own_settlements_keeps_the_same_origin_made_by_another_source()
    {
        // Two cash vouchers: X allocated Y (batch X), and Z allocated X (batch Z). Both are AtVoucher.
        var source = Guid.NewGuid();
        var other = Guid.NewGuid();
        var x = await EntryAsync(Draft(_partnerId, R, Inc, 1_000m, "2026-10-01 08:00", "X" + _suffix), sourceId: source);
        var y = await EntryAsync(Draft(_partnerId, R, Dec, 400m, "2026-10-02 08:00", "Y" + _suffix));
        var z = await EntryAsync(Draft(_partnerId, R, Dec, 300m, "2026-10-02 08:00", "Z" + _suffix), sourceId: other);
        await SettleAsync(SettlementOrigin.AtVoucher, source, new SettlementDraft(x, y, 400m));
        var foreign = (await SettleAsync(SettlementOrigin.AtVoucher, other, new SettlementDraft(x, z, 300m))).Single();

        await RemoveOwnAsync(source, SettlementOrigin.AtVoucher);

        (await SettlementsOfAsync(x)).Should().ContainSingle().Which.Id.Should().Be(foreign.Id);
        (await SettlementsOfAsync(y)).Should().BeEmpty();
        await AssertCashDebtInvariantsAsync();
    }

    private Task RemoveOwnAsync(Guid source, SettlementOrigin origin) =>
        InTransactionAsync(async (sp, ct) =>
        {
            await sp.GetRequiredService<IDebtLedgerService>()
                .RemoveOwnSettlementsAsync(DebtSourceType.Opening, source, origin, ct);
            return true;
        });

    [Fact]
    public async Task Remove_source_requires_confirmation_when_settled()
    {
        var source = Guid.NewGuid();
        var xCode = "X" + _suffix;
        var yCode = "Y" + _suffix;
        var x = await EntryAsync(Draft(_partnerId, R, Inc, 1_000m, "2026-10-01 08:00", xCode), sourceId: source);
        var y = await EntryAsync(Draft(_partnerId, R, Dec, 400m, "2026-10-02 08:00", yCode));
        await SettleAsync(SettlementOrigin.Manual, new SettlementDraft(x, y, 400m));

        var withoutConfirm = () => RemoveSourceAsync(source, confirm: false);
        var error = (await withoutConfirm.Should().ThrowAsync<ConfirmationRequiredException>()).Which;
        error.Code.Should().Be("DEBT_SETTLEMENTS_WILL_BE_REMOVED");
        error.CanConfirm.Should().BeTrue();
        error.Details.Keys.Should().Equal(yCode);
        error.Details[yCode].Should().Equal($"Bỏ đối trừ 400 với {xCode}");
        (await EntriesOfAsync(DebtSourceType.Opening, source)).Should().ContainSingle();
        (await SettlementsOfAsync(x)).Should().ContainSingle();

        await RemoveSourceAsync(source, confirm: true);

        (await EntriesOfAsync(DebtSourceType.Opening, source)).Should().BeEmpty();
        (await SettlementsOfAsync(y)).Should().BeEmpty();
        (await InDbAsync(db => db.DebtEntries.AnyAsync(e => e.Id == y))).Should().BeTrue();

        // A source without settlements is removed without confirmation.
        var unsettled = Guid.NewGuid();
        await EntryAsync(Draft(_partnerId, R, Inc, 100m, "2026-10-01 08:00", "U" + _suffix), sourceId: unsettled);
        await RemoveSourceAsync(unsettled, confirm: false);
        (await EntriesOfAsync(DebtSourceType.Opening, unsettled)).Should().BeEmpty();

        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Remove_source_with_a_token_removes_only_listed_settlements()
    {
        var source = Guid.NewGuid();
        var yCode = "Y" + _suffix;
        var zCode = "Z" + _suffix;
        var x = await EntryAsync(Draft(_partnerId, R, Inc, 1_000m, "2026-10-01 08:00", "X" + _suffix), sourceId: source);
        var y = await EntryAsync(Draft(_partnerId, R, Dec, 400m, "2026-10-02 08:00", yCode));
        var z = await EntryAsync(Draft(_partnerId, R, Dec, 300m, "2026-10-02 08:00", zCode));
        var s1 = (await SettleAsync(SettlementOrigin.Manual, new SettlementDraft(x, y, 400m))).Single();

        var first = (await FluentActions.Awaiting(() => RemoveSourceAsync(source, confirm: false))
            .Should().ThrowAsync<ConfirmationRequiredException>()).Which;
        first.Token.Should().Be(SettlementRemovalConfirmation.TokenOf(new[] { s1.Id }));

        var s2 = (await SettleAsync(SettlementOrigin.Manual, new SettlementDraft(x, z, 300m))).Single();

        var stale = (await FluentActions.Awaiting(() => RemoveSourceAsync(source, confirm: true, first.Token))
            .Should().ThrowAsync<ConfirmationRequiredException>()).Which;
        stale.Code.Should().Be("DEBT_SETTLEMENTS_WILL_BE_REMOVED");
        stale.Details.Keys.Should().BeEquivalentTo(yCode, zCode);
        SettlementRemovalConfirmation.Covers(stale.Token, new[] { s1.Id, s2.Id }).Should().BeTrue();
        (await EntriesOfAsync(DebtSourceType.Opening, source)).Should().ContainSingle();
        (await SettlementsOfAsync(x)).Should().HaveCount(2);

        await RemoveSourceAsync(source, confirm: true, stale.Token);

        (await EntriesOfAsync(DebtSourceType.Opening, source)).Should().BeEmpty();
        (await SettlementsOfAsync(y)).Should().BeEmpty();
        (await SettlementsOfAsync(z)).Should().BeEmpty();
        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Post_shrink_with_a_covering_token_proceeds()
    {
        var source = Guid.NewGuid();
        var xCode = "X" + _suffix;
        var x = await EntryAsync(Draft(_partnerId, R, Inc, 1_000m, "2026-10-01 08:00", xCode), sourceId: source);
        var y = await EntryAsync(Draft(_partnerId, R, Dec, 400m, "2026-10-02 08:00", "Y" + _suffix));
        var z = await EntryAsync(Draft(_partnerId, R, Dec, 300m, "2026-10-02 08:00", "Z" + _suffix));
        var s1 = (await SettleAsync(SettlementOrigin.Manual, new SettlementDraft(x, y, 400m))).Single();
        var shrunk = Draft(_partnerId, R, Inc, 100m, "2026-10-01 08:00", xCode);

        var first = (await FluentActions.Awaiting(() => RepostAsync(source, shrunk, confirm: false))
            .Should().ThrowAsync<ConfirmationRequiredException>()).Which;
        first.Token.Should().Be(SettlementRemovalConfirmation.TokenOf(new[] { s1.Id }));

        var s2 = (await SettleAsync(SettlementOrigin.Manual, new SettlementDraft(x, z, 300m))).Single();

        var stale = (await FluentActions.Awaiting(() => RepostAsync(source, shrunk, confirm: true, first.Token))
            .Should().ThrowAsync<ConfirmationRequiredException>()).Which;
        stale.Code.Should().Be("DEBT_SETTLEMENTS_WILL_BE_REMOVED");
        stale.Details.Keys.Should().BeEquivalentTo("Y" + _suffix, "Z" + _suffix);
        SettlementRemovalConfirmation.Covers(stale.Token, new[] { s1.Id, s2.Id }).Should().BeTrue();
        (await SettlementsOfAsync(x)).Should().HaveCount(2);

        await RepostAsync(source, shrunk, confirm: true, stale.Token);

        (await SettlementsOfAsync(x)).Should().BeEmpty();
        (await EntriesOfAsync(DebtSourceType.Opening, source)).Single().Amount.Should().Be(100m);
        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Settled_amounts_can_exclude_an_origin()
    {
        var x = await EntryAsync(Draft(_partnerId, R, Inc, 1_000m, "2026-10-01 08:00", "X" + _suffix));
        var y = await EntryAsync(Draft(_partnerId, R, Dec, 400m, "2026-10-02 08:00", "Y" + _suffix));
        var z = await EntryAsync(Draft(_partnerId, R, Dec, 300m, "2026-10-02 08:00", "Z" + _suffix));
        var open = await EntryAsync(Draft(_partnerId, R, Inc, 50m, "2026-10-01 08:00", "O" + _suffix));
        await SettleAsync(SettlementOrigin.Manual, new SettlementDraft(x, y, 400m));
        await SettleAsync(SettlementOrigin.Auto, new SettlementDraft(x, z, 300m));

        var all = await SettledAmountsAsync(new[] { x, y, z, open }, null);
        all.Should().BeEquivalentTo(new Dictionary<Guid, decimal> { [x] = 700m, [y] = 400m, [z] = 300m, [open] = 0m });

        var withoutAuto = await SettledAmountsAsync(new[] { x, y, z, open }, SettlementOrigin.Auto);
        withoutAuto.Should().BeEquivalentTo(new Dictionary<Guid, decimal> { [x] = 400m, [y] = 400m, [z] = 0m, [open] = 0m });

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

    private Task<IReadOnlyList<DebtSettlement>> SettleAsync(SettlementOrigin origin, params SettlementDraft[] drafts) =>
        SettleAsync(origin, Guid.NewGuid(), drafts);

    private Task<IReadOnlyList<DebtSettlement>> SettleAsync(SettlementOrigin origin, Guid batchId,
        params SettlementDraft[] drafts) =>
        InTransactionAsync((sp, ct) => sp.GetRequiredService<IDebtLedgerService>()
            .AddSettlementsAsync(drafts, origin, batchId, "items", ct));

    private async Task ExpectErrorsAsync(SettlementDraft[] drafts, params string[] keys)
    {
        var add = () => SettleAsync(SettlementOrigin.Manual, drafts);
        var error = (await add.Should().ThrowAsync<ValidationDomainException>()).Which;
        error.Errors.Keys.Should().BeEquivalentTo(keys);
    }

    private Task RemoveSourceAsync(Guid sourceId, bool confirm, string? confirmedSettlements = null) =>
        InTransactionAsync(async (sp, ct) =>
        {
            await sp.GetRequiredService<IDebtLedgerService>().RemoveSourceAsync(DebtSourceType.Opening, sourceId, confirm, confirmedSettlements, ct);
            return true;
        });

    private Task RepostAsync(Guid source, DebtEntryDraft draft, bool confirm, string? confirmedSettlements = null) =>
        InTransactionAsync(async (sp, ct) =>
        {
            await sp.GetRequiredService<IDebtLedgerService>()
                .PostAsync(DebtSourceType.Opening, source, new[] { draft }, confirm, confirmedSettlements, ct);
            return true;
        });

    private Task<IReadOnlyDictionary<Guid, decimal>> SettledAmountsAsync(Guid[] entryIds, SettlementOrigin? excludeOrigin) =>
        InTransactionAsync((sp, ct) => sp.GetRequiredService<IDebtLedgerService>()
            .SettledAmountsAsync(entryIds, excludeOrigin, ct));

    private Task<List<string>> SettlementRemovedActivitiesAsync(Guid stockVoucherId) =>
        InDbAsync(db => db.StockVoucherActivities.AsNoTracking()
            .Where(a => a.StockVoucherId == stockVoucherId && a.Action == StockVoucherActivityAction.SettlementRemoved)
            .Select(a => a.Description)
            .ToListAsync());
}
