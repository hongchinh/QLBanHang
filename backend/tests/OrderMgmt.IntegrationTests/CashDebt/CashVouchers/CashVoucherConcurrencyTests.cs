using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderMgmt.Application.CashDebt.CashVouchers.Models;
using OrderMgmt.Application.CashDebt.Guards;
using OrderMgmt.Domain.Enums;
using OrderMgmt.Infrastructure.Persistence;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.CashDebt.CashVouchers;

/// Parallel saves against the lock order (P14: branch gate → partner keys → fund keys → reason key → counter row),
/// and a rejected create that leaves nothing behind.
[Collection(nameof(PostgresCollection))]
public class CashVoucherConcurrencyTests : CashDebtTestBase
{
    private static readonly DateOnly Oct1 = new(2026, 10, 1);

    public CashVoucherConcurrencyTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Parallel_creates_get_distinct_sequential_codes()
    {
        var branchId = await CreateBranchAsync(NewCode("CVC"));
        var customerId = await CreatePartnerAsync(NewCode("KH"), isCustomer: true, isSupplier: false);
        var request = CashRequest(CashVoucherType.Receipt, await CashReasonIdAsync("TKH"),
            await FundIdAsync("QTM", branchId), "2026-10-05 09:00", customerId, 1_000m);
        var clients = Enumerable.Range(0, 5).Select(_ => ClientIn(branchId)).ToList();

        // One customer and one fund: while one create waits on the held number, the other four wait on its keys.
        var (results, keyWaiters) = await SendTogetherAsync(HoldNextCode(DocumentType.Receipt, branchId), clients,
            c => PostCashAsync(c, request));

        results.Should().OnlyContain(r => r.Status == HttpStatusCode.OK,
            string.Join("; ", results.Select(r => r.Error?.Message)));
        results.Select(r => r.Voucher!.Code).Should()
            .BeEquivalentTo("PT00001", "PT00002", "PT00003", "PT00004", "PT00005");
        keyWaiters.Should().Be(4, "a create takes its partner and fund keys before its number (P14)");
        (await InDbAsync(db => db.DebtEntries.CountAsync(e => e.PartnerId == customerId))).Should().Be(5);
        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Parallel_payments_cannot_overdraw_under_block()
    {
        var branchId = await CreateBranchAsync(NewCode("CVC"));
        var fundId = await FundIdAsync("QTM", branchId);
        await SetFundOpeningAmountAsync(fundId, 1_000m);
        var request = CashRequest(CashVoucherType.Payment, await CashReasonIdAsync("CK"), fundId, "2026-10-05 09:00",
            null, 700m);
        await UpdateCashDebtSettingsAsync(s => s.NegativeFundPolicy = LimitPolicy.Block);
        try
        {
            // While one payment waits on the held number, the other waits on its fund key: it reads the balance only
            // after the first one has committed.
            var (results, keyWaiters) = await SendTogetherAsync(HoldNextCode(DocumentType.Payment, branchId),
                new[] { ClientIn(branchId), ClientIn(branchId) }, c => PostCashAsync(c, request));

            results.Select(r => r.Status).Should()
                .BeEquivalentTo(new[] { HttpStatusCode.OK, HttpStatusCode.UnprocessableEntity });
            results.Single(r => r.Status == HttpStatusCode.OK).Voucher!.Code.Should().Be("PC00001");
            var error = results.Single(r => r.Status == HttpStatusCode.UnprocessableEntity).Error!;
            error.Code.Should().Be("NEGATIVE_FUND_BLOCKED");
            error.Details.Should().ContainKey("QTM").WhoseValue.Should().Equal("Âm 400 tại 05/10/2026 09:00");
            keyWaiters.Should().Be(1, "a payment takes its fund key before its number (P14)");

            var movements = await InTransactionAsync((sp, ct) =>
                sp.GetRequiredService<IFundBalanceGuard>().MovementsAsync(fundId, ct));
            movements.Select(m => m.Delta).Should().Equal(1_000m, -700m);
            await AssertCashDebtInvariantsAsync();
        }
        finally
        {
            await UpdateCashDebtSettingsAsync(s => s.NegativeFundPolicy = LimitPolicy.Warn);
        }
    }

    [Fact]
    public async Task Rejected_create_leaves_no_voucher_entry_or_counter_change()
    {
        var branchId = await CreateBranchAsync(NewCode("CVC"));
        await SetBranchOpeningDateAsync(branchId, Oct1);
        var client = ClientIn(branchId);
        var fundId = await FundIdAsync("QTM", branchId);
        await SetFundOpeningAmountAsync(fundId, 1_000m);
        var supplierId = await CreatePartnerAsync(NewCode("NCC"), isCustomer: false, isSupplier: true);
        var invoice = await CreateOpeningDebtAsync(client, supplierId, DebtSide.Payable, OpeningDebtKind.Debt, "HD1",
            new DateOnly(2026, 9, 10), 2_000m);
        var request = CashRequest(CashVoucherType.Payment, await CashReasonIdAsync("TNCC"), fundId, "2026-10-05 09:00",
            supplierId, 1_500m);
        request.Allocations = new List<AllocationRequest> { new() { EntryId = invoice.EntryId, Amount = 1_500m } };
        await UpdateCashDebtSettingsAsync(s => s.NegativeFundPolicy = LimitPolicy.Block);
        try
        {
            // A payment: a receipt only adds to its fund, so no create of one gets a 422. The fund check runs last: by
            // then the create has taken its number, saved the voucher and posted its P− entry and its settlement.
            var (status, _, error) = await PostCashAsync(client, request);
            status.Should().Be(HttpStatusCode.UnprocessableEntity, error?.Message);
            error!.Code.Should().Be("NEGATIVE_FUND_BLOCKED");

            (await InDbAsync(db => db.CashVouchers.IgnoreQueryFilters().CountAsync(v => v.BranchId == branchId)))
                .Should().Be(0);
            (await InDbAsync(db => db.DebtEntries.Where(e => e.PartnerId == supplierId).Select(e => e.Id).ToListAsync()))
                .Should().Equal(invoice.EntryId);
            (await SettlementsOfAsync(invoice.EntryId)).Should().BeEmpty();

            // The number was not used: the next create gets the first one.
            request.Lines[0].Amount = 1_000m;
            request.Allocations[0].Amount = 1_000m;
            (await CreateCashAsync(client, request)).Code.Should().Be("PC00001");
            await AssertCashDebtInvariantsAsync();
        }
        finally
        {
            await UpdateCashDebtSettingsAsync(s => s.NegativeFundPolicy = LimitPolicy.Warn);
        }
    }

    /// Sends one request per client at once while a transaction of the test holds a row (`hold`) that each request
    /// writes only after its locks and checks. The row is released once every request waits (on that row, or on a
    /// lock another request holds), so all of them are in flight together, or as soon as one of them has finished.
    /// KeyWaiters: how many requests were then waiting on an advisory key rather than on the held row.
    private async Task<(T[] Responses, int KeyWaiters)> SendTogetherAsync<T>(Func<IServiceProvider, Task> hold,
        IEnumerable<HttpClient> clients, Func<HttpClient, Task<T>> send)
    {
        Task<T>[] requests;
        int keyWaiters;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await using var transaction = await db.Database.BeginTransactionAsync();
            await hold(scope.ServiceProvider);

            requests = clients.Select(send).ToArray();
            await WaitForBlockedAsync(Task.WhenAny(requests), requests.Length);
            keyWaiters = await InDbAsync(other => other.Database.SqlQuery<int>(
                    $"SELECT count(*)::int AS \"Value\" FROM pg_locks WHERE locktype = 'advisory' AND NOT granted AND database = (SELECT oid FROM pg_database WHERE datname = current_database())")
                .SingleAsync());
            await transaction.RollbackAsync();
        }
        return (await Task.WhenAll(requests), keyWaiters);
    }

    /// An admin client working in the branch (a clone: logins are rate-limited).
    private HttpClient ClientIn(Guid branchId)
    {
        var client = CloneAdminClient();
        UseBranch(client, branchId);
        return client;
    }

    private static string NewCode(string prefix) => prefix + Guid.NewGuid().ToString("N")[..8];
}
