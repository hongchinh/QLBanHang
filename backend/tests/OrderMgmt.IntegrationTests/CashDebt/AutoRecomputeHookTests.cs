using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderMgmt.Application.CashDebt.CashVouchers.Models;
using OrderMgmt.Application.CashDebt.Ledger;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.CashDebt;

/// A write that removes settlements needs no RecomputeAutoAsync call of its own: the before-commit hook does it.
[Collection(nameof(PostgresCollection))]
public class AutoRecomputeHookTests : CashDebtTestBase
{
    private Guid _branchId;
    private Guid _warehouseId;
    private Guid _cashFundId;
    private Guid _cashMethodId;
    private HttpClient _branchClient = default!;

    public AutoRecomputeHookTests(PostgresFixture pg) : base(pg) { }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        var code = "AH" + Guid.NewGuid().ToString("N")[..8];
        _branchId = await CreateBranchAsync(code);
        await SetBranchOpeningDateAsync(_branchId, new DateOnly(2026, 10, 1));
        _warehouseId = await CreateWarehouseAsync(_branchId, code);
        _cashFundId = await CashFundIdAsync(_branchId);
        await SetFundOpeningAmountAsync(_cashFundId, 1_000_000m);
        _cashMethodId = await InDbAsync(db => db.PaymentMethods.Where(m => m.Code == "TM").Select(m => m.Id).SingleAsync());
        _branchClient = CloneAdminClient();
        UseBranch(_branchClient, _branchId);
    }

    [Fact]
    public async Task Removing_a_settlement_recomputes_the_Auto_settlement_before_commit()
    {
        var customer = await CreatePartnerAsync("KH-" + Guid.NewGuid().ToString("N")[..8], isCustomer: true, isSupplier: false);
        var product = await CreateInventoryProductAsync("SP-" + Guid.NewGuid().ToString("N")[..8], track: false);
        var create = VoucherRequest(StockDirection.Out, await ReasonIdAsync("XBH"), "2026-10-05 09:00",
            LineRequest(product, 3, 1_000));
        create.WarehouseId = _warehouseId;
        create.PartnerId = customer;
        var (createStatus, sale, createError) = await PostVoucherAsync(_branchClient, create);
        createStatus.Should().Be(HttpStatusCode.OK, createError?.Message);
        var stockEntry = (await EntriesOfAsync(DebtSourceType.StockOut, sale!.Id)).Single();

        var receiptRequest = CashRequest(CashVoucherType.Receipt, await CashReasonIdAsync("TKH"), _cashFundId,
            "2026-10-05 10:00", customer, 2_500m);
        receiptRequest.Allocations = new List<AllocationRequest> { new() { EntryId = stockEntry.Id, Amount = 2_500m } };
        var receipt = await CreateCashAsync(_branchClient, receiptRequest);
        var receiptEntry = (await EntriesOfAsync(DebtSourceType.Receipt, receipt.Id)).Single();
        var manual = (await SettlementsOfAsync(receiptEntry.Id)).Single();

        // Paid 1,000: Auto = min(1,000, 3,000 − 2,500, 1,000) = 500.
        var edit = UpdateRequestFrom(sale);
        edit.PaymentMethodId = _cashMethodId;
        edit.PaidAmount = 1_000;
        var (editStatus, _, editError) = await PutVoucherAsync(_branchClient, sale.Id, edit);
        editStatus.Should().Be(HttpStatusCode.OK, editError?.Message);
        (await AutoAmountAsync(stockEntry.Id)).Should().Be(500m);

        // Only the removal: the hook gives the room back to the Auto settlement.
        await InTransactionAsync(async (sp, ct) =>
        {
            await sp.GetRequiredService<IDebtLedgerService>().RemoveSettlementsAsync(new[] { manual.Id }, ct);
            return 0;
        });

        (await AutoAmountAsync(stockEntry.Id)).Should().Be(1_000m, "min(1,000, 3,000, 1,000)");
        await AssertCashDebtInvariantsAsync();
    }

    private async Task<decimal> AutoAmountAsync(Guid stockEntryId) =>
        (await SettlementsOfAsync(stockEntryId)).Where(s => s.Origin == SettlementOrigin.Auto).Sum(s => s.Amount);
}
