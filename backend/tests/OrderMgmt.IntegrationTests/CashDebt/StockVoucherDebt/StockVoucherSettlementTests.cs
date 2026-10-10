using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderMgmt.Application.CashDebt.CashVouchers.Models;
using OrderMgmt.Application.CashDebt.Ledger;
using OrderMgmt.Application.CashDebt.OpeningDebts.Models;
using OrderMgmt.Application.Inventory.StockVouchers.Models;
using OrderMgmt.Application.Organization.Branches.Models;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.CashDebt.StockVoucherDebt;

/// Settlement confirmations on stock voucher saves, cancellations and deletions (P10, P11, P21, C13).
[Collection(nameof(PostgresCollection))]
public class StockVoucherSettlementTests : CashDebtTestBase
{
    private const string Removed = "DEBT_SETTLEMENTS_WILL_BE_REMOVED";

    private Guid _branchId;
    private Guid _warehouseId;
    private Guid _cashFundId;
    private Guid _cashMethodId;
    private HttpClient _branchClient = default!;

    public StockVoucherSettlementTests(PostgresFixture pg) : base(pg) { }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        // A fresh branch per test: the codes PX00001 / PT00001 are asserted. Opening debts need its opening date.
        var code = "SS" + Guid.NewGuid().ToString("N")[..8];
        _branchId = await CreateBranchAsync(code);
        await SetBranchOpeningDateAsync(_branchId, new DateOnly(2026, 10, 1));
        _warehouseId = await CreateWarehouseAsync(_branchId, code);
        _cashFundId = await CashFundIdAsync(_branchId);
        // Cancelling or deleting a receipt takes money out of QTM: an opening balance keeps it from going negative.
        await SetFundOpeningAmountAsync(_cashFundId, 1_000_000m);
        _cashMethodId = await InDbAsync(db => db.PaymentMethods.Where(m => m.Code == "TM").Select(m => m.Id).SingleAsync());
        _branchClient = CloneAdminClient();
        UseBranch(_branchClient, _branchId);
    }

    [Fact]
    public async Task Discount_after_payment_needs_confirmation()
    {
        var customer = await NewCustomerAsync();
        var sale = await CreateSaleAsync(customer, 3, 1_000, paid: 0);
        var stockEntry = (await EntriesOfAsync(DebtSourceType.StockOut, sale.Id)).Single();
        var receipt = await CreateReceiptAsync(customer, 2_000m, (stockEntry.Id, 2_000m));
        receipt.Code.Should().Be("PT00001");

        // C13: a discount after the customer paid 2,000 brings the sale below what is settled.
        var discount = UpdateRequestFrom(sale);
        discount.Lines[0].UnitPrice = 500;
        var (status, _, error) = await PutVoucherAsync(_branchClient, sale.Id, discount);
        status.Should().Be(HttpStatusCode.UnprocessableEntity);
        error!.Code.Should().Be(Removed);
        error.Details.Should().ContainSingle().Which.Should().BeEquivalentTo(new KeyValuePair<string, string[]>(
            "PT00001", new[] { DebtActivityLog.SettlementRemovedText(2_000m, "PX00001") }));
        (await GetVoucherAsync(_branchClient, sale.Id)).Total.Should().Be(3_000m, "a rejected save changes nothing");
        (await SettlementsOfAsync(stockEntry.Id)).Should().ContainSingle();

        discount.ConfirmRemoveSettlements = true;
        var (confirmedStatus, discounted, confirmedError) = await PutVoucherAsync(_branchClient, sale.Id, discount);
        confirmedStatus.Should().Be(HttpStatusCode.OK, confirmedError?.Message);
        discounted!.Total.Should().Be(1_500m);
        (await EntriesOfAsync(DebtSourceType.StockOut, sale.Id)).Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new { stockEntry.Id, Amount = 1_500m });
        (await SettlementsOfAsync(stockEntry.Id)).Should().BeEmpty();
        (await StockActivitiesAsync(sale.Id, StockVoucherActivityAction.SettlementRemoved))
            .Should().Equal(DebtActivityLog.SettlementRemovedText(2_000m, "PT00001"));
        (await CashActivitiesAsync(receipt.Id, CashDocumentActivityAction.SettlementRemoved))
            .Should().Equal(DebtActivityLog.SettlementRemovedText(2_000m, "PX00001"));
        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Auto_settlement_never_asks()
    {
        var customer = await NewCustomerAsync();
        var sale = await CreateSaleAsync(customer, 3, 1_000, paid: 1_000);

        // Total 800 is below the Auto 1,000: the voucher's own settlement is recomputed, with no confirmation (P21).
        var edit = UpdateRequestFrom(sale);
        edit.Lines[0].Quantity = 1;
        edit.Lines[0].UnitPrice = 800;
        var (status, edited, error) = await PutVoucherAsync(_branchClient, sale.Id, edit);
        status.Should().Be(HttpStatusCode.OK, error?.Message);
        edited!.Should().BeEquivalentTo(new { Total = 800m, PaidAmount = 1_000m });

        var stockEntry = (await EntriesOfAsync(DebtSourceType.StockOut, sale.Id)).Single();
        var receiptEntry = (await EntriesOfAsync(DebtSourceType.Receipt, sale.AutoCashVoucherId!.Value)).Single();
        stockEntry.Amount.Should().Be(800m);
        receiptEntry.Amount.Should().Be(1_000m);
        (await SettlementsOfAsync(stockEntry.Id)).Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            IncreaseEntryId = stockEntry.Id,
            DecreaseEntryId = receiptEntry.Id,
            Amount = 800m,
            Origin = SettlementOrigin.Auto,
        });
        // The automatic receipt keeps 200 open as a prepayment.
        (receiptEntry.Amount - (await SettlementsOfAsync(receiptEntry.Id)).Sum(s => s.Amount)).Should().Be(200m);
        (await StockActivitiesAsync(sale.Id, StockVoucherActivityAction.SettlementRemoved)).Should().BeEmpty();
        (await CashActivitiesAsync(sale.AutoCashVoucherId!.Value, CashDocumentActivityAction.SettlementRemoved))
            .Should().BeEmpty();
        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Cancel_with_foreign_settlement_needs_confirmation()
    {
        var customer = await NewCustomerAsync();
        var sale = await CreateSaleAsync(customer, 3, 1_000, paid: 0);
        var stockEntry = (await EntriesOfAsync(DebtSourceType.StockOut, sale.Id)).Single();
        var receipt = await CreateReceiptAsync(customer, 1_000m, (stockEntry.Id, 1_000m));
        var receiptEntry = (await EntriesOfAsync(DebtSourceType.Receipt, receipt.Id)).Single();

        var cancel = new StockVoucherActionRequest { Version = sale.Version };
        var (status, _, error) = await ActionAsync(sale.Id, "cancel", cancel);
        status.Should().Be(HttpStatusCode.UnprocessableEntity);
        error!.Code.Should().Be(Removed);
        error.Details.Should().ContainSingle().Which.Key.Should().Be(receipt.Code);
        (await GetVoucherAsync(_branchClient, sale.Id)).Status.Should().Be(StockVoucherStatus.Active);
        (await SettlementsOfAsync(receiptEntry.Id)).Should().ContainSingle();

        cancel.ConfirmRemoveSettlements = true;
        var (confirmedStatus, cancelled, confirmedError) = await ActionAsync(sale.Id, "cancel", cancel);
        confirmedStatus.Should().Be(HttpStatusCode.OK, confirmedError?.Message);
        cancelled!.Status.Should().Be(StockVoucherStatus.Cancelled);
        (await EntriesOfAsync(DebtSourceType.StockOut, sale.Id)).Should().BeEmpty();
        (await SettlementsOfAsync(receiptEntry.Id)).Should().BeEmpty();
        (await StockActivitiesAsync(sale.Id, StockVoucherActivityAction.SettlementRemoved))
            .Should().Equal(DebtActivityLog.SettlementRemovedText(1_000m, receipt.Code));
        (await CashActivitiesAsync(receipt.Id, CashDocumentActivityAction.SettlementRemoved))
            .Should().Equal(DebtActivityLog.SettlementRemovedText(1_000m, sale.Code));
        await AssertCashDebtInvariantsAsync();

        // Delete asks the same way; its flags travel in the query string.
        var other = await CreateSaleAsync(customer, 2, 1_000, paid: 0);
        var otherEntry = (await EntriesOfAsync(DebtSourceType.StockOut, other.Id)).Single();
        var otherReceipt = await CreateReceiptAsync(customer, 500m, (otherEntry.Id, 500m));
        var (deleteStatus, deleteError) = await DeleteAsync(other.Id, other.Version, confirm: false);
        deleteStatus.Should().Be(HttpStatusCode.UnprocessableEntity);
        deleteError!.Details.Should().ContainSingle().Which.Key.Should().Be(otherReceipt.Code);
        (await DeleteAsync(other.Id, other.Version, confirm: true)).Status.Should().Be(HttpStatusCode.OK);
        (await EntriesOfAsync(DebtSourceType.StockOut, other.Id)).Should().BeEmpty();
        (await SettlementsOfAsync((await EntriesOfAsync(DebtSourceType.Receipt, otherReceipt.Id)).Single().Id))
            .Should().BeEmpty();
        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Changing_partner_with_foreign_settlement_is_409()
    {
        var customer = await NewCustomerAsync();
        var otherCustomer = await NewCustomerAsync();
        var sale = await CreateSaleAsync(customer, 3, 1_000, paid: 0);
        var stockEntry = (await EntriesOfAsync(DebtSourceType.StockOut, sale.Id)).Single();
        await CreateReceiptAsync(customer, 1_000m, (stockEntry.Id, 1_000m));

        // P11: a new partner, or a reason without debt effect, would move or drop a settled entry; even confirmed.
        var move = UpdateRequestFrom(sale);
        move.PartnerId = otherCustomer;
        move.PartnerName = move.PartnerAddress = move.PartnerTaxCode = null;
        move.ConfirmRemoveSettlements = true;
        var (status, _, error) = await PutVoucherAsync(_branchClient, sale.Id, move);
        status.Should().Be(HttpStatusCode.Conflict);
        error!.Code.Should().Be("DEBT_HAS_SETTLEMENTS");

        var noEffect = UpdateRequestFrom(sale);
        noEffect.ReasonId = await ReasonIdAsync("XKH");
        noEffect.ConfirmRemoveSettlements = true;
        var (noEffectStatus, _, noEffectError) = await PutVoucherAsync(_branchClient, sale.Id, noEffect);
        noEffectStatus.Should().Be(HttpStatusCode.Conflict);
        noEffectError!.Code.Should().Be("DEBT_HAS_SETTLEMENTS");

        (await EntriesOfAsync(DebtSourceType.StockOut, sale.Id)).Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new { stockEntry.Id, PartnerId = customer, Amount = 3_000m });
        (await SettlementsOfAsync(stockEntry.Id)).Should().ContainSingle();
        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Shrinking_overpayment_matched_elsewhere_needs_confirmation()
    {
        var customer = await NewCustomerAsync();
        var debt = await CreateOpeningDebtAsync(_branchClient, customer, DebtSide.Receivable, OpeningDebtKind.Debt, "HD1",
            new DateOnly(2026, 9, 20), 2_000m);
        // Paid 5,000 on 3,000: Auto 3,000, and the receipt's open 2,000 is matched to the opening debt.
        var sale = await CreateSaleAsync(customer, 3, 1_000, paid: 5_000);
        var autoId = sale.AutoCashVoucherId!.Value;
        var stockEntry = (await EntriesOfAsync(DebtSourceType.StockOut, sale.Id)).Single();
        var receiptEntry = (await EntriesOfAsync(DebtSourceType.Receipt, autoId)).Single();
        await MatchManuallyAsync(debt.EntryId, receiptEntry.Id, 2_000m);
        await AssertCashDebtInvariantsAsync();

        // P20: Auto takes only what the Manual settlement leaves, so paid 2,500 still holds it (Auto 500).
        var keep = UpdateRequestFrom(sale);
        keep.PaidAmount = 2_500;
        var (keptStatus, kept, keptError) = await PutVoucherAsync(_branchClient, sale.Id, keep);
        keptStatus.Should().Be(HttpStatusCode.OK, keptError?.Message);
        (await SettlementsOfAsync(receiptEntry.Id)).Select(s => (s.Origin, s.Amount)).Should().BeEquivalentTo(new[]
        {
            (SettlementOrigin.Manual, 2_000m),
            (SettlementOrigin.Auto, 500m),
        });
        await AssertCashDebtInvariantsAsync();

        // Paid 1,500 is below the 2,000 matched elsewhere: the Manual settlement goes only once confirmed.
        var shrink = UpdateRequestFrom(kept!);
        shrink.PaidAmount = 1_500;
        var (status, _, error) = await PutVoucherAsync(_branchClient, sale.Id, shrink);
        status.Should().Be(HttpStatusCode.UnprocessableEntity);
        error!.Code.Should().Be(Removed);
        error.Details.Should().ContainSingle().Which.Should().BeEquivalentTo(new KeyValuePair<string, string[]>(
            "HD1", new[] { DebtActivityLog.SettlementRemovedText(2_000m, "PT00001") }));
        (await GetVoucherAsync(_branchClient, sale.Id)).PaidAmount.Should().Be(2_500m, "a rejected save changes nothing");

        shrink.ConfirmRemoveSettlements = true;
        var (confirmedStatus, _, confirmedError) = await PutVoucherAsync(_branchClient, sale.Id, shrink);
        confirmedStatus.Should().Be(HttpStatusCode.OK, confirmedError?.Message);
        (await SettlementsOfAsync(debt.EntryId)).Should().BeEmpty();
        (await SettlementsOfAsync(receiptEntry.Id)).Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            IncreaseEntryId = stockEntry.Id,
            Amount = 1_500m,
            Origin = SettlementOrigin.Auto,
        });
        (await CashActivitiesAsync(autoId, CashDocumentActivityAction.SettlementRemoved))
            .Should().Equal(DebtActivityLog.SettlementRemovedText(2_000m, "HD1"));
        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Confirmation_lists_settlements_of_both_entries()
    {
        var customer = await NewCustomerAsync();
        var debt = await CreateOpeningDebtAsync(_branchClient, customer, DebtSide.Receivable, OpeningDebtKind.Debt, "HD1",
            new DateOnly(2026, 9, 20), 2_000m);
        // PX00001 3,000: a manual receipt PT00001 pays 1,000 of it, then the sale itself records 5,000 paid (PT00002):
        // Auto 2,000, and 2,000 of the automatic receipt's open 3,000 is matched to the opening debt.
        var sale = await CreateSaleAsync(customer, 3, 1_000, paid: 0);
        var stockEntry = (await EntriesOfAsync(DebtSourceType.StockOut, sale.Id)).Single();
        var receipt = await CreateReceiptAsync(customer, 1_000m, (stockEntry.Id, 1_000m));
        var paid = UpdateRequestFrom(sale);
        paid.PaymentMethodId = _cashMethodId;
        paid.PaidAmount = 5_000;
        var (paidStatus, paidSale, paidError) = await PutVoucherAsync(_branchClient, sale.Id, paid);
        paidStatus.Should().Be(HttpStatusCode.OK, paidError?.Message);
        var autoId = paidSale!.AutoCashVoucherId!.Value;
        paidSale.AutoCashVoucherCode.Should().Be("PT00002");
        var autoEntry = (await EntriesOfAsync(DebtSourceType.Receipt, autoId)).Single();
        await MatchManuallyAsync(debt.EntryId, autoEntry.Id, 2_000m);
        await AssertCashDebtInvariantsAsync();

        // Both entries lose a foreign settlement: one 422 lists them all, keyed by the other document.
        var expected = new Dictionary<string, string[]>
        {
            ["PT00001"] = new[] { DebtActivityLog.SettlementRemovedText(1_000m, "PX00001") },
            ["HD1"] = new[] { DebtActivityLog.SettlementRemovedText(2_000m, "PT00002") },
        };
        var (cancelStatus, _, cancelError) = await ActionAsync(sale.Id, "cancel",
            new StockVoucherActionRequest { Version = paidSale.Version });
        cancelStatus.Should().Be(HttpStatusCode.UnprocessableEntity);
        cancelError!.Code.Should().Be(Removed);
        cancelError.Details.Should().BeEquivalentTo(expected);

        var shrink = UpdateRequestFrom(paidSale);
        shrink.Lines[0].Quantity = 1;
        shrink.Lines[0].UnitPrice = 500;
        shrink.PaidAmount = 500;
        var (status, _, error) = await PutVoucherAsync(_branchClient, sale.Id, shrink);
        status.Should().Be(HttpStatusCode.UnprocessableEntity);
        error!.Code.Should().Be(Removed);
        error.Details.Should().BeEquivalentTo(expected);
        (await SettlementsOfAsync(debt.EntryId)).Should().ContainSingle("a rejected save changes nothing");

        shrink.ConfirmRemoveSettlements = true;
        var (confirmedStatus, _, confirmedError) = await PutVoucherAsync(_branchClient, sale.Id, shrink);
        confirmedStatus.Should().Be(HttpStatusCode.OK, confirmedError?.Message);
        (await SettlementsOfAsync(debt.EntryId)).Should().BeEmpty();
        (await SettlementsOfAsync((await EntriesOfAsync(DebtSourceType.Receipt, receipt.Id)).Single().Id))
            .Should().BeEmpty();
        (await SettlementsOfAsync(stockEntry.Id)).Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            DecreaseEntryId = autoEntry.Id,
            Amount = 500m,
            Origin = SettlementOrigin.Auto,
        });
        (await StockActivitiesAsync(sale.Id, StockVoucherActivityAction.SettlementRemoved))
            .Should().Equal(DebtActivityLog.SettlementRemovedText(1_000m, "PT00001"));
        (await CashActivitiesAsync(receipt.Id, CashDocumentActivityAction.SettlementRemoved))
            .Should().Equal(DebtActivityLog.SettlementRemovedText(1_000m, "PX00001"));
        (await CashActivitiesAsync(autoId, CashDocumentActivityAction.SettlementRemoved))
            .Should().Equal(DebtActivityLog.SettlementRemovedText(2_000m, "HD1"));
        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Cancelling_a_receipt_recomputes_the_auto_settlement()
    {
        var customer = await NewCustomerAsync();
        var (sale, receipt) = await PaidSaleCoveredByReceiptAsync(customer);

        // The sale is open 3,000 again and its automatic receipt open 1,000: Auto = min(1,000, 3,000, 1,000).
        var (status, _, error) = await CancelCashAsync(_branchClient, receipt.Id,
            new CashDocumentActionRequest { Version = receipt.Version, ConfirmRemoveSettlements = true });
        status.Should().Be(HttpStatusCode.OK, error?.Message);
        await AssertAutoSettlementAsync(sale, 1_000m);
        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Deleting_or_editing_a_receipt_recomputes_the_auto_settlement()
    {
        var customer = await NewCustomerAsync();

        // Delete.
        var (deletedSale, deleted) = await PaidSaleCoveredByReceiptAsync(customer);
        var (deleteStatus, _, deleteError) = await DeleteCashAsync(_branchClient, deleted.Id,
            new CashDocumentActionRequest { Version = deleted.Version, ConfirmRemoveSettlements = true });
        deleteStatus.Should().Be(HttpStatusCode.OK, deleteError?.Message);
        await AssertAutoSettlementAsync(deletedSale, 1_000m);
        await AssertCashDebtInvariantsAsync();

        // An edit that lowers the allocation to 1,000: Auto takes what is left after it, min(1,000, 2,000, 1,000).
        var (loweredSale, lowered) = await PaidSaleCoveredByReceiptAsync(customer);
        var loweredEntry = (await EntriesOfAsync(DebtSourceType.StockOut, loweredSale.Id)).Single();
        var lower = CashUpdateFrom(lowered);
        lower.Allocations = new List<AllocationRequest> { new() { EntryId = loweredEntry.Id, Amount = 1_000m } };
        var (lowerStatus, _, lowerError) = await PutCashAsync(_branchClient, lowered.Id, lower);
        lowerStatus.Should().Be(HttpStatusCode.OK, lowerError?.Message);
        await AssertAutoSettlementAsync(loweredSale, 1_000m);
        await AssertCashDebtInvariantsAsync();

        // An edit below the allocation, confirmed: the allocation goes and Auto takes the room.
        var (shrunkSale, shrunk) = await PaidSaleCoveredByReceiptAsync(customer);
        var shrink = CashUpdateFrom(shrunk);
        shrink.Lines[0].Amount = 2_500m;
        shrink.ConfirmRemoveSettlements = true;
        var (shrinkStatus, _, shrinkError) = await PutCashAsync(_branchClient, shrunk.Id, shrink);
        shrinkStatus.Should().Be(HttpStatusCode.OK, shrinkError?.Message);
        await AssertAutoSettlementAsync(shrunkSale, 1_000m);
        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Removing_settlements_through_a_stock_voucher_recomputes_another_auto_settlement()
    {
        var customer = await NewCustomerAsync();

        // A customer return (R− 3,000) matched to the sale; the sale then records 1,000 paid (Auto 0).
        var sale = await CreateSaleAsync(customer, 3, 1_000, paid: 0);
        var customerReturn = await CreateStockAsync(StockDirection.In, "NTL", customer, 3, 1_000, paid: 0);
        await MatchManuallyAsync((await EntriesOfAsync(DebtSourceType.StockOut, sale.Id)).Single().Id,
            (await EntriesOfAsync(DebtSourceType.StockIn, customerReturn.Id)).Single().Id, 3_000m);
        sale = await PayAsync(sale, 1_000);
        await AssertAutoSettlementAsync(sale, 0m);

        // Cancelling the return frees the sale's entry.
        var (status, _, error) = await ActionAsync(customerReturn.Id, "cancel",
            new StockVoucherActionRequest { Version = customerReturn.Version, ConfirmRemoveSettlements = true });
        status.Should().Be(HttpStatusCode.OK, error?.Message);
        await AssertAutoSettlementAsync(sale, 1_000m);
        await AssertCashDebtInvariantsAsync();

        // Another sale's automatic receipt (open 2,000 of an overpayment) matched to a third sale, which records
        // 1,000 paid (Auto 0); shrinking that overpayment below the match frees the third sale's entry.
        var prepaid = await CreateSaleAsync(customer, 3, 1_000, paid: 5_000);
        var third = await CreateSaleAsync(customer, 2, 1_000, paid: 0);
        await MatchManuallyAsync((await EntriesOfAsync(DebtSourceType.StockOut, third.Id)).Single().Id,
            (await EntriesOfAsync(DebtSourceType.Receipt, prepaid.AutoCashVoucherId!.Value)).Single().Id, 2_000m);
        third = await PayAsync(third, 1_000);
        await AssertAutoSettlementAsync(third, 0m);

        var shrink = UpdateRequestFrom(prepaid);
        shrink.PaidAmount = 1_000;
        shrink.ConfirmRemoveSettlements = true;
        var (shrinkStatus, _, shrinkError) = await PutVoucherAsync(_branchClient, prepaid.Id, shrink);
        shrinkStatus.Should().Be(HttpStatusCode.OK, shrinkError?.Message);
        await AssertAutoSettlementAsync(prepaid, 1_000m);
        await AssertAutoSettlementAsync(third, 1_000m);
        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Removing_an_opening_debt_settlement_recomputes_the_auto_settlement()
    {
        var customer = await NewCustomerAsync();

        // A prepayment carried over (R− 3,000) matched to the sale; the sale then records 1,000 paid (Auto 0).
        async Task<(StockVoucherDto Sale, Guid OpeningDebtId)> PrepaidSaleAsync(string docNo)
        {
            var prepayment = await CreateOpeningDebtAsync(_branchClient, customer, DebtSide.Receivable,
                OpeningDebtKind.Prepayment, docNo, new DateOnly(2026, 9, 20), 3_000m);
            var sale = await CreateSaleAsync(customer, 3, 1_000, paid: 0);
            await MatchManuallyAsync((await EntriesOfAsync(DebtSourceType.StockOut, sale.Id)).Single().Id,
                prepayment.EntryId, 3_000m);
            sale = await PayAsync(sale, 1_000);
            await AssertAutoSettlementAsync(sale, 0m);
            return (sale, prepayment.OpeningDebtId);
        }

        var (deletedSale, deletedId) = await PrepaidSaleAsync("TT1");
        var deleted = await _branchClient.DeleteAsync($"/api/opening-debts/{deletedId}?confirmRemoveSettlements=true");
        deleted.StatusCode.Should().Be(HttpStatusCode.OK, await deleted.Content.ReadAsStringAsync());
        await AssertAutoSettlementAsync(deletedSale, 1_000m);
        await AssertCashDebtInvariantsAsync();

        var (shrunkSale, shrunkId) = await PrepaidSaleAsync("TT2");
        var shrunk = await _branchClient.PutAsJsonAsync($"/api/opening-debts/{shrunkId}", new UpsertOpeningDebtRequest
        {
            PartnerId = customer,
            Side = DebtSide.Receivable,
            Kind = OpeningDebtKind.Prepayment,
            DocNo = "TT2",
            DocDate = new DateOnly(2026, 9, 20),
            Amount = 2_500m,
            ConfirmRemoveSettlements = true,
        }, TestJson.Options);
        shrunk.StatusCode.Should().Be(HttpStatusCode.OK, await shrunk.Content.ReadAsStringAsync());
        await AssertAutoSettlementAsync(shrunkSale, 1_000m);
        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Cancelling_a_receipt_leaves_a_locked_auto_settlement_unchanged()
    {
        var customer = await NewCustomerAsync();
        // The sale (10-05) is covered by a receipt dated 10-20, then records 1,000 paid (Auto 0).
        var (sale, receipt) = await PaidSaleCoveredByReceiptAsync(customer, receiptAt: "2026-10-20 10:00");

        // Brainstorm section-02 §5, user decision 2026-10-09: the receipt is in the open period, so cancelling it
        // succeeds; the sale's Auto settlement (10-05) is in the locked period and stays as it is.
        await LockBranchAsync(new DateOnly(2026, 10, 10));
        try
        {
            var (status, cancelled, error) = await CancelCashAsync(_branchClient, receipt.Id,
                new CashDocumentActionRequest { Version = receipt.Version, ConfirmRemoveSettlements = true });
            status.Should().Be(HttpStatusCode.OK, error?.Message);
            cancelled!.Status.Should().Be(DocumentStatus.Cancelled);
            (await SettlementsOfAsync((await EntriesOfAsync(DebtSourceType.StockOut, sale.Id)).Single().Id))
                .Should().BeEmpty("the receipt's allocation is gone and the locked Auto settlement is not added");
            await AssertCashDebtInvariantsAsync();
        }
        finally
        {
            await LockBranchAsync(null);
        }

        await UnlockRecomputedAutoAsync(sale, 1_000m);
    }

    [Fact]
    public async Task Cancelling_a_customer_return_leaves_a_locked_auto_settlement_unchanged()
    {
        var customer = await NewCustomerAsync();
        // A customer return dated 10-20 (R− 3,000) matched to the 10-05 sale, which then records 1,000 paid (Auto 0).
        var sale = await CreateSaleAsync(customer, 3, 1_000, paid: 0);
        var customerReturn = await CreateStockAsync(StockDirection.In, "NTL", customer, 3, 1_000, paid: 0,
            at: "2026-10-20 09:00");
        await MatchManuallyAsync((await EntriesOfAsync(DebtSourceType.StockOut, sale.Id)).Single().Id,
            (await EntriesOfAsync(DebtSourceType.StockIn, customerReturn.Id)).Single().Id, 3_000m);
        sale = await PayAsync(sale, 1_000);
        await AssertAutoSettlementAsync(sale, 0m);

        await LockBranchAsync(new DateOnly(2026, 10, 10));
        try
        {
            var (status, cancelled, error) = await ActionAsync(customerReturn.Id, "cancel",
                new StockVoucherActionRequest { Version = customerReturn.Version, ConfirmRemoveSettlements = true });
            status.Should().Be(HttpStatusCode.OK, error?.Message);
            cancelled!.Status.Should().Be(StockVoucherStatus.Cancelled);
            (await SettlementsOfAsync((await EntriesOfAsync(DebtSourceType.StockOut, sale.Id)).Single().Id))
                .Should().BeEmpty("the return's match is gone and the locked Auto settlement is not added");
            await AssertCashDebtInvariantsAsync();
        }
        finally
        {
            await LockBranchAsync(null);
        }

        await UnlockRecomputedAutoAsync(sale, 1_000m);
    }

    [Fact]
    public async Task Moving_the_lock_earlier_recomputes_only_the_reopened_auto_settlements()
    {
        var customer = await NewCustomerAsync();
        // The sale (10-05, paid 1,000, Auto 0) is covered by a receipt dated 10-20.
        var (sale, receipt) = await PaidSaleCoveredByReceiptAsync(customer, receiptAt: "2026-10-20 10:00");

        await LockBranchAsync(new DateOnly(2026, 10, 10));
        try
        {
            var (status, _, error) = await CancelCashAsync(_branchClient, receipt.Id,
                new CashDocumentActionRequest { Version = receipt.Version, ConfirmRemoveSettlements = true });
            status.Should().Be(HttpStatusCode.OK, error?.Message);

            // 10-05 is still locked: the sale's Auto settlement stays as it is.
            await LockBranchAsync(new DateOnly(2026, 10, 5));
            await AssertAutoSettlementAsync(sale, 0m);
            await AssertCashDebtInvariantsAsync();

            // 10-05 reopens: moving the lock recomputes it, with no other write; the strict invariant holds.
            await LockBranchAsync(new DateOnly(2026, 10, 4));
            await AssertAutoSettlementAsync(sale, 1_000m);
            await AssertCashDebtInvariantsAsync();
        }
        finally
        {
            await LockBranchAsync(null);
        }
    }

    [Fact]
    public async Task Moving_the_lock_later_changes_no_auto_settlement()
    {
        var customer = await NewCustomerAsync();
        var (sale, receipt) = await PaidSaleCoveredByReceiptAsync(customer, receiptAt: "2026-10-20 10:00");

        await LockBranchAsync(new DateOnly(2026, 10, 10));
        try
        {
            var (status, _, error) = await CancelCashAsync(_branchClient, receipt.Id,
                new CashDocumentActionRequest { Version = receipt.Version, ConfirmRemoveSettlements = true });
            status.Should().Be(HttpStatusCode.OK, error?.Message);

            // A later lock reopens nothing: the sale's Auto settlement (10-05) stays as it is.
            await LockBranchAsync(new DateOnly(2026, 10, 15));
            await AssertAutoSettlementAsync(sale, 0m);
            await AssertCashDebtInvariantsAsync();
        }
        finally
        {
            await LockBranchAsync(null);
        }

        await UnlockRecomputedAutoAsync(sale, 1_000m);
    }

    [Fact]
    public async Task Clearing_a_lock_when_there_was_none_is_a_no_op()
    {
        var customer = await NewCustomerAsync();
        var sale = await CreateSaleAsync(customer, 3, 1_000, paid: 1_000);
        await AssertAutoSettlementAsync(sale, 1_000m);
        // An Auto settlement gone behind the service's back: only a recompute would add it again.
        var stockEntry = (await EntriesOfAsync(DebtSourceType.StockOut, sale.Id)).Single();
        await InDbAsync(db => db.DebtSettlements.Where(s => s.IncreaseEntryId == stockEntry.Id).ExecuteDeleteAsync());

        // No previous lock: nothing reopens, so nothing is recomputed.
        await LockBranchAsync(null);
        await AssertAutoSettlementAsync(sale, 0m);

        // A real reopening recomputes it, and the strict invariant holds again.
        await LockBranchAsync(new DateOnly(2026, 10, 10));
        await LockBranchAsync(null);
        await UnlockRecomputedAutoAsync(sale, 1_000m);
    }

    [Theory]
    [InlineData("9999-12-31")]
    [InlineData("9999-01-01")]
    public async Task A_lock_in_year_9999_is_rejected(string date)
    {
        var response = await _client.PutAsJsonAsync($"/api/branches/{_branchId}/lock",
            new SetPeriodLockRequest { LockedUntil = DateOnly.Parse(date, System.Globalization.CultureInfo.InvariantCulture) });
        var (status, _, error) = await ReadDocumentResponseAsync<BranchDto>(response);
        status.Should().Be(HttpStatusCode.BadRequest);
        error!.Details.Should().ContainKey("lockedUntil");
        (await InDbAsync(db => db.Branches.Where(b => b.Id == _branchId).Select(b => b.LockedUntil).SingleAsync()))
            .Should().BeNull("a rejected lock changes nothing");
        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Clearing_a_lock_at_the_maximum_date_recomputes_the_reopened_auto_settlements()
    {
        var customer = await NewCustomerAsync();
        var (sale, receipt) = await PaidSaleCoveredByReceiptAsync(customer, receiptAt: "2026-10-20 10:00");
        await LockBranchAsync(new DateOnly(2026, 10, 10));
        try
        {
            var (status, _, error) = await CancelCashAsync(_branchClient, receipt.Id,
                new CashDocumentActionRequest { Version = receipt.Version, ConfirmRemoveSettlements = true });
            status.Should().Be(HttpStatusCode.OK, error?.Message);

            // A lock stored at DateOnly.MaxValue before the API refused year 9999.
            await InDbAsync(db => db.Branches.Where(b => b.Id == _branchId)
                .ExecuteUpdateAsync(s => s.SetProperty(b => b.LockedUntil, DateOnly.MaxValue)));
            await AssertAutoSettlementAsync(sale, 0m);
        }
        finally
        {
            await LockBranchAsync(null);
        }

        await UnlockRecomputedAutoAsync(sale, 1_000m);
    }

    /// Unlocking reopened the stock voucher's period, so the unlock itself recomputed its Auto settlement
    /// (BranchService.SetLockAsync): the strict invariant holds with no further write.
    private async Task UnlockRecomputedAutoAsync(StockVoucherDto voucher, decimal expectedAuto)
    {
        await AssertAutoSettlementAsync(voucher, expectedAuto);
        await AssertCashDebtInvariantsAsync();
    }

    /// PUT /api/branches/{test branch}/lock; null unlocks.
    private async Task LockBranchAsync(DateOnly? lockedUntil)
    {
        var response = await _client.PutAsJsonAsync($"/api/branches/{_branchId}/lock",
            new SetPeriodLockRequest { LockedUntil = lockedUntil });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    private Task<Guid> NewCustomerAsync() =>
        CreatePartnerAsync("KH-" + Guid.NewGuid().ToString("N")[..8], isCustomer: true, isSupplier: false);

    /// A sale (XBH) of one untracked line at 10-05 09:00 in the test branch; paid in cash (TM, QTM) when `paid` > 0.
    private Task<StockVoucherDto> CreateSaleAsync(Guid customerId, decimal quantity, decimal unitPrice, decimal paid) =>
        CreateStockAsync(StockDirection.Out, "XBH", customerId, quantity, unitPrice, paid);

    /// A voucher of one untracked line at `at` (default 10-05 09:00) in the test branch; paid in cash (TM, QTM) when
    /// `paid` > 0.
    private async Task<StockVoucherDto> CreateStockAsync(StockDirection type, string reasonCode, Guid partnerId,
        decimal quantity, decimal unitPrice, decimal paid, string at = "2026-10-05 09:00")
    {
        var product = await CreateInventoryProductAsync("SP-" + Guid.NewGuid().ToString("N")[..8], track: false);
        var request = VoucherRequest(type, await ReasonIdAsync(reasonCode), at, LineRequest(product, quantity, unitPrice));
        request.WarehouseId = _warehouseId;
        request.PartnerId = partnerId;
        request.PaymentMethodId = paid > 0 ? _cashMethodId : null;
        request.PaidAmount = paid;
        var (status, voucher, error) = await PostVoucherAsync(_branchClient, request);
        status.Should().Be(HttpStatusCode.OK, error?.Message);
        return voucher!;
    }

    /// Edits the stock voucher to `paid` in cash (TM, QTM); asserts 200.
    private async Task<StockVoucherDto> PayAsync(StockVoucherDto voucher, decimal paid)
    {
        var request = UpdateRequestFrom(voucher);
        request.PaymentMethodId = _cashMethodId;
        request.PaidAmount = paid;
        var (status, paidVoucher, error) = await PutVoucherAsync(_branchClient, voucher.Id, request);
        status.Should().Be(HttpStatusCode.OK, error?.Message);
        return paidVoucher!;
    }

    /// XBH 3,000 fully allocated by a manual receipt of 3,000 (AtVoucher) at `receiptAt` (default 10-05 10:00), then
    /// edited to paid 1,000: the automatic receipt finds no room on the sale, so it has no Auto settlement (P20).
    private async Task<(StockVoucherDto Sale, CashVoucherDto Receipt)> PaidSaleCoveredByReceiptAsync(Guid customerId,
        string receiptAt = "2026-10-05 10:00")
    {
        var sale = await CreateSaleAsync(customerId, 3, 1_000, paid: 0);
        var stockEntry = (await EntriesOfAsync(DebtSourceType.StockOut, sale.Id)).Single();
        var receipt = await CreateReceiptAtAsync(customerId, receiptAt, 3_000m, (stockEntry.Id, 3_000m));
        sale = await PayAsync(sale, 1_000);
        await AssertAutoSettlementAsync(sale, 0m);
        await AssertCashDebtInvariantsAsync();
        return (sale, receipt);
    }

    /// The Auto settlement (batch = the stock voucher) between its entry and its automatic voucher's entry.
    private async Task AssertAutoSettlementAsync(StockVoucherDto voucher, decimal expected)
    {
        var stockEntry = (await EntriesOfAsync(voucher.Type == StockDirection.In ? DebtSourceType.StockIn
            : DebtSourceType.StockOut, voucher.Id)).Single();
        var autoEntry = (await EntriesOfAsync(voucher.Type == StockDirection.In ? DebtSourceType.Payment
            : DebtSourceType.Receipt, voucher.AutoCashVoucherId!.Value)).Single();
        var auto = (await SettlementsOfAsync(stockEntry.Id)).Where(s => s.Origin == SettlementOrigin.Auto).ToList();
        if (expected == 0)
        {
            auto.Should().BeEmpty($"{voucher.Code} has no Auto settlement");
            return;
        }

        auto.Should().ContainSingle($"{voucher.Code} has one Auto settlement").Which.Should().BeEquivalentTo(new
        {
            IncreaseEntryId = stockEntry.Direction == DebtDirection.Increase ? stockEntry.Id : autoEntry.Id,
            DecreaseEntryId = stockEntry.Direction == DebtDirection.Increase ? autoEntry.Id : stockEntry.Id,
            Amount = expected,
            BatchId = voucher.Id,
        });
    }

    /// A manual receipt (TKH) of `amount` at 10-05 10:00 into QTM, allocated at voucher time (AtVoucher).
    private Task<CashVoucherDto> CreateReceiptAsync(Guid customerId, decimal amount,
        params (Guid EntryId, decimal Amount)[] allocations) =>
        CreateReceiptAtAsync(customerId, "2026-10-05 10:00", amount, allocations);

    /// A manual receipt (TKH) of `amount` at VN time `at` into QTM, allocated at voucher time (AtVoucher).
    private async Task<CashVoucherDto> CreateReceiptAtAsync(Guid customerId, string at, decimal amount,
        params (Guid EntryId, decimal Amount)[] allocations)
    {
        var request = CashRequest(CashVoucherType.Receipt, await CashReasonIdAsync("TKH"), _cashFundId, at, customerId,
            amount);
        request.Allocations = allocations.Select(a => new AllocationRequest { EntryId = a.EntryId, Amount = a.Amount })
            .ToList();
        return await CreateCashAsync(_branchClient, request);
    }

    /// A Manual settlement (matching screen, Phase 08) straight through the ledger.
    private Task MatchManuallyAsync(Guid increaseEntryId, Guid decreaseEntryId, decimal amount) =>
        InTransactionAsync((sp, ct) => sp.GetRequiredService<IDebtLedgerService>().AddSettlementsAsync(
            new[] { new SettlementDraft(increaseEntryId, decreaseEntryId, amount) }, SettlementOrigin.Manual,
            Guid.NewGuid(), "items", ct));

    /// POST /api/stock-vouchers/{id}/{action} (cancel or restore).
    private async Task<(HttpStatusCode Status, StockVoucherDto? Voucher, Application.Common.Models.ApiError? Error)>
        ActionAsync(Guid id, string action, StockVoucherActionRequest request) =>
        await ReadVoucherResponseAsync(await _branchClient.PostAsJsonAsync($"/api/stock-vouchers/{id}/{action}", request));

    /// DELETE /api/stock-vouchers/{id}; the action request travels in the query string.
    private async Task<(HttpStatusCode Status, Application.Common.Models.ApiError? Error)> DeleteAsync(Guid id,
        uint version, bool confirm)
    {
        var (status, _, error) = await ReadVoucherResponseAsync(await _branchClient.DeleteAsync(
            $"/api/stock-vouchers/{id}?version={version}&confirmRemoveSettlements={confirm}"));
        return (status, error);
    }

    /// Descriptions of the stock voucher's activities of one action, oldest first.
    private Task<List<string>> StockActivitiesAsync(Guid stockVoucherId, StockVoucherActivityAction action) =>
        InDbAsync(db => db.StockVoucherActivities.IgnoreQueryFilters().AsNoTracking()
            .Where(a => a.StockVoucherId == stockVoucherId && a.Action == action)
            .OrderBy(a => a.OccurredAt)
            .Select(a => a.Description)
            .ToListAsync());

    /// Descriptions of a receipt's or payment's activities of one action, oldest first.
    private Task<List<string>> CashActivitiesAsync(Guid documentId, CashDocumentActivityAction action) =>
        InDbAsync(db => db.CashDocumentActivities.AsNoTracking()
            .Where(a => a.DocumentId == documentId && a.Action == action)
            .OrderBy(a => a.OccurredAt)
            .Select(a => a.Description)
            .ToListAsync());
}
