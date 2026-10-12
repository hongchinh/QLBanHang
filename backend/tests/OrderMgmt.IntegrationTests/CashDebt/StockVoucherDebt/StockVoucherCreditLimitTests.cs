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

/// Credit-limit policy on stock voucher saves and restores (C12, P10; save steps 7 and 15).
[Collection(nameof(PostgresCollection))]
public class StockVoucherCreditLimitTests : CashDebtTestBase
{
    private const string Warning = "CREDIT_LIMIT_WARNING";
    private const string Blocked = "CREDIT_LIMIT_BLOCKED";

    private Guid _branchId;
    private Guid _warehouseId;
    private Guid _cashMethodId;
    private HttpClient _branchClient = default!;

    public StockVoucherCreditLimitTests(PostgresFixture pg) : base(pg) { }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        // A fresh branch per test: limits are per customer x branch. Opening debts need its opening date.
        var code = "CL" + Guid.NewGuid().ToString("N")[..8];
        _branchId = await CreateBranchAsync(code);
        await SetBranchOpeningDateAsync(_branchId, new DateOnly(2026, 10, 1));
        _warehouseId = await CreateWarehouseAsync(_branchId, code);
        // A paid sale brings money into QTM, so the negative-fund check never interferes here.
        _cashMethodId = await InDbAsync(db => db.PaymentMethods.Where(m => m.Code == "TM").Select(m => m.Id).SingleAsync());
        _branchClient = CloneAdminClient();
        UseBranch(_branchClient, _branchId);
    }

    [Fact]
    public async Task Warn_on_sale_over_limit_then_acknowledge()
    {
        var customer = await NewCustomerAsync();
        await SetLimitAsync(customer, _branchId, 5_000m);
        await CreateOpeningDebtAsync(_branchClient, customer, DebtSide.Receivable, OpeningDebtKind.Debt, "HD1",
            new DateOnly(2026, 9, 20), 3_000m);

        // 3,000 + 3,000 = 6,000 > 5,000.
        var sale = await SaleRequestAsync(customer, 3, 1_000, paid: 0);
        var (status, _, error) = await PostVoucherAsync(_branchClient, sale);
        status.Should().Be(HttpStatusCode.UnprocessableEntity);
        error!.Code.Should().Be(Warning);
        error.Details.Should().ContainKey("creditLimit");
        (await VoucherCountAsync()).Should().Be(0, "a rejected save changes nothing");
        (await ReceivableAsync(customer)).Should().Be(3_000m);

        sale.AcknowledgeCreditLimit = true;
        var (ackStatus, _, ackError) = await PostVoucherAsync(_branchClient, sale);
        ackStatus.Should().Be(HttpStatusCode.OK, ackError?.Message);
        (await ReceivableAsync(customer)).Should().Be(6_000m);
        await AssertCashDebtInvariantsAsync();

        // The same sale paid 1,000 for another customer in the same position: after = 5,000 is not above the limit (C12).
        var other = await NewCustomerAsync();
        await SetLimitAsync(other, _branchId, 5_000m);
        await CreateOpeningDebtAsync(_branchClient, other, DebtSide.Receivable, OpeningDebtKind.Debt, "HD2",
            new DateOnly(2026, 9, 20), 3_000m);
        var (paidStatus, _, paidError) = await PostVoucherAsync(_branchClient, await SaleRequestAsync(other, 3, 1_000, paid: 1_000));
        paidStatus.Should().Be(HttpStatusCode.OK, paidError?.Message);
        (await ReceivableAsync(other)).Should().Be(5_000m);
        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Block_policy_and_lowering_edit()
    {
        var customer = await NewCustomerAsync();
        await SetLimitAsync(customer, _branchId, 5_000m);
        // 6,000 > 5,000, accepted under Warn.
        var request = await SaleRequestAsync(customer, 1, 6_000, paid: 0);
        request.AcknowledgeCreditLimit = true;
        var (createdStatus, sale, createdError) = await PostVoucherAsync(_branchClient, request);
        createdStatus.Should().Be(HttpStatusCode.OK, createdError?.Message);

        await UpdateCashDebtSettingsAsync(s => s.CreditLimitPolicy = LimitPolicy.Block);
        try
        {
            // Block cannot be confirmed: the flag changes nothing.
            var more = await SaleRequestAsync(customer, 1, 500, paid: 0);
            more.AcknowledgeCreditLimit = true;
            var (status, _, error) = await PostVoucherAsync(_branchClient, more);
            status.Should().Be(HttpStatusCode.UnprocessableEntity);
            error!.Code.Should().Be(Blocked);
            (await VoucherCountAsync()).Should().Be(1, "a blocked save changes nothing");

            // Raising the voucher already over the limit is blocked too.
            var raise = UpdateRequestFrom(sale!);
            raise.Lines[0].UnitPrice = 6_500;
            var (raiseStatus, _, raiseError) = await PutVoucherAsync(_branchClient, sale!.Id, raise);
            raiseStatus.Should().Be(HttpStatusCode.UnprocessableEntity);
            raiseError!.Code.Should().Be(Blocked);

            // Lowering it is not: after 5,500 is still over 5,000 but below the 6,000 before (C12).
            var lower = UpdateRequestFrom(sale);
            lower.Lines[0].UnitPrice = 5_500;
            var (lowerStatus, lowered, lowerError) = await PutVoucherAsync(_branchClient, sale.Id, lower);
            lowerStatus.Should().Be(HttpStatusCode.OK, lowerError?.Message);
            lowered!.Total.Should().Be(5_500m);
            (await ReceivableAsync(customer)).Should().Be(5_500m);
            await AssertCashDebtInvariantsAsync();
        }
        finally
        {
            await UpdateCashDebtSettingsAsync(s => s.CreditLimitPolicy = LimitPolicy.Warn);
        }
    }

    [Fact]
    public async Task Restore_checks_limit()
    {
        var customer = await NewCustomerAsync();
        await SetLimitAsync(customer, _branchId, 5_000m);
        var (firstStatus, first, firstError) = await PostVoucherAsync(_branchClient, await SaleRequestAsync(customer, 3, 1_000, paid: 0));
        firstStatus.Should().Be(HttpStatusCode.OK, firstError?.Message);
        var (cancelStatus, cancelled, cancelError) = await ActionAsync(first!.Id, "cancel",
            new StockVoucherActionRequest { Version = first.Version });
        cancelStatus.Should().Be(HttpStatusCode.OK, cancelError?.Message);

        // A new sale up to the limit (5,000 = limit) passes.
        var (secondStatus, _, secondError) = await PostVoucherAsync(_branchClient, await SaleRequestAsync(customer, 5, 1_000, paid: 0));
        secondStatus.Should().Be(HttpStatusCode.OK, secondError?.Message);

        // Restoring the first brings the balance to 8,000.
        var restore = new StockVoucherActionRequest { Version = cancelled!.Version };
        var (status, _, error) = await ActionAsync(first.Id, "restore", restore);
        status.Should().Be(HttpStatusCode.UnprocessableEntity);
        error!.Code.Should().Be(Warning);
        (await GetVoucherAsync(_branchClient, first.Id)).Status.Should().Be(StockVoucherStatus.Cancelled,
            "a rejected restore changes nothing");
        (await ReceivableAsync(customer)).Should().Be(5_000m);

        restore.AcknowledgeCreditLimit = true;
        var (ackStatus, restored, ackError) = await ActionAsync(first.Id, "restore", restore);
        ackStatus.Should().Be(HttpStatusCode.OK, ackError?.Message);
        restored!.Status.Should().Be(StockVoucherStatus.Active);
        (await ReceivableAsync(customer)).Should().Be(8_000m);
        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Other_branch_limit_does_not_apply()
    {
        var customer = await NewCustomerAsync();
        var otherBranch = await CreateBranchAsync("CO" + Guid.NewGuid().ToString("N")[..8]);
        await SetLimitAsync(customer, otherBranch, 1_000m);

        // The limit is per customer x branch: the test branch has none.
        var (status, _, error) = await PostVoucherAsync(_branchClient, await SaleRequestAsync(customer, 3, 1_000, paid: 0));
        status.Should().Be(HttpStatusCode.OK, error?.Message);
        (await ReceivableAsync(customer)).Should().Be(3_000m);
        await AssertCashDebtInvariantsAsync();
    }

    private Task<Guid> NewCustomerAsync() =>
        CreatePartnerAsync("KH-" + Guid.NewGuid().ToString("N")[..8], isCustomer: true, isSupplier: false);

    private Task SetLimitAsync(Guid customerId, Guid branchId, decimal amount) => InDbAsync(async db =>
    {
        db.CreditLimits.Add(new CreditLimit { CustomerId = customerId, BranchId = branchId, Amount = amount });
        await db.SaveChangesAsync();
    });

    /// A sale (XBH) of one untracked line at 10-05 09:00 in the test branch; paid in cash (TM, QTM) when `paid` > 0.
    private async Task<UpsertStockVoucherRequest> SaleRequestAsync(Guid customerId, decimal quantity, decimal unitPrice,
        decimal paid)
    {
        var product = await CreateInventoryProductAsync("SP-" + Guid.NewGuid().ToString("N")[..8], track: false);
        var request = VoucherRequest(StockDirection.Out, await ReasonIdAsync("XBH"), "2026-10-05 09:00",
            LineRequest(product, quantity, unitPrice));
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

    private Task<int> VoucherCountAsync() =>
        InDbAsync(db => db.StockVouchers.CountAsync(v => v.BranchId == _branchId));

    /// POST /api/stock-vouchers/{id}/{action} (cancel or restore).
    private async Task<(HttpStatusCode Status, StockVoucherDto? Voucher, Application.Common.Models.ApiError? Error)>
        ActionAsync(Guid id, string action, StockVoucherActionRequest request) =>
        await ReadVoucherResponseAsync(await _branchClient.PostAsJsonAsync($"/api/stock-vouchers/{id}/{action}", request));
}
