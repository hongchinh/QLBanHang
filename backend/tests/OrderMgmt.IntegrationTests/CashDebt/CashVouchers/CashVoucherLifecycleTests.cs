using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.CashDebt.CashVouchers.Models;
using OrderMgmt.Application.CashDebt.Funds.Models;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Application.Organization.Branches.Models;
using OrderMgmt.Domain.Entities.CashDebt;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.CashDebt.CashVouchers;

/// Cancel, restore and delete, and the negative-fund policy on every write path.
[Collection(nameof(PostgresCollection))]
public class CashVoucherLifecycleTests : CashDebtTestBase
{
    private static readonly DateOnly Oct1 = new(2026, 10, 1);

    public CashVoucherLifecycleTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Cancel_restore_delete_follow_status_rules()
    {
        var (_, client, fundId) = await BranchClientAsync();
        var customerId = await CreatePartnerAsync(NewCode("KH"), isCustomer: true, isSupplier: false);
        var voucher = await CreateCashAsync(client, CashRequest(CashVoucherType.Receipt, await CashReasonIdAsync("TKH"),
            fundId, "2026-10-05 09:00", customerId, 1_000m));
        (await EntriesOfAsync(DebtSourceType.Receipt, voucher.Id)).Should().ContainSingle();

        // Cancel: status Cancelled, entry removed.
        var (cancelStatus, cancelled, cancelError) = await CancelCashAsync(client, voucher.Id,
            new CashDocumentActionRequest { Version = voucher.Version });
        cancelStatus.Should().Be(HttpStatusCode.OK, cancelError?.Message);
        cancelled!.Status.Should().Be(DocumentStatus.Cancelled);
        cancelled.CancelledAt.Should().NotBeNull();
        (await EntriesOfAsync(DebtSourceType.Receipt, voucher.Id)).Should().BeEmpty();

        // A Cancelled voucher cannot be cancelled again.
        (await CancelCashAsync(client, voucher.Id, new CashDocumentActionRequest { Version = cancelled.Version }))
            .Status.Should().Be(HttpStatusCode.Conflict);

        // Restore: entry back, with no settlements.
        var (restoreStatus, restored, restoreError) = await RestoreCashAsync(client, voucher.Id,
            new CashDocumentActionRequest { Version = cancelled.Version });
        restoreStatus.Should().Be(HttpStatusCode.OK, restoreError?.Message);
        restored!.Status.Should().Be(DocumentStatus.Active);
        restored.CancelledAt.Should().BeNull();
        restored.Allocations.Should().BeEmpty();
        var entry = (await EntriesOfAsync(DebtSourceType.Receipt, voucher.Id)).Should().ContainSingle().Subject;
        entry.Should().BeEquivalentTo(new { PartnerId = customerId, Amount = 1_000m, PostedAt = voucher.VoucherAt });
        (await SettlementsOfAsync(entry.Id)).Should().BeEmpty();
        // An Active voucher cannot be restored.
        (await RestoreCashAsync(client, voucher.Id, new CashDocumentActionRequest { Version = restored.Version }))
            .Status.Should().Be(HttpStatusCode.Conflict);

        // Delete of a Cancelled voucher: hidden from GET.
        var (_, cancelledAgain, _) = await CancelCashAsync(client, voucher.Id,
            new CashDocumentActionRequest { Version = restored.Version });
        var (deleteStatus, _, deleteError) = await DeleteCashAsync(client, voucher.Id,
            new CashDocumentActionRequest { Version = cancelledAgain!.Version });
        deleteStatus.Should().Be(HttpStatusCode.OK, deleteError?.Message);
        (await GetCashAsync(client, voucher.Id)).Status.Should().Be(HttpStatusCode.NotFound);
        (await EntriesOfAsync(DebtSourceType.Receipt, voucher.Id)).Should().BeEmpty();

        (await ActivitiesOfAsync(voucher.Id)).Select(a => a.Action).Should().Equal(
            CashDocumentActivityAction.Created,
            CashDocumentActivityAction.Cancelled,
            CashDocumentActivityAction.Restored,
            CashDocumentActivityAction.Cancelled,
            CashDocumentActivityAction.Deleted);
        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Cancel_with_settlements_needs_confirmation()
    {
        var (_, client, fundId) = await BranchClientAsync();
        var customerId = await CreatePartnerAsync(NewCode("KH"), isCustomer: true, isSupplier: false);
        var hd1 = await CreateOpeningDebtAsync(client, customerId, DebtSide.Receivable, OpeningDebtKind.Debt, "HD1",
            new DateOnly(2026, 9, 10), 2_000m);
        var create = CashRequest(CashVoucherType.Receipt, await CashReasonIdAsync("TKH"), fundId, "2026-10-05 09:00",
            customerId, 1_000m);
        create.Allocations = new List<AllocationRequest> { new() { EntryId = hd1.EntryId, Amount = 800m } };
        var voucher = await CreateCashAsync(client, create);

        var action = new CashDocumentActionRequest { Version = voucher.Version };
        var (status, _, error) = await CancelCashAsync(client, voucher.Id, action);
        status.Should().Be(HttpStatusCode.UnprocessableEntity);
        error!.Code.Should().Be("DEBT_SETTLEMENTS_WILL_BE_REMOVED");
        var (_, unchanged, _) = await GetCashAsync(client, voucher.Id);
        unchanged!.Should().BeEquivalentTo(new { Status = DocumentStatus.Active, voucher.Version });
        (await SettlementsOfAsync(hd1.EntryId)).Should().ContainSingle();

        action.ConfirmRemoveSettlements = true;
        var (confirmedStatus, confirmed, confirmedError) = await CancelCashAsync(client, voucher.Id, action);
        confirmedStatus.Should().Be(HttpStatusCode.OK, confirmedError?.Message);
        confirmed!.Status.Should().Be(DocumentStatus.Cancelled);
        (await SettlementsOfAsync(hd1.EntryId)).Should().BeEmpty();
        (await EntriesOfAsync(DebtSourceType.Receipt, voucher.Id)).Should().BeEmpty();
        (await ActivitiesOfAsync(voucher.Id)).Select(a => a.Action).Should().BeEquivalentTo(new[]
        {
            CashDocumentActivityAction.Created,
            CashDocumentActivityAction.SettlementRemoved,
            CashDocumentActivityAction.Cancelled,
        });
        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Negative_fund_policy_on_each_operation()
    {
        var (branchId, client, fundId) = await BranchClientAsync();
        await SetFundOpeningAmountAsync(fundId, 1_000m);
        var receiptReason = await CashReasonIdAsync("TK");
        var paymentReason = await CashReasonIdAsync("CK");

        try
        {
            // Create: payment 1,500 from 1,000 → Warn; acknowledged → 200.
            var overdraw = CashRequest(CashVoucherType.Payment, paymentReason, fundId, "2026-10-03 09:00", null, 1_500m);
            var (warnStatus, _, warnError) = await PostCashAsync(client, overdraw);
            AssertNegativeFund(warnStatus, warnError, "NEGATIVE_FUND_WARNING");
            warnError!.Details.Should().ContainKey("QTM").WhoseValue.Should().Equal("Âm 500 tại 03/10/2026 09:00");
            (await VoucherCountAsync(branchId)).Should().Be(0, "a rejected create saves nothing");
            overdraw.AcknowledgeNegativeFund = true;
            var overdrawn = await CreateCashAsync(client, overdraw);

            // Restore of a cancelled overdrawing payment asks again (cancelling it only improves the fund).
            var (_, cancelledPayment, _) = await CancelCashAsync(client, overdrawn.Id,
                new CashDocumentActionRequest { Version = overdrawn.Version });
            var restore = new CashDocumentActionRequest { Version = cancelledPayment!.Version };
            var (restoreWarn, _, restoreWarnError) = await RestoreCashAsync(client, overdrawn.Id, restore);
            AssertNegativeFund(restoreWarn, restoreWarnError, "NEGATIVE_FUND_WARNING");
            restore.AcknowledgeNegativeFund = true;
            var (restoredStatus, restoredPayment, restoredError) = await RestoreCashAsync(client, overdrawn.Id, restore);
            restoredStatus.Should().Be(HttpStatusCode.OK, restoredError?.Message);
            var (_, removed, _) = await CancelCashAsync(client, overdrawn.Id,
                new CashDocumentActionRequest { Version = restoredPayment!.Version });
            (await DeleteCashAsync(client, overdrawn.Id, new CashDocumentActionRequest { Version = removed!.Version }))
                .Status.Should().Be(HttpStatusCode.OK);

            // Receipt 2,000 @10-01 funds payment 1,500 @10-02: removing the receipt would overdraw the fund.
            var receipt = await CreateCashAsync(client,
                CashRequest(CashVoucherType.Receipt, receiptReason, fundId, "2026-10-01 09:00", null, 2_000m));
            var paymentRequest = CashRequest(CashVoucherType.Payment, paymentReason, fundId, "2026-10-02 09:00", null, 1_500m);
            paymentRequest.AcknowledgeNegativeFund = true;
            var payment = await CreateCashAsync(client, paymentRequest);

            var receiptAction = new CashDocumentActionRequest { Version = receipt.Version };
            var (cancelStatus, _, cancelError) = await CancelCashAsync(client, receipt.Id, receiptAction);
            AssertNegativeFund(cancelStatus, cancelError, "NEGATIVE_FUND_WARNING");
            cancelError!.Details.Should().ContainKey("QTM").WhoseValue.Should().Equal("Âm 500 tại 02/10/2026 09:00");
            var (deleteStatus, _, deleteError) = await DeleteCashAsync(client, receipt.Id, receiptAction);
            AssertNegativeFund(deleteStatus, deleteError, "NEGATIVE_FUND_WARNING");

            // Update: shrinking the receipt below the payment also asks.
            var shrink = CashUpdateFrom(receipt);
            shrink.Lines[0].Amount = 100m;
            var (shrinkStatus, _, shrinkError) = await PutCashAsync(client, receipt.Id, shrink);
            AssertNegativeFund(shrinkStatus, shrinkError, "NEGATIVE_FUND_WARNING");

            // Block: nothing changes, even when acknowledged.
            await SetPolicyAsync(LimitPolicy.Block);
            receiptAction.AcknowledgeNegativeFund = true;
            var (blockedStatus, _, blockedError) = await CancelCashAsync(client, receipt.Id, receiptAction);
            AssertNegativeFund(blockedStatus, blockedError, "NEGATIVE_FUND_BLOCKED");
            var (blockedDelete, _, blockedDeleteError) = await DeleteCashAsync(client, receipt.Id, receiptAction);
            AssertNegativeFund(blockedDelete, blockedDeleteError, "NEGATIVE_FUND_BLOCKED");
            var (_, stored, _) = await GetCashAsync(client, receipt.Id);
            stored.Should().BeEquivalentTo(new { Status = DocumentStatus.Active, Total = 2_000m, receipt.Version });

            // Moving the payment to a fund without money is blocked; to a fund with enough money it passes.
            var emptyBank = await CreateBankFundAsync(client, "NH0", 0m);
            var richBank = await CreateBankFundAsync(client, "NH1", 5_000m);
            var toEmpty = CashUpdateFrom(payment);
            toEmpty.FundId = emptyBank;
            toEmpty.AcknowledgeNegativeFund = true;
            var (emptyStatus, _, emptyError) = await PutCashAsync(client, payment.Id, toEmpty);
            AssertNegativeFund(emptyStatus, emptyError, "NEGATIVE_FUND_BLOCKED");
            emptyError!.Details.Should().ContainKey("NH0").And.NotContainKey("QTM");
            (await GetCashAsync(client, payment.Id)).Voucher!.FundId.Should().Be(fundId);

            var toRich = CashUpdateFrom(payment);
            toRich.FundId = richBank;
            var (movedStatus, moved, movedError) = await PutCashAsync(client, payment.Id, toRich);
            movedStatus.Should().Be(HttpStatusCode.OK, movedError?.Message);
            moved!.FundId.Should().Be(richBank);

            // QTM no longer pays anything, so the receipt can now be cancelled under Block.
            var (cancelledStatus, cancelledReceipt, cancelledError) = await CancelCashAsync(client, receipt.Id,
                new CashDocumentActionRequest { Version = receipt.Version });
            cancelledStatus.Should().Be(HttpStatusCode.OK, cancelledError?.Message);
            cancelledReceipt!.Status.Should().Be(DocumentStatus.Cancelled);

            // Under Block a new overdraft is rejected and nothing is saved.
            var count = await VoucherCountAsync(branchId);
            var blockedCreate = CashRequest(CashVoucherType.Payment, paymentReason, fundId, "2026-10-04 09:00", null, 5_000m);
            blockedCreate.AcknowledgeNegativeFund = true;
            var (blockedCreateStatus, _, blockedCreateError) = await PostCashAsync(client, blockedCreate);
            AssertNegativeFund(blockedCreateStatus, blockedCreateError, "NEGATIVE_FUND_BLOCKED");
            (await VoucherCountAsync(branchId)).Should().Be(count);

            // Allow: the same overdraft passes without acknowledgement.
            await SetPolicyAsync(LimitPolicy.Allow);
            blockedCreate.AcknowledgeNegativeFund = false;
            var (allowStatus, _, allowError) = await PostCashAsync(client, blockedCreate);
            allowStatus.Should().Be(HttpStatusCode.OK, allowError?.Message);
            await AssertCashDebtInvariantsAsync();
        }
        finally
        {
            await SetPolicyAsync(LimitPolicy.Warn);
        }
    }

    [Fact]
    public async Task Restore_requires_existing_references()
    {
        var (_, client, _) = await BranchClientAsync();
        var reasonId = await InDbAsync(async db =>
        {
            var reason = new CashReason
            {
                Code = NewCode("LD"), Name = "Thu khác có đối tượng", Type = CashVoucherType.Receipt,
                PartnerType = PartnerType.Any, DebtEffect = DebtEffect.None,
            };
            db.CashReasons.Add(reason);
            await db.SaveChangesAsync();
            return reason.Id;
        });
        var bankId = await CreateBankFundAsync(client, "NH1", 0m);
        var partnerId = await CreatePartnerAsync(NewCode("KH"), isCustomer: true, isSupplier: false);
        var voucher = await CreateCashAsync(client,
            CashRequest(CashVoucherType.Receipt, reasonId, bankId, "2026-10-05 09:00", partnerId, 1_000m));
        var (_, cancelled, _) = await CancelCashAsync(client, voucher.Id,
            new CashDocumentActionRequest { Version = voucher.Version });

        await InDbAsync(async db =>
        {
            await db.CashReasons.Where(r => r.Id == reasonId).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsDeleted, true));
            await db.Funds.Where(f => f.Id == bankId).ExecuteUpdateAsync(s => s.SetProperty(f => f.IsDeleted, true));
            await db.Customers.Where(p => p.Id == partnerId).ExecuteUpdateAsync(s => s.SetProperty(p => p.IsDeleted, true));
        });

        var (status, _, error) = await RestoreCashAsync(client, voucher.Id,
            new CashDocumentActionRequest { Version = cancelled!.Version });
        status.Should().Be(HttpStatusCode.BadRequest);
        error!.Details.Should().ContainKeys("reasonId", "fundId", "partnerId");
        var (_, stored, _) = await GetCashAsync(client, voucher.Id);
        stored.Should().BeEquivalentTo(new { Status = DocumentStatus.Cancelled, cancelled.Version });
        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Period_lock_on_lifecycle()
    {
        var (branchId, client, fundId) = await BranchClientAsync();
        var reasonId = await CashReasonIdAsync("TK");
        var active = await CreateCashAsync(client,
            CashRequest(CashVoucherType.Receipt, reasonId, fundId, "2026-10-05 09:00", null, 1_000m));
        var other = await CreateCashAsync(client,
            CashRequest(CashVoucherType.Receipt, reasonId, fundId, "2026-10-05 10:00", null, 500m));
        var (_, cancelled, _) = await CancelCashAsync(client, other.Id,
            new CashDocumentActionRequest { Version = other.Version });

        var lockResponse = await _client.PutAsJsonAsync($"/api/branches/{branchId}/lock",
            new SetPeriodLockRequest { LockedUntil = new DateOnly(2026, 10, 5) });
        lockResponse.StatusCode.Should().Be(HttpStatusCode.OK, await lockResponse.Content.ReadAsStringAsync());

        var activeAction = new CashDocumentActionRequest { Version = active.Version };
        AssertPeriodLocked(await CancelCashAsync(client, active.Id, activeAction));
        AssertPeriodLocked(await DeleteCashAsync(client, active.Id, activeAction));
        var cancelledAction = new CashDocumentActionRequest { Version = cancelled!.Version };
        AssertPeriodLocked(await RestoreCashAsync(client, other.Id, cancelledAction));
        AssertPeriodLocked(await DeleteCashAsync(client, other.Id, cancelledAction));

        (await GetCashAsync(client, active.Id)).Voucher!.Status.Should().Be(DocumentStatus.Active);
        (await GetCashAsync(client, other.Id)).Voucher!.Status.Should().Be(DocumentStatus.Cancelled);
        await AssertCashDebtInvariantsAsync();
    }

    private static void AssertNegativeFund(HttpStatusCode status, ApiError? error, string code)
    {
        status.Should().Be(HttpStatusCode.UnprocessableEntity, error?.Message);
        error!.Code.Should().Be(code);
    }

    private static void AssertPeriodLocked((HttpStatusCode Status, CashVoucherDto? Voucher, ApiError? Error) result)
    {
        result.Status.Should().Be(HttpStatusCode.BadRequest, result.Error?.Message);
        result.Error!.Code.Should().Be("PERIOD_LOCKED");
    }

    private Task SetPolicyAsync(LimitPolicy policy) =>
        UpdateCashDebtSettingsAsync(s => s.NegativeFundPolicy = policy);

    /// A bank fund of the client's working branch with the given opening amount.
    private async Task<Guid> CreateBankFundAsync(HttpClient client, string code, decimal openingAmount)
    {
        var fund = await ReadDataAsync<FundDto>(await client.PostAsJsonAsync("/api/funds", new CreateFundRequest
        {
            Code = code, Name = "Ngân hàng " + code, Kind = FundKind.Bank, IsActive = true,
        }));
        if (openingAmount != 0)
            await SetFundOpeningAmountAsync(fund.Id, openingAmount);
        return fund.Id;
    }

    private Task<int> VoucherCountAsync(Guid branchId) =>
        InDbAsync(db => db.CashVouchers.IgnoreQueryFilters().CountAsync(v => v.BranchId == branchId));

    /// A fresh branch opened on 10-01 with an admin client working in it and its default cash fund.
    private async Task<(Guid BranchId, HttpClient Client, Guid FundId)> BranchClientAsync()
    {
        var branchId = await CreateBranchAsync(NewCode("CVL"));
        await SetBranchOpeningDateAsync(branchId, Oct1);
        var client = CloneAdminClient();
        UseBranch(client, branchId);
        return (branchId, client, await FundIdAsync("QTM", branchId));
    }

    private Task<List<CashDocumentActivity>> ActivitiesOfAsync(Guid documentId) =>
        InDbAsync(db => db.CashDocumentActivities.AsNoTracking()
            .Where(a => a.DocumentId == documentId)
            .OrderBy(a => a.OccurredAt)
            .ToListAsync());

    private static string NewCode(string prefix) => prefix + Guid.NewGuid().ToString("N")[..8];
}
