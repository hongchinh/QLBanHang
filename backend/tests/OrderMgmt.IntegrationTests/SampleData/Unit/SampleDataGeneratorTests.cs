using System.Globalization;
using System.Text.Json;
using FluentAssertions;
using OrderMgmt.Application.Organization.SampleData.Generation;
using OrderMgmt.Application.Organization.SampleData.Models;
using OrderMgmt.Domain.Enums;
using Xunit;

namespace OrderMgmt.IntegrationTests.SampleData.Unit;

public class SampleDataGeneratorTests
{
    private static readonly DateOnly Today = new(2026, 10, 14);
    private static readonly TimeSpan Vn = TimeSpan.FromHours(7);
    private const decimal FundMargin = 5_000_000m;
    private const string DualRolePartner = "DM-DT01";

    internal static SampleDataPlan Generate(string code = "DEMO01", int ownerCount = 3, DateOnly? today = null,
        bool netExcludesVat = false) =>
        new SampleDataGenerator().Generate(new SampleDataGeneratorInput(code, today ?? Today, ownerCount, netExcludesVat));

    internal static DateOnly ParseDate(string text) => DateOnly.ParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    internal static DateOnly VnDate(SampleStep step) => DateOnly.FromDateTime(step.At.ToOffset(Vn).DateTime);

    /// The calendar months that lie entirely inside the window.
    internal static List<(int Year, int Month)> FullMonths(SampleDataPlan plan)
    {
        var months = new List<(int, int)>();
        for (var first = new DateOnly(plan.OpeningDate.Year, plan.OpeningDate.Month, 1); first <= plan.LastDocumentDate;
             first = first.AddMonths(1))
        {
            if (first >= plan.OpeningDate && first.AddMonths(1).AddDays(-1) <= plan.LastDocumentDate)
                months.Add((first.Year, first.Month));
        }
        return months;
    }

    private static List<string> Serialize(SampleDataPlan plan) =>
        plan.Steps.Select(s => JsonSerializer.Serialize(s, s.GetType())).ToList();

    [Fact]
    public void Steps_are_strictly_increasing_inside_the_window()
    {
        var plan = Generate();

        plan.OpeningDate.Should().Be(new DateOnly(2026, 7, 1));
        plan.LastDocumentDate.Should().Be(new DateOnly(2026, 10, 13));
        AssertTimes(plan);
    }

    [Fact]
    public void Keys_are_unique_and_targets_point_backwards() => AssertKeysAndTargets(Generate());

    [Fact]
    public void Reasons_partners_and_funds_are_consistent() => AssertReasonsPartnersAndFunds(Generate());

    [Fact]
    public void Owners_follow_the_customer() => AssertOwners(Generate());

    [Fact]
    public void Stock_never_goes_negative() => AssertStockNeverNegative(Generate());

    [Fact]
    public void Fund_estimates_keep_the_margin() => AssertFundsKeepTheMargin(Generate());

    [Fact]
    public void Credit_estimates_stay_under_80_percent() => AssertCreditUnder80Percent(Generate());

    [Fact]
    public void Monthly_volumes_match_the_spec()
    {
        var plan = Generate();

        FullMonths(plan).Should().Equal((2026, 7), (2026, 8), (2026, 9));
        AssertMonthlyVolumes(plan);
    }

    [Fact]
    public void Once_per_period_items() => AssertOncePerPeriodItems(Generate());

    [Fact]
    public void Sales_follow_the_price_discount_and_payment_rules() => AssertSalesRules(Generate());

    [Fact]
    public void Purchases_come_from_a_supplier_of_their_products_near_cost() => AssertPurchaseRules(Generate());

    [Fact]
    public void Receipts_gather_several_documents_and_one_or_two_pay_part() => AssertReceiptRules(Generate());

