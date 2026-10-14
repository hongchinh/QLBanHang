using System.Diagnostics;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Dashboards.Executive.Realtime;
using OrderMgmt.Application.Inventory.Common;
using OrderMgmt.Application.Notifications.Interfaces;
using OrderMgmt.Application.Organization.SampleData.Interfaces;
using OrderMgmt.Application.Organization.SampleData.Models;
using OrderMgmt.Application.Organization.SampleData.Services;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Entities.Organization;
using OrderMgmt.Domain.Enums;
using OrderMgmt.Infrastructure.Persistence;
using OrderMgmt.IntegrationTests.CashDebt;
using OrderMgmt.IntegrationTests.ExecutiveDashboard;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.SampleData;

/// How a run ends: a failing item, recovery after a restart, the dashboard signal of a successful run (S18) and the
/// job entry point that asks the generator for the plan.
[Collection(nameof(PostgresCollection))]
public class SampleDataRunnerStatusTests : CashDebtTestBase
{
    private const string InterruptedError = "Bị gián đoạn do ứng dụng khởi động lại. Hãy xóa chi nhánh demo và tạo lại.";

    public SampleDataRunnerStatusTests(PostgresFixture pg) : base(pg) { }

    /// 09:00 (VN time) on the `day`-th day after the opening date.
    private static DateTimeOffset At(int day) => VnTime.StartOfDay(TinyPlans.OpeningDate.AddDays(day)).AddHours(9);

    /// A credit sale of one EPS sheet to DM-KH01 whose note is its key.
    private static SampleStockVoucherStep Sale(string key, int day) =>
        new(key, At(day), null, StockDirection.Out, "XBH", "K1", "DM-KH01", null, null, key,
            new[] { new SampleStockLine("DM-SP01", 1m, 60_000m, 0m, 0m) });

    /// A receipt from DM-KH01 that allocates to `targetKey`.
    private static SampleCashVoucherStep Receipt(string key, int day, string targetKey) =>
        new(key, At(day), null, CashVoucherType.Receipt, "TKH", "VCB", "DM-KH01", null, key, null,
            new[] { new SampleAllocationTarget(targetKey, null) });

    private Task<Branch> BranchAsync(Guid branchId) =>
        InDbAsync(db => db.Branches.IgnoreQueryFilters().AsNoTracking().SingleAsync(b => b.Id == branchId));

    private Task SetStatusAsync(Guid branchId, bool isDemo, SampleDataStatus status, int progress, string? error = null) =>
        InDbAsync(db => db.Branches.IgnoreQueryFilters().Where(b => b.Id == branchId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(b => b.IsDemo, isDemo)
                .SetProperty(b => b.SampleDataStatus, status)
                .SetProperty(b => b.SampleDataProgress, progress)
                .SetProperty(b => b.SampleDataError, error)));

    [Fact]
    public async Task Invalid_step_fails_with_its_key_and_keeps_earlier_data()
    {
        var branchId = await CreateBranchAsync("RF01");
        var plan = TinyPlans.WithSteps(TinyPlans.OpeningDate,
            Sale("XBH-0001", 1),
            new SampleFundTransferStep("CQ-0001", At(2), null, "QTM", "VCB", 0m, "Nộp tiền vào ngân hàng"));

        var result = await TinyPlans.RunAsync(_factory, branchId, plan);

        result.Succeeded.Should().BeFalse();
        result.StepsExecuted.Should().Be(1);
        var branch = await BranchAsync(branchId);
        branch.SampleDataStatus.Should().Be(SampleDataStatus.Failed);
        branch.SampleDataError.Should().StartWith("CQ-0001: ").And.Be("CQ-0001: Số tiền phải lớn hơn 0.");
        result.Error.Should().Be(branch.SampleDataError);
        (await InDbAsync(db => db.StockVouchers.AnyAsync(v => v.BranchId == branchId && v.Note == "XBH-0001")))
            .Should().BeTrue("the steps before the failing one stay");
        (await InDbAsync(db => db.FundTransfers.AnyAsync(t => t.BranchId == branchId))).Should().BeFalse();
    }

