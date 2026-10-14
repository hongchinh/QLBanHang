using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Inventory.Common;
using OrderMgmt.Application.Organization.SampleData.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Entities.CashDebt;
using OrderMgmt.Domain.Entities.Inventory;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.CashDebt;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.SampleData;

[Collection(nameof(PostgresCollection))]
public class SampleDataRunnerStepTests : CashDebtTestBase
{
    public SampleDataRunnerStepTests(PostgresFixture pg) : base(pg) { }

    /// 09:00 + `minutes` (VN time) on the `day`-th day after the opening date.
    private static DateTimeOffset At(int day, int minutes = 0) =>
        VnTime.StartOfDay(TinyPlans.OpeningDate.AddDays(day)).AddHours(9).AddMinutes(minutes);

    /// A stock voucher step whose note is its key, so the test finds the voucher by its key.
    private static SampleStockVoucherStep Stock(string key, DateTimeOffset at, int? ownerIndex, StockDirection type,
        string reasonCode, string? partnerCode, string? paymentMethodCode, string? fundCode, params SampleStockLine[] lines) =>
        new(key, at, ownerIndex, type, reasonCode, "K1", partnerCode, paymentMethodCode, fundCode, key, lines);

    /// The branch's stock vouchers by note (= the step key).
    private Task<Dictionary<string, StockVoucher>> VouchersByKeyAsync(Guid branchId) =>
        InDbAsync(db => db.StockVouchers.AsNoTracking().Where(v => v.BranchId == branchId).ToDictionaryAsync(v => v.Note!));

    private const string PersonName = "Chị Lan";

    /// A fixed-amount cash voucher step without a partner. `description` defaults to the key, so the test finds the
    /// voucher by its line.
    private static SampleCashVoucherStep Fixed(string key, DateTimeOffset at, CashVoucherType type, string reasonCode,
        string fundCode, decimal amount, string? description = null) =>
        new(key, at, null, type, reasonCode, fundCode, null, PersonName, description ?? key, amount,
            Array.Empty<SampleAllocationTarget>());

    /// A cash voucher step that allocates to `targets`; its description is its key.
    private static SampleCashVoucherStep Allocate(string key, DateTimeOffset at, CashVoucherType type, string reasonCode,
        string fundCode, string partnerCode, params SampleAllocationTarget[] targets) =>
        new(key, at, null, type, reasonCode, fundCode, partnerCode, null, key, null, targets);

    private Task<Guid> PartnerIdAsync(string code) =>
        InDbAsync(db => db.Customers.Where(c => c.Code == code).Select(c => c.Id).SingleAsync());

    /// The branch's manual cash vouchers by the description of their single line.
    private Task<Dictionary<string, CashVoucher>> CashVouchersByLineAsync(Guid branchId) =>
        InDbAsync(db => db.CashVouchers.AsNoTracking().Include(v => v.Lines)
            .Where(v => v.BranchId == branchId && v.SourceStockVoucherId == null)
            .ToDictionaryAsync(v => v.Lines.Single().Description));

    /// The one debt entry of the branch's opening debt `docNo`.
    private Task<DebtEntry> OpeningEntryAsync(Guid branchId, string docNo) =>
        InDbAsync(db => db.DebtEntries.AsNoTracking()
            .SingleAsync(e => e.BranchId == branchId && e.SourceType == DebtSourceType.Opening && e.DocCode == docNo));

    private Task<SampleDataStatus> StatusAsync(Guid branchId) =>
        InDbAsync(db => db.Branches.IgnoreQueryFilters().Where(b => b.Id == branchId)
            .Select(b => b.SampleDataStatus).SingleAsync());

