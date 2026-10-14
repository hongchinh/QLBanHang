using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.CashDebt.CashVouchers.Models;
using OrderMgmt.Application.CashDebt.CreditLimits.Models;
using OrderMgmt.Application.CashDebt.DebtOffsets.Models;
using OrderMgmt.Application.CashDebt.FundTransfers.Models;
using OrderMgmt.Application.Inventory.OpeningStocks.Models;
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

    /// Ids of the rows FillAsync created in a demo branch. `CashVoucherIds` holds the automatic receipt too.
    protected sealed record DemoFill(Guid BankFundId, IReadOnlyList<Guid> ProductIds, IReadOnlyList<Guid> OpeningDebtIds,
        IReadOnlyList<Guid> StockVoucherIds, IReadOnlyList<Guid> CashVoucherIds, Guid FundTransferId, Guid DebtOffsetId,
        IReadOnlyList<Guid> EntryIds)
    {
        public int DocumentCount => StockVoucherIds.Count + CashVoucherIds.Count + 2;

        /// The documents whose activities live in cash_document_activities.
        public IEnumerable<Guid> CashDocumentIds => CashVoucherIds.Append(FundTransferId).Append(DebtOffsetId);
    }

    /// Fills a demo branch with a row of every kind a branch holds, all in its one warehouse, dated after
    /// `DemoOpeningDate`: fund opening balances, opening stock of two tracked products, two opening debts, a credit
    /// limit, a cash sale (automatic receipt and its settlement), a credit sale with a TKH receipt allocated to it, a
    /// return NTL, an XKH, a credit purchase from and a credit sale to a dual partner offset against each other, a fund
    /// transfer and a cancelled expense CK.
    protected async Task<DemoFill> FillAsync(DemoBranch branch)
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var client = branch.Client;
        var bankFund = await CreateBankFundAsync("NH01", branch.BranchId);
        await ReportDataset.SetFundOpeningAsync(client, branch.CashFundId, 10_000m);
        await ReportDataset.SetFundOpeningAsync(client, bankFund, 20_000m);

        var productA = await CreateInventoryProductAsync("DA" + suffix);
        var productB = await CreateInventoryProductAsync("DB" + suffix);
        var opening = await client.PutAsJsonAsync("/api/inventory/opening-stock", new SaveOpeningStockRequest
        {
            WarehouseId = branch.WarehouseId,
            Lines =
            {
                new SaveOpeningStockLine { ProductId = productA, Quantity = 10, Amount = 10_000m },
                new SaveOpeningStockLine { ProductId = productB, Quantity = 5, Amount = 5_000m },
            },
        });
        opening.StatusCode.Should().Be(HttpStatusCode.OK, await opening.Content.ReadAsStringAsync());

        var customer = await CreatePartnerAsync("DK" + suffix, isCustomer: true, isSupplier: false);
        var dual = await CreatePartnerAsync("DT" + suffix, isCustomer: true, isSupplier: true);
        var (openingDebt, _) = await CreateOpeningDebtAsync(client, customer, DebtSide.Receivable, OpeningDebtKind.Debt,
            "HD-DEMO", new DateOnly(2026, 9, 5), 5_000m);
        var (prepayment, _) = await CreateOpeningDebtAsync(client, customer, DebtSide.Receivable,
            OpeningDebtKind.Prepayment, "TT-DEMO", new DateOnly(2026, 9, 15), 1_000m);
        var limit = await client.PutAsJsonAsync($"/api/credit-limits/{customer}",
            new UpsertCreditLimitRequest { Amount = 100_000m });
        limit.StatusCode.Should().Be(HttpStatusCode.OK, await limit.Content.ReadAsStringAsync());

        var cashMethod = await ReportDataset.PaymentMethodIdAsync(this, "TM");
        var cashSale = await PostStockLineAsync(branch, StockDirection.Out, "XBH", "2026-10-02 09:00", customer,
            productA, 2, 1_500m, cashMethod, paid: 3_000m);
        var creditSale = await PostCreditSaleAsync(branch, customer, productA, 2_000m);
        var receipt = await PostAllocatedReceiptAsync(branch, customer, creditSale.Id, 1_000m);
        var saleReturn = await PostStockLineAsync(branch, StockDirection.In, "NTL", "2026-10-06 09:00", customer,
            productA, 1, 2_000m);
        var otherOut = await PostStockLineAsync(branch, StockDirection.Out, "XKH", "2026-10-06 10:00", null,
            productB, 1, 1_000m);

        var purchase = await PostStockLineAsync(branch, StockDirection.In, "NMH", "2026-10-07 09:00", dual,
            productB, 2, 1_000m);
        var dualSale = await PostStockLineAsync(branch, StockDirection.Out, "XBH", "2026-10-07 10:00", dual,
            productB, 1, 1_500m);
        var (offsetStatus, offset, offsetError) = await ReadDocumentResponseAsync<DebtOffsetDto>(
            await client.PostAsJsonAsync("/api/debt-offsets", new CreateDebtOffsetRequest
            {
                VoucherAt = Vn("2026-10-08 09:00"),
                PartnerId = dual,
                Receivables = new List<AllocationRequest>
                {
                    new() { EntryId = (await EntriesOfAsync(DebtSourceType.StockOut, dualSale.Id)).Single().Id, Amount = 1_000m },
                },
                Payables = new List<AllocationRequest>
                {
                    new() { EntryId = (await EntriesOfAsync(DebtSourceType.StockIn, purchase.Id)).Single().Id, Amount = 1_000m },
                },
            }, TestJson.Options));
        offsetStatus.Should().Be(HttpStatusCode.OK, offsetError?.Message);

        var transfer = await PostFundTransferAsync(branch, bankFund, 5_000m);
        var expenseRequest = CashRequest(CashVoucherType.Payment, await CashReasonIdAsync("CK"), branch.CashFundId,
            "2026-10-09 09:00", partnerId: null, 700m);
        expenseRequest.PersonName = "Nguyễn A";
        var expense = await CreateCashAsync(client, expenseRequest);
        var (cancelStatus, _, cancelError) = await CancelCashAsync(client, expense.Id,
            new CashDocumentActionRequest { Version = expense.Version });
        cancelStatus.Should().Be(HttpStatusCode.OK, cancelError?.Message);

        var entryIds = await InDbAsync(db => db.DebtEntries.Where(e => e.BranchId == branch.BranchId)
            .Select(e => e.Id).ToListAsync());
        return new DemoFill(bankFund, new[] { productA, productB }, new[] { openingDebt, prepayment },
            new[] { cashSale.Id, creditSale.Id, saleReturn.Id, otherOut.Id, purchase.Id, dualSale.Id },
            new[] { cashSale.AutoCashVoucherId!.Value, receipt.Id, expense.Id }, transfer.Id, offset!.Id, entryIds);
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