    [Fact]
    public void Sales_placed_relative_to_T_fall_inside_the_shortest_window()
    {
        // T − OpeningDate is 89 days at least (T on 1 May after a 28-day February); a sale placed CreditDays + overdue days
        // before T must not fall before the opening date.
        var shortest = Enumerable.Range(0, 4 * 366)
            .Select(d => new DateOnly(2025, 1, 1).AddDays(d))
            .Min(t => t.DayNumber - SampleCalendar.OpeningDate(t).DayNumber);
        var creditDays = SampleCatalog.BuildPartners(1).ToDictionary(p => p.Code, p => p.CreditDays ?? 0);

        shortest.Should().Be(89);
        foreach (var (habit, overdueDays) in new[]
                 {
                     (PaymentHabit.Late, SampleDataVolumes.LateSaleOverdueDays),
                     (PaymentHabit.BadDebt, SampleDataVolumes.BadDebtSaleOverdueDays),
                 })
        {
            var customer = SampleCatalog.PlacedSaleCustomer(habit).Code;
            (creditDays[customer] + overdueDays).Should().BeLessThanOrEqualTo(shortest, customer);
        }
    }

    [Fact]
    public void Is_deterministic()
    {
        var first = Serialize(Generate());

        first.Should().NotBeEmpty();
        first.Should().Equal(Serialize(Generate()));
    }

    [Fact]
    public void Depends_on_branch_code() => Serialize(Generate("DEMO02")).Should().NotEqual(Serialize(Generate()));

    /// Every invariant and the monthly volumes of every full month, for other codes, dates (February, windows opening on
    /// any weekday), owner counts and the NetExcludesVat inventory setting.
    [Theory]
    [InlineData("DEMO01", "2026-10-14", 1, false)]
    [InlineData("DEMO01", "2026-10-14", 3, true)]
    [InlineData("DEMO03", "2026-03-01", 3, false)]
    [InlineData("CN021", "2026-06-01", 3, false)]
    [InlineData("CN024", "2026-07-31", 3, false)]
    [InlineData("CN-DEMO", "2026-12-31", 2, true)]
    [InlineData("B011", "2027-06-01", 3, false)]
    public void Invariants_hold_for_several_inputs(string code, string today, int owners, bool netExcludesVat)
    {
        var plan = Generate(code, owners, ParseDate(today), netExcludesVat);

        plan.Steps.Should().OnlyContain(s => s.OwnerIndex == null || (s.OwnerIndex >= 0 && s.OwnerIndex < owners));
        AssertAllInvariants(plan, netExcludesVat);
        FullMonths(plan).Should().HaveCountGreaterThanOrEqualTo(2);
        AssertMonthlyVolumes(plan);
    }

    internal static void AssertAllInvariants(SampleDataPlan plan, bool netExcludesVat = false)
    {
        AssertTimes(plan);
        AssertKeysAndTargets(plan);
        AssertReasonsPartnersAndFunds(plan);
        AssertOwners(plan);
        AssertStockNeverNegative(plan, netExcludesVat);
        AssertFundsKeepTheMargin(plan, netExcludesVat);
        AssertCreditUnder80Percent(plan, netExcludesVat);
        AssertAllocationsArePositive(plan, netExcludesVat);
        AssertOncePerPeriodItems(plan);
        AssertSalesRules(plan);
        AssertPurchaseRules(plan);
        AssertReceiptRules(plan);
    }

    internal static void AssertTimes(SampleDataPlan plan)
    {
        plan.Steps.Should().NotBeEmpty();
        DateTimeOffset? previous = null;
        foreach (var step in plan.Steps)
        {
            if (previous is { } before)
                step.At.Should().BeAfter(before, step.Key);
            step.At.Offset.Should().Be(TimeSpan.Zero, step.Key);
            var date = VnDate(step);
            date.Should().BeOnOrAfter(plan.OpeningDate, step.Key).And.BeOnOrBefore(plan.LastDocumentDate, step.Key);
            date.DayOfWeek.Should().NotBe(DayOfWeek.Sunday, step.Key);
            step.At.ToOffset(Vn).TimeOfDay.TotalMinutes.Should().BeInRange(8 * 60, 17 * 60 + 30, step.Key);
            previous = step.At;
        }
    }

