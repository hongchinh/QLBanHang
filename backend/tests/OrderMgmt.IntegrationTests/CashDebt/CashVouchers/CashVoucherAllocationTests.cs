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

/// Allocation at voucher time (rule 4, AllocationValidator, AtVoucher settlements) on create, and the ownership of
/// AtVoucher settlements between two cash vouchers on edit.
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
    public async Task Allocation_amounts_must_fit_numeric_18_2()
    {
        var (branchId, client, fundId) = await BranchClientAsync();
        var tkh = await CashReasonIdAsync("TKH");
        var customerId = await CreatePartnerAsync(NewCode("KH"), isCustomer: true, isSupplier: false);
        var hd1 = await CreateOpeningDebtAsync(client, customerId, DebtSide.Receivable, OpeningDebtKind.Debt, "HD1",
            new DateOnly(2026, 9, 10), 1_000m);
        var hd2 = await CreateOpeningDebtAsync(client, customerId, DebtSide.Receivable, OpeningDebtKind.Debt, "HD2",
            new DateOnly(2026, 9, 15), 1_000m);

        UpsertCashVoucherRequest Receipt(decimal total, params (Guid EntryId, decimal Amount)[] items)
        {
            var r = CashRequest(CashVoucherType.Receipt, tkh, fundId, "2026-10-05 09:00", customerId, total);
            r.Allocations = items.Select(i => new AllocationRequest { EntryId = i.EntryId, Amount = i.Amount }).ToList();
            return r;
        }

        // Σ is exactly 1,000, but numeric(18,2) would store 500.01 + 500.00 and over-settle the receipt.
        var split = await PostCashAsync(client, Receipt(1_000m, (hd1.EntryId, 500.005m), (hd2.EntryId, 499.995m)));
        AssertFieldError(split, "allocations[0].amount");
        split.Error!.Details.Should().ContainKey("allocations[1].amount");

        // Would round to 0.00 and break ck_debt_settlements_amount_positive.
        AssertFieldError(await PostCashAsync(client, Receipt(1_000m, (hd1.EntryId, 0.004m))), "allocations[0].amount");

        // Beyond 16 integer digits (and Σ would overflow decimal).
        AssertFieldError(await PostCashAsync(client,
            Receipt(1_000m, (hd1.EntryId, 5e28m), (hd2.EntryId, 5e28m))), "allocations[0].amount");

        // Zero or negative.
        AssertFieldError(await PostCashAsync(client, Receipt(1_000m, (hd1.EntryId, 0m))), "allocations[0].amount");

        (await InDbAsync(db => db.CashVouchers.IgnoreQueryFilters().CountAsync(v => v.BranchId == branchId)))
            .Should().Be(0, "every rejected save rolls back");
        foreach (var entryId in new[] { hd1.EntryId, hd2.EntryId })
            (await SettlementsOfAsync(entryId)).Should().BeEmpty();
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

    [Fact]
    public async Task Editing_a_voucher_keeps_the_allocations_another_voucher_made_against_it()
    {
        var (branchId, client, fundId) = await BranchClientAsync();
        await SetFundOpeningAmountAsync(fundId, 10_000m);
        var customerId = await CreatePartnerAsync(NewCode("KH"), isCustomer: true, isSupplier: false);
        var otherId = await CreatePartnerAsync(NewCode("KH"), isCustomer: true, isSupplier: false);

        // A prepayment receipt, then a refund payment allocated against it: the AtVoucher settlement is the refund's.
        var receipt = await CreateCashAsync(client, CashRequest(CashVoucherType.Receipt, await CashReasonIdAsync("TKH"),
            fundId, "2026-10-05 09:00", customerId, 2_000m));
        var receiptEntry = (await EntriesOfAsync(DebtSourceType.Receipt, receipt.Id)).Single();
        var refundRequest = CashRequest(CashVoucherType.Payment, await CashReasonIdAsync("CHKH"), fundId,
            "2026-10-06 09:00", customerId, 1_500m);
        refundRequest.Allocations = new List<AllocationRequest> { new() { EntryId = receiptEntry.Id, Amount = 1_500m } };
        var refund = await CreateCashAsync(client, refundRequest);
        var settlement = (await SettlementsOfAsync(receiptEntry.Id)).Should().ContainSingle().Subject;
        settlement.BatchId.Should().Be(refund.Id, "an AtVoucher batch belongs to the voucher that made it");

        refund.Allocations.Should().ContainSingle().Which.IsOwn.Should().BeTrue();
        var (_, fetched, _) = await GetCashAsync(client, receipt.Id);
        fetched!.Allocations.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            SettlementId = settlement.Id, Origin = SettlementOrigin.AtVoucher, IsOwn = false, Amount = 1_500m,
        });

        // The receipt's edit grid gets no room back from the refund's settlement.
        var editOpen = await InTransactionAsync((sp, ct) => sp.GetRequiredService<IDebtQueryService>()
            .OpenItemsAsync(new OpenItemsQuery
            {
                BranchId = branchId,
                PartnerId = customerId,
                Side = DebtSide.Receivable,
                ForSourceType = DebtSourceType.Receipt,
                ForSourceId = receipt.Id,
            }, ct));
        editOpen.Should().BeEmpty();

        // An empty allocation list clears only the receipt's own allocations.
        var clear = CashUpdateFrom(fetched);
        clear.Allocations = new List<AllocationRequest>();
        var (clearStatus, cleared, clearError) = await PutCashAsync(client, receipt.Id, clear);
        clearStatus.Should().Be(HttpStatusCode.OK, clearError?.Message);
        (await SettlementsOfAsync(receiptEntry.Id)).Should().ContainSingle().Which.Id.Should().Be(settlement.Id);

        // The refund's settlement still blocks a partner change (P11).
        var move = CashUpdateFrom(cleared!);
        move.PartnerId = otherId;
        move.Allocations = new List<AllocationRequest>();
        var (moveStatus, _, moveError) = await PutCashAsync(client, receipt.Id, move);
        moveStatus.Should().Be(HttpStatusCode.Conflict, moveError?.Message);
        moveError!.Code.Should().Be("DEBT_HAS_SETTLEMENTS");
        (await SettlementsOfAsync(receiptEntry.Id)).Should().ContainSingle().Which.Id.Should().Be(settlement.Id);
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
