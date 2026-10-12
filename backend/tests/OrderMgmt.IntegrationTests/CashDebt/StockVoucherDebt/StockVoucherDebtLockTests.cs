using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderMgmt.Application.CashDebt.CashVouchers.Models;
using OrderMgmt.Application.CashDebt.Interfaces;
using OrderMgmt.Application.Inventory.Posting;
using OrderMgmt.Application.Inventory.StockVouchers.Models;
using OrderMgmt.Domain.Entities.CashDebt;
using OrderMgmt.Domain.Enums;
using OrderMgmt.Infrastructure.Persistence;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.CashDebt.StockVoucherDebt;

/// The stock voucher lock order (P14: branch gate → product keys → partner keys → fund keys → reason keys → counter
/// row) and parallel saves of the same customer. Regression tests for the P14 design.
[Collection(nameof(PostgresCollection))]
public class StockVoucherDebtLockTests : CashDebtTestBase
{
    private Guid _branchId;
    private Guid _warehouseId;
    private Guid _cashFundId;
    private Guid _cashMethodId;

    public StockVoucherDebtLockTests(PostgresFixture pg) : base(pg) { }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        // A fresh branch per test: limits are per customer x branch, and opening debts need its opening date.
        var code = "SL" + Guid.NewGuid().ToString("N")[..8];
        _branchId = await CreateBranchAsync(code);
        await SetBranchOpeningDateAsync(_branchId, new DateOnly(2026, 10, 1));
        _warehouseId = await CreateWarehouseAsync(_branchId, code);
        _cashFundId = await CashFundIdAsync(_branchId);
        _cashMethodId = await InDbAsync(db => db.PaymentMethods.Where(m => m.Code == "TM").Select(m => m.Id).SingleAsync());
    }

    [Fact]
    public async Task Stock_voucher_takes_gate_products_partner_and_fund_once()
    {
        var productIds = Enumerable.Range(0, 50).Select(_ => Guid.NewGuid()).ToList();
        var partnerId = Guid.NewGuid();
        var fundId = Guid.NewGuid();

        var held = await InTransactionAsync(async (sp, ct) =>
        {
            await sp.GetRequiredService<IInventoryPostingService>().AcquireLocksAsync(MainBranchId, productIds, ct);
            await sp.GetRequiredService<ICashDebtLock>().AcquireAsync(MainBranchId, new[] { partnerId }, new[] { fundId }, ct);
            return await sp.GetRequiredService<AppDbContext>().Database
                .SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM pg_locks WHERE locktype = 'advisory' AND pid = pg_backend_pid()")
                .SingleAsync(ct);
        });

        held.Should().Be(1 + 50 + 1 + 1, "the shared branch gate, one key per product, the partner key and the fund key");
    }

    [Fact]
    public async Task Parallel_sales_cannot_both_pass_a_blocking_credit_limit()
    {
        var customer = await NewCustomerAsync();
        await InDbAsync(async db =>
        {
            db.CreditLimits.Add(new CreditLimit { CustomerId = customer, BranchId = _branchId, Amount = 5_000m });
            await db.SaveChangesAsync();
        });
        var first = await SaleRequestAsync(customer, 3_000, paid: 0, "2026-10-05 09:00");
        var second = await SaleRequestAsync(customer, 3_000, paid: 0, "2026-10-05 09:05");
        var original = await InDbAsync(db => db.CashDebtSettings.Where(s => s.Id == 1)
            .Select(s => s.CreditLimitPolicy).SingleAsync());
        await UpdateCashDebtSettingsAsync(s => s.CreditLimitPolicy = LimitPolicy.Block);
        try
        {
            // The first sale holds the customer's partner key while it waits on the held number; the second (other
            // products) waits on that key, so it reads the balance only after the first has committed (P14, step 7).
            // The counter row would serialise two creates on its own, so the test also checks where the second waits.
            var partnerWaiters = -1;
            var (firstResult, secondResult) = await RaceAsync(HoldNextCode(DocumentType.StockOut, _branchId),
                () => PostVoucherAsync(ClientInBranch(), first), () => PostVoucherAsync(ClientInBranch(), second),
                async () => partnerWaiters = await KeyWaitersAsync(PartnerKey(_branchId, customer)));

            partnerWaiters.Should().Be(1, "the second sale waits on the customer's partner key, taken before its number");
            firstResult.Status.Should().Be(HttpStatusCode.OK, firstResult.Error?.Message);
            secondResult.Status.Should().Be(HttpStatusCode.UnprocessableEntity,
                "the second sale sees the first (it got code {0})", secondResult.Voucher?.Code);
            secondResult.Error!.Code.Should().Be("CREDIT_LIMIT_BLOCKED");

            (await InDbAsync(db => db.StockVouchers.CountAsync(v => v.BranchId == _branchId))).Should().Be(1);
            (await ReceivableAsync(customer)).Should().Be(3_000m);
            await AssertCashDebtInvariantsAsync();
        }
        finally
        {
            await UpdateCashDebtSettingsAsync(s => s.CreditLimitPolicy = original);
        }
    }

    [Fact]
    public async Task Parallel_sale_and_receipt_for_same_customer_keep_invariants()
    {
        const int rounds = 5;
        var customer = await NewCustomerAsync();
        var opening = await CreateOpeningDebtAsync(ClientInBranch(), customer, DebtSide.Receivable, OpeningDebtKind.Debt,
            "HD1", new DateOnly(2026, 9, 20), 10_000m);
        var receiptReasonId = await CashReasonIdAsync("TKH");

        for (var round = 0; round < rounds; round++)
        {
            // A sale of 2,000 paid 500 in cash (its automatic receipt goes to QTM) and a manual receipt of 1,000 into
            // QTM allocated to the opening debt: both take the customer's partner key and the QTM fund key.
            var at = $"2026-10-05 {9 + round:00}:00";
            var sale = await SaleRequestAsync(customer, 2_000, paid: 500, at);
            var receipt = CashRequest(CashVoucherType.Receipt, receiptReasonId, _cashFundId, at, customer, 1_000m);
            receipt.Allocations = new List<AllocationRequest> { new() { EntryId = opening.EntryId, Amount = 1_000m } };
            Func<Task<(HttpStatusCode Status, StockVoucherDto? Voucher, Application.Common.Models.ApiError? Error)>> postSale =
                () => PostVoucherAsync(ClientInBranch(), sale);
            Func<Task<(HttpStatusCode Status, CashVoucherDto? Voucher, Application.Common.Models.ApiError? Error)>> postReceipt =
                () => PostCashAsync(ClientInBranch(), receipt);

            // Each one goes first in turn: it waits on its held number with its keys taken, and the other waits on the
            // partner key, the first key both take (the sale after its gate and product keys), not on the fund key.
            var waiters = (Partner: -1, Fund: -1);
            Func<Task> countWaiters = async () => waiters =
                (await KeyWaitersAsync(PartnerKey(_branchId, customer)), await KeyWaitersAsync(FundKey(_cashFundId)));
            if (round % 2 == 0)
            {
                var (saleResult, receiptResult) = await RaceAsync(HoldNextCode(DocumentType.StockOut, _branchId),
                    postSale, postReceipt, countWaiters);
                saleResult.Status.Should().Be(HttpStatusCode.OK, saleResult.Error?.Message);
                receiptResult.Status.Should().Be(HttpStatusCode.OK, receiptResult.Error?.Message);
            }
            else
            {
                var (receiptResult, saleResult) = await RaceAsync(HoldNextCode(DocumentType.Receipt, _branchId),
                    postReceipt, postSale, countWaiters);
                receiptResult.Status.Should().Be(HttpStatusCode.OK, receiptResult.Error?.Message);
                saleResult.Status.Should().Be(HttpStatusCode.OK, saleResult.Error?.Message);
            }
            waiters.Should().Be((1, 0), "round {0}: partner keys come before fund keys (P14)", round);
        }

        // Opening 10,000 + 5 sales of 2,000 − 5 automatic receipts of 500 − 5 manual receipts of 1,000.
        (await ReceivableAsync(customer)).Should().Be(12_500m);
        (await SettlementsOfAsync(opening.EntryId)).Sum(s => s.Amount).Should().Be(5_000m);
        var receipts = await InDbAsync(db => db.CashVouchers
            .Where(v => v.BranchId == _branchId && v.Type == CashVoucherType.Receipt)
            .Select(v => new { v.Code, v.SourceStockVoucherId, v.Total })
            .ToListAsync());
        receipts.Should().HaveCount(2 * rounds);
        receipts.Select(r => r.Code).Should().OnlyHaveUniqueItems();
        receipts.Where(r => r.SourceStockVoucherId != null).Select(r => r.Total).Should().Equal(Enumerable.Repeat(500m, rounds));
        await AssertCashDebtInvariantsAsync();
    }

    private Task<Guid> NewCustomerAsync() =>
        CreatePartnerAsync("KH-" + Guid.NewGuid().ToString("N")[..8], isCustomer: true, isSupplier: false);

    /// A sale (XBH) of one untracked new product at VN time `at` in the test branch; paid in cash (TM, QTM) when
    /// `paid` > 0.
    private async Task<UpsertStockVoucherRequest> SaleRequestAsync(Guid customerId, decimal total, decimal paid, string at)
    {
        var product = await CreateInventoryProductAsync("SP-" + Guid.NewGuid().ToString("N")[..8], track: false);
        var request = VoucherRequest(StockDirection.Out, await ReasonIdAsync("XBH"), at, LineRequest(product, 1, total));
        request.WarehouseId = _warehouseId;
        request.PartnerId = customerId;
        request.PaymentMethodId = paid > 0 ? _cashMethodId : null;
        request.PaidAmount = paid;
        return request;
    }

    /// Receivable balance (Σ Increase − Σ Decrease) of the customer in the test branch.
    private Task<decimal> ReceivableAsync(Guid customerId) =>
        InDbAsync(db => db.DebtEntries
            .Where(e => e.BranchId == _branchId && e.PartnerId == customerId && e.Side == DebtSide.Receivable)
            .SumAsync(e => e.Direction == DebtDirection.Increase ? e.Amount : -e.Amount));

    /// An admin client working in the test branch (a clone: logins are rate-limited).
    private HttpClient ClientInBranch()
    {
        var client = CloneAdminClient();
        UseBranch(client, _branchId);
        return client;
    }
}
