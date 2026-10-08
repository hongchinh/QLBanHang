using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderMgmt.Application.CashDebt.CashVouchers.Models;
using OrderMgmt.Application.CashDebt.Ledger;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.CashDebt.CashVouchers;

/// Allocation at voucher time (rule 4, AllocationValidator, AtVoucher settlements) on create.
[Collection(nameof(PostgresCollection))]
public class CashVoucherAllocationTests : CashDebtTestBase
{
    private static readonly DateOnly Oct1 = new(2026, 10, 1);

    public CashVoucherAllocationTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Allocates_receipt_to_open_invoices()
    {
        var (branchId, client, fundId) = await BranchClientAsync();
        var customerId = await CreatePartnerAsync(NewCode("KH"), isCustomer: true, isSupplier: false);
        var hd1 = await CreateOpeningDebtAsync(client, customerId, DebtSide.Receivable, OpeningDebtKind.Debt, "HD1",
            new DateOnly(2026, 9, 10), 1_000m, dueDate: new DateOnly(2026, 9, 20));
        var hd2 = await CreateOpeningDebtAsync(client, customerId, DebtSide.Receivable, OpeningDebtKind.Debt, "HD2",
            new DateOnly(2026, 9, 15), 2_000m, dueDate: new DateOnly(2026, 9, 25));

        var request = CashRequest(CashVoucherType.Receipt, await CashReasonIdAsync("TKH"), fundId, "2026-10-05 09:00",
            customerId, 2_500m);
        request.Allocations = new List<AllocationRequest>
        {
            new() { EntryId = hd1.EntryId, Amount = 1_000m },
            new() { EntryId = hd2.EntryId, Amount = 1_500m },
        };
        var voucher = await CreateCashAsync(client, request);

        var own = (await EntriesOfAsync(DebtSourceType.Receipt, voucher.Id)).Should().ContainSingle().Subject;
        var settlements = await SettlementsOfAsync(own.Id);
        settlements.Should().HaveCount(2);
        settlements.Should().OnlyContain(s => s.Origin == SettlementOrigin.AtVoucher && s.DecreaseEntryId == own.Id
                                              && s.EffectiveAt == Vn("2026-10-05 09:00"));
        settlements.Select(s => s.BatchId).Distinct().Should().ContainSingle("one batch per save");
        settlements.Select(s => (s.IncreaseEntryId, s.Amount)).Should()
            .BeEquivalentTo(new[] { (hd1.EntryId, 1_000m), (hd2.EntryId, 1_500m) });

        var (status, fetched, error) = await GetCashAsync(client, voucher.Id);
        status.Should().Be(HttpStatusCode.OK, error?.Message);
        fetched!.Allocations.Should().BeEquivalentTo(voucher.Allocations);
        fetched.Allocations.OrderBy(a => a.DocCode).Should().BeEquivalentTo(new[]
        {
            new
            {
                EntryId = hd1.EntryId, SourceType = DebtSourceType.Opening, SourceId = hd1.OpeningDebtId,
                DocCode = "HD1", DocDate = new DateOnly(2026, 9, 10), Amount = 1_000m, Origin = SettlementOrigin.AtVoucher,
            },
            new
            {
                EntryId = hd2.EntryId, SourceType = DebtSourceType.Opening, SourceId = hd2.OpeningDebtId,
                DocCode = "HD2", DocDate = new DateOnly(2026, 9, 15), Amount = 1_500m, Origin = SettlementOrigin.AtVoucher,
            },
        }, o => o.WithStrictOrdering());
        fetched.Allocations.Select(a => a.SettlementId).Should().BeEquivalentTo(settlements.Select(s => s.Id));

        var open = await OpenItemsAsync(branchId, customerId);
        open.Should().ContainSingle().Which.Should().BeEquivalentTo(new { EntryId = hd2.EntryId, DocCode = "HD2", Open = 500m });
        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Partial_allocation_leaves_prepayment()
    {
        var (branchId, client, fundId) = await BranchClientAsync();
        var customerId = await CreatePartnerAsync(NewCode("KH"), isCustomer: true, isSupplier: false);
        var hd1 = await CreateOpeningDebtAsync(client, customerId, DebtSide.Receivable, OpeningDebtKind.Debt, "HD1",
            new DateOnly(2026, 9, 10), 1_000m);

        var request = CashRequest(CashVoucherType.Receipt, await CashReasonIdAsync("TKH"), fundId, "2026-10-05 09:00",
            customerId, 3_000m);
        request.Allocations = new List<AllocationRequest> { new() { EntryId = hd1.EntryId, Amount = 1_000m } };
        var voucher = await CreateCashAsync(client, request);

        voucher.Allocations.Should().ContainSingle().Which.Amount.Should().Be(1_000m);
        var open = await OpenItemsAsync(branchId, customerId);
        open.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            SourceType = DebtSourceType.Receipt,
            SourceId = voucher.Id,
            Direction = DebtDirection.Decrease,
            Amount = 3_000m,
            Settled = 1_000m,
            Open = 2_000m,
        });
        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Allocation_errors()
    {
        var (branchId, client, fundId) = await BranchClientAsync();
        var tkh = await CashReasonIdAsync("TKH");
        var customerId = await CreatePartnerAsync(NewCode("KH"), isCustomer: true, isSupplier: false);
        var otherId = await CreatePartnerAsync(NewCode("KH"), isCustomer: true, isSupplier: false);
        var hd1 = await CreateOpeningDebtAsync(client, customerId, DebtSide.Receivable, OpeningDebtKind.Debt, "HD1",
            new DateOnly(2026, 9, 10), 1_000m);
        var hd2 = await CreateOpeningDebtAsync(client, customerId, DebtSide.Receivable, OpeningDebtKind.Debt, "HD2",
            new DateOnly(2026, 9, 15), 2_000m);
        var otherHd = await CreateOpeningDebtAsync(client, otherId, DebtSide.Receivable, OpeningDebtKind.Debt, "HDX",
            new DateOnly(2026, 9, 15), 2_000m);

        UpsertCashVoucherRequest Receipt(Guid reasonId, Guid? partnerId, decimal total, params (Guid EntryId, decimal Amount)[] items)
        {
            var r = CashRequest(CashVoucherType.Receipt, reasonId, fundId, "2026-10-05 09:00", partnerId, total);
            r.Allocations = items.Select(i => new AllocationRequest { EntryId = i.EntryId, Amount = i.Amount }).ToList();
            return r;
        }

        // More than the open amount of HD1.
        AssertFieldError(await PostCashAsync(client, Receipt(tkh, customerId, 2_000m, (hd1.EntryId, 1_500m))),
            "allocations[0].amount");

        // Σ allocations over the voucher Total.
        AssertFieldError(await PostCashAsync(client,
            Receipt(tkh, customerId, 1_000m, (hd1.EntryId, 600m), (hd2.EntryId, 500m))), "allocations");

        // A reason without debt effect takes no allocations (TK, partner allowed: PartnerType Any).
        AssertFieldError(await PostCashAsync(client,
            Receipt(await CashReasonIdAsync("TK"), customerId, 1_000m, (hd1.EntryId, 500m))), "allocations");

        // An entry of another partner.
        AssertFieldError(await PostCashAsync(client, Receipt(tkh, customerId, 1_000m, (otherHd.EntryId, 500m))),
            "allocations[0].entryId");

        (await InDbAsync(db => db.CashVouchers.IgnoreQueryFilters().CountAsync(v => v.BranchId == branchId)))
            .Should().Be(0, "every rejected save rolls back");
        foreach (var entryId in new[] { hd1.EntryId, hd2.EntryId, otherHd.EntryId })
            (await SettlementsOfAsync(entryId)).Should().BeEmpty();

        // An empty list is always accepted, also with a reason without debt effect.
        var empty = Receipt(await CashReasonIdAsync("TK"), null, 1_000m);
        (await PostCashAsync(client, empty)).Status.Should().Be(HttpStatusCode.OK);
        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Refund_payment_allocates_against_prepayment()
    {
        var (branchId, client, fundId) = await BranchClientAsync();
        // Enough money for the payment, so the negative-fund policy never applies here.
        await SetFundOpeningAmountAsync(fundId, 10_000m);
        var customerId = await CreatePartnerAsync(NewCode("KH"), isCustomer: true, isSupplier: false);
        var prepayment = await CreateOpeningDebtAsync(client, customerId, DebtSide.Receivable,
            OpeningDebtKind.Prepayment, "UT1", new DateOnly(2026, 9, 10), 2_000m);

        var request = CashRequest(CashVoucherType.Payment, await CashReasonIdAsync("CHKH"), fundId, "2026-10-05 09:00",
            customerId, 1_500m);
        request.Allocations = new List<AllocationRequest> { new() { EntryId = prepayment.EntryId, Amount = 1_500m } };
        var voucher = await CreateCashAsync(client, request);

        var own = (await EntriesOfAsync(DebtSourceType.Payment, voucher.Id)).Should().ContainSingle().Subject;
        own.Direction.Should().Be(DebtDirection.Increase);
        (await SettlementsOfAsync(own.Id)).Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            IncreaseEntryId = own.Id,
            DecreaseEntryId = prepayment.EntryId,
            Amount = 1_500m,
            Origin = SettlementOrigin.AtVoucher,
        });
        voucher.Allocations.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            EntryId = prepayment.EntryId,
            DocCode = "UT1",
            Amount = 1_500m,
        });

        (await OpenItemsAsync(branchId, customerId)).Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new { EntryId = prepayment.EntryId, Open = 500m });
        await AssertCashDebtInvariantsAsync();
    }

    /// A fresh branch opened on 10-01 with an admin client working in it and its default cash fund.
    private async Task<(Guid BranchId, HttpClient Client, Guid FundId)> BranchClientAsync()
    {
        var branchId = await CreateBranchAsync(NewCode("CVA"));
        await SetBranchOpeningDateAsync(branchId, Oct1);
        var client = CloneAdminClient();
        UseBranch(client, branchId);
        return (branchId, client, await FundIdAsync("QTM", branchId));
    }

    private Task<IReadOnlyList<OpenItemDto>> OpenItemsAsync(Guid branchId, Guid partnerId) =>
        InTransactionAsync((sp, ct) => sp.GetRequiredService<IDebtQueryService>().OpenItemsAsync(new OpenItemsQuery
        {
            BranchId = branchId,
            PartnerId = partnerId,
            Side = DebtSide.Receivable,
        }, ct));

    private static void AssertFieldError(
        (HttpStatusCode Status, CashVoucherDto? Voucher, Application.Common.Models.ApiError? Error) result, string key)
    {
        result.Status.Should().Be(HttpStatusCode.BadRequest, result.Error?.Message);
        result.Error!.Details.Should().ContainKey(key);
    }

    private static string NewCode(string prefix) => prefix + Guid.NewGuid().ToString("N")[..8];
}