    [Fact]
    public async Task Stock_steps_post_vouchers_debt_and_automatic_receipts()
    {
        var branchId = await CreateBranchAsync("RT01");
        var adminId = await TinyPlans.AdminIdAsync(_factory);
        await CreateTestUserAsync("sd_sales_a", "Pass@123", RoleCodes.Sales);
        await CreateTestUserAsync("sd_sales_b", "Pass@123", RoleCodes.Sales);
        // The clone may hold other SALES users, so the owners come from the S5 query.
        var owners = await TinyPlans.SalesOwnersAsync(_factory);
        owners.Should().HaveCountGreaterThanOrEqualTo(2);

        var plan = TinyPlans.WithSteps(TinyPlans.OpeningDate,
            Stock("NMH-0001", At(1), null, StockDirection.In, "NMH", "DM-NCC01", null, null,
                new SampleStockLine("DM-SP01", 50m, 40_000m, 0m, 10m)),
            Stock("XBH-0001", At(2), 0, StockDirection.Out, "XBH", "DM-KH01", "TM", "QTM",
                new SampleStockLine("DM-SP01", 10m, 60_000m, 0m, 10m),
                new SampleStockLine("DM-SP30", 1m, 350_000m, 0m, 0m)),
            Stock("XBH-0002", At(3), 1, StockDirection.Out, "XBH", "DM-KH01", null, null,
                new SampleStockLine("DM-SP01", 20m, 60_000m, 0m, 0m)),
            Stock("XKH-0001", At(4), null, StockDirection.Out, "XKH", null, null, null,
                new SampleStockLine("DM-SP01", 1m, 0m, 0m, 0m)));

        var result = await TinyPlans.RunAsync(_factory, branchId, plan);

        result.Succeeded.Should().BeTrue(result.Error);
        result.StepsExecuted.Should().Be(4);
        result.StepsSkipped.Should().Be(0);

        var vouchers = await VouchersByKeyAsync(branchId);
        vouchers.Keys.Should().BeEquivalentTo("NMH-0001", "XBH-0001", "XBH-0002", "XKH-0001");
        vouchers["NMH-0001"].Code.Should().StartWith("PN");
        vouchers["NMH-0001"].Type.Should().Be(StockDirection.In);
        foreach (var key in new[] { "XBH-0001", "XBH-0002", "XKH-0001" })
        {
            vouchers[key].Code.Should().StartWith("PX", key);
            vouchers[key].Type.Should().Be(StockDirection.Out, key);
        }

        // XBH-0001 is paid in cash: its automatic receipt goes into QTM.
        var qtmId = await FundIdAsync("QTM", branchId);
        var xbh1 = vouchers["XBH-0001"];
        xbh1.Total.Should().Be(1_010_000m);
        var receipt = await InDbAsync(db => db.CashVouchers.AsNoTracking().SingleAsync(c => c.SourceStockVoucherId == xbh1.Id));
        receipt.Type.Should().Be(CashVoucherType.Receipt);
        receipt.FundId.Should().Be(qtmId);
        receipt.Total.Should().Be(1_010_000m);
        // A paid sale still posts its receivable, which its automatic receipt settles in full (Auto): the key names an
        // entry with nothing open.
        var paidEntry = (await EntriesOfAsync(DebtSourceType.StockOut, xbh1.Id)).Should().ContainSingle().Subject;
        paidEntry.Amount.Should().Be(1_010_000m);
        var receiptEntry = (await EntriesOfAsync(DebtSourceType.Receipt, receipt.Id)).Should().ContainSingle().Subject;
        var auto = (await SettlementsOfAsync(paidEntry.Id)).Should().ContainSingle().Subject;
        (auto.DecreaseEntryId, auto.Amount, auto.Origin).Should().Be((receiptEntry.Id, 1_010_000m, SettlementOrigin.Auto));

        // Credit documents post their debt.
        var xbh2 = vouchers["XBH-0002"];
        var receivable = (await EntriesOfAsync(DebtSourceType.StockOut, xbh2.Id)).Should().ContainSingle().Subject;
        receivable.Side.Should().Be(DebtSide.Receivable);
        receivable.Direction.Should().Be(DebtDirection.Increase);
        receivable.Amount.Should().Be(1_200_000m);
        var nmh = vouchers["NMH-0001"];
        var payable = (await EntriesOfAsync(DebtSourceType.StockIn, nmh.Id)).Should().ContainSingle().Subject;
        payable.Side.Should().Be(DebtSide.Payable);
        payable.Direction.Should().Be(DebtDirection.Increase);
        payable.Amount.Should().Be(2_200_000m);
        (await EntriesOfAsync(DebtSourceType.StockOut, vouchers["XKH-0001"].Id)).Should().BeEmpty();

        // Each step acts as its owner; OwnerIndex null is the requesting admin.
        (xbh1.OwnerUserId, xbh1.CreatedBy).Should().Be((owners[0], owners[0]));
        (xbh2.OwnerUserId, xbh2.CreatedBy).Should().Be((owners[1], owners[1]));
        (nmh.OwnerUserId, nmh.CreatedBy).Should().Be((adminId, adminId));
        vouchers["XKH-0001"].OwnerUserId.Should().Be(adminId);
        receipt.OwnerUserId.Should().Be(owners[0]);

        var warehouseId = await InDbAsync(db => db.Warehouses.Where(w => w.Code == "RT01-K1").Select(w => w.Id).SingleAsync());
        var productId = await InDbAsync(db => db.Products.Where(p => p.Code == "DM-SP01").Select(p => p.Id).SingleAsync());
        var balance = await InDbAsync(db => db.StockBalances.AsNoTracking()
            .SingleAsync(b => b.WarehouseId == warehouseId && b.ProductId == productId));
        balance.Quantity.Should().Be(100m + 50m - 10m - 20m - 1m);

        await AssertInvariantsAsync();
        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Owners_are_active_sales_users_by_username()
    {
        var branchId = await CreateBranchAsync("RT02");
        foreach (var username in new[] { "sd_o3", "sd_o1", "sd_o2", "sd_o4" })
            await CreateTestUserAsync(username, "Pass@123", RoleCodes.Sales);
        await InDbAsync(db => db.Users.Where(u => u.Username == "sd_o2")
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.Status, UserStatus.Disabled)));
        var ids = await InDbAsync(db => db.Users.Where(u => u.Username.StartsWith("sd_o"))
            .ToDictionaryAsync(u => u.Username, u => u.Id));

