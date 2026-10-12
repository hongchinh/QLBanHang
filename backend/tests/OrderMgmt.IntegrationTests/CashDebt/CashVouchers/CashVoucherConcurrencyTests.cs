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
/// a fund balance read only after the fund key, and a rejected create that leaves nothing behind.
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
        var fundId = await FundIdAsync("QTM", branchId);
        var request = CashRequest(CashVoucherType.Receipt, await CashReasonIdAsync("TKH"), fundId, "2026-10-05 09:00",
            customerId, 1_000m);
        var clients = Enumerable.Range(0, 5).Select(_ => ClientIn(branchId)).ToList();

        // One customer and one fund: while one create waits on the held number, the other four wait on its partner
        // key, the first key a create takes, and none of them gets as far as the fund key.
        var (results, waiters) = await SendTogetherAsync(HoldNextCode(DocumentType.Receipt, branchId), clients,
            c => PostCashAsync(c, request), PartnerKey(branchId, customerId), FundKey(fundId));

        results.Should().OnlyContain(r => r.Status == HttpStatusCode.OK,
            string.Join("; ", results.Select(r => r.Error?.Message)));
        results.Select(r => r.Voucher!.Code).Should()
            .BeEquivalentTo("PT00001", "PT00002", "PT00003", "PT00004", "PT00005");
        waiters.Should().Equal(new[] { 4, 0 },
            "a create takes its partner key, then its fund key, before its number (P14)");
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
            // While one payment waits on the held number, the other waits on its fund key. With two equal payments a
            // balance read before that key would still block the second one, so
            // Payment_reads_the_fund_balance_after_its_fund_lock covers when the balance is read.
            var (results, waiters) = await SendTogetherAsync(HoldNextCode(DocumentType.Payment, branchId),
                new[] { ClientIn(branchId), ClientIn(branchId) }, c => PostCashAsync(c, request), FundKey(fundId));

            results.Select(r => r.Status).Should()
                .BeEquivalentTo(new[] { HttpStatusCode.OK, HttpStatusCode.UnprocessableEntity });
            results.Single(r => r.Status == HttpStatusCode.OK).Voucher!.Code.Should().Be("PC00001");
            var error = results.Single(r => r.Status == HttpStatusCode.UnprocessableEntity).Error!;
            error.Code.Should().Be("NEGATIVE_FUND_BLOCKED");
            error.Details.Should().ContainKey("QTM").WhoseValue.Should().Equal("Âm 400 tại 05/10/2026 09:00");
            waiters.Should().Equal(new[] { 1 }, "a payment takes its fund key before its number (P14)");

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
    public async Task Payment_reads_the_fund_balance_after_its_fund_lock()
    {
        var branchId = await CreateBranchAsync(NewCode("CVC"));
        await SetBranchOpeningDateAsync(branchId, Oct1);
        var client = ClientIn(branchId);
        var fundId = await FundIdAsync("QTM", branchId);
        await SetFundOpeningAmountAsync(fundId, 1_000m);
        // The fund is already at −500 from 10-05 10:00.
        await InsertCashVoucherAsync(branchId, fundId, CashVoucherType.Payment, 1_500m, Vn("2026-10-05 10:00"));
        var receipt = CashRequest(CashVoucherType.Receipt, await CashReasonIdAsync("TK"), fundId, "2026-10-04 09:00",
            null, 1_000m);
        var payment = CashRequest(CashVoucherType.Payment, await CashReasonIdAsync("CK"), fundId, "2026-10-06 09:00",
            null, 800m);
        await UpdateCashDebtSettingsAsync(s => s.NegativeFundPolicy = LimitPolicy.Block);
        try
        {
            // The earlier receipt holds the fund key while it waits on the held number, and the payment waits on that
            // key. Read after the key, the balance includes the receipt: +500 before the payment and −300 after it.
            // Read before the key, it would be −500 before and −300 after, which is not worse (D31), and the payment
            // would leave the fund negative under Block.
            var (receiptResult, paymentResult) = await RaceAsync(HoldNextCode(DocumentType.Receipt, branchId),
                () => PostCashAsync(client, receipt), () => PostCashAsync(client, payment));

            receiptResult.Status.Should().Be(HttpStatusCode.OK, receiptResult.Error?.Message);
            paymentResult.Status.Should().Be(HttpStatusCode.UnprocessableEntity,
                "the payment sees the receipt (it got code {0})", paymentResult.Voucher?.Code);
            paymentResult.Error!.Code.Should().Be("NEGATIVE_FUND_BLOCKED");
            paymentResult.Error.Details.Should().ContainKey("QTM").WhoseValue.Should()
                .Equal("Âm 300 tại 06/10/2026 09:00");

            var movements = await InTransactionAsync((sp, ct) =>
                sp.GetRequiredService<IFundBalanceGuard>().MovementsAsync(fundId, ct));
            movements.Select(m => m.Delta).Should().Equal(1_000m, 1_000m, -1_500m);
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
    /// Waiters: for each of `keys`, how many requests were then waiting on that advisory key.
    private async Task<(T[] Responses, int[] Waiters)> SendTogetherAsync<T>(Func<IServiceProvider, Task> hold,
        IEnumerable<HttpClient> clients, Func<HttpClient, Task<T>> send, params string[] keys)
    {
        Task<T>[] requests;
        var waiters = new int[keys.Length];
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await using var transaction = await db.Database.BeginTransactionAsync();
            await hold(scope.ServiceProvider);

            requests = clients.Select(send).ToArray();
            await WaitForBlockedAsync(Task.WhenAny(requests), requests.Length);
            for (var i = 0; i < keys.Length; i++)
                waiters[i] = await KeyWaitersAsync(keys[i]);
            await transaction.RollbackAsync();
        }
        return (await Task.WhenAll(requests), waiters);
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
