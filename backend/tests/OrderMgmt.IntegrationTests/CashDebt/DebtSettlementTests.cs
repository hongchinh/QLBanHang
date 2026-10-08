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
        var productId = await CreateInventoryProductAsync("SV" + _suffix);
        var stockIn = await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-01 08:00", LineRequest(productId, 10, 100));
        var stockOut = await CreateVoucherAsync(_client, StockDirection.Out, "XKH", "2026-10-02 08:00", LineRequest(productId, 2, 100));

        var outEntry = await EntryAsync(Draft(_partnerId, R, Inc, 200m, "2026-10-02 08:00", stockOut.Code),
            DebtSourceType.StockOut, stockOut.Id);
        var inEntry = await EntryAsync(Draft(_partnerId, R, Dec, 150m, "2026-10-01 08:00", stockIn.Code),
            DebtSourceType.StockIn, stockIn.Id);
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
        await SettleAsync(SettlementOrigin.Auto, new SettlementDraft(x, y, 400m));
        var manual = (await SettleAsync(SettlementOrigin.Manual, new SettlementDraft(x, z, 300m))).Single();

        await InTransactionAsync(async (sp, ct) =>
        {
            await sp.GetRequiredService<IDebtLedgerService>()
                .RemoveOwnSettlementsAsync(DebtSourceType.Opening, source, SettlementOrigin.Auto, ct);
            return true;
        });

        (await SettlementsOfAsync(x)).Should().ContainSingle().Which.Id.Should().Be(manual.Id);
        (await SettlementsOfAsync(y)).Should().BeEmpty();
        await AssertCashDebtInvariantsAsync();
    }

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
        var entries = await InTransactionAsync((sp, ct) => sp.GetRequiredService<IDebtLedgerService>()
            .PostAsync(sourceType, sourceId ?? Guid.NewGuid(), new[] { draft }, false, ct));
        return entries.Single().Id;
    }

    private Task<IReadOnlyList<DebtSettlement>> SettleAsync(SettlementOrigin origin, params SettlementDraft[] drafts) =>
        InTransactionAsync((sp, ct) => sp.GetRequiredService<IDebtLedgerService>()
            .AddSettlementsAsync(drafts, origin, Guid.NewGuid(), "items", ct));

    private async Task ExpectErrorsAsync(SettlementDraft[] drafts, params string[] keys)
    {
        var add = () => SettleAsync(SettlementOrigin.Manual, drafts);
        var error = (await add.Should().ThrowAsync<ValidationDomainException>()).Which;
        error.Errors.Keys.Should().BeEquivalentTo(keys);
    }

    private Task RemoveSourceAsync(Guid sourceId, bool confirm) =>
        InTransactionAsync(async (sp, ct) =>
        {
            await sp.GetRequiredService<IDebtLedgerService>().RemoveSourceAsync(DebtSourceType.Opening, sourceId, confirm, ct);
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
