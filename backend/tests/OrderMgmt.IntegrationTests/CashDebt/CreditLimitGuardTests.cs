using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using OrderMgmt.Application.CashDebt.Guards;
using OrderMgmt.Application.CashDebt.Ledger;
using OrderMgmt.Domain.Common;
using OrderMgmt.Domain.Entities.CashDebt;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.CashDebt;

[Collection(nameof(PostgresCollection))]
public class CreditLimitGuardTests : CashDebtTestBase
{
    private const DebtSide R = DebtSide.Receivable;
    private const DebtDirection Inc = DebtDirection.Increase;
    private const DebtDirection Dec = DebtDirection.Decrease;

    private Guid _partnerId;
    private string _suffix = default!;

    public CreditLimitGuardTests(PostgresFixture pg) : base(pg) { }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _suffix = Guid.NewGuid().ToString("N")[..8];
        _partnerId = await CreatePartnerAsync("GL" + _suffix, isCustomer: true, isSupplier: true);
    }

    [Fact]
    public async Task No_limit_or_allow_policy_passes()
    {
        try
        {
            await SetPolicyAsync(LimitPolicy.Block);
            // No limit in this branch: a limit of another branch and a Payable balance do not count.
            var otherBranch = await CreateBranchAsync("G" + _suffix);
            await SetLimitAsync(otherBranch, 100m);
            await PostAsync(Draft(_partnerId, DebtSide.Payable, Inc, 9_000m, "2026-10-01 08:00", "P" + _suffix));

            var before = await SaveAsync(Draft(_partnerId, R, Inc, 5_000m, "2026-10-02 08:00", "A" + _suffix),
                acknowledge: false);
            before.Should().Be(0m);

            // A limit with policy Allow never stops the save.
            await SetLimitAsync(MainBranchId, 1_000m);
            await SetPolicyAsync(LimitPolicy.Allow);
            before = await SaveAsync(Draft(_partnerId, R, Inc, 2_000m, "2026-10-03 08:00", "B" + _suffix),
                acknowledge: false);
            before.Should().Be(5_000m);

            (await BalanceAsync()).Should().Be(7_000m);
            await AssertCashDebtInvariantsAsync();
        }
        finally
        {
            await SetPolicyAsync(LimitPolicy.Warn);
        }
    }

    [Fact]
    public async Task Warn_requires_acknowledgement()
    {
        try
        {
            await SetPolicyAsync(LimitPolicy.Warn);
            await SetLimitAsync(MainBranchId, 1_000m);
            await PostAsync(Draft(_partnerId, R, Inc, 800m, "2026-10-01 08:00", "A" + _suffix));

            var act = () => SaveAsync(Draft(_partnerId, R, Inc, 500m, "2026-10-02 08:00", "B" + _suffix),
                acknowledge: false);
            var ex = (await act.Should().ThrowAsync<ConfirmationRequiredException>()).Which;
            ex.Code.Should().Be("CREDIT_LIMIT_WARNING");
            ex.CanConfirm.Should().BeTrue();
            ex.Details.Should().ContainKey("creditLimit")
                .WhoseValue.Should().Equal("Hạn mức 1.000, dư nợ sau khi lưu 1.300 (trước 800).");
            (await BalanceAsync()).Should().Be(800m, "the rejected save is rolled back");

            var before = await SaveAsync(Draft(_partnerId, R, Inc, 500m, "2026-10-02 08:00", "B" + _suffix),
                acknowledge: true);
            before.Should().Be(800m);
            (await BalanceAsync()).Should().Be(1_300m);
            await AssertCashDebtInvariantsAsync();
        }
        finally
        {
            await SetPolicyAsync(LimitPolicy.Warn);
        }
    }

    [Fact]
    public async Task Block_cannot_be_acknowledged()
    {
        try
        {
            await SetPolicyAsync(LimitPolicy.Block);
            await SetLimitAsync(MainBranchId, 1_000m);
            await PostAsync(Draft(_partnerId, R, Inc, 800m, "2026-10-01 08:00", "A" + _suffix));

            var act = () => SaveAsync(Draft(_partnerId, R, Inc, 500m, "2026-10-02 08:00", "B" + _suffix),
                acknowledge: true);
            var ex = (await act.Should().ThrowAsync<ConfirmationRequiredException>()).Which;
            ex.Code.Should().Be("CREDIT_LIMIT_BLOCKED");
            ex.CanConfirm.Should().BeFalse();
            ex.Details.Should().ContainKey("creditLimit")
                .WhoseValue.Should().Equal("Hạn mức 1.000, dư nợ sau khi lưu 1.300 (trước 800).");
            (await BalanceAsync()).Should().Be(800m, "the rejected save is rolled back");
            await AssertCashDebtInvariantsAsync();
        }
        finally
        {
            await SetPolicyAsync(LimitPolicy.Warn);
        }
    }

    [Fact]
    public async Task Lowering_debt_never_triggers()
    {
        try
        {
            await SetPolicyAsync(LimitPolicy.Block);
            await SetLimitAsync(MainBranchId, 1_000m);
            await PostAsync(Draft(_partnerId, R, Inc, 1_500m, "2026-10-01 08:00", "A" + _suffix));

            // Still over the limit after the save, but lower than before.
            var before = await SaveAsync(Draft(_partnerId, R, Dec, 300m, "2026-10-02 08:00", "B" + _suffix),
                acknowledge: false);
            before.Should().Be(1_500m);
            (await BalanceAsync()).Should().Be(1_200m);
            await AssertCashDebtInvariantsAsync();
        }
        finally
        {
            await SetPolicyAsync(LimitPolicy.Warn);
        }
    }

    /// One save: capture the balance, post the draft as a new source, then check. Returns the captured balance.
    private Task<decimal> SaveAsync(DebtEntryDraft draft, bool acknowledge) =>
        InTransactionAsync(async (sp, ct) =>
        {
            var guard = sp.GetRequiredService<ICreditLimitGuard>();
            var before = await guard.CaptureAsync(draft.BranchId, draft.PartnerId, ct);
            await sp.GetRequiredService<IDebtLedgerService>()
                .PostAsync(DebtSourceType.Opening, Guid.NewGuid(), new[] { draft }, false, ct);
            await guard.CheckAsync(draft.BranchId, draft.PartnerId, before, acknowledge, ct);
            return before;
        });

    private Task PostAsync(DebtEntryDraft draft) =>
        InTransactionAsync((sp, ct) => sp.GetRequiredService<IDebtLedgerService>()
            .PostAsync(DebtSourceType.Opening, Guid.NewGuid(), new[] { draft }, false, ct));

    private Task<decimal> BalanceAsync() =>
        InTransactionAsync((sp, ct) => sp.GetRequiredService<IDebtQueryService>()
            .BalanceAsync(MainBranchId, _partnerId, R, null, ct));

    private Task SetLimitAsync(Guid branchId, decimal amount) => InDbAsync(async db =>
    {
        db.CreditLimits.Add(new CreditLimit { CustomerId = _partnerId, BranchId = branchId, Amount = amount });
        await db.SaveChangesAsync();
    });

    private Task SetPolicyAsync(LimitPolicy policy) =>
        UpdateCashDebtSettingsAsync(s => s.CreditLimitPolicy = policy);
}
