using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.CashDebt.CashVouchers.Models;
using OrderMgmt.Application.CashDebt.Funds.Models;
using OrderMgmt.Application.Organization.Branches.Models;
using OrderMgmt.Domain.Enums;

// Same namespace as CashDebtTestBase: ReportDataset is nested in it (partial) so it can call the base class's protected
// helpers on the test instance it is given.
namespace OrderMgmt.IntegrationTests.CashDebt;

/// Ids of the Phase 09 report dataset (Task 9.1). "Row" numbers follow the plan's dataset table.
public sealed class ReportIds
{
    public Guid MainBranchId { get; init; }   // the dataset's main branch (fresh, opening date 2026-10-01)
    public Guid BranchBId { get; init; }      // isolation branch: holds row 12 only

    public Guid Kh1Id { get; init; }
    public string Kh1Code { get; init; } = default!;
    public Guid Ncc1Id { get; init; }
    public string Ncc1Code { get; init; } = default!;

    public Guid QtmId { get; init; }          // default cash fund of the main branch
    public Guid Nh01Id { get; init; }         // default bank fund of the main branch

    public Guid OpeningHdOldId { get; init; }      // row 1: opening debt row
    public Guid OpeningHdOldEntryId { get; init; }
    public Guid OpeningTtOldId { get; init; }      // row 2: opening debt row
    public Guid OpeningTtOldEntryId { get; init; }
    public Guid SaleId { get; init; }              // row 4: XBH to KH1
    public Guid AutoReceiptId { get; init; }       // row 4: PT00001
    public Guid Receipt5Id { get; init; }          // row 5: PT00002
    public Guid Return6Id { get; init; }           // row 6: NTL PN00001
    public Guid Purchase7Id { get; init; }         // row 7: NMH PN00002
    public Guid AutoPaymentId { get; init; }       // row 7: PC00001
    public Guid Payment8Id { get; init; }          // row 8: PC00002
    public Guid Transfer9Id { get; init; }         // row 9: CQ00001
    public Guid Payment10Id { get; init; }         // row 10: PC00003
    public Guid Receipt11Id { get; init; }         // row 11: PT00003, cancelled
    public Guid BranchBSaleId { get; init; }       // row 12
}

public abstract partial class CashDebtTestBase
{
    /// The shared report dataset of Phase 09 (Task 9.1), created through the API.
    protected static class ReportDataset
    {
        public static readonly DateOnly OpeningDate = new(2026, 10, 1);

        /// Seeds a fresh main branch and a fresh branch B (so every seed gets PT00001, PN00001 … and its own KH1/NCC1)
        /// and returns their ids. The hand-computed expectations are in the plan (Phase 09, Task 9.1).
        public static async Task<ReportIds> SeedAsync(CashDebtTestBase t)
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var mainBranchId = await t.CreateBranchAsync("RD" + suffix);
            var branchBId = await t.CreateBranchAsync("RB" + suffix);
            var mainWarehouseId = await t.CreateWarehouseAsync(mainBranchId, "RD" + suffix);
            var branchBWarehouseId = await t.CreateWarehouseAsync(branchBId, "RB" + suffix);
            var client = t.CloneAdminClient();
            UseBranch(client, mainBranchId);

            var kh1Code = "KH1" + suffix;
            var ncc1Code = "NCC1" + suffix;
            var kh1 = await t.CreatePartnerAsync(kh1Code, isCustomer: true, isSupplier: false);
            var ncc1 = await t.CreatePartnerAsync(ncc1Code, isCustomer: false, isSupplier: true);

            // Opening data (rows 1–3). BranchService created QTM as the default cash fund; NH01 is the first bank fund,
            // so it becomes the default bank fund (used by the CK-paid purchase).
            await SetOpeningDateAsync(t, mainBranchId, OpeningDate);
            var qtm = await t.CashFundIdAsync(mainBranchId);
            var nh01 = await t.CreateBankFundAsync("NH01", mainBranchId);
            await SetFundOpeningAsync(client, qtm, 10_000m);
            await SetFundOpeningAsync(client, nh01, 20_000m);
            var (hdOld, hdOldEntry) = await t.CreateOpeningDebtAsync(client, kh1, DebtSide.Receivable,
                OpeningDebtKind.Debt, "HD-OLD", new DateOnly(2026, 9, 5), 5_000m, dueDate: new DateOnly(2026, 9, 20));
            var (ttOld, ttOldEntry) = await t.CreateOpeningDebtAsync(client, kh1, DebtSide.Receivable,
                OpeningDebtKind.Prepayment, "TT-OLD", new DateOnly(2026, 9, 15), 1_000m);

            var cashMethod = await PaymentMethodIdAsync(t, "TM");
            var bankMethod = await PaymentMethodIdAsync(t, "CK");

            // Row 4: sale 3,000 to KH1, paid 1,000 in cash → automatic receipt PT00001 into QTM.
            var sale = await PostStockAsync(t, client, StockDirection.Out, "XBH", "2026-10-03 09:00", mainWarehouseId,
                kh1, 3_000m, cashMethod, paid: 1_000m);
            sale.AutoCashVoucherCode.Should().Be("PT00001");

            // Row 5: receipt 4,000 from KH1 into QTM, allocated to HD-OLD.
            var receipt5Request = CashRequest(CashVoucherType.Receipt, await t.CashReasonIdAsync("TKH"), qtm,
                "2026-10-05 09:00", kh1, 4_000m);
            receipt5Request.Allocations = new List<AllocationRequest> { new() { EntryId = hdOldEntry, Amount = 4_000m } };
            var receipt5 = await t.CreateCashAsync(client, receipt5Request);

