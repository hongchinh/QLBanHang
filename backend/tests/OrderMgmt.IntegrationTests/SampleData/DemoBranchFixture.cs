using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.CashDebt.CashVouchers.Models;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.CashDebt;
using OrderMgmt.IntegrationTests.Fixtures;

namespace OrderMgmt.IntegrationTests.SampleData;

/// Builders of the demo branch tests: a demo branch and documents posted through the API. Derives from the test base
/// because the helpers it reuses are protected; the demo branch test classes derive from it.
public abstract class DemoBranchFixture : CashDebtTestBase
{
    protected DemoBranchFixture(PostgresFixture pg) : base(pg) { }

    protected static readonly DateOnly DemoOpeningDate = new(2026, 10, 1);

    /// A branch created through the API, flagged as a demo one with finished sample data; `Client` is an admin client
    /// working in it. Its opening date is set so documents can be posted.
    protected async Task<(Guid BranchId, HttpClient Client)> CreateDemoBranchAsync(string code,
        SampleDataStatus status = SampleDataStatus.Ready)
    {
        var branchId = await CreateBranchAsync(code);
        await InDbAsync(db => db.Branches.Where(b => b.Id == branchId).ExecuteUpdateAsync(s => s
            .SetProperty(b => b.IsDemo, true)
            .SetProperty(b => b.SampleDataStatus, status)));
        await ReportDataset.SetOpeningDateAsync(this, branchId, DemoOpeningDate);
        var client = CloneAdminClient();
        UseBranch(client, branchId);
        return (branchId, client);
    }

    /// A credit sale (XBH, nothing paid, untracked product) of `amount` to the customer in the branch's own warehouse.
    protected async Task<OrderMgmt.Application.Inventory.StockVouchers.Models.StockVoucherDto> PostCreditSaleAsync(
        Guid branchId, HttpClient client, Guid customerId, decimal amount)
    {
        var warehouseId = await CreateWarehouseAsync(branchId, "W" + Guid.NewGuid().ToString("N")[..6]);
        return await ReportDataset.PostStockAsync(this, client, StockDirection.Out, "XBH", "2026-10-03 09:00",
            warehouseId, customerId, amount, paymentMethodId: null, paid: 0m);
    }

    /// A TKH receipt of `amount` into the branch's cash fund, allocated to the debt entry of the sale.
    protected async Task<CashVoucherDto> PostAllocatedReceiptAsync(Guid branchId, HttpClient client, Guid customerId,
        Guid saleId, decimal amount)
    {
        var entry = (await EntriesOfAsync(DebtSourceType.StockOut, saleId)).Single();
        var request = CashRequest(CashVoucherType.Receipt, await CashReasonIdAsync("TKH"), await CashFundIdAsync(branchId),
            "2026-10-05 09:00", customerId, amount);
        request.Allocations = new List<AllocationRequest> { new() { EntryId = entry.Id, Amount = amount } };
        return await CreateCashAsync(client, request);
    }

    /// A transfer of `amount` from the branch's bank fund `bankCode` to its cash fund.
    protected async Task PostFundTransferAsync(Guid branchId, HttpClient client, string bankCode, decimal amount)
    {
        var bank = await CreateBankFundAsync(bankCode, branchId, opening: amount);
        var (status, _, error) = await PostTransferAsync(client,
            TransferRequest(bank, await CashFundIdAsync(branchId), amount, "2026-10-06 09:00"));
        status.Should().Be(HttpStatusCode.OK, error?.Message);
    }
}
