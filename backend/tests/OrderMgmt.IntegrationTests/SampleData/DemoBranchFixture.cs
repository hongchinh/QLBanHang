using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.CashDebt.CashVouchers.Models;
using OrderMgmt.Application.CashDebt.FundTransfers.Models;
using OrderMgmt.Application.Inventory.StockVouchers.Models;
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

    /// A demo branch with its one warehouse and its default cash fund QTM; `Client` is an admin client working in it.
    protected sealed record DemoBranch(Guid BranchId, Guid WarehouseId, Guid CashFundId, HttpClient Client);

    /// A branch created through the API, flagged as a demo one (by default with finished sample data), with one
    /// warehouse. Its opening date (default `DemoOpeningDate`) is set because opening stock, opening debts and fund
    /// opening balances need one (OPENING_DATE_NOT_SET).
    protected async Task<DemoBranch> CreateDemoBranchAsync(string code, SampleDataStatus status = SampleDataStatus.Ready,
        DateOnly? openingDate = null)
    {
        var branchId = await CreateBranchAsync(code);
        await InDbAsync(db => db.Branches.Where(b => b.Id == branchId).ExecuteUpdateAsync(s => s
            .SetProperty(b => b.IsDemo, true)
            .SetProperty(b => b.SampleDataStatus, status)));
        await ReportDataset.SetOpeningDateAsync(this, branchId, openingDate ?? DemoOpeningDate);
        var warehouseId = await CreateWarehouseAsync(branchId, "W" + code);
        var client = CloneAdminClient();
        UseBranch(client, branchId);
        return new DemoBranch(branchId, warehouseId, await CashFundIdAsync(branchId), client);
    }

    /// A one-line stock voucher in the branch's warehouse, unpaid unless `paymentMethodId` and `paid` are given;
    /// asserts 200.
    protected async Task<StockVoucherDto> PostStockLineAsync(DemoBranch branch, StockDirection type, string reasonCode,
        string at, Guid? partnerId, Guid productId, decimal quantity, decimal unitPrice, Guid? paymentMethodId = null,
        decimal paid = 0m)
    {
        var request = VoucherRequest(type, await ReasonIdAsync(reasonCode), at, LineRequest(productId, quantity, unitPrice));
        request.WarehouseId = branch.WarehouseId;
        request.PartnerId = partnerId;
        request.PaymentMethodId = paymentMethodId;
        request.PaidAmount = paid;
        var (status, voucher, error) = await PostVoucherAsync(branch.Client, request);
        status.Should().Be(HttpStatusCode.OK, error?.Message);
        return voucher!;
    }

    /// A credit sale (XBH, nothing paid) of one `productId` at `amount` to the customer.
    protected Task<StockVoucherDto> PostCreditSaleAsync(DemoBranch branch, Guid customerId, Guid productId,
        decimal amount) =>
        PostStockLineAsync(branch, StockDirection.Out, "XBH", "2026-10-03 09:00", customerId, productId, 1, amount);

    /// A TKH receipt of `amount` into the branch's cash fund, allocated to the debt entry of the sale.
    protected async Task<CashVoucherDto> PostAllocatedReceiptAsync(DemoBranch branch, Guid customerId, Guid saleId,
        decimal amount)
    {
        var entry = (await EntriesOfAsync(DebtSourceType.StockOut, saleId)).Single();
        var request = CashRequest(CashVoucherType.Receipt, await CashReasonIdAsync("TKH"), branch.CashFundId,
            "2026-10-05 09:00", customerId, amount);
        request.Allocations = new List<AllocationRequest> { new() { EntryId = entry.Id, Amount = amount } };
        return await CreateCashAsync(branch.Client, request);
    }

    /// A transfer of `amount` from `fromFundId` (a fund of the branch) to its cash fund.
    protected async Task<FundTransferDto> PostFundTransferAsync(DemoBranch branch, Guid fromFundId, decimal amount)
    {
        var (status, transfer, error) = await PostTransferAsync(branch.Client,
            TransferRequest(fromFundId, branch.CashFundId, amount, "2026-10-06 09:00"));
        status.Should().Be(HttpStatusCode.OK, error?.Message);
        return transfer!;
    }

    /// Asserts the status and the error code of a refused request.
    protected static async Task AssertErrorAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        var (actual, _, error) = await ReadDocumentResponseAsync<object>(response);
        actual.Should().Be(status, error?.Message);
        error.Should().NotBeNull();
        error!.Code.Should().Be(code);
    }
}