            // Row 6: customer return 500 from KH1, nothing paid.
            var return6 = await PostStockAsync(t, client, StockDirection.In, "NTL", "2026-10-08 09:00", mainWarehouseId,
                kh1, 500m, paymentMethodId: null, paid: 0m);

            // Row 7: purchase 6,000 from NCC1, paid 2,000 by bank → automatic payment PC00001 from NH01.
            var purchase7 = await PostStockAsync(t, client, StockDirection.In, "NMH", "2026-10-10 09:00", mainWarehouseId,
                ncc1, 6_000m, bankMethod, paid: 2_000m);
            purchase7.AutoCashVoucherCode.Should().Be("PC00001");
            purchase7.FundId.Should().Be(nh01);

            // Row 8: payment 3,000 to NCC1 from NH01.
            var payment8 = await t.CreateCashAsync(client, CashRequest(CashVoucherType.Payment,
                await t.CashReasonIdAsync("TNCC"), nh01, "2026-10-12 09:00", ncc1, 3_000m));

            // Row 9: transfer 5,000 NH01 → QTM.
            var (transferStatus, transfer9, transferError) =
                await PostTransferAsync(client, TransferRequest(nh01, qtm, 5_000m, "2026-10-15 09:00"));
            transferStatus.Should().Be(HttpStatusCode.OK, transferError?.Message);

            // Row 10: other payment 700 from QTM, no partner.
            var payment10Request = CashRequest(CashVoucherType.Payment, await t.CashReasonIdAsync("CK"), qtm,
                "2026-10-16 09:00", partnerId: null, 700m);
            payment10Request.PersonName = "Nguyễn A";
            var payment10 = await t.CreateCashAsync(client, payment10Request);

            // Row 11: receipt 300 from KH1 into QTM, then cancelled.
            var receipt11 = await t.CreateCashAsync(client, CashRequest(CashVoucherType.Receipt,
                await t.CashReasonIdAsync("TKH"), qtm, "2026-10-18 09:00", kh1, 300m));
            var (cancelStatus, _, cancelError) = await CancelCashAsync(client, receipt11.Id,
                new CashDocumentActionRequest { Version = receipt11.Version });
            cancelStatus.Should().Be(HttpStatusCode.OK, cancelError?.Message);

            // Row 12: branch B sells 9,999 to KH1; it must never appear in a main-branch report.
            var branchBClient = t.CloneAdminClient();
            UseBranch(branchBClient, branchBId);
            var branchBSale = await PostStockAsync(t, branchBClient, StockDirection.Out, "XBH", "2026-10-03 09:00",
                branchBWarehouseId, kh1, 9_999m, paymentMethodId: null, paid: 0m);

            return new ReportIds
            {
                MainBranchId = mainBranchId,
                BranchBId = branchBId,
                Kh1Id = kh1,
                Kh1Code = kh1Code,
                Ncc1Id = ncc1,
                Ncc1Code = ncc1Code,
                QtmId = qtm,
                Nh01Id = nh01,
                OpeningHdOldId = hdOld,
                OpeningHdOldEntryId = hdOldEntry,
                OpeningTtOldId = ttOld,
                OpeningTtOldEntryId = ttOldEntry,
                SaleId = sale.Id,
                AutoReceiptId = sale.AutoCashVoucherId!.Value,
                Receipt5Id = receipt5.Id,
                Return6Id = return6.Id,
                Purchase7Id = purchase7.Id,
                AutoPaymentId = purchase7.AutoCashVoucherId!.Value,
                Payment8Id = payment8.Id,
                Transfer9Id = transfer9!.Id,
                Payment10Id = payment10.Id,
                Receipt11Id = receipt11.Id,
                BranchBSaleId = branchBSale.Id,
            };
        }

        public static async Task SetOpeningDateAsync(CashDebtTestBase t, Guid branchId, DateOnly date)
        {
            var response = await t._client.PutAsJsonAsync($"/api/branches/{branchId}/opening-date",
                new SetOpeningDateRequest { OpeningDate = date });
            response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        }

        public static async Task SetFundOpeningAsync(HttpClient client, Guid fundId, decimal amount)
        {
            var response = await client.PutAsJsonAsync($"/api/funds/{fundId}/opening-balance",
                new SetFundOpeningBalanceRequest { Amount = amount });
            response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        }

        public static Task<Guid> PaymentMethodIdAsync(CashDebtTestBase t, string code) =>
            t.InDbAsync(db => db.PaymentMethods.Where(m => m.Code == code).Select(m => m.Id).SingleAsync());

        /// A one-line stock voucher (untracked product, quantity 1 × amount) in the client's branch; asserts 200.
        public static async Task<Application.Inventory.StockVouchers.Models.StockVoucherDto> PostStockAsync(
            CashDebtTestBase t, HttpClient client, StockDirection type, string reasonCode, string at, Guid warehouseId,
            Guid partnerId, decimal amount, Guid? paymentMethodId, decimal paid)
        {
            var product = await t.CreateInventoryProductAsync("SP-" + Guid.NewGuid().ToString("N")[..8], track: false);
            var request = t.VoucherRequest(type, await t.ReasonIdAsync(reasonCode), at, LineRequest(product, 1, amount));
            request.WarehouseId = warehouseId;
            request.PartnerId = partnerId;
            request.PaymentMethodId = paymentMethodId;
            request.PaidAmount = paid;
            var (status, voucher, error) = await PostVoucherAsync(client, request);
            status.Should().Be(HttpStatusCode.OK, error?.Message);
            return voucher!;
        }
    }
}