    internal static void AssertKeysAndTargets(SampleDataPlan plan)
    {
        plan.Steps.Should().NotBeEmpty();
        plan.OpeningDebts.Select(d => d.Key).Concat(plan.Steps.Select(s => s.Key)).Should().OnlyHaveUniqueItems();

        var openings = plan.OpeningDebts.ToDictionary(d => d.Key);
        var earlier = new Dictionary<string, SampleStep>();
        foreach (var step in plan.Steps)
        {
            switch (step)
            {
                case SampleCashVoucherStep c:
                    c.Targets.Select(t => t.TargetKey).Should().OnlyHaveUniqueItems($"{c.Key} allocates each item once");
                    // The allocation validator rejects mixed directions: receipts and supplier payments settle invoices
                    // (Increase items), the refund settles the prepayment (a Decrease item).
                    var expectIncrease = c.ReasonCode switch
                    {
                        "TKH" or "TNCC" => true,
                        "CHKH" => false,
                        _ when c.Targets.Count == 0 => true,
                        _ => throw new InvalidOperationException($"{c.Key}: {c.ReasonCode} must not allocate."),
                    };
                    foreach (var target in c.Targets)
                    {
                        var because = $"{c.Key} targets {target.TargetKey}";
                        var isOpening = openings.TryGetValue(target.TargetKey, out var opening);
                        (isOpening || earlier.ContainsKey(target.TargetKey)).Should().BeTrue(because + ", an earlier key");
                        if (isOpening)
                        {
                            opening!.PartnerCode.Should().Be(c.PartnerCode, because);
                            opening.Side.Should().Be(c.ReasonCode == "TNCC" ? DebtSide.Payable : DebtSide.Receivable, because);
                            (opening.Kind == OpeningDebtKind.Debt).Should().Be(expectIncrease, because + " in the right direction");
                        }
                        else
                        {
                            var stock = earlier[target.TargetKey].Should().BeOfType<SampleStockVoucherStep>(because).Subject;
                            stock.PartnerCode.Should().Be(c.PartnerCode, because);
                            expectIncrease.Should().BeTrue(because + ": only invoices of documents are allocated");
                            stock.ReasonCode.Should().Be(c.ReasonCode == "TKH" ? "XBH" : "NMH", because);
                        }
                    }
                    break;
                case SampleDebtOffsetStep o:
                    o.PartnerCode.Should().Be(DualRolePartner);
                    AssertEarlierDocument(earlier, o.ReceivableTargetKey, "XBH", DualRolePartner, o.Key);
                    AssertEarlierDocument(earlier, o.PayableTargetKey, "NMH", DualRolePartner, o.Key);
                    break;
                case SampleCancelStep x:
                    earlier.Should().ContainKey(x.TargetKey, $"{x.Key} cancels an earlier step");
                    earlier[x.TargetKey].Should().BeOfType<SampleCashVoucherStep>()
                        .Which.Should().Match<SampleCashVoucherStep>(c => c.ReasonCode == "TK" && c.Type == CashVoucherType.Receipt);
                    break;
            }
            earlier.Add(step.Key, step);
        }
    }

    private static void AssertEarlierDocument(Dictionary<string, SampleStep> earlier, string key, string reason, string partner,
        string because)
    {
        earlier.Should().ContainKey(key, because);
        var stock = earlier[key].Should().BeOfType<SampleStockVoucherStep>(because).Subject;
        stock.ReasonCode.Should().Be(reason, because);
        stock.PartnerCode.Should().Be(partner, because);
    }

