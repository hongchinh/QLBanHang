using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderMgmt.Application.CashDebt.Guards;
using OrderMgmt.Domain.Common;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.CashDebt.CashVouchers;

[Collection(nameof(PostgresCollection))]
public class FundBalanceGuardTests : CashDebtTestBase
{
    private Guid _branchId;
    private Guid _fundId;

    public FundBalanceGuardTests(PostgresFixture pg) : base(pg) { }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        // A fresh branch: its QTM fund has no other movements.
        _branchId = await CreateBranchAsync("FG" + Guid.NewGuid().ToString("N")[..8]);
        _fundId = await CashFundIdAsync(_branchId);
        await SetFundOpeningAmountAsync(_fundId, 1_000m);
    }

    [Fact]
    public async Task Warn_and_block_on_a_payment_that_overdraws()
    {
        try
        {
            await SetPolicyAsync(LimitPolicy.Warn);
            var at = Vn("2026-10-05 09:00");

            Guid paymentId = default;
            var act = () => SaveAsync(at, async () => paymentId = await PayAsync(1_500m, at), acknowledge: false);
            var ex = (await act.Should().ThrowAsync<ConfirmationRequiredException>()).Which;
            ex.Code.Should().Be("NEGATIVE_FUND_WARNING");
            ex.Message.Should().Be("Quỹ sẽ bị âm.");
            ex.CanConfirm.Should().BeTrue();
            ex.Details.Should().ContainKey("QTM").WhoseValue.Should().Equal("Âm 500 tại 05/10/2026 09:00");

            // Acknowledged: the same overdraft passes.
            await SoftDeleteAsync(paymentId);
            await SaveAsync(at, () => PayAsync(1_500m, at), acknowledge: true);

            // Block cannot be acknowledged.
            await SetPolicyAsync(LimitPolicy.Block);
            var later = Vn("2026-10-06 10:30");
            var blocked = () => SaveAsync(later, () => PayAsync(200m, later), acknowledge: true);
            var blockedEx = (await blocked.Should().ThrowAsync<ConfirmationRequiredException>()).Which;
            blockedEx.Code.Should().Be("NEGATIVE_FUND_BLOCKED");
            blockedEx.Message.Should().Be("Không đủ tiền trong quỹ.");
            blockedEx.CanConfirm.Should().BeFalse();
            blockedEx.Details.Should().ContainKey("QTM").WhoseValue.Should().Equal("Âm 700 tại 06/10/2026 10:30");
        }
        finally
        {
            await SetPolicyAsync(LimitPolicy.Warn);
        }
    }

    [Fact]
    public async Task Allow_policy_passes()
    {
        try
        {
            await SetPolicyAsync(LimitPolicy.Allow);
            var at = Vn("2026-10-05 09:00");

            // The same fund listed twice: the earlier From wins.
            var capture = await InTransactionAsync(async (sp, ct) =>
            {
                var guard = sp.GetRequiredService<IFundBalanceGuard>();
                var captured = await guard.CaptureAsync(new[] { (_fundId, Vn("2026-10-07 08:00")), (_fundId, at) }, ct);
                await PayAsync(1_500m, at);
                await guard.CheckAsync(captured, acknowledge: false, ct);
                return captured;
            });

            var item = capture.Items.Should().ContainSingle().Subject;
            item.FundId.Should().Be(_fundId);
            item.From.Should().Be(at);
            item.Before.Min.Should().Be(1_000m);
            item.Before.FirstNegativeAt.Should().BeNull();
        }
        finally
        {
            await SetPolicyAsync(LimitPolicy.Warn);
        }
    }

    [Fact]
    public async Task Existing_deficit_reduced_passes()
    {
        try
        {
            await SetPolicyAsync(LimitPolicy.Block);
            // Opening 1,000, a payment of 1,500 on 10-03 → −500. Cancelled and deleted vouchers do not count.
            await PayAsync(1_500m, Vn("2026-10-03 09:00"));
            await InsertCashVoucherAsync(_branchId, _fundId, CashVoucherType.Payment, 9_000m, Vn("2026-10-03 10:00"),
                DocumentStatus.Cancelled);
            await SoftDeleteAsync(await PayAsync(9_000m, Vn("2026-10-03 11:00")));

            // A receipt of 200 before the deficit lowers it to −300: not worse, even under Block.
            var at = Vn("2026-10-02 09:00");
            var capture = await SaveAsync(at,
                () => InsertCashVoucherAsync(_branchId, _fundId, CashVoucherType.Receipt, 200m, at), acknowledge: false);
            capture.Items.Single().Before.Min.Should().Be(-500m);

            var movements = await InTransactionAsync((sp, ct) =>
                sp.GetRequiredService<IFundBalanceGuard>().MovementsAsync(_fundId, ct));
            movements.Select(m => m.Delta).Should().Equal(1_000m, 200m, -1_500m);

            // A later receipt does not move the first negative point either.
            await SaveAsync(Vn("2026-10-04 09:00"),
                () => InsertCashVoucherAsync(_branchId, _fundId, CashVoucherType.Receipt, 100m, Vn("2026-10-04 09:00")),
                acknowledge: false);
        }
        finally
        {
            await SetPolicyAsync(LimitPolicy.Warn);
        }
    }

    /// One save: capture the fund from `from`, apply `change` (committed by its own context), then check.
    private Task<FundCapture> SaveAsync(DateTimeOffset from, Func<Task> change, bool acknowledge) =>
        InTransactionAsync(async (sp, ct) =>
        {
            var guard = sp.GetRequiredService<IFundBalanceGuard>();
            var capture = await guard.CaptureAsync(new[] { (_fundId, from) }, ct);
            await change();
            await guard.CheckAsync(capture, acknowledge, ct);
            return capture;
        });

    private Task<Guid> PayAsync(decimal total, DateTimeOffset at) =>
        InsertCashVoucherAsync(_branchId, _fundId, CashVoucherType.Payment, total, at);

    private Task SoftDeleteAsync(Guid voucherId) =>
        InDbAsync(db => db.CashVouchers.Where(v => v.Id == voucherId)
            .ExecuteUpdateAsync(s => s.SetProperty(v => v.IsDeleted, true)));

    private Task SetPolicyAsync(LimitPolicy policy) =>
        UpdateCashDebtSettingsAsync(s => s.NegativeFundPolicy = policy);
}
