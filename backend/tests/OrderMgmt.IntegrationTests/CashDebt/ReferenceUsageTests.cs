using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using OrderMgmt.Application.Common.References;
using OrderMgmt.Application.Inventory.PaymentMethods.Models;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using System.Net.Http.Json;
using Xunit;

namespace OrderMgmt.IntegrationTests.CashDebt;

/// The "is it used" queries of partners, payment methods, branches and funds.
[Collection(nameof(PostgresCollection))]
public class ReferenceUsageTests : CashDebtTestBase
{
    private static readonly DateOnly Oct1 = new(2026, 10, 1);

    public ReferenceUsageTests(PostgresFixture pg) : base(pg) { }

    private Task<T> UsageAsync<T>(Func<IReferenceUsage, CancellationToken, Task<T>> query) =>
        InTransactionAsync((sp, ct) => query(sp.GetRequiredService<IReferenceUsage>(), ct));

    private async Task<(Guid BranchId, HttpClient Client)> NewBranchAsync()
    {
        var branchId = await CreateBranchAsync("RU" + Guid.NewGuid().ToString("N")[..8]);
        await SetBranchOpeningDateAsync(branchId, Oct1);
        var client = CloneAdminClient();
        UseBranch(client, branchId);
        return (branchId, client);
    }

    [Fact]
    public async Task Partner_usage_reads_cash_debt_data_and_debt_sides()
    {
        var (_, client) = await NewBranchAsync();
        var withDebt = await CreatePartnerAsync("KH" + Guid.NewGuid().ToString("N")[..8], isCustomer: true, isSupplier: false);
        await CreateOpeningDebtAsync(client, withDebt, DebtSide.Receivable, OpeningDebtKind.Debt, "HD1",
            new DateOnly(2026, 9, 10), 1_000m);
        var fresh = await CreatePartnerAsync("KH" + Guid.NewGuid().ToString("N")[..8], isCustomer: true, isSupplier: false);

        (await UsageAsync((u, ct) => u.PartnerHasCashOrDebtDataAsync(withDebt, ct))).Should().BeTrue();
        (await UsageAsync((u, ct) => u.PartnerHasCashOrDebtDataAsync(fresh, ct))).Should().BeFalse();
        (await UsageAsync((u, ct) => u.PartnerHasDebtOnSideAsync(withDebt, DebtSide.Receivable, ct))).Should().BeTrue();
        (await UsageAsync((u, ct) => u.PartnerHasDebtOnSideAsync(withDebt, DebtSide.Payable, ct))).Should().BeFalse();
        (await UsageAsync((u, ct) => u.PartnerHasStockVouchersAsync(fresh, ct))).Should().BeFalse();
    }

    [Fact]
    public async Task Payment_method_usage_reads_paid_stock_vouchers()
    {
        var code = "RP" + Guid.NewGuid().ToString("N")[..8];
        var branchId = await CreateBranchAsync(code);
        await SetBranchOpeningDateAsync(branchId, new DateOnly(2026, 9, 1));
        var warehouseId = await CreateWarehouseAsync(branchId, code);
        var client = CloneAdminClient();
        UseBranch(client, branchId);
        var customer = await CreatePartnerAsync("KH-" + code, isCustomer: true, isSupplier: false);

        async Task<PaymentMethodDto> MethodAsync(string suffix) => await ReadDataAsync<PaymentMethodDto>(
            await _client.PostAsJsonAsync("/api/payment-methods", new { code = code + suffix, name = "Ví", fundKind = "Cash" }));
        var used = await MethodAsync("U");
        var fresh = await MethodAsync("F");
        var product = await CreateInventoryProductAsync("SP-" + Guid.NewGuid().ToString("N")[..8], track: false);
        await CreateVoucherAsync(client, StockDirection.Out, "XBH", "2026-10-05 09:00", r =>
        {
            r.WarehouseId = warehouseId;
            r.PartnerId = customer;
            r.PaymentMethodId = used.Id;
            r.PaidAmount = 500m;
        }, LineRequest(product, 1, 1_000));

        (await UsageAsync((u, ct) => u.PaymentMethodUsedByPaidStockVouchersAsync(used.Id, ct))).Should().BeTrue();
        (await UsageAsync((u, ct) => u.PaymentMethodUsedByStockVouchersAsync(used.Id, ct))).Should().BeTrue();
        (await UsageAsync((u, ct) => u.PaymentMethodUsedByPaidStockVouchersAsync(fresh.Id, ct))).Should().BeFalse();
        (await UsageAsync((u, ct) => u.PartnerHasStockVouchersAsync(customer, ct))).Should().BeTrue();
    }

    [Fact]
    public async Task Branch_and_fund_usage_read_the_documents()
    {
        var (branchId, client) = await NewBranchAsync();
        var (emptyBranchId, _) = await NewBranchAsync();
        var fundId = await FundIdAsync("QTM", branchId);
        var freshFund = await FundIdAsync("QTM", emptyBranchId);
        await CreateCashAsync(client, CashRequest(CashVoucherType.Receipt, await CashReasonIdAsync("TK"), fundId,
            "2026-10-05 09:00", null, 300m));

        (await UsageAsync((u, ct) => u.BranchHasDocumentsAsync(branchId, ct))).Should().BeTrue();
        (await UsageAsync((u, ct) => u.BranchHasDocumentsAsync(emptyBranchId, ct))).Should().BeFalse();
        (await UsageAsync((u, ct) => u.BranchHasOpeningDataAsync(branchId, ct))).Should().BeFalse();
        (await UsageAsync((u, ct) => u.EarliestDocumentDateAsync(branchId, ct))).Should().Be(new DateOnly(2026, 10, 5));

        (await UsageAsync((u, ct) => u.FundUsageAsync(fundId, ct))).Should().Be(FundUsage.CashVouchers);
        (await UsageAsync((u, ct) => u.FundUsageAsync(freshFund, ct))).Should().Be(FundUsage.None);
    }
}
