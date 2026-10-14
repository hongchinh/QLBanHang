using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Inventory.Common;
using OrderMgmt.Application.Organization.Branches.Models;
using OrderMgmt.Application.Organization.SampleData.Interfaces;
using OrderMgmt.Application.Organization.SampleData.Models;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.CashDebt;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.SampleData;

/// The background worker: a demo branch created through the API is filled by the host's own worker. The requests go
/// to a host of this test's database whose generator answers with a tiny plan (S19); every host has its own queue
/// and worker, so only that host's worker sees these jobs.
[Collection(nameof(PostgresCollection))]
public class SampleDataHostedServiceTests : CashDebtTestBase
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan PollEvery = TimeSpan.FromMilliseconds(500);

    public SampleDataHostedServiceTests(PostgresFixture pg) : base(pg) { }

    /// The setup items and one credit sale.
    private static SampleDataPlan Plan() => TinyPlans.WithSteps(TinyPlans.OpeningDate,
        new SampleStockVoucherStep("XBH-0001", VnTime.StartOfDay(TinyPlans.OpeningDate.AddDays(1)).AddHours(9), null,
            StockDirection.Out, "XBH", "K1", "DM-KH01", null, null, "XBH-0001",
            new[] { new SampleStockLine("DM-SP01", 1m, 60_000m, 0m, 0m) }));

    /// A host whose generator answers with Plan(), and an admin client on it (the admin token of _client). Creating
    /// the client starts the host, so its startup recovery runs before any demo branch exists; a test builds one such
    /// host only, since a host started later would fail the branches still Running in the shared database.
    private (WebApplicationFactory<Program> Host, HttpClient Client) WorkerHost()
    {
        var host = TinyPlans.WithService<ISampleDataGenerator>(_factory, new FakeSampleDataGenerator(_ => Plan()));
        var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = _client.DefaultRequestHeaders.Authorization;
        return (host, client);
    }

    private static async Task CreateDemoBranchAsync(HttpClient client, string code)
    {
        var created = await ReadDataAsync<BranchDto>(
            await client.PostAsJsonAsync("/api/branches", new { code, name = "CN demo", withSampleData = true }));
        created.SampleDataStatus.Should().Be(SampleDataStatus.Running);
    }

    /// Reads GET /api/branches every 500 ms until the branch leaves Running, for up to 60 s.
    private static async Task<BranchDto> WaitUntilDoneAsync(HttpClient client, string code)
    {
        var deadline = DateTime.UtcNow + Deadline;
        while (true)
        {
            var branch = (await ReadDataAsync<List<BranchDto>>(await client.GetAsync("/api/branches")))
                .Single(b => b.Code == code);
            if (branch.SampleDataStatus != SampleDataStatus.Running)
                return branch;
            if (DateTime.UtcNow >= deadline)
                throw new TimeoutException($"Branch {code} is still {branch.SampleDataStatus} " +
                    $"({branch.SampleDataProgress}%) after {Deadline.TotalSeconds} s; error: {branch.SampleDataError ?? "none"}");
            await Task.Delay(PollEvery);
        }
    }

    private static void ShouldBeReady(BranchDto branch) =>
        (branch.SampleDataStatus, branch.SampleDataProgress, branch.SampleDataError)
            .Should().Be((SampleDataStatus.Ready, 100, (string?)null), branch.Code);

    [Fact]
    public async Task Created_demo_branch_is_seeded_in_the_background()
    {
        var (host, client) = WorkerHost();
        await using (host)
        {
            await CreateDemoBranchAsync(client, "HS01");

            var branch = await WaitUntilDoneAsync(client, "HS01");

            ShouldBeReady(branch);
            var reasons = await InDbAsync(db => db.StockVouchers.Where(v => v.BranchId == branch.Id)
                .Select(v => v.ReasonId).ToListAsync());
            reasons.Should().Equal(await ReasonIdAsync("XBH"));
        }
    }

    [Fact]
    public async Task Jobs_run_one_after_another()
    {
        var (host, client) = WorkerHost();
        await using (host)
        {
            await CreateDemoBranchAsync(client, "HS02");
            await CreateDemoBranchAsync(client, "HS03");

            var first = await WaitUntilDoneAsync(client, "HS02");
            var second = await WaitUntilDoneAsync(client, "HS03");

            ShouldBeReady(first);
            ShouldBeReady(second);
            var warehouses = await InDbAsync(db => db.Warehouses
                .Where(w => w.BranchId == first.Id || w.BranchId == second.Id).Select(w => w.Code).ToListAsync());
            warehouses.Should().BeEquivalentTo("HS02-K1", "HS03-K1");
            (await InDbAsync(db => db.Products.IgnoreQueryFilters().CountAsync(p => p.Code == "DM-SP01"))).Should().Be(1);
        }
    }
}
