using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderMgmt.Application.CashDebt.CashVouchers.Models;
using OrderMgmt.Application.CashDebt.Debt.Models;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Inventory.Common;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Entities.CashDebt;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.CashDebt.StockVoucherDebt;

/// GET /api/debt/partner-summary: the debt panel of the stock-out form (P6).
[Collection(nameof(PostgresCollection))]
public class PartnerSummaryTests : CashDebtTestBase
{
    private Guid _branchId;
    private Guid _warehouseId;
    private HttpClient _branchClient = default!;
    private DateOnly _today;

    public PartnerSummaryTests(PostgresFixture pg) : base(pg) { }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        // "Today" as the service sees it: the VN date of the server clock, so due dates stay relative to the run date.
        _today = VnTime.ToVnDate(_factory.Services.GetRequiredService<IDateTime>().UtcNow);
        var code = "PS" + Guid.NewGuid().ToString("N")[..8];
        _branchId = await CreateBranchAsync(code);
        await SetBranchOpeningDateAsync(_branchId, _today.AddDays(-30));
        _warehouseId = await CreateWarehouseAsync(_branchId, code);
        _branchClient = CloneAdminClient();
        UseBranch(_branchClient, _branchId);
    }

    [Fact]
    public async Task Summary_shows_balance_limit_and_overdue()
    {
        var customer = await CreatePartnerAsync(NewCode("KH"), isCustomer: true, isSupplier: false);
        await InDbAsync(db => db.Customers.Where(c => c.Id == customer)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.CreditDays, 15)));

        // Opening debt 5,000 due 7 days ago.
        var opening = await CreateOpeningDebtAsync(_branchClient, customer, DebtSide.Receivable, OpeningDebtKind.Debt,
            "HD1", _today.AddDays(-40), 5_000m, dueDate: _today.AddDays(-7));

        // XBH 3,000 due in 10 days (unpaid).
        var yesterday = $"{_today.AddDays(-1):yyyy-MM-dd} 09:00";
        var product = await CreateInventoryProductAsync(NewCode("SP"), track: false);
        var sale = VoucherRequest(StockDirection.Out, await ReasonIdAsync("XBH"), yesterday,
            LineRequest(product, 3, 1_000));
        sale.WarehouseId = _warehouseId;
        sale.PartnerId = customer;
        sale.PaidAmount = 0;
        sale.DueDate = _today.AddDays(10);
        var (saleStatus, _, saleError) = await PostVoucherAsync(_branchClient, sale);
        saleStatus.Should().Be(HttpStatusCode.OK, saleError?.Message);

        // A receipt 1,000 allocated to the opening debt.
        var receipt = CashRequest(CashVoucherType.Receipt, await CashReasonIdAsync("TKH"), await CashFundIdAsync(_branchId),
            yesterday, customer, 1_000m);
        receipt.Allocations = new List<AllocationRequest> { new() { EntryId = opening.EntryId, Amount = 1_000m } };
        await CreateCashAsync(_branchClient, receipt);

        // No limit yet: CreditLimit and Available are null.
        var noLimit = await GetSummaryAsync(_branchClient, customer);
        noLimit.PartnerId.Should().Be(customer);
        noLimit.ReceivableBalance.Should().Be(7_000m);
        noLimit.OverdueAmount.Should().Be(4_000m, "only the open part of the opening debt is past due");
        noLimit.CreditLimit.Should().BeNull();
        noLimit.Available.Should().BeNull();
        noLimit.CreditDays.Should().Be(15);

        await InDbAsync(async db =>
        {
            db.CreditLimits.Add(new CreditLimit { CustomerId = customer, BranchId = _branchId, Amount = 10_000m });
            await db.SaveChangesAsync();
        });
        var summary = await GetSummaryAsync(_branchClient, customer);
        summary.ReceivableBalance.Should().Be(7_000m);
        summary.OverdueAmount.Should().Be(4_000m);
        summary.CreditLimit.Should().Be(10_000m);
        summary.Available.Should().Be(3_000m);

        // The summary is per working branch: another branch sees none of it.
        var other = CloneAdminClient();
        UseBranch(other, MainBranchId);
        var elsewhere = await GetSummaryAsync(other, customer);
        elsewhere.ReceivableBalance.Should().Be(0m);
        elsewhere.OverdueAmount.Should().Be(0m);
        elsewhere.CreditLimit.Should().BeNull();

        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Summary_shows_payable_balance_next_to_receivable()
    {
        // A partner that is both customer and supplier.
        var partner = await CreatePartnerAsync(NewCode("DT"), isCustomer: true, isSupplier: true);
        var yesterday = $"{_today.AddDays(-1):yyyy-MM-dd} 09:00";
        var product = await CreateInventoryProductAsync(NewCode("SP"), track: false);

        // XBH 3,000 unpaid → receivable 3,000.
        var sale = VoucherRequest(StockDirection.Out, await ReasonIdAsync("XBH"), yesterday, LineRequest(product, 3, 1_000));
        sale.WarehouseId = _warehouseId;
        sale.PartnerId = partner;
        sale.PaidAmount = 0;
        var (saleStatus, _, saleError) = await PostVoucherAsync(_branchClient, sale);
        saleStatus.Should().Be(HttpStatusCode.OK, saleError?.Message);

        // NMH 2,000 unpaid → payable 2,000.
        var purchase = VoucherRequest(StockDirection.In, await ReasonIdAsync("NMH"), yesterday, LineRequest(product, 2, 1_000));
        purchase.WarehouseId = _warehouseId;
        purchase.PartnerId = partner;
        purchase.PaidAmount = 0;
        var (buyStatus, _, buyError) = await PostVoucherAsync(_branchClient, purchase);
        buyStatus.Should().Be(HttpStatusCode.OK, buyError?.Message);

        var summary = await GetSummaryAsync(_branchClient, partner);
        summary.ReceivableBalance.Should().Be(3_000m);
        summary.PayableBalance.Should().Be(2_000m);

        // Per working branch, like the receivable.
        var other = CloneAdminClient();
        UseBranch(other, MainBranchId);
        (await GetSummaryAsync(other, partner)).PayableBalance.Should().Be(0m);

        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Permission_rules()
    {
        var customer = await CreatePartnerAsync(NewCode("KH"), isCustomer: true, isSupplier: false);
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var sales = await CreateClientForRoleAsync("ps_sales_" + suffix, RoleCodes.Sales, _branchId);
        (await sales.GetAsync(SummaryUrl(customer))).StatusCode.Should().Be(HttpStatusCode.OK);

        var warehouse = await CreateClientForRoleAsync("ps_wh_" + suffix, RoleCodes.Warehouse, _branchId);
        (await warehouse.GetAsync(SummaryUrl(customer))).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var reports = await CreateClientWithPermissionsAsync("ps_rep_" + suffix, _branchId, Permissions.Reports.Debt);
        (await reports.GetAsync(SummaryUrl(customer))).StatusCode.Should().Be(HttpStatusCode.OK);

        await AssertCashDebtInvariantsAsync();
    }

    private static string NewCode(string prefix) => $"{prefix}-{Guid.NewGuid().ToString("N")[..8]}";

    private static string SummaryUrl(Guid partnerId) => $"/api/debt/partner-summary?partnerId={partnerId}";

    private static async Task<PartnerDebtSummaryDto> GetSummaryAsync(HttpClient client, Guid partnerId) =>
        await ReadDataAsync<PartnerDebtSummaryDto>(await client.GetAsync(SummaryUrl(partnerId)));
}
