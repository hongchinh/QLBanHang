using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Inventory.StockVouchers.Models;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.CashDebt.StockVoucherDebt;

[Collection(nameof(PostgresCollection))]
public class StockVoucherDebtPostingTests : CashDebtTestBase
{
    private Guid _branchId;
    private Guid _warehouseId;
    private HttpClient _branchClient = default!;

    public StockVoucherDebtPostingTests(PostgresFixture pg) : base(pg) { }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        // A fresh branch per test: the codes PX00001 / PN00001 are asserted.
        var code = "DP" + Guid.NewGuid().ToString("N")[..8];
        _branchId = await CreateBranchAsync(code);
        _warehouseId = await CreateWarehouseAsync(_branchId, code);
        _branchClient = CloneAdminClient();
        UseBranch(_branchClient, _branchId);
    }

    [Fact]
    public async Task Sale_posts_receivable_increase_and_purchase_posts_payable_increase()
    {
        var customer = await NewPartnerAsync(isCustomer: true, isSupplier: false);
        var supplier = await NewPartnerAsync(isCustomer: false, isSupplier: true);
        var adminId = await InDbAsync(db => db.Users.Where(u => u.Username == "admin").Select(u => u.Id).SingleAsync());

        var sale = await CreateAsync(StockDirection.Out, "XBH", customer, "2026-10-05 09:00", 3, 1_000);
        sale.Code.Should().Be("PX00001");
        (await EntriesOfAsync(DebtSourceType.StockOut, sale.Id)).Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            BranchId = _branchId,
            PartnerId = customer,
            Side = DebtSide.Receivable,
            Direction = DebtDirection.Increase,
            Amount = 3_000m,
            PostedAt = Vn("2026-10-05 09:00"),
            DocDate = new DateOnly(2026, 10, 5),
            // No due date and no credit days: due on the document date.
            DueDate = (DateOnly?)new DateOnly(2026, 10, 5),
            OwnerUserId = (Guid?)adminId,
            DocCode = "PX00001",
            Description = "Xuất bán hàng",
        });

        var purchase = await CreateAsync(StockDirection.In, "NMH", supplier, "2026-10-04 10:00", 2, 1_000,
            r => r.DueDate = new DateOnly(2026, 10, 20));
        purchase.Code.Should().Be("PN00001");
        (await EntriesOfAsync(DebtSourceType.StockIn, purchase.Id)).Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            BranchId = _branchId,
            PartnerId = supplier,
            Side = DebtSide.Payable,
            Direction = DebtDirection.Increase,
            Amount = 2_000m,
            PostedAt = Vn("2026-10-04 10:00"),
            DocDate = new DateOnly(2026, 10, 4),
            DueDate = (DateOnly?)new DateOnly(2026, 10, 20),
            DocCode = "PN00001",
            Description = "Nhập mua hàng",
        });

        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Returns_post_decreases_on_the_right_side()
    {
        var customer = await NewPartnerAsync(isCustomer: true, isSupplier: false);
        var supplier = await NewPartnerAsync(isCustomer: false, isSupplier: true);

        // A customer returns goods (stock-in): the receivable decreases.
        var customerReturn = await CreateAsync(StockDirection.In, "NTL", customer, "2026-10-05 09:00", 1, 400);
        (await EntriesOfAsync(DebtSourceType.StockIn, customerReturn.Id)).Should().ContainSingle().Which.Should()
            .BeEquivalentTo(new
            {
                PartnerId = customer,
                Side = DebtSide.Receivable,
                Direction = DebtDirection.Decrease,
                Amount = 400m,
                DueDate = (DateOnly?)null,
                DocCode = customerReturn.Code,
            });

        // Goods go back to a supplier (stock-out): the payable decreases.
        var supplierReturn = await CreateAsync(StockDirection.Out, "XTL", supplier, "2026-10-05 10:00", 1, 700);
        (await EntriesOfAsync(DebtSourceType.StockOut, supplierReturn.Id)).Should().ContainSingle().Which.Should()
            .BeEquivalentTo(new
            {
                PartnerId = supplier,
                Side = DebtSide.Payable,
                Direction = DebtDirection.Decrease,
                Amount = 700m,
                DueDate = (DateOnly?)null,
                DocCode = supplierReturn.Code,
            });

        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Reason_without_effect_posts_nothing()
    {
        var partner = await NewPartnerAsync(isCustomer: true, isSupplier: true);

        var otherOut = await CreateAsync(StockDirection.Out, "XKH", partner, "2026-10-05 09:00", 2, 500);
        var otherIn = await CreateAsync(StockDirection.In, "NKH", partner, "2026-10-05 10:00", 2, 500);

        (await EntriesOfAsync(DebtSourceType.StockOut, otherOut.Id)).Should().BeEmpty();
        (await EntriesOfAsync(DebtSourceType.StockIn, otherIn.Id)).Should().BeEmpty();
        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Edit_updates_entry_in_place_and_cancel_restore_delete_follow()
    {
        var customer = await NewPartnerAsync(isCustomer: true, isSupplier: false);
        var sale = await CreateAsync(StockDirection.Out, "XBH", customer, "2026-10-05 09:00", 3, 1_000);
        var entry = (await EntriesOfAsync(DebtSourceType.StockOut, sale.Id)).Single();

        // Quantity 3 → 5 and the date moves to 10-06: the same entry follows.
        var edit = UpdateRequestFrom(sale);
        edit.Lines[0].Quantity = 5;
        edit.VoucherAt = Vn("2026-10-06 14:00");
        var (status, edited, error) = await PutVoucherAsync(_branchClient, sale.Id, edit);
        status.Should().Be(HttpStatusCode.OK, error?.Message);
        (await EntriesOfAsync(DebtSourceType.StockOut, sale.Id)).Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            entry.Id,
            Amount = 5_000m,
            PostedAt = Vn("2026-10-06 14:00"),
            DocDate = new DateOnly(2026, 10, 6),
            DueDate = (DateOnly?)new DateOnly(2026, 10, 6),
        });
        await AssertCashDebtInvariantsAsync();

        var cancelled = await ActionOkAsync(sale.Id, "cancel", edited!.Version);
        (await EntriesOfAsync(DebtSourceType.StockOut, sale.Id)).Should().BeEmpty();
        await AssertCashDebtInvariantsAsync();

        var restored = await ActionOkAsync(sale.Id, "restore", cancelled.Version);
        (await EntriesOfAsync(DebtSourceType.StockOut, sale.Id)).Should().ContainSingle()
            .Which.Amount.Should().Be(5_000m);
        await AssertCashDebtInvariantsAsync();

        var deleted = await _branchClient.DeleteAsync($"/api/stock-vouchers/{sale.Id}?version={restored.Version}");
        deleted.StatusCode.Should().Be(HttpStatusCode.OK, await deleted.Content.ReadAsStringAsync());
        (await EntriesOfAsync(DebtSourceType.StockOut, sale.Id)).Should().BeEmpty();
        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Changing_to_a_reason_without_effect_removes_the_entry()
    {
        var customer = await NewPartnerAsync(isCustomer: true, isSupplier: false);
        var sale = await CreateAsync(StockDirection.Out, "XBH", customer, "2026-10-05 09:00", 3, 1_000);
        (await EntriesOfAsync(DebtSourceType.StockOut, sale.Id)).Should().ContainSingle();

        var edit = UpdateRequestFrom(sale);
        edit.ReasonId = await ReasonIdAsync("XKH");
        var (status, edited, error) = await PutVoucherAsync(_branchClient, sale.Id, edit);
        status.Should().Be(HttpStatusCode.OK, error?.Message);
        edited!.ReasonDebtEffect.Should().Be(DebtEffect.None);

        (await EntriesOfAsync(DebtSourceType.StockOut, sale.Id)).Should().BeEmpty();
        await AssertCashDebtInvariantsAsync();
    }

    private Task<Guid> NewPartnerAsync(bool isCustomer, bool isSupplier) =>
        CreatePartnerAsync("DT-" + Guid.NewGuid().ToString("N")[..8], isCustomer, isSupplier);

    /// A voucher of one untracked line in the test branch, nothing paid; asserts 200.
    private async Task<StockVoucherDto> CreateAsync(StockDirection type, string reasonCode, Guid partnerId, string at,
        decimal quantity, decimal unitPrice, Action<UpsertStockVoucherRequest>? configure = null)
    {
        var product = await CreateInventoryProductAsync("SP-" + Guid.NewGuid().ToString("N")[..8], track: false);
        var request = VoucherRequest(type, await ReasonIdAsync(reasonCode), at, LineRequest(product, quantity, unitPrice));
        request.WarehouseId = _warehouseId;
        request.PartnerId = partnerId;
        request.PaidAmount = 0;
        configure?.Invoke(request);
        var (status, voucher, error) = await PostVoucherAsync(_branchClient, request);
        status.Should().Be(HttpStatusCode.OK, error?.Message);
        return voucher!;
    }

    /// POST /api/stock-vouchers/{id}/{action} (cancel or restore); asserts 200.
    private async Task<StockVoucherDto> ActionOkAsync(Guid id, string action, uint version)
    {
        var (status, voucher, error) = await ReadVoucherResponseAsync(await _branchClient.PostAsJsonAsync(
            $"/api/stock-vouchers/{id}/{action}", new StockVoucherActionRequest { Version = version }));
        status.Should().Be(HttpStatusCode.OK, error?.Message);
        return voucher!;
    }
}