    internal static void AssertReasonsPartnersAndFunds(SampleDataPlan plan)
    {
        var partners = plan.Partners.ToDictionary(p => p.Code);
        var products = plan.Products.Select(p => p.Code).ToHashSet();
        var funds = new[] { "QTM", "VCB", "TCB" };

        plan.Steps.Should().NotBeEmpty();
        foreach (var step in plan.Steps)
        {
            switch (step)
            {
                case SampleStockVoucherStep s:
                    var (type, role) = s.ReasonCode switch
                    {
                        "NMH" => (StockDirection.In, "supplier"),
                        "XTL" => (StockDirection.Out, "supplier"),
                        "XBH" => (StockDirection.Out, "customer"),
                        "NTL" => (StockDirection.In, "customer"),
                        "XKH" => (StockDirection.Out, "none"),
                        _ => throw new InvalidOperationException($"{s.Key}: unexpected stock reason {s.ReasonCode}."),
                    };
                    s.Type.Should().Be(type, s.Key);
                    AssertPartnerRole(partners, s.PartnerCode, role, s.Key);
                    if (role == "none")
                        s.PaymentMethodCode.Should().BeNull(s.Key);
                    AssertPayment(s.PaymentMethodCode, s.FundCode, s.Key);
                    new[] { "K1", "K2" }.Should().Contain(s.WarehouseKey, s.Key);
                    s.Lines.Should().NotBeEmpty(s.Key);
                    s.Lines.Should().OnlyContain(l => products.Contains(l.ProductCode) && l.Quantity > 0 && l.UnitPrice >= 0, s.Key);
                    break;
                case SampleCashVoucherStep c:
                    var (cashType, cashRole, allocates) = c.ReasonCode switch
                    {
                        "TKH" => (CashVoucherType.Receipt, "customer", true),
                        "CHKH" => (CashVoucherType.Payment, "customer", true),
                        "TNCC" => (CashVoucherType.Payment, "supplier", true),
                        "TK" => (CashVoucherType.Receipt, "none", false),
                        "CK" => (CashVoucherType.Payment, "none", false),
                        _ => throw new InvalidOperationException($"{c.Key}: unexpected cash reason {c.ReasonCode}."),
                    };
                    c.Type.Should().Be(cashType, c.Key);
                    AssertPartnerRole(partners, c.PartnerCode, cashRole, c.Key);
                    funds.Should().Contain(c.FundCode, c.Key);
                    if (allocates)
                    {
                        c.Targets.Should().NotBeEmpty(c.Key);
                        c.FixedAmount.Should().BeNull(c.Key);
                    }
                    else
                    {
                        c.Targets.Should().BeEmpty(c.Key);
                        c.FixedAmount.Should().BeGreaterThan(0, c.Key);
                    }
                    break;
                case SampleFundTransferStep t:
                    funds.Should().Contain(new[] { t.FromFundCode, t.ToFundCode }, t.Key);
                    t.FromFundCode.Should().NotBe(t.ToFundCode, t.Key);
                    t.Amount.Should().BeGreaterThan(0, t.Key);
                    break;
                case SampleDebtOffsetStep o:
                    partners[o.PartnerCode].IsCustomer.Should().BeTrue(o.Key);
                    partners[o.PartnerCode].IsSupplier.Should().BeTrue(o.Key);
                    break;
            }
        }
    }

    private static void AssertPartnerRole(Dictionary<string, SamplePartnerSpec> partners, string? code, string role, string key)
    {
        if (role == "none")
        {
            code.Should().BeNull(key);
            return;
        }
        code.Should().NotBeNull(key);
        partners.Should().ContainKey(code!, key);
        (role == "supplier" ? partners[code!].IsSupplier : partners[code!].IsCustomer).Should().BeTrue($"{key} needs a {role}");
    }

    private static void AssertPayment(string? method, string? fund, string key)
    {
        switch (method)
        {
            case null:
                fund.Should().BeNull(key);
                break;
            case "TM":
                fund.Should().Be("QTM", key);
                break;
            case "CK":
                new string?[] { "VCB", "TCB" }.Should().Contain(fund, key);
                break;
            default:
                throw new InvalidOperationException($"{key}: unexpected payment method {method}.");
        }
    }

    internal static void AssertOwners(SampleDataPlan plan)
    {
        var owners = plan.Partners.ToDictionary(p => p.Code, p => p.OwnerIndex);
        plan.Steps.Should().NotBeEmpty();
        foreach (var step in plan.Steps)
        {
            var customerReason = step switch
            {
                SampleStockVoucherStep s when s.ReasonCode is "XBH" or "NTL" => s.PartnerCode,
                SampleCashVoucherStep c when c.ReasonCode is "TKH" or "CHKH" => c.PartnerCode,
                _ => null,
            };
            step.OwnerIndex.Should().Be(customerReason is null ? null : owners[customerReason], step.Key);
        }
    }

    // Replays a non-empty plan, so a check never passes on no steps at all.
    private static void Replay(SampleDataPlan plan, bool netExcludesVat, Action<SampleStep, PlanReplay> afterStep)
    {
        plan.Steps.Should().NotBeEmpty();
        PlanReplay.Run(plan, afterStep, netExcludesVat);
    }

    internal static void AssertStockNeverNegative(SampleDataPlan plan, bool netExcludesVat = false) =>
        Replay(plan, netExcludesVat, (step, replay) =>
        {
            var negative = replay.Stock.Where(kv => kv.Value < 0).Select(kv => $"{kv.Key}: {kv.Value}").ToList();
            negative.Should().BeEmpty($"stock after {step.Key}");
        });