        var expected = await TinyPlans.SalesOwnersAsync(_factory);
        expected.Should().HaveCount(3);
        expected.Should().NotContain(ids["sd_o2"]);
        var otherSales = await InDbAsync(db => db.Users.AnyAsync(u => !u.IsDeleted && u.Status == UserStatus.Active
            && !u.Username.StartsWith("sd_o") && u.UserRoles.Any(r => r.Role.Code == RoleCodes.Sales)));
        if (!otherSales)
            expected.Should().Equal(ids["sd_o1"], ids["sd_o3"], ids["sd_o4"]);

        var steps = Enumerable.Range(0, 4)
            .Select(i => (SampleStep)Stock($"XBH-000{i + 1}", At(1, i), i, StockDirection.Out, "XBH", "DM-KH01", null, null,
                new SampleStockLine("DM-SP01", 1m, 60_000m, 0m, 0m)))
            .ToArray();

        var result = await TinyPlans.RunAsync(_factory, branchId, TinyPlans.WithSteps(TinyPlans.OpeningDate, steps));

        result.Succeeded.Should().BeTrue(result.Error);
        result.StepsExecuted.Should().Be(4);
        var vouchers = await VouchersByKeyAsync(branchId);
        new[] { "XBH-0001", "XBH-0002", "XBH-0003", "XBH-0004" }.Select(k => vouchers[k].OwnerUserId)
            .Should().Equal(expected[0], expected[1], expected[2], expected[0]);
        vouchers.Values.Should().NotContain(v => v.OwnerUserId == ids["sd_o2"]);

