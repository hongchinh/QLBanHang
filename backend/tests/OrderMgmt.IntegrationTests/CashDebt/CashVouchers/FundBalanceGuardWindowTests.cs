using System.Data.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OrderMgmt.Application.CashDebt.Common;
using OrderMgmt.Application.CashDebt.Guards;
using OrderMgmt.Domain.Common;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Domain.Enums;
using OrderMgmt.Infrastructure.Persistence;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.CashDebt.CashVouchers;

/// Pins the boundary of the negative-fund guard's window (P17, D31): the balance strictly before `from`, then the
/// movements from `from` on, inflows before outflows at the same instant. Opening amount of the fund: 1,000.
[Collection(nameof(PostgresCollection))]
public class FundBalanceGuardWindowTests : CashDebtTestBase
{
    private Guid _branchId;
    private Guid _fundId;

    public FundBalanceGuardWindowTests(PostgresFixture pg) : base(pg) { }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _branchId = await CreateBranchAsync("FW" + Guid.NewGuid().ToString("N")[..8]);
        await SetBranchOpeningDateAsync(_branchId, new DateOnly(2026, 10, 1));
        _fundId = await CashFundIdAsync(_branchId);
        await SetFundOpeningAmountAsync(_fundId, 1_000m);
    }

    [Fact]
    public async Task Movement_exactly_at_from_belongs_to_the_window()
    {
        var at = Vn("2026-10-05 09:00");
        await InsertCashVoucherAsync(_branchId, _fundId, CashVoucherType.Payment, 1_500m, at);

        var before = await EvaluateAsync(at);

        // Start 1,000 (the payment is not before `from`), then −500 at `from`.
        before.Min.Should().Be(-500m);
        before.FirstNegativeAt.Should().Be(at);
        before.Closing.Should().Be(-500m);
    }

    [Fact]
    public async Task Movement_one_second_before_from_is_in_the_start_balance()
    {
        var at = Vn("2026-10-05 09:00");
        await InsertCashVoucherAsync(_branchId, _fundId, CashVoucherType.Payment, 1_500m, at.AddSeconds(-1));

        var before = await EvaluateAsync(at);

        // Start −500 < 0: the first negative point is `from` itself.
        before.Min.Should().Be(-500m);
        before.FirstNegativeAt.Should().Be(at);
        before.Closing.Should().Be(-500m);
    }

    [Fact]
    public async Task Earlier_movements_bring_the_start_balance_to_exactly_zero()
    {
        var at = Vn("2026-10-05 09:00");
        await InsertCashVoucherAsync(_branchId, _fundId, CashVoucherType.Payment, 1_000m, Vn("2026-10-04 23:59"));

        var zero = await EvaluateAsync(at);
        zero.Min.Should().Be(0m);
        zero.FirstNegativeAt.Should().BeNull();

        await InsertCashVoucherAsync(_branchId, _fundId, CashVoucherType.Payment, 1m, at);
        var negative = await EvaluateAsync(at);
        negative.Min.Should().Be(-1m);
        negative.FirstNegativeAt.Should().Be(at);
        negative.Closing.Should().Be(-1m);
    }

    [Fact]
    public async Task Inflow_precedes_outflow_at_the_same_instant()
    {
        var at = Vn("2026-10-05 09:00");
        await InsertCashVoucherAsync(_branchId, _fundId, CashVoucherType.Payment, 1_500m, at);
        await InsertCashVoucherAsync(_branchId, _fundId, CashVoucherType.Receipt, 200m, at);

        var result = await EvaluateAsync(at);

        // 1,000 + 200 = 1,200, then −300; the lowest point is −300 at the shared instant.
        result.Min.Should().Be(-300m);
        result.FirstNegativeAt.Should().Be(at);
        result.Closing.Should().Be(-300m);
    }

    [Fact]
    public async Task Cancelled_movements_do_not_count()
    {
        var at = Vn("2026-10-05 09:00");
        await InsertCashVoucherAsync(_branchId, _fundId, CashVoucherType.Payment, 9_000m, at.AddHours(-1),
            DocumentStatus.Cancelled);
        await InsertCashVoucherAsync(_branchId, _fundId, CashVoucherType.Payment, 9_000m, at, DocumentStatus.Cancelled);
        await InsertFundTransferAsync(_branchId, _fundId, await OtherFundAsync(), 9_000m, at, DocumentStatus.Cancelled);

        var result = await EvaluateAsync(at);

        result.Min.Should().Be(1_000m);
        result.FirstNegativeAt.Should().BeNull();
        result.Closing.Should().Be(1_000m);
    }

    [Fact]
    public async Task Transfer_moves_money_between_two_funds_in_one_capture()
    {
        var other = await OtherFundAsync();
        await SetFundOpeningAmountAsync(other, 300m);
        var at = Vn("2026-10-05 09:00");
        // An earlier transfer of 100 goes from the other fund to the cash fund; 1,100 leaves the cash fund at `at`.
        await InsertFundTransferAsync(_branchId, other, _fundId, 100m, at.AddHours(-2));
        await InsertFundTransferAsync(_branchId, _fundId, other, 1_100m, at);

        var capture = await InTransactionAsync((sp, ct) =>
            sp.GetRequiredService<IFundBalanceGuard>().CaptureAsync(new[] { (_fundId, at), (other, at) }, ct));

        var cash = capture.Items.Single(i => i.FundId == _fundId).Before;
        cash.Min.Should().Be(0m);          // start 1,000 + 100, then −1,100
        cash.FirstNegativeAt.Should().BeNull();
        cash.Closing.Should().Be(0m);
        var bank = capture.Items.Single(i => i.FundId == other).Before;
        bank.Min.Should().Be(200m);        // start 300 − 100, then +1,100
        bank.FirstNegativeAt.Should().BeNull();
        bank.Closing.Should().Be(1_300m);
    }

    [Fact]
    public async Task Captures_of_different_froms_in_one_call_each_use_their_own_boundary()
    {
        var other = await OtherFundAsync();
        var t1 = Vn("2026-10-03 09:00");
        var t2 = Vn("2026-10-06 09:00");
        await InsertCashVoucherAsync(_branchId, _fundId, CashVoucherType.Payment, 1_200m, t1);
        await InsertCashVoucherAsync(_branchId, other, CashVoucherType.Receipt, 50m, t1);
        await InsertCashVoucherAsync(_branchId, other, CashVoucherType.Payment, 80m, t2);

        var capture = await InTransactionAsync((sp, ct) =>
            sp.GetRequiredService<IFundBalanceGuard>().CaptureAsync(new[] { (_fundId, t2), (other, t1) }, ct));

        var cash = capture.Items.Single(i => i.FundId == _fundId).Before;
        cash.Min.Should().Be(-200m);
        cash.FirstNegativeAt.Should().Be(t2);   // start −200 at t2, the payment at t1 is before it
        var bank = capture.Items.Single(i => i.FundId == other).Before;
        bank.Min.Should().Be(-30m);              // 0, +50 at t1, −30 at t2
        bank.FirstNegativeAt.Should().Be(t2);
        bank.Closing.Should().Be(-30m);
    }

    [Fact]
    public async Task First_negative_point_moved_earlier_is_worse_even_with_the_same_minimum()
    {
        await UpdateCashDebtSettingsAsync(s => s.NegativeFundPolicy = LimitPolicy.Block);
        try
        {
            // Opening 1,000, payment 1,500 on 10-03: −500 from 10-03.
            await InsertCashVoucherAsync(_branchId, _fundId, CashVoucherType.Payment, 1_500m, Vn("2026-10-03 09:00"));
            var from = Vn("2026-10-02 00:00");

            var act = () => InTransactionAsync(async (sp, ct) =>
            {
                var guard = sp.GetRequiredService<IFundBalanceGuard>();
                var capture = await guard.CaptureAsync(new[] { (_fundId, from) }, ct);
                capture.Items.Single().Before.Min.Should().Be(-500m);
                capture.Items.Single().Before.FirstNegativeAt.Should().Be(Vn("2026-10-03 09:00"));
                // −200 at 10-02 09:00, back to 1,000 at 12:00: the minimum stays −500, the first negative moves earlier.
                await InsertCashVoucherAsync(_branchId, _fundId, CashVoucherType.Payment, 1_200m, Vn("2026-10-02 09:00"));
                await InsertCashVoucherAsync(_branchId, _fundId, CashVoucherType.Receipt, 1_200m, Vn("2026-10-02 12:00"));
                await guard.CheckAsync(capture, acknowledge: false, ct);
                return 0;
            });

            var ex = (await act.Should().ThrowAsync<ConfirmationRequiredException>()).Which;
            ex.Code.Should().Be("NEGATIVE_FUND_BLOCKED");
            ex.Details.Should().ContainKey("QTM").WhoseValue.Should().Equal("Âm 500 tại 02/10/2026 09:00");
        }
        finally
        {
            await UpdateCashDebtSettingsAsync(s => s.NegativeFundPolicy = LimitPolicy.Warn);
        }
    }

    /// Robust RED/GREEN proof that older history is no longer read: the guard runs on its own context with a command
    /// interceptor, and every statement on the movement tables must be bounded below by `from` (the old query had no
    /// such predicate and also read reason and fund names through subqueries).
    [Fact]
    public async Task Capture_reads_the_history_only_through_bounded_sql()
    {
        var at = Vn("2026-10-05 09:00");
        await InsertCashVoucherAsync(_branchId, _fundId, CashVoucherType.Payment, 100m, at.AddDays(-3));
        await InsertFundTransferAsync(_branchId, _fundId, await OtherFundAsync(), 100m, at.AddDays(-2));
        await InsertCashVoucherAsync(_branchId, _fundId, CashVoucherType.Receipt, 100m, at);

        var recorder = new CommandRecorder();
        FundCapture capture;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var sp = scope.ServiceProvider;
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(sp.GetRequiredService<AppDbContext>().Database.GetConnectionString())
                .UseSnakeCaseNamingConvention()
                .AddInterceptors(recorder)
                .Options;
            await using var db = new AppDbContext(options, sp.GetRequiredService<ICurrentUser>(),
                sp.GetRequiredService<IDateTime>());
            capture = await new FundBalanceGuard(db).CaptureAsync(new[] { (_fundId, at) }, default);
        }

        // Same answer as the characterization tests: 1,000 − 100 − 100 = 800 at the start, +100 at `from`.
        capture.Items.Single().Before.Min.Should().Be(800m);
        capture.Items.Single().Before.Closing.Should().Be(900m);

        var onMovements = recorder.Commands
            .Where(c => c.Contains("cash_vouchers") || c.Contains("fund_transfers")).ToList();
        onMovements.Should().NotBeEmpty();
        onMovements.Should().OnlyContain(c => c.Contains("voucher_at >=") || c.Contains("voucher_at <"),
            "every read of the movement tables is bounded by `from`");
        recorder.Commands.Should().NotContain(c => c.Contains("cash_reasons"),
            "the guard does not need reason names");
    }

    private sealed class CommandRecorder : DbCommandInterceptor
    {
        public List<string> Commands { get; } = new();

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken ct = default)
        {
            Commands.Add(command.CommandText);
            return base.ReaderExecutingAsync(command, eventData, result, ct);
        }
    }

    private async Task<RunningBalanceResult> EvaluateAsync(DateTimeOffset from)
    {
        var capture = await InTransactionAsync((sp, ct) =>
            sp.GetRequiredService<IFundBalanceGuard>().CaptureAsync(new[] { (_fundId, from) }, ct));
        return capture.Items.Single().Before;
    }

    private Task<Guid> OtherFundAsync() => CreateBankFundAsync("NH01", _branchId);
}