    internal static void AssertFundsKeepTheMargin(SampleDataPlan plan, bool netExcludesVat = false)
    {
        plan.Funds.Should().OnlyContain(f => f.OpeningAmount >= FundMargin);
        Replay(plan, netExcludesVat, (step, replay) =>
        {
            var low = replay.Funds.Where(kv => kv.Value < FundMargin).Select(kv => $"{kv.Key}: {kv.Value:N0}").ToList();
            low.Should().BeEmpty($"funds after {step.Key}");
        });
    }

    /// The runner skips a receipt, payment or offset whose computed amount is 0; the plan never relies on that.
    internal static void AssertAllocationsArePositive(SampleDataPlan plan, bool netExcludesVat = false) =>
        Replay(plan, netExcludesVat, (step, replay) =>
        {
            if (step is SampleCashVoucherStep { Targets.Count: > 0 } or SampleDebtOffsetStep)
                replay.LastAmount.Should().BeGreaterThan(0m, step.Key);
        });

    internal static void AssertCreditUnder80Percent(SampleDataPlan plan, bool netExcludesVat = false)
    {
        plan.CreditLimits.Should().NotBeEmpty();
        var creditSales = 0;
        Replay(plan, netExcludesVat, (step, replay) =>
        {
            if (step is not SampleStockVoucherStep { ReasonCode: "XBH" } sale)
                return;
            if (plan.CreditLimits.Any(l => l.PartnerCode == sale.PartnerCode) && sale.PaymentMethodCode == null)
                creditSales++;
            foreach (var (code, limit) in replay.CreditLimits)
                replay.CreditBalance(code).Should().BeLessThanOrEqualTo(limit * 0.8m, $"{code} after {step.Key}");
        });
        creditSales.Should().BeGreaterThan(0, "limited customers also buy on credit");
    }

    /// The spec volumes of every full month of the window.
    internal static void AssertMonthlyVolumes(SampleDataPlan plan)
    {
        var cancelled = plan.Steps.OfType<SampleCancelStep>().Select(x => x.TargetKey).ToHashSet();
        foreach (var (year, month) in FullMonths(plan))
        {
            var steps = plan.Steps.Where(s => VnDate(s).Year == year && VnDate(s).Month == month).ToList();
            var stock = steps.OfType<SampleStockVoucherStep>().ToList();
            var cash = steps.OfType<SampleCashVoucherStep>().ToList();
            var transfers = steps.OfType<SampleFundTransferStep>().ToList();
            var because = $"month {year}-{month:00}";

            stock.Count(s => s.ReasonCode == "XBH").Should().BeInRange(35, 45, because);
            stock.Count(s => s.ReasonCode == "NMH").Should().BeInRange(8, 14, because);
            stock.Count(s => s.ReasonCode == "NTL").Should().BeInRange(1, 2, because);
            stock.Count(s => s.ReasonCode == "XTL").Should().Be(1, because);
            stock.Count(s => s.ReasonCode == "XKH").Should().Be(1, because);
            cash.Count(c => c.ReasonCode == "CK").Should().BeInRange(5, 7, because);
            cash.Count(c => c.ReasonCode == "TK" && !cancelled.Contains(c.Key)).Should().Be(1, because);
            transfers.Count(t => t.FromFundCode == "QTM" && t.ToFundCode == "VCB").Should().BeInRange(4, 6, because);
            transfers.Count(t => t.FromFundCode == "VCB" && t.ToFundCode == "TCB").Should().Be(1, because);
        }
    }

    internal static void AssertOncePerPeriodItems(SampleDataPlan plan)
    {
        plan.Steps.OfType<SampleDebtOffsetStep>().Should().ContainSingle();
        plan.Steps.OfType<SampleCashVoucherStep>().Where(c => c.ReasonCode == "CHKH").Should().ContainSingle()
            .Which.Targets.Should().ContainSingle().Which.MaxAmount.Should().Be(5_000_000m);
        plan.Steps.OfType<SampleCancelStep>().Should().ContainSingle();
        plan.Steps.OfType<SampleCashVoucherStep>().Where(c => c.ReasonCode == "TK").Should().HaveCountGreaterThan(1);
    }

