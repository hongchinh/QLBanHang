using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderMgmt.Application.CashDebt.CashVouchers.Models;
using OrderMgmt.Application.CashDebt.Ledger;
using OrderMgmt.Application.Inventory.StockVouchers.Models;
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

    private Task<Guid> NewCustomerAsync() =>
        CreatePartnerAsync("KH-" + Guid.NewGuid().ToString("N")[..8], isCustomer: true, isSupplier: false);

    /// A sale (XBH) of one untracked line at 10-05 09:00 in the test branch; paid in cash (TM, QTM) when `paid` > 0.
    private async Task<StockVoucherDto> CreateSaleAsync(Guid customerId, decimal quantity, decimal unitPrice, decimal paid)
    {
        var product = await CreateInventoryProductAsync("SP-" + Guid.NewGuid().ToString("N")[..8], track: false);
        var request = VoucherRequest(StockDirection.Out, await ReasonIdAsync("XBH"), "2026-10-05 09:00",
            LineRequest(product, quantity, unitPrice));
        request.WarehouseId = _warehouseId;
        request.PartnerId = customerId;
        request.PaymentMethodId = paid > 0 ? _cashMethodId : null;
        request.PaidAmount = paid;
        var (status, voucher, error) = await PostVoucherAsync(_branchClient, request);
        status.Should().Be(HttpStatusCode.OK, error?.Message);
        return voucher!;
    }

    /// A manual receipt (TKH) of `amount` at 10-05 10:00 into QTM, allocated at voucher time (AtVoucher).
    private async Task<CashVoucherDto> CreateReceiptAsync(Guid customerId, decimal amount,
        params (Guid EntryId, decimal Amount)[] allocations)
    {
        var request = CashRequest(CashVoucherType.Receipt, await CashReasonIdAsync("TKH"), _cashFundId,
            "2026-10-05 10:00", customerId, amount);
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
