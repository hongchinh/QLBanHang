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
public class DebtLedgerPostTests : CashDebtTestBase
{
    private const DebtSide R = DebtSide.Receivable;
    private const DebtSide P = DebtSide.Payable;
    private const DebtDirection Inc = DebtDirection.Increase;
    private const DebtDirection Dec = DebtDirection.Decrease;

    private Guid _partnerId;
    private string _xCode = default!;
    private string _yCode = default!;

    public DebtLedgerPostTests(PostgresFixture pg) : base(pg) { }

    protected override bool PostsSyntheticOpeningEntries => true;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        _partnerId = await CreatePartnerAsync("LP" + suffix, isCustomer: true, isSupplier: true);
        _xCode = "X" + suffix;
        _yCode = "Y" + suffix;
    }

    [Fact]
    public async Task Insert_update_in_place_and_delete_side()
    {
        var source = Guid.NewGuid();

        await PostAsync(source, Draft(_partnerId, R, Inc, 1_000m, "2026-10-01 08:00", _xCode));
        var inserted = (await EntriesOfAsync(DebtSourceType.Opening, source)).Should().ContainSingle().Subject;
        inserted.Amount.Should().Be(1_000m);
        inserted.Direction.Should().Be(Inc);
        inserted.DocCode.Should().Be(_xCode);
        inserted.PostedAt.Should().Be(Vn("2026-10-01 08:00"));
        inserted.DueDate.Should().Be(new DateOnly(2026, 10, 1));

        await PostAsync(source, Draft(_partnerId, R, Inc, 1_500m, "2026-10-02 09:00", _xCode));
        var updated = (await EntriesOfAsync(DebtSourceType.Opening, source)).Should().ContainSingle().Subject;
        updated.Id.Should().Be(inserted.Id);
        updated.Amount.Should().Be(1_500m);
        updated.PostedAt.Should().Be(Vn("2026-10-02 09:00"));
        updated.DocDate.Should().Be(new DateOnly(2026, 10, 2));

        await PostAsync(source);
        (await EntriesOfAsync(DebtSourceType.Opening, source)).Should().BeEmpty();

        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Two_sides_for_one_source()
    {
        var source = Guid.NewGuid();

        await PostAsync(source,
            Draft(_partnerId, R, Dec, 300m, "2026-10-01 08:00", _xCode),
            Draft(_partnerId, P, Dec, 300m, "2026-10-01 08:00", _xCode));

        var entries = await EntriesOfAsync(DebtSourceType.Opening, source);
        entries.Select(e => (e.Side, e.Direction, e.Amount)).Should().BeEquivalentTo(new[]
        {
            (R, Dec, 300m),
            (P, Dec, 300m),
        });
        entries.Should().OnlyContain(e => e.DueDate == null, "Decrease entries have no due date");

        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Changing_posted_at_recomputes_effective_at()
    {
        var (x, _, settlementId) = await SettledPairAsync();
        (await SettlementOf(settlementId)).EffectiveAt.Should().Be(Vn("2026-10-03 08:00"));

        await PostAsync(x, Draft(_partnerId, R, Inc, 1_000m, "2026-10-05 10:00", _xCode));

        (await SettlementOf(settlementId)).EffectiveAt.Should().Be(Vn("2026-10-05 10:00"));
        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Amount_below_settled_needs_confirmation()
    {
        var (x, _, settlementId) = await SettledPairAsync();
        var xEntryId = (await EntriesOfAsync(DebtSourceType.Opening, x)).Single().Id;

        var withoutConfirm = () => PostAsync(x, Draft(_partnerId, R, Inc, 300m, "2026-10-01 08:00", _xCode));
        var error = (await withoutConfirm.Should().ThrowAsync<ConfirmationRequiredException>()).Which;
        error.Code.Should().Be("DEBT_SETTLEMENTS_WILL_BE_REMOVED");
        error.CanConfirm.Should().BeTrue();
        error.Details.Keys.Should().Equal(_yCode);
        error.Details[_yCode].Should().Equal($"Bỏ đối trừ 400 với {_xCode}");

        // Nothing changed.
        (await EntriesOfAsync(DebtSourceType.Opening, x)).Single().Amount.Should().Be(1_000m);
        (await SettlementsOfAsync(xEntryId)).Should().ContainSingle(s => s.Id == settlementId);

        await PostAsync(x, confirm: true, Draft(_partnerId, R, Inc, 300m, "2026-10-01 08:00", _xCode));

        var entry = (await EntriesOfAsync(DebtSourceType.Opening, x)).Single();
        entry.Id.Should().Be(xEntryId);
        entry.Amount.Should().Be(300m);
        (await SettlementsOfAsync(xEntryId)).Should().BeEmpty();
        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Amount_above_settled_keeps_settlements()
    {
        var (x, _, settlementId) = await SettledPairAsync();

        await PostAsync(x, Draft(_partnerId, R, Inc, 600m, "2026-10-01 08:00", _xCode));

        var entry = (await EntriesOfAsync(DebtSourceType.Opening, x)).Single();
        entry.Amount.Should().Be(600m);
        (await SettlementsOfAsync(entry.Id)).Should().ContainSingle()
            .Which.Should().Match<DebtSettlement>(s => s.Id == settlementId && s.Amount == 400m);
        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Partner_or_direction_change_with_settlements_is_409()
    {
        var (x, _, settlementId) = await SettledPairAsync();
        var otherPartner = await CreatePartnerAsync("LQ" + Guid.NewGuid().ToString("N")[..8], isCustomer: true, isSupplier: true);

        var changePartner = () => PostAsync(x, confirm: true, Draft(otherPartner, R, Inc, 1_000m, "2026-10-01 08:00", _xCode));
        (await changePartner.Should().ThrowAsync<ConflictException>()).Which.Code.Should().Be("DEBT_HAS_SETTLEMENTS");

        var changeDirection = () => PostAsync(x, confirm: true, Draft(_partnerId, R, Dec, 1_000m, "2026-10-01 08:00", _xCode));
        (await changeDirection.Should().ThrowAsync<ConflictException>()).Which.Code.Should().Be("DEBT_HAS_SETTLEMENTS");

        var entry = (await EntriesOfAsync(DebtSourceType.Opening, x)).Single();
        (entry.PartnerId, entry.Direction).Should().Be((_partnerId, Inc));
        (await SettlementsOfAsync(entry.Id)).Should().ContainSingle(s => s.Id == settlementId);
        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Removing_a_settled_side_on_save_is_409()
    {
        var (x, _, _) = await SettledPairAsync();

        var removeSide = () => PostAsync(x, confirm: true);
        (await removeSide.Should().ThrowAsync<ConflictException>()).Which.Code.Should().Be("DEBT_HAS_SETTLEMENTS");

        (await EntriesOfAsync(DebtSourceType.Opening, x)).Should().ContainSingle();
        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Two_drafts_for_one_side_throw_argument_exception()
    {
        var source = Guid.NewGuid();

        var post = () => PostAsync(source,
            Draft(_partnerId, R, Inc, 100m, "2026-10-01 08:00", _xCode),
            Draft(_partnerId, R, Dec, 50m, "2026-10-01 08:00", _xCode));

        await post.Should().ThrowAsync<ArgumentException>();
        (await EntriesOfAsync(DebtSourceType.Opening, source)).Should().BeEmpty();
        await AssertCashDebtInvariantsAsync();
    }

    /// X = R+ 1,000 @10-01, Y = R− 400 @10-03, settled 400 (Manual).
    private async Task<(Guid X, Guid Y, Guid SettlementId)> SettledPairAsync()
    {
        var x = Guid.NewGuid();
        var y = Guid.NewGuid();
        var xEntry = (await PostAsync(x, Draft(_partnerId, R, Inc, 1_000m, "2026-10-01 08:00", _xCode))).Single();
        var yEntry = (await PostAsync(y, Draft(_partnerId, R, Dec, 400m, "2026-10-03 08:00", _yCode))).Single();

        var settlements = await InTransactionAsync((sp, ct) => sp.GetRequiredService<IDebtLedgerService>()
            .AddSettlementsAsync(new[] { new SettlementDraft(xEntry.Id, yEntry.Id, 400m) },
                SettlementOrigin.Manual, Guid.NewGuid(), "items", ct));
        return (x, y, settlements.Single().Id);
    }

    private Task<IReadOnlyList<DebtEntry>> PostAsync(Guid sourceId, params DebtEntryDraft[] drafts) =>
        PostAsync(sourceId, confirm: false, drafts);

    private Task<IReadOnlyList<DebtEntry>> PostAsync(Guid sourceId, bool confirm, params DebtEntryDraft[] drafts) =>
        InTransactionAsync((sp, ct) => sp.GetRequiredService<IDebtLedgerService>()
            .PostAsync(DebtSourceType.Opening, sourceId, drafts, confirm, ct));

    private Task<DebtSettlement> SettlementOf(Guid settlementId) =>
        InDbAsync(db => db.DebtSettlements.AsNoTracking().SingleAsync(s => s.Id == settlementId));
}