    internal static void AssertSalesRules(SampleDataPlan plan)
    {
        var products = plan.Products.ToDictionary(p => p.Code);
        var groups = plan.Partners.ToDictionary(p => p.Code, p => p.Group);
        var sales = plan.Steps.OfType<SampleStockVoucherStep>().Where(s => s.ReasonCode == "XBH").ToList();

        foreach (var sale in sales)
        {
            var group = groups[sale.PartnerCode!];
            var discount = group switch
            {
                CustomerGroup.Agent => 5m,
                CustomerGroup.Project => 3m,
                CustomerGroup.Company => 2m,
                _ => 0m,
            };
            var goods = sale.Lines.Where(l => products[l.ProductCode].TrackInventory).ToList();
            goods.Count.Should().BeInRange(1, 6, sale.Key);
            goods.Select(l => l.ProductCode).Should().OnlyHaveUniqueItems(sale.Key);
            var extras = sale.Lines.Where(l => !products[l.ProductCode].TrackInventory).ToList();
            extras.Count.Should().BeLessThanOrEqualTo(1, sale.Key);
            extras.All(l => l.ProductCode == "DM-SP30" && l.Quantity == 1).Should().BeTrue($"{sale.Key} adds only transport");
            sale.Lines.Should().OnlyContain(l => l.UnitPrice == products[l.ProductCode].DefaultPrice
                && l.DiscountRate == discount && l.VatRate == 10m && l.Quantity == decimal.Truncate(l.Quantity), sale.Key);
            if (group == CustomerGroup.Retail)
                (sale.PaymentMethodCode, sale.FundCode).Should().Be(("TM", "QTM"), sale.Key);
        }

        var nonRetail = sales.Where(s => groups[s.PartnerCode!] != CustomerGroup.Retail).ToList();
        nonRetail.Should().Contain(s => s.PaymentMethodCode == null).And.Contain(s => s.PaymentMethodCode != null);
        sales.Count(s => s.WarehouseKey == "K1").Should().BeGreaterThan(sales.Count / 2);
        sales.Count(s => s.Lines.Any(l => l.ProductCode == "DM-SP30")).Should().BeGreaterThan(0);
    }

    internal static void AssertPurchaseRules(SampleDataPlan plan)
    {
        var products = plan.Products.ToDictionary(p => p.Code);
        var plans = SampleCatalog.ProductPlans.ToDictionary(p => p.ProductCode);
        var purchases = plan.Steps.OfType<SampleStockVoucherStep>().Where(s => s.ReasonCode == "NMH").ToList();

        purchases.Should().Contain(p => p.PartnerCode == DualRolePartner);
        foreach (var purchase in purchases)
        {
            purchase.Lines.Count.Should().BeInRange(3, 8, purchase.Key);
            purchase.Lines.Select(l => l.ProductCode).Should().OnlyHaveUniqueItems(purchase.Key);
            foreach (var line in purchase.Lines)
            {
                var productPlan = plans[line.ProductCode];
                new[] { productPlan.PrimarySupplier, productPlan.AltSupplier }.Should().Contain(purchase.PartnerCode, purchase.Key);
                var cost = products[line.ProductCode].CostPrice;
                line.UnitPrice.Should().BeInRange(Math.Floor(cost * 0.95m / 100m) * 100m, Math.Ceiling(cost * 1.05m / 100m) * 100m,
                    purchase.Key);
                (line.UnitPrice % 100m).Should().Be(0m, purchase.Key);
                line.Quantity.Should().Be(decimal.Truncate(line.Quantity), purchase.Key);
            }
            if (purchase.PaymentMethodCode != null)
                purchase.PaymentMethodCode.Should().Be("CK", purchase.Key);
        }
        purchases.Should().Contain(p => p.PaymentMethodCode == null).And.Contain(p => p.PaymentMethodCode != null);
    }

    internal static void AssertReceiptRules(SampleDataPlan plan)
    {
        var receipts = plan.Steps.OfType<SampleCashVoucherStep>().Where(c => c.ReasonCode == "TKH").ToList();

        receipts.Should().Contain(c => c.Targets.Count > 1);
        receipts.Count(c => c.Targets.Any(t => t.MaxAmount != null)).Should().BeInRange(1, 2);
        receipts.Should().OnlyContain(c => c.Targets.Select(t => t.TargetKey).Distinct().Count() == c.Targets.Count);
        receipts.Should().Contain(c => c.FundCode == "QTM").And.Contain(c => c.FundCode != "QTM");
    }
}