    [Fact]
    public async Task Unknown_target_key_fails()
    {
        var branchId = await CreateBranchAsync("RF02");

        var result = await TinyPlans.RunAsync(_factory, branchId,
            TinyPlans.WithSteps(TinyPlans.OpeningDate, Receipt("TKH-0001", 1, "XBH-9999")));

        result.Succeeded.Should().BeFalse();
        var branch = await BranchAsync(branchId);
        branch.SampleDataStatus.Should().Be(SampleDataStatus.Failed);
        branch.SampleDataError.Should().StartWith("TKH-0001: ").And.Contain("XBH-9999");
    }

    /// No validator message echoes its input, but the message of an unknown key does, so a 2,000-character key checks
    /// the cut. The surrogate-safe cut is covered by SampleDataErrorFormatTests.
    [Fact]
    public async Task Error_is_cut_to_1000_characters()
    {
        var keyBranch = await CreateBranchAsync("RF03");
        var longKey = new string('K', 2000);

        var keyed = await TinyPlans.RunAsync(_factory, keyBranch,
            TinyPlans.WithSteps(TinyPlans.OpeningDate, Receipt("TKH-0001", 1, longKey)));

        keyed.Succeeded.Should().BeFalse();
        var cut = (await BranchAsync(keyBranch)).SampleDataError;
        cut.Should().HaveLength(1000).And.StartWith("TKH-0001: ").And.EndWith("KKKK");
        keyed.Error.Should().Be(cut);
    }

