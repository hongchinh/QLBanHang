using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.CashDebt.CashVouchers.Models;
using OrderMgmt.Application.CashDebt.DebtOffsets.Models;
using OrderMgmt.Application.CashDebt.Funds.Models;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.CashDebt;

/// Once a branch has an opening date, no document may be dated before it (VN date): stock and cash vouchers, fund
/// transfers and debt offsets answer 400 voucherAt.
[Collection(nameof(PostgresCollection))]
public class OpeningDateGateTests : CashDebtTestBase
{
    private Guid _branchId;
    private Guid _warehouseId;
    private Guid _qtmId;
    private HttpClient _client2 = default!;

    public OpeningDateGateTests(PostgresFixture pg) : base(pg) { }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        var code = "OG" + Guid.NewGuid().ToString("N")[..8];
        _branchId = await CreateBranchAsync(code);
        await SetBranchOpeningDateAsync(_branchId, new DateOnly(2026, 10, 1));
        _warehouseId = await CreateWarehouseAsync(_branchId, code);
        _qtmId = await CashFundIdAsync(_branchId);
        _client2 = CloneAdminClient();
        UseBranch(_client2, _branchId);
    }

    [Fact]
    public async Task Cash_vouchers_cannot_be_dated_before_the_opening_date()
    {
        var tk = await CashReasonIdAsync("TK");
        AssertVoucherAtError(await PostCashAsync(_client2,
            CashRequest(CashVoucherType.Receipt, tk, _qtmId, "2026-09-30 23:59", null, 100m)));

        var onTheDate = await CreateCashAsync(_client2,
            CashRequest(CashVoucherType.Receipt, tk, _qtmId, "2026-10-01 00:00", null, 100m));
        var moved = CashUpdateFrom(onTheDate);
        moved.VoucherAt = Vn("2026-09-30 09:00");
        AssertVoucherAtError(await PutCashAsync(_client2, onTheDate.Id, moved));
    }

    [Fact]
    public async Task Stock_vouchers_cannot_be_dated_before_the_opening_date()
    {
        var product = await CreateInventoryProductAsync("SP-" + Guid.NewGuid().ToString("N")[..8], track: false);
        var request = VoucherRequest(StockDirection.In, await ReasonIdAsync("NKH"), "2026-09-30 23:59",
            LineRequest(product, 1, 1_000));
        request.WarehouseId = _warehouseId;
        var (status, _, error) = await PostVoucherAsync(_client2, request);
        status.Should().Be(HttpStatusCode.BadRequest, error?.Message);
        error!.Details.Should().ContainKey("voucherAt");

        request.VoucherAt = Vn("2026-10-01 00:00");
        (await PostVoucherAsync(_client2, request)).Status.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Fund_transfers_and_offsets_cannot_be_dated_before_the_opening_date()
    {
        var bank = await ReadDataAsync<FundDto>(await _client2.PostAsJsonAsync("/api/funds", new CreateFundRequest
        {
            Code = "NH" + Guid.NewGuid().ToString("N")[..6], Name = "Ngân hàng", Kind = FundKind.Bank, IsActive = true,
        }));
        await SetFundOpeningAmountAsync(_qtmId, 1_000m);
        AssertVoucherAtError(await PostTransferAsync(_client2, TransferRequest(_qtmId, bank.Id, 100m, "2026-09-30 23:59")));

        var partner = await CreatePartnerAsync("DT-" + Guid.NewGuid().ToString("N")[..8], isCustomer: true, isSupplier: true);
        var receivable = await CreateOpeningDebtAsync(_client2, partner, DebtSide.Receivable, OpeningDebtKind.Debt, "HD1",
            new DateOnly(2026, 9, 10), 500m);
        var payable = await CreateOpeningDebtAsync(_client2, partner, DebtSide.Payable, OpeningDebtKind.Debt, "HDM1",
            new DateOnly(2026, 9, 10), 500m);
        var offset = await _client2.PostAsJsonAsync("/api/debt-offsets", new CreateDebtOffsetRequest
        {
            VoucherAt = Vn("2026-09-30 23:59"),
            PartnerId = partner,
            Receivables = { new AllocationRequest { EntryId = receivable.EntryId, Amount = 200m } },
            Payables = { new AllocationRequest { EntryId = payable.EntryId, Amount = 200m } },
        }, TestJson.Options);
        offset.StatusCode.Should().Be(HttpStatusCode.BadRequest, await offset.Content.ReadAsStringAsync());
        (await offset.Content.ReadFromJsonAsync<ApiResponse>(TestJson.Options))!.Error!.Details.Should().ContainKey("voucherAt");

        (await InDbAsync(db => db.FundTransfers.IgnoreQueryFilters().CountAsync(t => t.BranchId == _branchId))).Should().Be(0);
        (await InDbAsync(db => db.DebtOffsetVouchers.IgnoreQueryFilters().CountAsync(o => o.BranchId == _branchId))).Should().Be(0);
        await AssertCashDebtInvariantsAsync();
    }

    private static void AssertVoucherAtError<T>((HttpStatusCode Status, T? Document, ApiError? Error) result)
    {
        result.Status.Should().Be(HttpStatusCode.BadRequest, result.Error?.Message);
        result.Error!.Details.Should().ContainKey("voucherAt");
    }
}
