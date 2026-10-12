using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Inventory.StockVouchers.Models;
using OrderMgmt.Domain.Entities.CashDebt;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.CashDebt.StockVoucherDebt;

/// Negative-fund policy on stock voucher saves, cancellations, restores and deletions (P17, D31, P10; save steps 8 and
/// 16): the automatic receipt or payment is what moves the fund.
[Collection(nameof(PostgresCollection))]
public class StockVoucherFundTests : CashDebtTestBase
{
    private const string Warning = "NEGATIVE_FUND_WARNING";
    private const string Blocked = "NEGATIVE_FUND_BLOCKED";

    private Guid _branchId;
    private Guid _warehouseId;
    private Guid _cashFundId;
    private Guid _cashMethodId;
    private HttpClient _branchClient = default!;

    public StockVoucherFundTests(PostgresFixture pg) : base(pg) { }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        // A fresh branch per test: its cash fund QTM starts at 0.
        var code = "SF" + Guid.NewGuid().ToString("N")[..8];
        _branchId = await CreateBranchAsync(code);
        await SetBranchOpeningDateAsync(_branchId, new DateOnly(2026, 9, 1));
        _warehouseId = await CreateWarehouseAsync(_branchId, code);
        _cashFundId = await CashFundIdAsync(_branchId);
        _cashMethodId = await InDbAsync(db => db.PaymentMethods.Where(m => m.Code == "TM").Select(m => m.Id).SingleAsync());
        _branchClient = CloneAdminClient();
        UseBranch(_branchClient, _branchId);
    }

    [Fact]
    public async Task Purchase_paid_from_cash_respects_negative_fund_policy()
    {
        await SetFundOpeningAmountAsync(_cashFundId, 1_000m);
        var supplier = await NewSupplierAsync();

        // The automatic payment of 2,000 takes QTM to −1,000.
        var request = await StockRequestAsync(StockDirection.In, "NMH", supplier, 2, 1_000, paid: 2_000, "2026-10-05 09:00");
        var (status, _, error) = await PostVoucherAsync(_branchClient, request);
        status.Should().Be(HttpStatusCode.UnprocessableEntity);
        error!.Code.Should().Be(Warning);
        error.Details.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new KeyValuePair<string, string[]>("QTM", new[] { "Âm 1.000 tại 05/10/2026 09:00" }));
        (await StockVoucherCountAsync()).Should().Be(0, "a rejected save changes nothing");
        (await CashVoucherCountAsync()).Should().Be(0, "a rejected save changes nothing");

        request.AcknowledgeNegativeFund = true;
        var (ackStatus, purchase, ackError) = await PostVoucherAsync(_branchClient, request);
        ackStatus.Should().Be(HttpStatusCode.OK, ackError?.Message);
        purchase!.AutoCashVoucherType.Should().Be(CashVoucherType.Payment);
        await AssertCashDebtInvariantsAsync();

        // Cancelling gives the money back (no check fails); restoring takes it again and asks again.
        var (cancelStatus, cancelled, cancelError) = await ActionAsync(purchase.Id, "cancel",
            new StockVoucherActionRequest { Version = purchase.Version });
        cancelStatus.Should().Be(HttpStatusCode.OK, cancelError?.Message);
        var restore = new StockVoucherActionRequest { Version = cancelled!.Version };
        var (restoreStatus, _, restoreError) = await ActionAsync(purchase.Id, "restore", restore);
        restoreStatus.Should().Be(HttpStatusCode.UnprocessableEntity);
        restoreError!.Code.Should().Be(Warning);
        restoreError.Details.Should().ContainKey("QTM");
        (await GetVoucherAsync(_branchClient, purchase.Id)).Status.Should().Be(StockVoucherStatus.Cancelled);
        restore.AcknowledgeNegativeFund = true;
        var (restoredStatus, _, restoredError) = await ActionAsync(purchase.Id, "restore", restore);
        restoredStatus.Should().Be(HttpStatusCode.OK, restoredError?.Message);
        await AssertCashDebtInvariantsAsync();

        await UpdateCashDebtSettingsAsync(s => s.NegativeFundPolicy = LimitPolicy.Block);
        try
        {
            // Block cannot be confirmed: the flag changes nothing.
            var more = await StockRequestAsync(StockDirection.In, "NMH", supplier, 1, 500, paid: 500, "2026-10-06 09:00");
            more.AcknowledgeNegativeFund = true;
            var (blockedStatus, _, blockedError) = await PostVoucherAsync(_branchClient, more);
            blockedStatus.Should().Be(HttpStatusCode.UnprocessableEntity);
            blockedError!.Code.Should().Be(Blocked);
            blockedError.Details.Should().ContainKey("QTM");
            (await StockVoucherCountAsync()).Should().Be(1, "a blocked save changes nothing");
            await AssertCashDebtInvariantsAsync();
        }
        finally
        {
            await UpdateCashDebtSettingsAsync(s => s.NegativeFundPolicy = LimitPolicy.Warn);
        }
    }

    [Fact]
    public async Task Cancelling_a_paid_sale_can_overdraw()
    {
        var customer = await NewCustomerAsync();
        var sale = await CreateStockAsync(StockDirection.Out, "XBH", customer, 2, 1_000, paid: 2_000, "2026-10-01 09:00");
        await CreateCashAsync(_branchClient, CashRequest(CashVoucherType.Payment, await CashReasonIdAsync("CK"),
            _cashFundId, "2026-10-02 09:00", null, 1_500m));

        // Without the automatic receipt of 2,000, the payment of 10-02 takes QTM to −1,500.
        var cancel = new StockVoucherActionRequest { Version = sale.Version };
        var (status, _, error) = await ActionAsync(sale.Id, "cancel", cancel);
        status.Should().Be(HttpStatusCode.UnprocessableEntity);
        error!.Code.Should().Be(Warning);
        error.Details.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new KeyValuePair<string, string[]>("QTM", new[] { "Âm 1.500 tại 02/10/2026 09:00" }));
        (await GetVoucherAsync(_branchClient, sale.Id)).Status.Should().Be(StockVoucherStatus.Active,
            "a rejected cancel changes nothing");

        cancel.AcknowledgeNegativeFund = true;
        var (ackStatus, cancelled, ackError) = await ActionAsync(sale.Id, "cancel", cancel);
        ackStatus.Should().Be(HttpStatusCode.OK, ackError?.Message);
        cancelled!.Status.Should().Be(StockVoucherStatus.Cancelled);
        await AssertCashDebtInvariantsAsync();

        // Another paid sale on 10-01 lifts QTM to −500; deleting it takes it back to −1,500, so the delete asks too.
        var other = await CreateStockAsync(StockDirection.Out, "XBH", customer, 1, 1_000, paid: 1_000, "2026-10-01 10:00");
        var (deleteStatus, deleteError) = await DeleteAsync(other.Id, other.Version, acknowledge: false);
        deleteStatus.Should().Be(HttpStatusCode.UnprocessableEntity);
        deleteError!.Code.Should().Be(Warning);
        (await DeleteAsync(other.Id, other.Version, acknowledge: true)).Status.Should().Be(HttpStatusCode.OK);
        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Confirmation_order()
    {
        var customer = await NewCustomerAsync();
        await InDbAsync(async db =>
        {
            db.CreditLimits.Add(new CreditLimit { CustomerId = customer, BranchId = _branchId, Amount = 5_000m });
            await db.SaveChangesAsync();
        });
        // Receivable 1,000 (3,000 − 2,000 paid); QTM +2,000 on 10-01, then −1,500 on 10-02.
        var sale = await CreateStockAsync(StockDirection.Out, "XBH", customer, 3, 1_000, paid: 2_000, "2026-10-01 09:00");
        await CreateCashAsync(_branchClient, CashRequest(CashVoucherType.Payment, await CashReasonIdAsync("CK"),
            _cashFundId, "2026-10-02 09:00", null, 1_500m));

        // One edit: receivable 6,000 > 5,000, and without the automatic receipt QTM goes to −1,500.
        var edit = UpdateRequestFrom(sale);
        edit.Lines[0].Quantity = 6;
        edit.PaidAmount = 0;
        var (creditStatus, _, creditError) = await PutVoucherAsync(_branchClient, sale.Id, edit);
        creditStatus.Should().Be(HttpStatusCode.UnprocessableEntity);
        creditError!.Code.Should().Be("CREDIT_LIMIT_WARNING");

        edit.AcknowledgeCreditLimit = true;
        var (fundStatus, _, fundError) = await PutVoucherAsync(_branchClient, sale.Id, edit);
        fundStatus.Should().Be(HttpStatusCode.UnprocessableEntity);
        fundError!.Code.Should().Be(Warning);
        fundError.Details.Should().ContainKey("QTM");
        (await GetVoucherAsync(_branchClient, sale.Id)).Should().BeEquivalentTo(new { Total = 3_000m, PaidAmount = 2_000m },
            "a rejected save changes nothing");

        edit.AcknowledgeNegativeFund = true;
        var (status, saved, error) = await PutVoucherAsync(_branchClient, sale.Id, edit);
        status.Should().Be(HttpStatusCode.OK, error?.Message);
        saved!.Should().BeEquivalentTo(new { Total = 6_000m, PaidAmount = 0m, AutoCashVoucherId = (Guid?)null });
        await AssertCashDebtInvariantsAsync();
    }

    private Task<Guid> NewCustomerAsync() =>
        CreatePartnerAsync("KH-" + Guid.NewGuid().ToString("N")[..8], isCustomer: true, isSupplier: false);

    private Task<Guid> NewSupplierAsync() =>
        CreatePartnerAsync("NCC-" + Guid.NewGuid().ToString("N")[..8], isCustomer: false, isSupplier: true);

    /// A voucher of one untracked line at VN time `at` in the test branch; paid in cash (TM, QTM) when `paid` > 0.
    private async Task<UpsertStockVoucherRequest> StockRequestAsync(StockDirection type, string reasonCode,
        Guid partnerId, decimal quantity, decimal unitPrice, decimal paid, string at)
    {
        var product = await CreateInventoryProductAsync("SP-" + Guid.NewGuid().ToString("N")[..8], track: false);
        var request = VoucherRequest(type, await ReasonIdAsync(reasonCode), at, LineRequest(product, quantity, unitPrice));
        request.WarehouseId = _warehouseId;
        request.PartnerId = partnerId;
        request.PaymentMethodId = paid > 0 ? _cashMethodId : null;
        request.PaidAmount = paid;
        return request;
    }

    /// Creates the voucher of StockRequestAsync and asserts 200.
    private async Task<StockVoucherDto> CreateStockAsync(StockDirection type, string reasonCode, Guid partnerId,
        decimal quantity, decimal unitPrice, decimal paid, string at)
    {
        var (status, voucher, error) = await PostVoucherAsync(_branchClient,
            await StockRequestAsync(type, reasonCode, partnerId, quantity, unitPrice, paid, at));
        status.Should().Be(HttpStatusCode.OK, error?.Message);
        return voucher!;
    }

    [Fact]
    public async Task Restore_into_a_deactivated_fund_is_rejected()
    {
        // A sale paid into a second, non-default cash fund, then cancelled.
        var qtm2 = new Fund
        {
            BranchId = _branchId, Code = "QTM2", Name = "Quỹ tiền mặt 2", Kind = FundKind.Cash, IsActive = true,
        };
        await InDbAsync(async db =>
        {
            db.Funds.Add(qtm2);
            await db.SaveChangesAsync();
        });
        var request = await StockRequestAsync(StockDirection.Out, "XBH", await NewCustomerAsync(), 1, 1_000, paid: 1_000,
            "2026-10-05 09:00");
        request.FundId = qtm2.Id;
        var (status, sale, error) = await PostVoucherAsync(_branchClient, request);
        status.Should().Be(HttpStatusCode.OK, error?.Message);
        var (_, cancelled, _) = await ActionAsync(sale!.Id, "cancel", new StockVoucherActionRequest { Version = sale.Version });

        // The fund stops taking money: the automatic receipt cannot come back into it.
        await InDbAsync(db => db.Funds.Where(f => f.Id == qtm2.Id).ExecuteUpdateAsync(s => s.SetProperty(f => f.IsActive, false)));
        var restore = new StockVoucherActionRequest { Version = cancelled!.Version };
        var (blockedStatus, _, blockedError) = await ActionAsync(sale.Id, "restore", restore);
        blockedStatus.Should().Be(HttpStatusCode.Conflict, blockedError?.Message);
        (await InDbAsync(db => db.StockVouchers.Where(v => v.Id == sale.Id).Select(v => v.Status).SingleAsync()))
            .Should().Be(StockVoucherStatus.Cancelled);

        await InDbAsync(db => db.Funds.Where(f => f.Id == qtm2.Id).ExecuteUpdateAsync(s => s.SetProperty(f => f.IsActive, true)));
        (await ActionAsync(sale.Id, "restore", restore)).Status.Should().Be(HttpStatusCode.OK);
        await AssertCashDebtInvariantsAsync();
    }

    private Task<int> StockVoucherCountAsync() =>
        InDbAsync(db => db.StockVouchers.CountAsync(v => v.BranchId == _branchId));

    private Task<int> CashVoucherCountAsync() =>
        InDbAsync(db => db.CashVouchers.CountAsync(v => v.BranchId == _branchId));

    /// POST /api/stock-vouchers/{id}/{action} (cancel or restore).
    private async Task<(HttpStatusCode Status, StockVoucherDto? Voucher, Application.Common.Models.ApiError? Error)>
        ActionAsync(Guid id, string action, StockVoucherActionRequest request) =>
        await ReadVoucherResponseAsync(await _branchClient.PostAsJsonAsync($"/api/stock-vouchers/{id}/{action}", request));

    /// DELETE /api/stock-vouchers/{id}; the action request travels in the query string.
    private async Task<(HttpStatusCode Status, Application.Common.Models.ApiError? Error)> DeleteAsync(Guid id,
        uint version, bool acknowledge)
    {
        var (status, _, error) = await ReadVoucherResponseAsync(await _branchClient.DeleteAsync(
            $"/api/stock-vouchers/{id}?version={version}&acknowledgeNegativeFund={acknowledge}"));
        return (status, error);
    }
}