    [Fact]
    public async Task Recover_marks_running_branches_failed()
    {
        var running1 = await CreateBranchAsync("RR01");
        var running2 = await CreateBranchAsync("RR02");
        var ready = await CreateBranchAsync("RR03");
        var realRunning = await CreateBranchAsync("RR04");
        await SetStatusAsync(running1, isDemo: true, SampleDataStatus.Running, 40);
        await SetStatusAsync(running2, isDemo: true, SampleDataStatus.Running, 0);
        await SetStatusAsync(ready, isDemo: true, SampleDataStatus.Ready, 100);
        await SetStatusAsync(realRunning, isDemo: false, SampleDataStatus.Running, 10);

        await using (var scope = _factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<ISampleDataRunner>().RecoverInterruptedAsync();

        foreach (var (id, progress) in new[] { (running1, 40), (running2, 0) })
        {
            var branch = await BranchAsync(id);
            (branch.SampleDataStatus, branch.SampleDataError, branch.SampleDataProgress)
                .Should().Be((SampleDataStatus.Failed, InterruptedError, progress), branch.Code);
        }
        var readyBranch = await BranchAsync(ready);
        (readyBranch.SampleDataStatus, readyBranch.SampleDataError, readyBranch.SampleDataProgress)
            .Should().Be((SampleDataStatus.Ready, (string?)null, 100));
        var realBranch = await BranchAsync(realRunning);
        (realBranch.SampleDataStatus, realBranch.SampleDataError, realBranch.SampleDataProgress)
            .Should().Be((SampleDataStatus.Running, (string?)null, 10), "a real branch is never touched");
    }

    [Fact]
    public async Task Successful_run_sends_one_dashboard_signal()
    {
        var recorder = new RecordingRealtimeNotifier();
        var host = TinyPlans.WithService<IRealtimeNotifier>(_factory, recorder);
        var failing = await CreateBranchAsync("RS01");
        var succeeding = await CreateBranchAsync("RS02");

        var failed = await TinyPlans.RunAsync(host, failing,
            TinyPlans.WithSteps(TinyPlans.OpeningDate, Receipt("TKH-0001", 1, "XBH-9999")));

        failed.Succeeded.Should().BeFalse();
        recorder.DashboardSignals.Should().BeEmpty("a failed run sends no signal");

        var result = await TinyPlans.RunAsync(host, succeeding,
            TinyPlans.WithSteps(TinyPlans.OpeningDate, Sale("XBH-0001", 1), Receipt("TKH-0001", 2, "XBH-0001")));

        result.Succeeded.Should().BeTrue(result.Error);
        result.StepsExecuted.Should().Be(2);
        recorder.DashboardSignals.Should().Equal((succeeding, DashboardDomains.All));
    }

    [Fact]
    public async Task Run_job_uses_the_generator_and_today()
    {
        await CreateTestUserAsync("sd_job_a", "Pass@123", RoleCodes.Sales);
        await CreateTestUserAsync("sd_job_b", "Pass@123", RoleCodes.Sales);
        var owners = await TinyPlans.SalesOwnersAsync(_factory);
        owners.Should().HaveCountGreaterThanOrEqualTo(2);
        var generator = new FakeSampleDataGenerator(_ => TinyPlans.Setup(TinyPlans.OpeningDate));
        var host = TinyPlans.WithService<ISampleDataGenerator>(_factory, generator);
        var branchId = await CreateBranchAsync("RJ01");
        await TinyPlans.MarkDemoAsync(_factory, branchId);
        var adminId = await TinyPlans.AdminIdAsync(_factory);
        var before = VnTime.ToVnDate(DateTimeOffset.UtcNow);

        await using (var scope = host.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<ISampleDataRunner>()
                .RunJobAsync(new SampleDataJob(branchId, adminId));

        var after = VnTime.ToVnDate(DateTimeOffset.UtcNow);
        var input = generator.Inputs.Should().ContainSingle().Subject;
        (input.BranchCode, input.OwnerCount).Should().Be(("RJ01", owners.Count));
        input.Today.Should().BeOneOf(before, after);
        var branch = await BranchAsync(branchId);
        (branch.SampleDataStatus, branch.SampleDataProgress, branch.SampleDataError)
            .Should().Be((SampleDataStatus.Ready, 100, (string?)null));
        (await InDbAsync(db => db.Warehouses.AnyAsync(w => w.Code == "RJ01-K1"))).Should().BeTrue("the plan ran");
    }

    /// The generator estimates voucher totals as the stock voucher service computes them, so it gets the inventory
    /// setting that leaves VAT out of the net amount; false is the default setting, left untouched.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Run_job_passes_the_inventory_vat_setting(bool netExcludesVat)
    {
        if (netExcludesVat)
            await UpdateInventorySettingsAsync(s => s.NetExcludesVat = true);
        var generator = new FakeSampleDataGenerator(_ => TinyPlans.Setup(TinyPlans.OpeningDate));
        var host = TinyPlans.WithService<ISampleDataGenerator>(_factory, generator);
        var branchId = await CreateBranchAsync("RJ05");
        await TinyPlans.MarkDemoAsync(_factory, branchId);
        var adminId = await TinyPlans.AdminIdAsync(_factory);

        await using (var scope = host.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<ISampleDataRunner>()
                .RunJobAsync(new SampleDataJob(branchId, adminId));

        generator.Inputs.Should().ContainSingle().Which.NetExcludesVat.Should().Be(netExcludesVat);
        (await BranchAsync(branchId)).SampleDataStatus.Should().Be(SampleDataStatus.Ready);
    }

    [Fact]
    public async Task Generator_failure_fails_the_job()
    {
        var generator = new FakeSampleDataGenerator(_ => throw new InvalidOperationException("boom"));
        var host = TinyPlans.WithService<ISampleDataGenerator>(_factory, generator);
        var branchId = await CreateBranchAsync("RJ02");
        await TinyPlans.MarkDemoAsync(_factory, branchId);
        var adminId = await TinyPlans.AdminIdAsync(_factory);

        await using (var scope = host.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<ISampleDataRunner>()
                .RunJobAsync(new SampleDataJob(branchId, adminId));

        generator.Inputs.Should().ContainSingle();
        var branch = await BranchAsync(branchId);
        (branch.SampleDataStatus, branch.SampleDataError).Should().Be((SampleDataStatus.Failed,
            "KE-HOACH: Không tạo được kế hoạch dữ liệu mẫu. Hãy xóa chi nhánh demo và tạo lại."));
        (await InDbAsync(db => db.Warehouses.AnyAsync(w => w.Code == "RJ02-K1"))).Should().BeFalse();
    }

    /// A real branch is skipped before any plan is made, and its row is not touched.
    [Fact]
    public async Task Run_job_on_a_real_branch_makes_no_plan()
    {
        var generator = new FakeSampleDataGenerator(_ => TinyPlans.Setup(TinyPlans.OpeningDate));
        var host = TinyPlans.WithService<ISampleDataGenerator>(_factory, generator);
        var branchId = await CreateBranchAsync("RJ03");
        var adminId = await TinyPlans.AdminIdAsync(_factory);

        await using (var scope = host.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<ISampleDataRunner>()
                .RunJobAsync(new SampleDataJob(branchId, adminId));

        generator.Inputs.Should().BeEmpty();
        var branch = await BranchAsync(branchId);
        (branch.IsDemo, branch.SampleDataStatus, branch.SampleDataError).Should().Be((false, SampleDataStatus.None, (string?)null));
        (await InDbAsync(db => db.Warehouses.AnyAsync(w => w.Code == "RJ03-K1"))).Should().BeFalse();
    }

    /// The worker breaks on cancellation and the next start recovers a Running branch; both need a cancelled run to
    /// throw and leave the branch as it was.
    [Fact]
    public async Task Cancelled_run_throws_and_leaves_the_branch_running()
    {
        var branchId = await CreateBranchAsync("RC01");
        await SetStatusAsync(branchId, isDemo: true, SampleDataStatus.Running, 30);
        var adminId = await TinyPlans.AdminIdAsync(_factory);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var runner = scope.ServiceProvider.GetRequiredService<ISampleDataRunner>();
            var run = () => runner.RunPlanAsync(new SampleDataJob(branchId, adminId),
                TinyPlans.Setup(TinyPlans.OpeningDate), cancelled.Token);
            await run.Should().ThrowAsync<OperationCanceledException>();
        }

        var branch = await BranchAsync(branchId);
        (branch.SampleDataStatus, branch.SampleDataProgress, branch.SampleDataError)
            .Should().Be((SampleDataStatus.Running, 30, (string?)null));
    }

    /// While the host stops, a failure may surface as another exception than a cancellation (a disposed scope, a broken
    /// connection): it is thrown too, so the branch keeps Running for the restart recovery instead of a generic error.
    [Fact]
    public async Task Failure_after_cancellation_leaves_the_branch_running()
    {
        using var stopping = new CancellationTokenSource();
        var generator = new FakeSampleDataGenerator(_ =>
        {
            stopping.Cancel();
            throw new InvalidOperationException("connection broken");
        });
        var host = TinyPlans.WithService<ISampleDataGenerator>(_factory, generator);
        var branchId = await CreateBranchAsync("RC02");
        await SetStatusAsync(branchId, isDemo: true, SampleDataStatus.Running, 0);
        var adminId = await TinyPlans.AdminIdAsync(_factory);

        await using (var scope = host.Services.CreateAsyncScope())
        {
            var runner = scope.ServiceProvider.GetRequiredService<ISampleDataRunner>();
            var run = () => runner.RunJobAsync(new SampleDataJob(branchId, adminId), stopping.Token);
            await run.Should().ThrowAsync<SampleDataPlanException>()
                .WithInnerExceptionExactly<SampleDataPlanException, InvalidOperationException>();
        }

        generator.Inputs.Should().ContainSingle();
        var branch = await BranchAsync(branchId);
        (branch.SampleDataStatus, branch.SampleDataError).Should().Be((SampleDataStatus.Running, (string?)null));
    }

    /// The same once the plan runs: the first item's scope finds the host gone.
    [Fact]
    public async Task Failure_after_cancellation_during_a_run_leaves_the_branch_running()
    {
        using var stopping = new CancellationTokenSource();
        var host = TinyPlans.WithService<IActingIdentity>(_factory, new StoppingIdentity(stopping));
        var branchId = await CreateBranchAsync("RC04");
        await TinyPlans.MarkDemoAsync(_factory, branchId);
        var adminId = await TinyPlans.AdminIdAsync(_factory);

        await using (var scope = host.Services.CreateAsyncScope())
        {
            var runner = scope.ServiceProvider.GetRequiredService<ISampleDataRunner>();
            var run = () => runner.RunPlanAsync(new SampleDataJob(branchId, adminId),
                TinyPlans.Setup(TinyPlans.OpeningDate), stopping.Token);
            await run.Should().ThrowAsync<ObjectDisposedException>();
        }

        var branch = await BranchAsync(branchId);
        (branch.SampleDataStatus, branch.SampleDataError).Should().Be((SampleDataStatus.Running, (string?)null));
        (await InDbAsync(db => db.Warehouses.AnyAsync(w => w.BranchId == branchId))).Should().BeFalse();
    }

    /// A rolling deploy can run two instances for a while: the new one's recovery fails a branch the old one may still
    /// be filling. Only a Running branch is filled or written, so the recovery's verdict stands.
    [Fact]
    public async Task Run_on_a_branch_failed_by_recovery_changes_nothing()
    {
        var branchId = await CreateBranchAsync("RC03");
        await SetStatusAsync(branchId, isDemo: true, SampleDataStatus.Failed, 40, InterruptedError);
        var adminId = await TinyPlans.AdminIdAsync(_factory);

        SampleDataRunResult result;
        await using (var scope = _factory.Services.CreateAsyncScope())
            result = await scope.ServiceProvider.GetRequiredService<ISampleDataRunner>()
                .RunPlanAsync(new SampleDataJob(branchId, adminId), TinyPlans.Setup(TinyPlans.OpeningDate));

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be("KE-HOACH: Chi nhánh demo không còn ở trạng thái đang tạo dữ liệu mẫu.");
        var branch = await BranchAsync(branchId);
        (branch.SampleDataStatus, branch.SampleDataProgress, branch.SampleDataError)
            .Should().Be((SampleDataStatus.Failed, 40, InterruptedError));
        (await InDbAsync(db => db.Warehouses.AnyAsync(w => w.BranchId == branchId))).Should().BeFalse("nothing ran");
    }

    /// The same overlap at the end of a run: the other instance's recovery fails the branch while it is being filled.
    /// Its verdict stands: no progress, no Ready, no dashboard signal, and the run does not count as a success.
    [Fact]
    public async Task Branch_failed_by_recovery_during_the_run_stays_failed()
    {
        var branchId = await CreateBranchAsync("RC05");
        var recorder = new RecordingRealtimeNotifier();
        var identity = new InterruptingIdentity(() => FailAsRecoveryWould(branchId));
        var host = TinyPlans.WithServices(_factory, s =>
        {
            TinyPlans.Replace<IRealtimeNotifier>(s, recorder);
            TinyPlans.Replace<IActingIdentity>(s, identity);
        });

        // 10 setup items and 2 steps: the progress write after the 10th item finds the branch Failed and stops the run
        // before its steps.
        var result = await TinyPlans.RunAsync(host, branchId,
            TinyPlans.WithSteps(TinyPlans.OpeningDate, Sale("XBH-0001", 1), Sale("XBH-0002", 2)));

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be("TIEN-DO: Chi nhánh demo không còn ở trạng thái đang tạo dữ liệu mẫu.");
        result.StepsExecuted.Should().Be(0);
        (await InDbAsync(db => db.StockVouchers.AnyAsync(v => v.BranchId == branchId))).Should().BeFalse();
        var branch = await BranchAsync(branchId);
        (branch.SampleDataStatus, branch.SampleDataProgress, branch.SampleDataError)
            .Should().Be((SampleDataStatus.Failed, 0, InterruptedError));
        recorder.DashboardSignals.Should().BeEmpty();
    }

    /// A step that finished before the progress write stopped the run is counted.
    [Fact]
    public async Task Steps_done_before_the_stop_are_counted()
    {
        var branchId = await CreateBranchAsync("RC06");
        // Items 11 to 20 are the 10 steps; the branch is failed as the first one starts, and the progress write after
        // the 10th step (item 20) stops the run.
        var identity = new InterruptingIdentity(() => FailAsRecoveryWould(branchId), atSet: 11);
        var host = TinyPlans.WithService<IActingIdentity>(_factory, identity);
        var steps = Enumerable.Range(1, 10).Select(i => (SampleStep)Sale($"XBH-{i:0000}", i)).ToArray();

        var result = await TinyPlans.RunAsync(host, branchId, TinyPlans.WithSteps(TinyPlans.OpeningDate, steps));

        result.Succeeded.Should().BeFalse();
        result.StepsExecuted.Should().Be(10);
        (await InDbAsync(db => db.StockVouchers.CountAsync(v => v.BranchId == branchId))).Should().Be(10);
        result.Error.Should().Be("TIEN-DO: Chi nhánh demo không còn ở trạng thái đang tạo dữ liệu mẫu.");
    }

    [Fact]
    public async Task Run_job_on_a_branch_failed_by_recovery_makes_no_plan()
    {
        var generator = new FakeSampleDataGenerator(_ => TinyPlans.Setup(TinyPlans.OpeningDate));
        var host = TinyPlans.WithService<ISampleDataGenerator>(_factory, generator);
        var branchId = await CreateBranchAsync("RJ04");
        await SetStatusAsync(branchId, isDemo: true, SampleDataStatus.Failed, 40, InterruptedError);
        var adminId = await TinyPlans.AdminIdAsync(_factory);

        await using (var scope = host.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<ISampleDataRunner>()
                .RunJobAsync(new SampleDataJob(branchId, adminId));

        generator.Inputs.Should().BeEmpty();
        var branch = await BranchAsync(branchId);
        (branch.SampleDataStatus, branch.SampleDataProgress, branch.SampleDataError)
            .Should().Be((SampleDataStatus.Failed, 40, InterruptedError));
    }

    /// What the startup recovery of another instance does to the branch, synchronously (it runs inside Set).
    private void FailAsRecoveryWould(Guid branchId)
    {
        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Branches.IgnoreQueryFilters()
            .Where(b => b.Id == branchId)
            .ExecuteUpdate(s => s
                .SetProperty(b => b.SampleDataStatus, SampleDataStatus.Failed)
                .SetProperty(b => b.SampleDataError, InterruptedError));
    }

    /// S18: a signal that fails or never completes is given up (after 3 s) and does not fail the saved run.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Signal_that_cannot_be_sent_does_not_fail_the_run(bool stall)
    {
        var recorder = new RecordingRealtimeNotifier { ThrowOnDashboardSignal = !stall, StallOnDashboardSignal = stall };
        var host = TinyPlans.WithService<IRealtimeNotifier>(_factory, recorder);
        var branchId = await CreateBranchAsync("RS03");
        var stopwatch = Stopwatch.StartNew();

        var result = await TinyPlans.RunAsync(host, branchId, TinyPlans.Setup(TinyPlans.OpeningDate))
            .WaitAsync(TimeSpan.FromSeconds(60));

        stopwatch.Stop();
        result.Succeeded.Should().BeTrue(result.Error);
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10), "a stalled signal is given up after 3 s");
        recorder.DashboardSignals.Should().BeEmpty();
        var branch = await BranchAsync(branchId);
        (branch.SampleDataStatus, branch.SampleDataProgress, branch.SampleDataError)
            .Should().Be((SampleDataStatus.Ready, 100, (string?)null));
    }

    /// The acting identity, which lets `interrupt` run on its `atSet`-th Set (one per plan item), as another instance
    /// would meanwhile.
    private sealed class InterruptingIdentity : IActingIdentity
    {
        private readonly Action _interrupt;
        private readonly int _atSet;
        private int _sets;

        public InterruptingIdentity(Action interrupt, int atSet = 1)
        {
            _interrupt = interrupt;
            _atSet = atSet;
        }

        public ActingIdentity? Current { get; private set; }

        public void Set(ActingIdentity identity)
        {
            if (Interlocked.Increment(ref _sets) == _atSet)
                _interrupt();
            Current = identity;
        }
    }

    /// The acting identity of a host that is stopping: setting it stops the run's token, then fails as a disposed scope.
    private sealed class StoppingIdentity : IActingIdentity
    {
        private readonly CancellationTokenSource _stopping;

        public StoppingIdentity(CancellationTokenSource stopping) => _stopping = stopping;

        public ActingIdentity? Current => null;

        public void Set(ActingIdentity identity)
        {
            _stopping.Cancel();
            throw new ObjectDisposedException(nameof(IServiceProvider));
        }
    }
}
