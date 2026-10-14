using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderMgmt.Application.Dashboards.Executive.Realtime;
using OrderMgmt.Application.Inventory.Common;
using OrderMgmt.Application.Notifications.Interfaces;
using OrderMgmt.Application.Organization.SampleData.Interfaces;
using OrderMgmt.Application.Organization.SampleData.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Entities.Organization;
using OrderMgmt.Domain.Enums;
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

    private Task SetStatusAsync(Guid branchId, bool isDemo, SampleDataStatus status, int progress) =>
        InDbAsync(db => db.Branches.IgnoreQueryFilters().Where(b => b.Id == branchId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(b => b.IsDemo, isDemo)
                .SetProperty(b => b.SampleDataStatus, status)
                .SetProperty(b => b.SampleDataProgress, progress)));

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

    /// No validator message echoes the description, so the over-long description only checks the bound; the message of
    /// an unknown key echoes the key, so a 2,000-character key checks the cut itself. The surrogate-safe cut is covered
    /// by SampleDataErrorFormatTests.
    [Fact]
    public async Task Error_is_cut_to_1000_characters()
    {
        var descriptionBranch = await CreateBranchAsync("RF03");
        var tooLong = new SampleCashVoucherStep("CK-0001", At(1), null, CashVoucherType.Payment, "CK", "QTM", null,
            "Chị Lan", new string('D', 2000), 1_000_000m, Array.Empty<SampleAllocationTarget>());

        var described = await TinyPlans.RunAsync(_factory, descriptionBranch,
            TinyPlans.WithSteps(TinyPlans.OpeningDate, tooLong));

        described.Succeeded.Should().BeFalse();
        var branch = await BranchAsync(descriptionBranch);
        branch.SampleDataStatus.Should().Be(SampleDataStatus.Failed);
        branch.SampleDataError.Should().StartWith("CK-0001: ");
        branch.SampleDataError!.Length.Should().BeLessThanOrEqualTo(1000);

        var keyBranch = await CreateBranchAsync("RF04");
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
        var ownerCount = owners.Count == 0 ? 1 : owners.Count;
        ownerCount.Should().BeGreaterThanOrEqualTo(2);
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
        (input.BranchCode, input.OwnerCount).Should().Be(("RJ01", ownerCount));
        input.Today.Should().BeOneOf(before, after);
        var branch = await BranchAsync(branchId);
        (branch.SampleDataStatus, branch.SampleDataProgress, branch.SampleDataError)
            .Should().Be((SampleDataStatus.Ready, 100, (string?)null));
        (await InDbAsync(db => db.Warehouses.AnyAsync(w => w.Code == "RJ01-K1"))).Should().BeTrue("the plan ran");
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
        (branch.SampleDataStatus, branch.SampleDataError).Should().Be((SampleDataStatus.Failed, "KE-HOACH: boom"));
        (await InDbAsync(db => db.Warehouses.AnyAsync(w => w.Code == "RJ02-K1"))).Should().BeFalse();
    }
}
