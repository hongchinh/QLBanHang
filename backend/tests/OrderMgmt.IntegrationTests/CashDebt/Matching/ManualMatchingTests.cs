using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.CashDebt.CashVouchers.Models;
using OrderMgmt.Application.CashDebt.Debt.Models;
using OrderMgmt.Application.CashDebt.Ledger;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Application.Inventory.StockVouchers.Models;
using OrderMgmt.Application.Organization.Branches.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Entities.CashDebt;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.CashDebt.Matching;

/// Manual matching and unmatching on the matching screen (Task 8.2, P22).
[Collection(nameof(PostgresCollection))]
public class ManualMatchingTests : CashDebtTestBase
{
    private Guid _branchId;
    private Guid _cashFundId;
    private HttpClient _branchClient = default!;

    public ManualMatchingTests(PostgresFixture pg) : base(pg) { }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        // A fresh branch per test: the code PT00001 is asserted, and opening debts need the branch's opening date.
        _branchId = await CreateBranchAsync("MM" + Guid.NewGuid().ToString("N")[..8]);
        await SetBranchOpeningDateAsync(_branchId, new DateOnly(2026, 9, 1));
        _cashFundId = await CashFundIdAsync(_branchId);
        _branchClient = CloneAdminClient();
        UseBranch(_branchClient, _branchId);
    }

    [Fact]
    public async Task Match_prepayment_to_invoices_in_one_batch()
    {
        var (customer, receipt, receiptEntry, hd1, hd2) = await PrepaymentAndInvoicesAsync();

        var (status, batch, error) = await PostBatchAsync(_branchClient, customer, DebtSide.Receivable,
            (hd1.EntryId, receiptEntry, 1_000m), (hd2.EntryId, receiptEntry, 2_000m));
        status.Should().Be(HttpStatusCode.OK, error?.Message);
        batch!.Settlements.Should().HaveCount(2);
        batch.Settlements.Should().OnlyContain(s => s.BatchId == batch.BatchId && s.Origin == SettlementOrigin.Manual);

        var open = await OpenItemsAsync(customer);
        open.Should().ContainSingle("the receipt and HD1 are fully matched").Which.Should().BeEquivalentTo(new
        {
            EntryId = hd2.EntryId,
            DocCode = "HD2",
            Settled = 2_000m,
            Open = 500m,
        });

        var listed = await ListSettlementsAsync(_branchClient, customer, DebtSide.Receivable);
        listed.Should().HaveCount(2);
        listed.Should().OnlyContain(s => s.BatchId == batch.BatchId);
        listed.Single(s => s.IncreaseEntryId == hd1.EntryId).Should().BeEquivalentTo(new
        {
            Origin = SettlementOrigin.Manual,
            IncreaseDocCode = "HD1",
            IncreaseDocDate = new DateOnly(2026, 8, 20),
            IncreaseSourceType = DebtSourceType.Opening,
            IncreaseSourceId = hd1.OpeningDebtId,
            DecreaseEntryId = receiptEntry,
            DecreaseDocCode = "PT00001",
            DecreaseDocDate = new DateOnly(2026, 10, 5),
            DecreaseSourceType = DebtSourceType.Receipt,
            DecreaseSourceId = receipt.Id,
            Amount = 1_000m,
            EffectiveAt = Vn("2026-10-05 09:00"),
            CreatedByName = await AdminFullNameAsync(),
        });

        // The payable side of the partner has no settlements.
        (await ListSettlementsAsync(_branchClient, customer, DebtSide.Payable)).Should().BeEmpty();
        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Validation()
    {
        var (customer, _, receiptEntry, hd1, _) = await PrepaymentAndInvoicesAsync();

        // Above the open amount of HD1 (1,000).
        var (overStatus, _, overError) = await PostBatchAsync(_branchClient, customer, DebtSide.Receivable,
            (hd1.EntryId, receiptEntry, 1_500m));
        overStatus.Should().Be(HttpStatusCode.BadRequest);
        overError!.Details.Should().ContainKey("items[0].amount");

        // An entry of another partner.
        var other = await NewCustomerAsync();
        var otherDebt = await CreateOpeningDebtAsync(_branchClient, other, DebtSide.Receivable, OpeningDebtKind.Debt,
            "HDX", new DateOnly(2026, 8, 20), 1_000m);
        var (foreignStatus, _, foreignError) = await PostBatchAsync(_branchClient, customer, DebtSide.Receivable,
            (otherDebt.EntryId, receiptEntry, 500m));
        foreignStatus.Should().Be(HttpStatusCode.BadRequest);
        foreignError!.Details.Should().ContainKey("items[0].increaseEntryId");

        // An entry of the other side of the same partner is refused too.
        var (sideStatus, _, sideError) = await PostBatchAsync(_branchClient, customer, DebtSide.Payable,
            (hd1.EntryId, receiptEntry, 500m));
        sideStatus.Should().Be(HttpStatusCode.BadRequest);
        sideError!.Details.Should().ContainKeys("items[0].increaseEntryId", "items[0].decreaseEntryId");

        // The settlement would be dated in a locked period (EffectiveAt 10-05).
        await LockBranchAsync(new DateOnly(2026, 10, 5));
        try
        {
            var (lockedStatus, _, lockedError) = await PostBatchAsync(_branchClient, customer, DebtSide.Receivable,
                (hd1.EntryId, receiptEntry, 500m));
            lockedStatus.Should().Be(HttpStatusCode.BadRequest);
            lockedError!.Code.Should().Be("PERIOD_LOCKED");
        }
        finally
        {
            await LockBranchAsync(null);
        }

        // SALES has no debt.matching.
        var sales = await CreateClientForRoleAsync("mm_sales_" + Guid.NewGuid().ToString("N")[..8], RoleCodes.Sales,
            _branchId);
        var (salesStatus, _, _) = await PostBatchAsync(sales, customer, DebtSide.Receivable,
            (hd1.EntryId, receiptEntry, 500m));
        salesStatus.Should().Be(HttpStatusCode.Forbidden);
        (await sales.GetAsync($"/api/debt/settlements?partnerId={customer}&side=Receivable")).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);

        (await SettlementsOfAsync(receiptEntry)).Should().BeEmpty("no refused request matched anything");
        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Unmatch_single_and_batch_and_log_activity()
    {
        var (customer, receipt, receiptEntry, hd1, hd2) = await PrepaymentAndInvoicesAsync();
        var (status, batch, error) = await PostBatchAsync(_branchClient, customer, DebtSide.Receivable,
            (hd1.EntryId, receiptEntry, 1_000m), (hd2.EntryId, receiptEntry, 2_000m));
        status.Should().Be(HttpStatusCode.OK, error?.Message);

        var first = batch!.Settlements.Single(s => s.IncreaseEntryId == hd1.EntryId);
        (await _branchClient.DeleteAsync($"/api/debt/settlements/{first.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);

        var open = await OpenItemsAsync(customer);
        open.Single(i => i.EntryId == hd1.EntryId).Open.Should().Be(1_000m, "HD1 is open again");
        open.Single(i => i.EntryId == receiptEntry).Open.Should().Be(1_000m, "the receipt has 1,000 left");
        var activities = await ReadDataAsync<List<CashDocumentActivityDto>>(
            await _branchClient.GetAsync($"/api/cash-vouchers/{receipt.Id}/activities"));
        activities.Where(a => a.Action == CashDocumentActivityAction.SettlementRemoved).Select(a => a.Description)
            .Should().Equal(DebtActivityLog.SettlementRemovedText(1_000m, "HD1"));

        // An unknown settlement.
        (await _branchClient.DeleteAsync($"/api/debt/settlements/{Guid.NewGuid()}")).StatusCode
            .Should().Be(HttpStatusCode.NotFound);

        (await _branchClient.DeleteAsync($"/api/debt/settlement-batches/{batch.BatchId}")).StatusCode
            .Should().Be(HttpStatusCode.OK);
        (await SettlementsOfAsync(receiptEntry)).Should().BeEmpty();
        (await ListSettlementsAsync(_branchClient, customer, DebtSide.Receivable)).Should().BeEmpty();
        (await OpenItemsAsync(customer)).Sum(i => i.Open).Should().Be(1_000m + 2_500m + 3_000m);

        // The batch is gone.
        (await _branchClient.DeleteAsync($"/api/debt/settlement-batches/{batch.BatchId}")).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Matching_changes_the_version_of_the_cash_vouchers_it_touches()
    {
        var customer = await NewCustomerAsync();
        var hd1 = await CreateOpeningDebtAsync(_branchClient, customer, DebtSide.Receivable, OpeningDebtKind.Debt, "HD1",
            new DateOnly(2026, 8, 20), 1_000m);
        var hd2 = await CreateOpeningDebtAsync(_branchClient, customer, DebtSide.Receivable, OpeningDebtKind.Debt, "HD2",
            new DateOnly(2026, 8, 20), 500m);
        var request = CashRequest(CashVoucherType.Receipt, await CashReasonIdAsync("TKH"), _cashFundId, "2026-10-05 09:00",
            customer, 2_000m);
        request.Allocations = new List<AllocationRequest> { new() { EntryId = hd1.EntryId, Amount = 1_000m } };
        var stale = await CreateCashAsync(_branchClient, request);
        var receiptEntry = (await EntriesOfAsync(DebtSourceType.Receipt, stale.Id)).Single().Id;

        // Another user unmatches the receipt's allocation; a form opened before must not bring it back silently.
        var allocation = stale.Allocations.Single();
        (await _branchClient.DeleteAsync($"/api/debt/settlements/{allocation.SettlementId}")).StatusCode
            .Should().Be(HttpStatusCode.OK);
        var resend = CashUpdateFrom(stale);
        resend.Note = "Sửa ghi chú";
        resend.Allocations = new List<AllocationRequest> { new() { EntryId = hd1.EntryId, Amount = 1_000m } };
        var (status, _, error) = await PutCashAsync(_branchClient, stale.Id, resend);
        status.Should().Be(HttpStatusCode.Conflict, error?.Message);
        error!.Code.Should().Be("CONCURRENCY");
        (await SettlementsOfAsync(hd1.EntryId)).Should().BeEmpty();

        // A manual match on the receipt changes its version too.
        var (_, beforeMatch, _) = await GetCashAsync(_branchClient, stale.Id);
        (await PostBatchAsync(_branchClient, customer, DebtSide.Receivable, (hd2.EntryId, receiptEntry, 500m)))
            .Status.Should().Be(HttpStatusCode.OK);
        var (_, afterMatch, _) = await GetCashAsync(_branchClient, stale.Id);
        afterMatch!.Version.Should().NotBe(beforeMatch!.Version);
        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Managed_origins_cannot_be_unmatched()
    {
        var customer = await NewCustomerAsync();
        var sale = await CreateSaleAsync(customer, paid: 1_000m);
        var stockEntry = (await EntriesOfAsync(DebtSourceType.StockOut, sale.Id)).Single();
        var auto = (await SettlementsOfAsync(stockEntry.Id)).Should().ContainSingle().Subject;
        auto.Origin.Should().Be(SettlementOrigin.Auto);

        var single = await _branchClient.DeleteAsync($"/api/debt/settlements/{auto.Id}");
        single.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReadDocumentResponseAsync<object>(single)).Error!.Code.Should().Be("SETTLEMENT_MANAGED");
        var wholeBatch = await _branchClient.DeleteAsync($"/api/debt/settlement-batches/{auto.BatchId}");
        wholeBatch.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReadDocumentResponseAsync<object>(wholeBatch)).Error!.Code.Should().Be("SETTLEMENT_MANAGED");
        (await SettlementsOfAsync(stockEntry.Id)).Should().ContainSingle("nothing was removed");

        // An allocation made on the receipt (AtVoucher) can be removed here.
        var hd1 = await CreateOpeningDebtAsync(_branchClient, customer, DebtSide.Receivable, OpeningDebtKind.Debt, "HD1",
            new DateOnly(2026, 8, 20), 1_000m);
        var request = CashRequest(CashVoucherType.Receipt, await CashReasonIdAsync("TKH"), _cashFundId,
            "2026-10-05 10:00", customer, 1_000m);
        request.Allocations = new List<AllocationRequest> { new() { EntryId = hd1.EntryId, Amount = 1_000m } };
        await CreateCashAsync(_branchClient, request);
        var atVoucher = (await SettlementsOfAsync(hd1.EntryId)).Should().ContainSingle().Subject;
        atVoucher.Origin.Should().Be(SettlementOrigin.AtVoucher);

        (await _branchClient.DeleteAsync($"/api/debt/settlements/{atVoucher.Id}")).StatusCode
            .Should().Be(HttpStatusCode.OK);
        (await SettlementsOfAsync(hd1.EntryId)).Should().BeEmpty();
        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Unmatching_recomputes_the_auto_settlement()
    {
        var customer = await NewCustomerAsync();
        var sale = await CreateSaleAsync(customer, paid: 0m);
        var stockEntry = (await EntriesOfAsync(DebtSourceType.StockOut, sale.Id)).Single();
        var receipt = await CreateCashAsync(_branchClient, CashRequest(CashVoucherType.Receipt,
            await CashReasonIdAsync("TKH"), _cashFundId, "2026-10-05 10:00", customer, 3_000m));
        var receiptEntry = (await EntriesOfAsync(DebtSourceType.Receipt, receipt.Id)).Single();
        var (status, batch, error) = await PostBatchAsync(_branchClient, customer, DebtSide.Receivable,
            (stockEntry.Id, receiptEntry.Id, 3_000m));
        status.Should().Be(HttpStatusCode.OK, error?.Message);

        // Paid 1,000: the sale is fully matched, so its automatic receipt gets no Auto settlement (P20).
        var edit = UpdateRequestFrom(sale);
        edit.PaymentMethodId = await CashMethodIdAsync();
        edit.PaidAmount = 1_000m;
        var (editStatus, paid, editError) = await PutVoucherAsync(_branchClient, sale.Id, edit);
        editStatus.Should().Be(HttpStatusCode.OK, editError?.Message);
        (await AutoSettlementsAsync(stockEntry.Id)).Should().BeEmpty();

        (await _branchClient.DeleteAsync($"/api/debt/settlements/{batch!.Settlements.Single().Id}")).StatusCode
            .Should().Be(HttpStatusCode.OK);

        var autoEntry = (await EntriesOfAsync(DebtSourceType.Receipt, paid!.AutoCashVoucherId!.Value)).Single();
        (await AutoSettlementsAsync(stockEntry.Id)).Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            IncreaseEntryId = stockEntry.Id,
            DecreaseEntryId = autoEntry.Id,
            Amount = 1_000m,
            BatchId = sale.Id,
        });
        await AssertCashDebtInvariantsAsync();
    }

    // ---- Helpers ------------------------------------------------------------------------------

    /// A customer with a receipt (TKH) of 3,000 at 10-05 09:00 without allocation (PT00001) and opening debts
    /// HD1 1,000 and HD2 2,500 dated 08-20.
    private async Task<(Guid Customer, CashVoucherDto Receipt, Guid ReceiptEntry, (Guid OpeningDebtId, Guid EntryId) Hd1,
        (Guid OpeningDebtId, Guid EntryId) Hd2)> PrepaymentAndInvoicesAsync()
    {
        var customer = await NewCustomerAsync();
        var receipt = await CreateCashAsync(_branchClient, CashRequest(CashVoucherType.Receipt,
            await CashReasonIdAsync("TKH"), _cashFundId, "2026-10-05 09:00", customer, 3_000m));
        receipt.Code.Should().Be("PT00001");
        var receiptEntry = (await EntriesOfAsync(DebtSourceType.Receipt, receipt.Id)).Single().Id;
        var hd1 = await CreateOpeningDebtAsync(_branchClient, customer, DebtSide.Receivable, OpeningDebtKind.Debt, "HD1",
            new DateOnly(2026, 8, 20), 1_000m);
        var hd2 = await CreateOpeningDebtAsync(_branchClient, customer, DebtSide.Receivable, OpeningDebtKind.Debt, "HD2",
            new DateOnly(2026, 8, 20), 2_500m);
        return (customer, receipt, receiptEntry, hd1, hd2);
    }

    /// A sale (XBH) of 3,000 (one untracked line) at 10-05 09:00 in the test branch; paid in cash (TM, QTM) when
    /// `paid` > 0.
    private async Task<StockVoucherDto> CreateSaleAsync(Guid customerId, decimal paid)
    {
        var product = await CreateInventoryProductAsync("SP-" + Guid.NewGuid().ToString("N")[..8], track: false);
        var request = VoucherRequest(StockDirection.Out, await ReasonIdAsync("XBH"), "2026-10-05 09:00",
            LineRequest(product, 3, 1_000m));
        request.WarehouseId = await CreateWarehouseAsync(_branchId, "K" + Guid.NewGuid().ToString("N")[..8]);
        request.PartnerId = customerId;
        request.PaymentMethodId = paid > 0 ? await CashMethodIdAsync() : null;
        request.PaidAmount = paid;
        var (status, voucher, error) = await PostVoucherAsync(_branchClient, request);
        status.Should().Be(HttpStatusCode.OK, error?.Message);
        return voucher!;
    }

    private Task<Guid> CashMethodIdAsync() =>
        InDbAsync(db => db.PaymentMethods.Where(m => m.Code == "TM").Select(m => m.Id).SingleAsync());

    private Task<Guid> NewCustomerAsync() =>
        CreatePartnerAsync("KH-" + Guid.NewGuid().ToString("N")[..8], isCustomer: true, isSupplier: false);

    private Task<string> AdminFullNameAsync() =>
        InDbAsync(db => db.Users.Where(u => u.Username == "admin").Select(u => u.FullName).SingleAsync());

    private async Task<List<DebtSettlement>> AutoSettlementsAsync(Guid entryId) =>
        (await SettlementsOfAsync(entryId)).Where(s => s.Origin == SettlementOrigin.Auto).ToList();

    /// PUT /api/branches/{test branch}/lock; null unlocks.
    private async Task LockBranchAsync(DateOnly? lockedUntil)
    {
        var response = await _client.PutAsJsonAsync($"/api/branches/{_branchId}/lock",
            new SetPeriodLockRequest { LockedUntil = lockedUntil });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    private static async Task<(HttpStatusCode Status, SettlementBatchDto? Batch, ApiError? Error)> PostBatchAsync(
        HttpClient client, Guid partnerId, DebtSide side, params (Guid Increase, Guid Decrease, decimal Amount)[] items) =>
        await ReadDocumentResponseAsync<SettlementBatchDto>(await client.PostAsJsonAsync("/api/debt/settlements",
            new CreateSettlementBatchRequest
            {
                PartnerId = partnerId,
                Side = side,
                Items = items.Select(i => new SettlementItemRequest
                {
                    IncreaseEntryId = i.Increase,
                    DecreaseEntryId = i.Decrease,
                    Amount = i.Amount,
                }).ToList(),
            }, TestJson.Options));

    private static async Task<List<SettlementDto>> ListSettlementsAsync(HttpClient client, Guid partnerId, DebtSide side) =>
        await ReadDataAsync<List<SettlementDto>>(
            await client.GetAsync($"/api/debt/settlements?partnerId={partnerId}&side={side}"));

    private async Task<List<OpenItemDto>> OpenItemsAsync(Guid partnerId) =>
        await ReadDataAsync<List<OpenItemDto>>(
            await _branchClient.GetAsync($"/api/debt/open-items?partnerId={partnerId}&side=Receivable"));
}
