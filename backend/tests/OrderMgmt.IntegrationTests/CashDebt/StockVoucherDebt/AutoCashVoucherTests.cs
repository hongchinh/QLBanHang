using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.CashDebt.CashVouchers.Models;
using OrderMgmt.Application.CashDebt.Funds.Models;
using OrderMgmt.Application.Inventory.StockVouchers.Models;
using OrderMgmt.Domain.Entities.CashDebt;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.CashDebt.StockVoucherDebt;

/// The receipt or payment a stock voucher manages for its paid amount (P20, P3).
[Collection(nameof(PostgresCollection))]
public class AutoCashVoucherTests : CashDebtTestBase
{
    private const string Handler = "Nguyễn Văn A";

    private Guid _branchId;
    private Guid _warehouseId;
    private Guid _cashFundId;
    private Guid _cashMethodId;
    private HttpClient _branchClient = default!;

    public AutoCashVoucherTests(PostgresFixture pg) : base(pg) { }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        // A fresh branch per test: the codes PX00001 / PT00001 / PC00001 are asserted.
        var code = "AV" + Guid.NewGuid().ToString("N")[..8];
        _branchId = await CreateBranchAsync(code);
        _warehouseId = await CreateWarehouseAsync(_branchId, code);
        _cashFundId = await CashFundIdAsync(_branchId);
        // Payments take money out of QTM: an opening balance keeps it from going negative.
        await SetFundOpeningAmountAsync(_cashFundId, 1_000_000m);
        _cashMethodId = await InDbAsync(db => db.PaymentMethods.Where(m => m.Code == "TM").Select(m => m.Id).SingleAsync());
        _branchClient = CloneAdminClient();
        UseBranch(_branchClient, _branchId);
    }

    [Fact]
    public async Task Sale_with_payment_creates_managed_receipt_and_auto_settlement()
    {
        var customer = await NewPartnerAsync(isCustomer: true, isSupplier: false);
        var adminId = await InDbAsync(db => db.Users.Where(u => u.Username == "admin").Select(u => u.Id).SingleAsync());

        var sale = await CreatePaidAsync(StockDirection.Out, "XBH", customer, 3, 1_000, paid: 1_000);
        sale.Code.Should().Be("PX00001");
        sale.Should().BeEquivalentTo(new
        {
            AutoCashVoucherCode = "PT00001",
            AutoCashVoucherType = (CashVoucherType?)CashVoucherType.Receipt,
        });
        (await GetVoucherAsync(_branchClient, sale.Id)).AutoCashVoucherCode.Should().Be("PT00001");

        var receipt = await GetCashOkAsync(sale.AutoCashVoucherId!.Value);
        receipt.Should().BeEquivalentTo(new
        {
            Type = CashVoucherType.Receipt,
            Code = "PT00001",
            VoucherAt = Vn("2026-10-05 09:00"),
            BranchId = _branchId,
            FundId = _cashFundId,
            FundCode = "QTM",
            ReasonCode = "TPX",
            PartnerId = (Guid?)customer,
            sale.PartnerName,
            sale.PartnerAddress,
            sale.PartnerTaxCode,
            PersonName = Handler,
            Description = "Thu tiền theo phiếu xuất PX00001",
            Total = 1_000m,
            Status = DocumentStatus.Active,
            OwnerUserId = adminId,
            SourceStockVoucherId = (Guid?)sale.Id,
            SourceStockVoucherCode = "PX00001",
            IsManaged = true,
        });
        receipt.Lines.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Description = "Thu tiền theo phiếu xuất PX00001",
            Amount = 1_000m,
        });

        // R− 1,000 on the receipt, matched against the sale's R+ 3,000.
        var stockEntry = (await EntriesOfAsync(DebtSourceType.StockOut, sale.Id)).Single();
        var receiptEntry = (await EntriesOfAsync(DebtSourceType.Receipt, receipt.Id)).Should().ContainSingle().Subject;
        receiptEntry.Should().BeEquivalentTo(new
        {
            BranchId = _branchId,
            PartnerId = customer,
            Side = DebtSide.Receivable,
            Direction = DebtDirection.Decrease,
            Amount = 1_000m,
            PostedAt = Vn("2026-10-05 09:00"),
            DocDate = new DateOnly(2026, 10, 5),
            DueDate = (DateOnly?)null,
            OwnerUserId = (Guid?)adminId,
            DocCode = "PT00001",
            Description = "Thu tiền theo phiếu xuất PX00001",
        });
        (await SettlementsOfAsync(stockEntry.Id)).Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            IncreaseEntryId = stockEntry.Id,
            DecreaseEntryId = receiptEntry.Id,
            Amount = 1_000m,
            Origin = SettlementOrigin.Auto,
            BatchId = sale.Id,
        });
        receipt.Allocations.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            DocCode = "PX00001",
            Amount = 1_000m,
            Origin = SettlementOrigin.Auto,
        });

        (await CashActivitiesOfAsync(receipt.Id)).Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            DocumentType = DocumentType.Receipt,
            Action = CashDocumentActivityAction.Created,
            Description = "Theo phiếu PX00001",
        });
        (await AutoVoucherActivitiesOfAsync(sale.Id)).Should().Equal("Tạo phiếu thu tự động PT00001");
        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Purchase_creates_managed_payment()
    {
        var supplier = await NewPartnerAsync(isCustomer: false, isSupplier: true);

        var purchase = await CreatePaidAsync(StockDirection.In, "NMH", supplier, 2, 1_000, paid: 1_200);
        purchase.Code.Should().Be("PN00001");
        purchase.Should().BeEquivalentTo(new
        {
            AutoCashVoucherCode = "PC00001",
            AutoCashVoucherType = (CashVoucherType?)CashVoucherType.Payment,
        });

        var payment = await GetCashOkAsync(purchase.AutoCashVoucherId!.Value);
        payment.Should().BeEquivalentTo(new
        {
            Type = CashVoucherType.Payment,
            Code = "PC00001",
            FundId = _cashFundId,
            ReasonCode = "CPN",
            PartnerId = (Guid?)supplier,
            PersonName = Handler,
            Description = "Chi tiền theo phiếu nhập PN00001",
            Total = 1_200m,
            IsManaged = true,
        });

        // P− 1,200 on the payment, matched against the purchase's P+ 2,000.
        var stockEntry = (await EntriesOfAsync(DebtSourceType.StockIn, purchase.Id)).Single();
        var paymentEntry = (await EntriesOfAsync(DebtSourceType.Payment, payment.Id)).Should().ContainSingle().Subject;
        paymentEntry.Should().BeEquivalentTo(new
        {
            PartnerId = supplier,
            Side = DebtSide.Payable,
            Direction = DebtDirection.Decrease,
            Amount = 1_200m,
            DocCode = "PC00001",
        });
        (await SettlementsOfAsync(stockEntry.Id)).Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            IncreaseEntryId = stockEntry.Id,
            DecreaseEntryId = paymentEntry.Id,
            Amount = 1_200m,
            Origin = SettlementOrigin.Auto,
        });
        (await AutoVoucherActivitiesOfAsync(purchase.Id)).Should().Equal("Tạo phiếu chi tự động PC00001");
        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Return_refund_has_same_side_opposite_direction()
    {
        var customer = await NewPartnerAsync(isCustomer: true, isSupplier: false);

        // A customer returns goods for 800 (R−) and gets 500 back: a payment with R+ 500.
        var customerReturn = await CreatePaidAsync(StockDirection.In, "NTL", customer, 1, 800, paid: 500);
        customerReturn.Should().BeEquivalentTo(new
        {
            AutoCashVoucherCode = "PC00001",
            AutoCashVoucherType = (CashVoucherType?)CashVoucherType.Payment,
        });

        var stockEntry = (await EntriesOfAsync(DebtSourceType.StockIn, customerReturn.Id)).Single();
        stockEntry.Should().BeEquivalentTo(new { Side = DebtSide.Receivable, Direction = DebtDirection.Decrease, Amount = 800m });
        var refundEntry = (await EntriesOfAsync(DebtSourceType.Payment, customerReturn.AutoCashVoucherId!.Value))
            .Should().ContainSingle().Subject;
        refundEntry.Should().BeEquivalentTo(new
        {
            PartnerId = customer,
            Side = DebtSide.Receivable,
            Direction = DebtDirection.Increase,
            Amount = 500m,
            // An Increase without a term is due on its document date.
            DueDate = (DateOnly?)new DateOnly(2026, 10, 5),
            DocCode = "PC00001",
        });
        (await SettlementsOfAsync(stockEntry.Id)).Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            IncreaseEntryId = refundEntry.Id,
            DecreaseEntryId = stockEntry.Id,
            Amount = 500m,
            Origin = SettlementOrigin.Auto,
        });
        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Overpayment_leaves_unallocated_prepayment()
    {
        var customer = await NewPartnerAsync(isCustomer: true, isSupplier: false);

        var sale = await CreatePaidAsync(StockDirection.Out, "XBH", customer, 3, 1_000, paid: 5_000);

        var stockEntry = (await EntriesOfAsync(DebtSourceType.StockOut, sale.Id)).Single();
        var receiptEntry = (await EntriesOfAsync(DebtSourceType.Receipt, sale.AutoCashVoucherId!.Value)).Single();
        receiptEntry.Amount.Should().Be(5_000m);
        var auto = (await SettlementsOfAsync(stockEntry.Id)).Should().ContainSingle().Subject;
        auto.Should().BeEquivalentTo(new { DecreaseEntryId = receiptEntry.Id, Amount = 3_000m, Origin = SettlementOrigin.Auto });
        // The sale is settled in full; the receipt keeps 2,000 open as a prepayment.
        (await SettlementsOfAsync(receiptEntry.Id)).Sum(s => s.Amount).Should().Be(3_000m);
        (receiptEntry.Amount - auto.Amount).Should().Be(2_000m);
        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Reason_without_effect_still_creates_voucher_without_debt()
    {
        var other = await CreatePaidAsync(StockDirection.Out, "XKH", partnerId: null, 1, 500, paid: 200);
        other.AutoCashVoucherCode.Should().Be("PT00001");

        var receipt = await GetCashOkAsync(other.AutoCashVoucherId!.Value);
        receipt.Should().BeEquivalentTo(new
        {
            ReasonCode = "TPX",
            PartnerId = (Guid?)null,
            Total = 200m,
            FundId = _cashFundId,
            IsManaged = true,
        });
        (await EntriesOfAsync(DebtSourceType.StockOut, other.Id)).Should().BeEmpty();
        (await EntriesOfAsync(DebtSourceType.Receipt, receipt.Id)).Should().BeEmpty();
        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Edits_keep_the_code_and_follow_the_stock_voucher()
    {
        var customer = await NewPartnerAsync(isCustomer: true, isSupplier: false);
        var qtm2 = (await ReadDataAsync<FundDto>(await _branchClient.PostAsJsonAsync("/api/funds", new CreateFundRequest
        {
            Code = "QTM2", Name = "Quỹ tiền mặt 2", Kind = FundKind.Cash, IsActive = true,
        }))).Id;
        var sale = await CreatePaidAsync(StockDirection.Out, "XBH", customer, 3, 1_000, paid: 1_000);
        var before = await GetCashOkAsync(sale.AutoCashVoucherId!.Value);
        var entryBefore = (await EntriesOfAsync(DebtSourceType.Receipt, before.Id)).Single();

        // Date, fund, paid amount and handler change.
        var edit = UpdateRequestFrom(sale);
        edit.VoucherAt = Vn("2026-10-06 14:00");
        edit.FundId = qtm2;
        edit.PaidAmount = 1_500;
        edit.HandlerName = "Trần Thị B";
        var (status, edited, error) = await PutVoucherAsync(_branchClient, sale.Id, edit);
        status.Should().Be(HttpStatusCode.OK, error?.Message);
        edited!.Should().BeEquivalentTo(new { sale.AutoCashVoucherId, AutoCashVoucherCode = "PT00001" });

        var receipt = await GetCashOkAsync(before.Id);
        receipt.Should().BeEquivalentTo(new
        {
            Code = "PT00001",
            VoucherAt = Vn("2026-10-06 14:00"),
            FundId = qtm2,
            FundCode = "QTM2",
            PersonName = "Trần Thị B",
            Total = 1_500m,
            Status = DocumentStatus.Active,
        });
        receipt.Lines.Should().ContainSingle().Which.Should().BeEquivalentTo(new { before.Lines[0].Id, Amount = 1_500m });

        (await EntriesOfAsync(DebtSourceType.Receipt, receipt.Id)).Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            entryBefore.Id,
            Amount = 1_500m,
            PostedAt = Vn("2026-10-06 14:00"),
            DocDate = new DateOnly(2026, 10, 6),
        });
        var stockEntry = (await EntriesOfAsync(DebtSourceType.StockOut, sale.Id)).Single();
        (await SettlementsOfAsync(stockEntry.Id)).Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            DecreaseEntryId = entryBefore.Id,
            Amount = 1_500m,
            Origin = SettlementOrigin.Auto,
            EffectiveAt = Vn("2026-10-06 14:00"),
        });

        (await CashActivitiesOfAsync(receipt.Id)).Select(a => (a.Action, a.Description)).Should().Equal(
            (CashDocumentActivityAction.Created, "Theo phiếu PX00001"),
            (CashDocumentActivityAction.Updated, "Theo phiếu PX00001"));
        (await AutoVoucherActivitiesOfAsync(sale.Id)).Should().Equal("Tạo phiếu thu tự động PT00001");
        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Paid_amount_to_zero_deletes_the_auto_voucher_and_new_payment_gets_a_new_code()
    {
        var customer = await NewPartnerAsync(isCustomer: true, isSupplier: false);
        var sale = await CreatePaidAsync(StockDirection.Out, "XBH", customer, 3, 1_000, paid: 1_000);
        var firstId = sale.AutoCashVoucherId!.Value;
        var stockEntry = (await EntriesOfAsync(DebtSourceType.StockOut, sale.Id)).Single();

        // P3: nothing paid any more → the receipt is soft-deleted and its number stays used.
        var zero = UpdateRequestFrom(sale);
        zero.PaidAmount = 0;
        var (status, unpaid, error) = await PutVoucherAsync(_branchClient, sale.Id, zero);
        status.Should().Be(HttpStatusCode.OK, error?.Message);
        unpaid!.Should().BeEquivalentTo(new
        {
            PaidAmount = 0m,
            FundId = (Guid?)null,
            AutoCashVoucherId = (Guid?)null,
            AutoCashVoucherCode = (string?)null,
            AutoCashVoucherType = (CashVoucherType?)null,
        });
        (await AutoVouchersOfAsync(sale.Id)).Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Id = firstId,
            Code = "PT00001",
            IsDeleted = true,
        });
        (await GetCashAsync(_branchClient, firstId)).Status.Should().Be(HttpStatusCode.NotFound);
        (await EntriesOfAsync(DebtSourceType.Receipt, firstId)).Should().BeEmpty();
        (await SettlementsOfAsync(stockEntry.Id)).Should().BeEmpty();
        (await CashActivitiesOfAsync(firstId)).Select(a => (a.Action, a.Description)).Should().Equal(
            (CashDocumentActivityAction.Created, "Theo phiếu PX00001"),
            (CashDocumentActivityAction.Deleted, "Theo phiếu PX00001"));
        (await AutoVoucherActivitiesOfAsync(sale.Id)).Should().Equal(
            "Tạo phiếu thu tự động PT00001",
            "Xóa phiếu thu tự động PT00001");
        await AssertCashDebtInvariantsAsync();

        // A new payment gets a new voucher with the next number.
        var again = UpdateRequestFrom(unpaid!);
        again.PaidAmount = 300;
        var (againStatus, repaid, againError) = await PutVoucherAsync(_branchClient, sale.Id, again);
        againStatus.Should().Be(HttpStatusCode.OK, againError?.Message);
        repaid!.AutoCashVoucherCode.Should().Be("PT00002");
        repaid.AutoCashVoucherId.Should().NotBe(firstId);
        var secondEntry = (await EntriesOfAsync(DebtSourceType.Receipt, repaid.AutoCashVoucherId!.Value)).Single();
        (await SettlementsOfAsync(stockEntry.Id)).Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            DecreaseEntryId = secondEntry.Id,
            Amount = 300m,
            Origin = SettlementOrigin.Auto,
        });
        (await AutoVoucherActivitiesOfAsync(sale.Id)).Should().Equal(
            "Tạo phiếu thu tự động PT00001",
            "Xóa phiếu thu tự động PT00001",
            "Tạo phiếu thu tự động PT00002");
        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Cancel_restore_delete_cascade()
    {
        var customer = await NewPartnerAsync(isCustomer: true, isSupplier: false);
        var sale = await CreatePaidAsync(StockDirection.Out, "XBH", customer, 3, 1_000, paid: 1_000);
        var autoId = sale.AutoCashVoucherId!.Value;

        // Cancel: the receipt is cancelled with the sale and posts nothing.
        var cancelled = await ActionOkAsync(sale.Id, "cancel", sale.Version);
        cancelled.AutoCashVoucherCode.Should().Be("PT00001");
        (await GetCashOkAsync(autoId)).Status.Should().Be(DocumentStatus.Cancelled);
        (await EntriesOfAsync(DebtSourceType.Receipt, autoId)).Should().BeEmpty();
        (await EntriesOfAsync(DebtSourceType.StockOut, sale.Id)).Should().BeEmpty();
        await AssertCashDebtInvariantsAsync();

        // Restore: Active again with the same code, and the Auto settlement comes back (C7).
        var restored = await ActionOkAsync(sale.Id, "restore", cancelled.Version);
        restored.Should().BeEquivalentTo(new { AutoCashVoucherId = (Guid?)autoId, AutoCashVoucherCode = "PT00001" });
        (await GetCashOkAsync(autoId)).Status.Should().Be(DocumentStatus.Active);
        var stockEntry = (await EntriesOfAsync(DebtSourceType.StockOut, sale.Id)).Single();
        var receiptEntry = (await EntriesOfAsync(DebtSourceType.Receipt, autoId)).Should().ContainSingle().Subject;
        receiptEntry.Should().BeEquivalentTo(new { Direction = DebtDirection.Decrease, Amount = 1_000m });
        (await SettlementsOfAsync(stockEntry.Id)).Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            IncreaseEntryId = stockEntry.Id,
            DecreaseEntryId = receiptEntry.Id,
            Amount = 1_000m,
            Origin = SettlementOrigin.Auto,
        });
        await AssertCashDebtInvariantsAsync();

        // Delete: the receipt is soft-deleted with the sale.
        var deleted = await _branchClient.DeleteAsync($"/api/stock-vouchers/{sale.Id}?version={restored.Version}");
        deleted.StatusCode.Should().Be(HttpStatusCode.OK, await deleted.Content.ReadAsStringAsync());
        (await AutoVouchersOfAsync(sale.Id)).Should().ContainSingle().Which.IsDeleted.Should().BeTrue();
        (await EntriesOfAsync(DebtSourceType.Receipt, autoId)).Should().BeEmpty();
        (await EntriesOfAsync(DebtSourceType.StockOut, sale.Id)).Should().BeEmpty();

        (await CashActivitiesOfAsync(autoId)).Select(a => a.Action).Should().Equal(
            CashDocumentActivityAction.Created,
            CashDocumentActivityAction.Cancelled,
            CashDocumentActivityAction.Restored,
            CashDocumentActivityAction.Deleted);
        (await AutoVoucherActivitiesOfAsync(sale.Id)).Should().Equal(
            "Tạo phiếu thu tự động PT00001",
            "Xóa phiếu thu tự động PT00001");
        await AssertCashDebtInvariantsAsync();
    }

    private Task<Guid> NewPartnerAsync(bool isCustomer, bool isSupplier) =>
        CreatePartnerAsync("DT-" + Guid.NewGuid().ToString("N")[..8], isCustomer, isSupplier);

    /// A voucher of one untracked line at 10-05 09:00 in the test branch, paid in cash (TM, fund QTM); asserts 200.
    private async Task<StockVoucherDto> CreatePaidAsync(StockDirection type, string reasonCode, Guid? partnerId,
        decimal quantity, decimal unitPrice, decimal paid)
    {
        var product = await CreateInventoryProductAsync("SP-" + Guid.NewGuid().ToString("N")[..8], track: false);
        var request = VoucherRequest(type, await ReasonIdAsync(reasonCode), "2026-10-05 09:00",
            LineRequest(product, quantity, unitPrice));
        request.WarehouseId = _warehouseId;
        request.PartnerId = partnerId;
        request.HandlerName = Handler;
        request.PaymentMethodId = _cashMethodId;
        request.PaidAmount = paid;
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

    private async Task<CashVoucherDto> GetCashOkAsync(Guid id)
    {
        var (status, voucher, error) = await GetCashAsync(_branchClient, id);
        status.Should().Be(HttpStatusCode.OK, error?.Message);
        return voucher!;
    }

    /// Every automatic voucher of the stock voucher, deleted ones included, by code.
    private Task<List<CashVoucher>> AutoVouchersOfAsync(Guid stockVoucherId) =>
        InDbAsync(db => db.CashVouchers.IgnoreQueryFilters().AsNoTracking()
            .Where(v => v.SourceStockVoucherId == stockVoucherId)
            .OrderBy(v => v.Code)
            .ToListAsync());

    private Task<List<CashDocumentActivity>> CashActivitiesOfAsync(Guid documentId) =>
        InDbAsync(db => db.CashDocumentActivities.AsNoTracking()
            .Where(a => a.DocumentId == documentId)
            .OrderBy(a => a.OccurredAt)
            .ToListAsync());

    /// Descriptions of the stock voucher's AutoVoucherChanged activities, oldest first (deleted vouchers included).
    private Task<List<string>> AutoVoucherActivitiesOfAsync(Guid stockVoucherId) =>
        InDbAsync(db => db.StockVoucherActivities.IgnoreQueryFilters().AsNoTracking()
            .Where(a => a.StockVoucherId == stockVoucherId && a.Action == StockVoucherActivityAction.AutoVoucherChanged)
            .OrderBy(a => a.OccurredAt)
            .Select(a => a.Description)
            .ToListAsync());
}