        await AssertInvariantsAsync();
        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Receipt_allocates_real_open_amounts()
    {
        var branchId = await CreateBranchAsync("RT03");
        var plan = TinyPlans.WithSteps(TinyPlans.OpeningDate,
            Stock("XBH-0001", At(1), null, StockDirection.Out, "XBH", "DM-KH01", null, null,
                new SampleStockLine("DM-SP01", 10m, 100_000m, 0m, 10m)),
            Allocate("TKH-0001", At(2), CashVoucherType.Receipt, "TKH", "VCB", "DM-KH01",
                new SampleAllocationTarget("OD-01", null), new SampleAllocationTarget("XBH-0001", 500_000m)));

        var result = await TinyPlans.RunAsync(_factory, branchId, plan);

        result.Succeeded.Should().BeTrue(result.Error);
        result.StepsExecuted.Should().Be(2);
        result.StepsSkipped.Should().Be(0);

        var receipt = (await CashVouchersByLineAsync(branchId)).Should().ContainSingle().Subject;
        receipt.Key.Should().Be("TKH-0001");
        receipt.Value.Type.Should().Be(CashVoucherType.Receipt);
        receipt.Value.FundId.Should().Be(await FundIdAsync("VCB", branchId));
        receipt.Value.PartnerId.Should().Be(await PartnerIdAsync("DM-KH01"));
        receipt.Value.Total.Should().Be(3_500_000m);

        var own = (await EntriesOfAsync(DebtSourceType.Receipt, receipt.Value.Id)).Should().ContainSingle().Subject;
        (own.Side, own.Direction, own.Amount).Should().Be((DebtSide.Receivable, DebtDirection.Decrease, 3_500_000m));
        var od01 = await OpeningEntryAsync(branchId, "HD-DK-01");
        var xbh = (await EntriesOfAsync(DebtSourceType.StockOut, (await VouchersByKeyAsync(branchId))["XBH-0001"].Id))
            .Should().ContainSingle().Subject;
        xbh.Amount.Should().Be(1_100_000m);

        var settlements = await SettlementsOfAsync(own.Id);
        settlements.Should().HaveCount(2)
            .And.OnlyContain(s => s.Origin == SettlementOrigin.AtVoucher && s.BatchId == receipt.Value.Id);
        settlements.ToDictionary(s => s.IncreaseEntryId, s => s.Amount).Should().BeEquivalentTo(
            new Dictionary<Guid, decimal> { [od01.Id] = 3_000_000m, [xbh.Id] = 500_000m });
        // OD-01 is fully settled.
        (await SettlementsOfAsync(od01.Id)).Sum(s => s.Amount).Should().Be(od01.Amount);

        await AssertInvariantsAsync();
        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Payment_and_fixed_amount_vouchers()
    {
        var branchId = await CreateBranchAsync("RT04");
        var plan = TinyPlans.WithSteps(TinyPlans.OpeningDate,
            Allocate("TNCC-0001", At(1), CashVoucherType.Payment, "TNCC", "VCB", "DM-NCC01",
                new SampleAllocationTarget("OD-02", null)),
            Fixed("CK-0001", At(2), CashVoucherType.Payment, "CK", "QTM", 1_000_000m, "Tiền điện"),
            Fixed("TK-0001", At(3), CashVoucherType.Receipt, "TK", "QTM", 2_000_000m));

        var result = await TinyPlans.RunAsync(_factory, branchId, plan);

        result.Succeeded.Should().BeTrue(result.Error);
        result.StepsExecuted.Should().Be(3);
        result.StepsSkipped.Should().Be(0);

        var vouchers = await CashVouchersByLineAsync(branchId);
        vouchers.Keys.Should().BeEquivalentTo("TNCC-0001", "Tiền điện", "TK-0001");
        var vcbId = await FundIdAsync("VCB", branchId);
        var qtmId = await FundIdAsync("QTM", branchId);

        var payment = vouchers["TNCC-0001"];
        (payment.Type, payment.FundId, payment.Total).Should().Be((CashVoucherType.Payment, vcbId, 2_000_000m));
        payment.PartnerId.Should().Be(await PartnerIdAsync("DM-NCC01"));
        var own = (await EntriesOfAsync(DebtSourceType.Payment, payment.Id)).Should().ContainSingle().Subject;
        (own.Side, own.Direction).Should().Be((DebtSide.Payable, DebtDirection.Decrease));
        var od02 = await OpeningEntryAsync(branchId, "HDM-DK-01");
        var settlement = (await SettlementsOfAsync(own.Id)).Should().ContainSingle().Subject;
        (settlement.IncreaseEntryId, settlement.Amount, settlement.Origin)
            .Should().Be((od02.Id, 2_000_000m, SettlementOrigin.AtVoucher));

        // Fixed amounts: one line, no partner, no debt entry and no allocation.
        var electricity = vouchers["Tiền điện"];
        (electricity.Type, electricity.FundId, electricity.Total).Should().Be((CashVoucherType.Payment, qtmId, 1_000_000m));
        (electricity.PartnerId, electricity.PersonName).Should().Be(((Guid?)null, PersonName));
        var other = vouchers["TK-0001"];
        (other.Type, other.FundId, other.Total).Should().Be((CashVoucherType.Receipt, qtmId, 2_000_000m));
        foreach (var (voucher, type) in new[] { (electricity, DebtSourceType.Payment), (other, DebtSourceType.Receipt) })
        {
            (await EntriesOfAsync(type, voucher.Id)).Should().BeEmpty();
            (await InDbAsync(db => db.DebtSettlements.AnyAsync(s => s.BatchId == voucher.Id))).Should().BeFalse();
        }

        await AssertInvariantsAsync();
        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Transfer_offset_and_cancel()
    {
        var branchId = await CreateBranchAsync("RT05");
        var plan = TinyPlans.WithSteps(TinyPlans.OpeningDate,
            new SampleFundTransferStep("CQ-0001", At(1), null, "QTM", "VCB", 5_000_000m, "Nộp tiền vào ngân hàng"),
            Stock("XBH-0001", At(2), null, StockDirection.Out, "XBH", "DM-DT01", null, null,
                new SampleStockLine("DM-SP01", 10m, 100_000m, 0m, 10m)),
            Stock("NMH-0001", At(3), null, StockDirection.In, "NMH", "DM-DT01", null, null,
                new SampleStockLine("DM-SP01", 10m, 50_000m, 0m, 10m)),
            new SampleDebtOffsetStep("BT-0001", At(4), null, "DM-DT01", "XBH-0001", "NMH-0001"),
            Fixed("TK-0001", At(5), CashVoucherType.Receipt, "TK", "QTM", 1_000_000m),
            new SampleCancelStep("HUY-0001", At(6), null, "TK-0001"));

        var result = await TinyPlans.RunAsync(_factory, branchId, plan);

        result.Succeeded.Should().BeTrue(result.Error);
        result.StepsExecuted.Should().Be(6);
        result.StepsSkipped.Should().Be(0);

        var transfer = await InDbAsync(db => db.FundTransfers.AsNoTracking().SingleAsync(t => t.BranchId == branchId));
        (transfer.FromFundId, transfer.ToFundId, transfer.Amount, transfer.Description).Should().Be(
            (await FundIdAsync("QTM", branchId), await FundIdAsync("VCB", branchId), 5_000_000m, "Nộp tiền vào ngân hàng"));

        var partnerId = await PartnerIdAsync("DM-DT01");
        var offset = await InDbAsync(db => db.DebtOffsetVouchers.AsNoTracking().SingleAsync(o => o.BranchId == branchId));
        (offset.PartnerId, offset.Amount, offset.Status).Should().Be((partnerId, 550_000m, DocumentStatus.Active));
        var stock = await VouchersByKeyAsync(branchId);
        var receivable = (await EntriesOfAsync(DebtSourceType.StockOut, stock["XBH-0001"].Id)).Should().ContainSingle().Subject;
        var payable = (await EntriesOfAsync(DebtSourceType.StockIn, stock["NMH-0001"].Id)).Should().ContainSingle().Subject;
        (receivable.Amount, payable.Amount).Should().Be((1_100_000m, 550_000m));
        var payableSettlement = (await SettlementsOfAsync(payable.Id)).Should().ContainSingle().Subject;
        (payableSettlement.Amount, payableSettlement.Origin, payableSettlement.BatchId)
            .Should().Be((550_000m, SettlementOrigin.Offset, offset.Id));
        (await SettlementsOfAsync(receivable.Id)).Sum(s => s.Amount).Should().Be(550_000m);

        var cancelled = (await CashVouchersByLineAsync(branchId)).Should().ContainSingle().Subject;
        cancelled.Key.Should().Be("TK-0001");
        cancelled.Value.Status.Should().Be(DocumentStatus.Cancelled);

        await AssertInvariantsAsync();
        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Zero_amount_steps_are_skipped()
    {
        var branchId = await CreateBranchAsync("RT06");
        var plan = TinyPlans.WithSteps(TinyPlans.OpeningDate,
            Allocate("TKH-0001", At(1), CashVoucherType.Receipt, "TKH", "VCB", "DM-KH01",
                new SampleAllocationTarget("OD-01", null)),
            Allocate("TKH-0002", At(2), CashVoucherType.Receipt, "TKH", "VCB", "DM-KH01",
                new SampleAllocationTarget("OD-01", null)));

        var result = await TinyPlans.RunAsync(_factory, branchId, plan);

        result.Succeeded.Should().BeTrue(result.Error);
        result.StepsExecuted.Should().Be(1);
        result.StepsSkipped.Should().Be(1);
        var receipt = (await CashVouchersByLineAsync(branchId)).Should().ContainSingle().Subject;
        receipt.Key.Should().Be("TKH-0001");
        receipt.Value.Total.Should().Be(3_000_000m);
        (await StatusAsync(branchId)).Should().Be(SampleDataStatus.Ready);

        await AssertInvariantsAsync();
        await AssertCashDebtInvariantsAsync();
    }

    /// A cash voucher's key names its own debt entry: a refund allocates to an advance receipt by its key, and a target
    /// with nothing open (a paid sale settled by its automatic receipt, a fully allocated receipt) counts as 0.
    [Fact]
    public async Task Cash_voucher_keys_name_their_own_entry()
    {
        var branchId = await CreateBranchAsync("RT07");
        var plan = TinyPlans.WithSteps(TinyPlans.OpeningDate,
            // An advance: a TKH receipt without allocations leaves its whole entry open.
            new SampleCashVoucherStep("TKH-0001", At(1), null, CashVoucherType.Receipt, "TKH", "VCB", "DM-KH01", null,
                "TKH-0001", 1_000_000m, Array.Empty<SampleAllocationTarget>()),
            Allocate("CHKH-0001", At(2), CashVoucherType.Payment, "CHKH", "VCB", "DM-KH01",
                new SampleAllocationTarget("TKH-0001", 400_000m)),
            Allocate("TKH-0002", At(3), CashVoucherType.Receipt, "TKH", "VCB", "DM-KH01",
                new SampleAllocationTarget("OD-01", null)),
            Stock("XBH-0001", At(4), null, StockDirection.Out, "XBH", "DM-KH01", "TM", "QTM",
                new SampleStockLine("DM-SP01", 1m, 100_000m, 0m, 0m)),
            Allocate("CHKH-0002", At(5), CashVoucherType.Payment, "CHKH", "VCB", "DM-KH01",
                new SampleAllocationTarget("TKH-0002", null)),
            Allocate("TKH-0003", At(6), CashVoucherType.Receipt, "TKH", "VCB", "DM-KH01",
                new SampleAllocationTarget("XBH-0001", null)));

        var result = await TinyPlans.RunAsync(_factory, branchId, plan);

        result.Succeeded.Should().BeTrue(result.Error);
        result.StepsExecuted.Should().Be(4);
        result.StepsSkipped.Should().Be(2);

        var vouchers = await CashVouchersByLineAsync(branchId);
        vouchers.Keys.Should().BeEquivalentTo("TKH-0001", "CHKH-0001", "TKH-0002");
        var advance = (await EntriesOfAsync(DebtSourceType.Receipt, vouchers["TKH-0001"].Id)).Should().ContainSingle().Subject;
        (advance.Direction, advance.Amount).Should().Be((DebtDirection.Decrease, 1_000_000m));
        var refund = vouchers["CHKH-0001"];
        refund.Total.Should().Be(400_000m);
        var refundEntry = (await EntriesOfAsync(DebtSourceType.Payment, refund.Id)).Should().ContainSingle().Subject;
        (refundEntry.Side, refundEntry.Direction).Should().Be((DebtSide.Receivable, DebtDirection.Increase));
        var settlement = (await SettlementsOfAsync(advance.Id)).Should().ContainSingle().Subject;
        (settlement.IncreaseEntryId, settlement.Amount, settlement.Origin, settlement.BatchId)
            .Should().Be((refundEntry.Id, 400_000m, SettlementOrigin.AtVoucher, refund.Id));

        await AssertInvariantsAsync();
        await AssertCashDebtInvariantsAsync();
    }

    /// S11: MaxAmount only caps. A target with less open than its MaxAmount allocates what is open, and a target with
    /// nothing open allocates nothing whatever its MaxAmount.
    [Fact]
    public async Task Max_amount_above_the_open_amount_allocates_what_is_open()
    {
        var branchId = await CreateBranchAsync("RT08");
        var plan = TinyPlans.WithSteps(TinyPlans.OpeningDate,
            Stock("XBH-0001", At(1), null, StockDirection.Out, "XBH", "DM-KH01", null, null,
                new SampleStockLine("DM-SP01", 10m, 100_000m, 0m, 10m)),
            Allocate("TKH-0001", At(2), CashVoucherType.Receipt, "TKH", "VCB", "DM-KH01",
                new SampleAllocationTarget("OD-01", null), new SampleAllocationTarget("XBH-0001", 5_000_000m)),
            Allocate("TKH-0002", At(3), CashVoucherType.Receipt, "TKH", "VCB", "DM-KH01",
                new SampleAllocationTarget("OD-01", 1_000_000m)));

        var result = await TinyPlans.RunAsync(_factory, branchId, plan);

        result.Succeeded.Should().BeTrue(result.Error);
        (result.StepsExecuted, result.StepsSkipped).Should().Be((2, 1));
        var receipt = (await CashVouchersByLineAsync(branchId)).Should().ContainSingle().Subject;
        receipt.Key.Should().Be("TKH-0001");
        receipt.Value.Total.Should().Be(4_100_000m);
        var own = (await EntriesOfAsync(DebtSourceType.Receipt, receipt.Value.Id)).Should().ContainSingle().Subject;
        var od01 = await OpeningEntryAsync(branchId, "HD-DK-01");
        var xbh = (await EntriesOfAsync(DebtSourceType.StockOut, (await VouchersByKeyAsync(branchId))["XBH-0001"].Id))
            .Should().ContainSingle().Subject;
        (await SettlementsOfAsync(own.Id)).ToDictionary(s => s.IncreaseEntryId, s => s.Amount).Should().BeEquivalentTo(
            new Dictionary<Guid, decimal> { [od01.Id] = 3_000_000m, [xbh.Id] = 1_100_000m });

        await AssertInvariantsAsync();
        await AssertCashDebtInvariantsAsync();
    }

    /// A target with nothing open (a cash sale settled by its automatic receipt) is left out of a voucher that also
    /// allocates to an open one.
    [Fact]
    public async Task Zero_open_target_is_left_out_of_a_mixed_receipt()
    {
        var branchId = await CreateBranchAsync("RT09");
        var plan = TinyPlans.WithSteps(TinyPlans.OpeningDate,
            Stock("XBH-0001", At(1), null, StockDirection.Out, "XBH", "DM-KH01", "TM", "QTM",
                new SampleStockLine("DM-SP01", 1m, 100_000m, 0m, 0m)),
            Allocate("TKH-0001", At(2), CashVoucherType.Receipt, "TKH", "VCB", "DM-KH01",
                new SampleAllocationTarget("XBH-0001", null), new SampleAllocationTarget("OD-01", null)));

        var result = await TinyPlans.RunAsync(_factory, branchId, plan);

        result.Succeeded.Should().BeTrue(result.Error);
        (result.StepsExecuted, result.StepsSkipped).Should().Be((2, 0));
        var receipt = (await CashVouchersByLineAsync(branchId)).Should().ContainSingle().Subject;
        receipt.Value.Total.Should().Be(3_000_000m);
        var own = (await EntriesOfAsync(DebtSourceType.Receipt, receipt.Value.Id)).Should().ContainSingle().Subject;
        var settlement = (await SettlementsOfAsync(own.Id)).Should().ContainSingle().Subject;
        (settlement.IncreaseEntryId, settlement.Amount)
            .Should().Be(((await OpeningEntryAsync(branchId, "HD-DK-01")).Id, 3_000_000m));

        await AssertInvariantsAsync();
        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Duplicate_target_keys_fail_the_step()
    {
        var branchId = await CreateBranchAsync("RT10");
        var plan = TinyPlans.WithSteps(TinyPlans.OpeningDate,
            Allocate("TKH-0001", At(1), CashVoucherType.Receipt, "TKH", "VCB", "DM-KH01",
                new SampleAllocationTarget("OD-01", null), new SampleAllocationTarget("OD-01", 1_000_000m)));

        var result = await TinyPlans.RunAsync(_factory, branchId, plan);

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be("TKH-0001: Bước TKH-0001 chọn khóa OD-01 hai lần.");
        (await CashVouchersByLineAsync(branchId)).Should().BeEmpty();
    }

    /// A cash step has a fixed amount or targets: never both (the targets would be ignored), never neither (the step
    /// would count as skipped).
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Cash_step_needs_a_fixed_amount_or_targets(bool both)
    {
        var branchId = await CreateBranchAsync("RT11");
        var step = new SampleCashVoucherStep("TKH-0001", At(1), null, CashVoucherType.Receipt, "TKH", "VCB", "DM-KH01",
            null, "TKH-0001", both ? 1_000_000m : null,
            both ? new[] { new SampleAllocationTarget("OD-01", null) } : Array.Empty<SampleAllocationTarget>());

        var result = await TinyPlans.RunAsync(_factory, branchId, TinyPlans.WithSteps(TinyPlans.OpeningDate, step));

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be(
            "TKH-0001: Bước TKH-0001 phải có đúng một trong hai: số tiền cố định hoặc chứng từ cần đối trừ.");
        (await CashVouchersByLineAsync(branchId)).Should().BeEmpty();
    }

    /// A skipped step wrote nothing, so a step naming it finds nothing open there: an allocation leaves it out, and a
    /// step left with nothing to do (allocate, cancel, offset) is skipped too.
    [Fact]
    public async Task Steps_naming_a_skipped_step_are_skipped()
    {
        var branchId = await CreateBranchAsync("RT12");
        var plan = TinyPlans.WithSteps(TinyPlans.OpeningDate,
            Allocate("TKH-0001", At(1), CashVoucherType.Receipt, "TKH", "VCB", "DM-KH01",
                new SampleAllocationTarget("OD-01", null)),
            // OD-01 is settled: skipped, so TKH-0002 names nothing.
            Allocate("TKH-0002", At(2), CashVoucherType.Receipt, "TKH", "VCB", "DM-KH01",
                new SampleAllocationTarget("OD-01", null)),
            new SampleCashVoucherStep("TKH-0003", At(3), null, CashVoucherType.Receipt, "TKH", "VCB", "DM-KH01", null,
                "TKH-0003", 1_000_000m, Array.Empty<SampleAllocationTarget>()),
            Allocate("CHKH-0001", At(4), CashVoucherType.Payment, "CHKH", "VCB", "DM-KH01",
                new SampleAllocationTarget("TKH-0002", null), new SampleAllocationTarget("TKH-0003", 400_000m)),
            Allocate("CHKH-0002", At(5), CashVoucherType.Payment, "CHKH", "VCB", "DM-KH01",
                new SampleAllocationTarget("TKH-0002", null)),
            new SampleCancelStep("HUY-0001", At(6), null, "TKH-0002"),
            new SampleDebtOffsetStep("BT-0001", At(7), null, "DM-KH01", "CHKH-0002", "OD-02"),
            new SampleDebtOffsetStep("BT-0002", At(8), null, "DM-KH01", "OD-01", "TKH-0002"));

        var result = await TinyPlans.RunAsync(_factory, branchId, plan);

        result.Succeeded.Should().BeTrue(result.Error);
        (result.StepsExecuted, result.StepsSkipped).Should().Be((3, 5));
        var vouchers = await CashVouchersByLineAsync(branchId);
        vouchers.Keys.Should().BeEquivalentTo("TKH-0001", "TKH-0003", "CHKH-0001");
        vouchers.Values.Should().OnlyContain(v => v.Status == DocumentStatus.Active);
        var refund = vouchers["CHKH-0001"];
        refund.Total.Should().Be(400_000m);
        var advance = (await EntriesOfAsync(DebtSourceType.Receipt, vouchers["TKH-0003"].Id)).Should().ContainSingle().Subject;
        var settlement = (await SettlementsOfAsync(advance.Id)).Should().ContainSingle().Subject;
        (settlement.Amount, settlement.BatchId).Should().Be((400_000m, refund.Id));
        (await InDbAsync(db => db.DebtOffsetVouchers.AnyAsync(o => o.BranchId == branchId))).Should().BeFalse();

        await AssertInvariantsAsync();
        await AssertCashDebtInvariantsAsync();
    }

    /// An offset naming a skipped step is skipped, but only once its other keys are known: a typo in one of them is
    /// still a plan error.
    [Theory]
    [InlineData("DM-KH99", "OD-01", "TKH-0002", "DM-KH99")]
    [InlineData("DM-KH01", "XBH-9999", "TKH-0002", "XBH-9999")]
    [InlineData("DM-KH01", "TKH-0002", "XBH-9999", "XBH-9999")]
    public async Task Offset_naming_a_skipped_step_still_checks_its_other_keys(string partnerCode, string receivableKey,
        string payableKey, string unknownKey)
    {
        var branchId = await CreateBranchAsync("RT14");
        var plan = TinyPlans.WithSteps(TinyPlans.OpeningDate,
            Allocate("TKH-0001", At(1), CashVoucherType.Receipt, "TKH", "VCB", "DM-KH01",
                new SampleAllocationTarget("OD-01", null)),
            Allocate("TKH-0002", At(2), CashVoucherType.Receipt, "TKH", "VCB", "DM-KH01",
                new SampleAllocationTarget("OD-01", null)),
            new SampleDebtOffsetStep("BT-0001", At(3), null, partnerCode, receivableKey, payableKey));

        var result = await TinyPlans.RunAsync(_factory, branchId, plan);

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be($"BT-0001: Kế hoạch tham chiếu khóa không tồn tại: {unknownKey}");
    }

    /// Cancelling an allocated receipt removes its settlements, so what it settled is open again for a later step.
    [Fact]
    public async Task Cancel_of_an_allocated_receipt_reopens_its_targets()
    {
        var branchId = await CreateBranchAsync("RT13");
        var plan = TinyPlans.WithSteps(TinyPlans.OpeningDate,
            Stock("XBH-0001", At(1), null, StockDirection.Out, "XBH", "DM-KH01", null, null,
                new SampleStockLine("DM-SP01", 10m, 100_000m, 0m, 10m)),
            Allocate("TKH-0001", At(2), CashVoucherType.Receipt, "TKH", "VCB", "DM-KH01",
                new SampleAllocationTarget("XBH-0001", null)),
            new SampleCancelStep("HUY-0001", At(3), null, "TKH-0001"),
            Allocate("TKH-0002", At(4), CashVoucherType.Receipt, "TKH", "VCB", "DM-KH01",
                new SampleAllocationTarget("XBH-0001", null)));

        var result = await TinyPlans.RunAsync(_factory, branchId, plan);

        result.Succeeded.Should().BeTrue(result.Error);
        (result.StepsExecuted, result.StepsSkipped).Should().Be((4, 0));
        var vouchers = await CashVouchersByLineAsync(branchId);
        vouchers["TKH-0001"].Status.Should().Be(DocumentStatus.Cancelled);
        (await EntriesOfAsync(DebtSourceType.Receipt, vouchers["TKH-0001"].Id)).Should().BeEmpty();
        var second = vouchers["TKH-0002"];
        (second.Status, second.Total).Should().Be((DocumentStatus.Active, 1_100_000m));
        var xbh = (await EntriesOfAsync(DebtSourceType.StockOut, (await VouchersByKeyAsync(branchId))["XBH-0001"].Id))
            .Should().ContainSingle().Subject;
        var settlement = (await SettlementsOfAsync(xbh.Id)).Should().ContainSingle().Subject;
        (settlement.Amount, settlement.BatchId).Should().Be((1_100_000m, second.Id));

        await AssertInvariantsAsync();
        await AssertCashDebtInvariantsAsync();
    }
}
