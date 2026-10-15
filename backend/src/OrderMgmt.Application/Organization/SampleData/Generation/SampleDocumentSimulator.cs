using OrderMgmt.Application.Organization.SampleData.Models;
using OrderMgmt.Domain.Enums;
using static OrderMgmt.Application.Organization.SampleData.Generation.SampleDataVolumes;

namespace OrderMgmt.Application.Organization.SampleData.Generation;

/// Simulates the documents of the window day by day (working days only). It tracks stock per warehouse, estimated fund
/// balances and open debt items, so the plan never makes stock negative, keeps every fund at or above the margin and
/// keeps credit balances at or below 80% of the limit (S12). Draws continue the generator's random stream.
/// Split by concern: this file has the day loop and the schedule, `.Sales` the sales, `.Stock` the other stock vouchers,
/// `.Cash` the cash documents.
internal sealed partial class SampleDocumentSimulator
{
    private const string CashFund = "QTM";
    private const string Vcb = "VCB";
    private const string Tcb = "TCB";
    private const string K1 = "K1";
    private const string K2 = "K2";
    private const string DualRolePartner = "DM-DT01";

    private static readonly string[] BankFunds = { Vcb, Tcb };
    private static readonly string[] WarehouseKeys = { K1, K2 };

    private enum MonthEvent
    {
        SaleReturn,
        PurchaseReturn,
        Damaged,
        Scrap,
        Electricity,
        Fuel,
        BankTransfer,
    }

    /// The month's working days (whole month, also outside the window), its sales per day, its revenue target and its
    /// pending events. An event fires on the first working day on or after its date where it can; it is dropped at the
    /// end of the month.
    private sealed class MonthPlan
    {
        public MonthPlan(IReadOnlyList<DateOnly> days)
        {
            Days = days;
            SalesPerDay = new int[days.Count];
        }

        public IReadOnlyList<DateOnly> Days { get; }
        public int[] SalesPerDay { get; }
        public List<(DateOnly Date, MonthEvent Kind)> Events { get; } = new();

        public int PlannedSales { get; set; }
        public decimal RevenueTarget { get; set; }
        public int SalesDone { get; set; }
        public decimal Revenue { get; set; }
    }

    private readonly SampleRandom _random;
    private readonly SampleDataPlan _plan;
    private readonly SampleLedger _ledger;
    private readonly Dictionary<string, SamplePartnerSpec> _partners;
    private readonly Dictionary<string, SampleProductSpec> _products;
    private readonly Dictionary<string, SampleCustomerProfile> _profiles;
    private readonly List<SampleStep> _today = new();
    private readonly List<(DateOnly Date, SampleCustomerProfile Customer, bool Hold)> _plannedSales = new();
    private readonly HashSet<string> _heldKeys = new();
    private bool _holdStarted;

    private DateOnly _date;
    private MonthPlan? _month;

    private SampleDocumentSimulator(SampleRandom random, SampleDataPlan plan, bool netExcludesVat)
    {
        _random = random;
        _plan = plan;
        _ledger = new SampleLedger(plan, netExcludesVat);
        _partners = plan.Partners.ToDictionary(p => p.Code);
        _products = plan.Products.ToDictionary(p => p.Code);
        _profiles = SampleCatalog.CustomerProfiles.ToDictionary(p => p.Code);
    }

    /// The document steps of `plan` (catalog, dates and opening balances already filled), in strictly increasing time.
    /// `netExcludesVat` is the company-wide `InventorySettings.NetExcludesVat` the voucher totals are estimated with.
    public static List<SampleStep> Run(SampleRandom random, SampleDataPlan plan, bool netExcludesVat) =>
        new SampleDocumentSimulator(random, plan, netExcludesVat).Simulate();

    /// The plan of the current month; the day loop sets it before anything reads it.
    private MonthPlan Month => _month ?? throw new InvalidOperationException("No month is planned yet.");

    /// What `fund` can pay and still keep the margin (S12). A payment the business could postpone (a paid purchase, a
    /// supplier payment, the VCB → TCB transfer) also leaves the reserve in VCB, which pays the rent and the salaries.
    private decimal Room(string fund, bool postponable) =>
        _ledger.Fund(fund) - FundMargin - (postponable && fund == Vcb ? VcbReserve : 0m);

    private List<SampleStep> Simulate()
    {
        PlanOncePerPeriod();
        PlanCreditSales();
        TrackOpeningDebts();

        var steps = new List<SampleStep>();
        foreach (var day in SampleCalendar.WorkingDays(_plan.OpeningDate, _plan.LastDocumentDate))
        {
            _date = day;
            if (_month is null || _month.Days[0].Month != day.Month || _month.Days[0].Year != day.Year)
                _month = PlanMonth(day);
            var month = _month;

            if (day == month.Days[0])
                Expense(Vcb, Rent, $"Tiền thuê kho tháng {MonthText(day)}");
            StartEndHold();
            Purchases();
            SupplierPayments();
            for (var i = month.SalesPerDay[DayIndex(day)]; i > 0; i--)
                Sale();
            RunEvents(MonthEvent.SaleReturn);
            RunEvents(MonthEvent.PurchaseReturn);
            RunEvents(MonthEvent.Damaged);
            DebtOffset();
            RunEvents(MonthEvent.Scrap);
            MistakenIncome();
            RefundPrepayment();
            RunEvents(MonthEvent.Electricity);
            RunEvents(MonthEvent.Fuel);
            CancelMistakenIncome();
            CustomerReceipts(); // after the cash expenses, so the deposit day sees what the till really holds
            if (day == month.Days[^1])
                Expense(Vcb, Salary, $"Lương nhân viên tháng {MonthText(day)}");
            RunEvents(MonthEvent.BankTransfer);
            CashDeposit();

            steps.AddRange(StampTimes());
        }
        return steps;
    }

    private MonthPlan PlanMonth(DateOnly day)
    {
        var first = new DateOnly(day.Year, day.Month, 1);
        var month = new MonthPlan(SampleCalendar.WorkingDays(first, first.AddMonths(1).AddDays(-1)));
        var last = month.Days.Count - 1;

        // One sale per working day, the rest on random days, so no week goes without sales.
        var sales = _random.Between(SalesPerMonthMin, SalesPerMonthMax);
        for (var i = 0; i < sales; i++)
            month.SalesPerDay[i <= last ? i : _random.Between(0, last)]++;
        month.PlannedSales = sales;
        month.RevenueTarget = _random.NextAmount(RevenueTargetMin, RevenueTargetMax, RevenueTargetStep);

        void Add(MonthEvent kind, int fromIndex, int toIndex) =>
            month.Events.Add((month.Days[_random.Between(Math.Min(fromIndex, last), Math.Min(toIndex, last))], kind));

        for (var i = _random.Between(SaleReturnsPerMonthMin, SaleReturnsPerMonthMax); i > 0; i--)
            Add(MonthEvent.SaleReturn, SaleReturnFirstDayIndex, last);
        for (var i = 0; i < PurchaseReturnsPerMonth; i++)
            Add(MonthEvent.PurchaseReturn, PurchaseReturnFirstDayIndex, last);
        for (var i = 0; i < DamagedPerMonth; i++)
            Add(MonthEvent.Damaged, 0, last);
        Add(MonthEvent.Scrap, 0, last);
        Add(MonthEvent.Electricity, 0, last);
        for (var i = _random.Between(FuelPerMonthMin, FuelPerMonthMax); i > 0; i--)
            Add(MonthEvent.Fuel, 0, last);
        Add(MonthEvent.BankTransfer, BankTransferDayIndexMin, BankTransferDayIndexMax);
        return month;
    }

    private void RunEvents(MonthEvent kind)
    {
        var events = Month.Events;
        for (var i = 0; i < events.Count;)
        {
            var (date, eventKind) = events[i];
            if (eventKind == kind && date <= _date && TryEvent(kind))
                events.RemoveAt(i);
            else
                i++;
        }
    }

    private bool TryEvent(MonthEvent kind) => kind switch
    {
        MonthEvent.SaleReturn => SaleReturn(),
        MonthEvent.PurchaseReturn => PurchaseReturn(),
        MonthEvent.Damaged => Damaged(),
        MonthEvent.Scrap => ScrapIncome(),
        MonthEvent.Electricity => Expense(CashFund, _random.NextAmount(ElectricityMin, ElectricityMax, CashAmountStep),
            $"Tiền điện tháng {MonthText(_date)}"),
        MonthEvent.Fuel => Expense(CashFund, _random.NextAmount(FuelMin, FuelMax, FuelAmountStep), "Xăng xe giao hàng"),
        MonthEvent.BankTransfer => BankTransfer(),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    private int DayIndex(DateOnly day)
    {
        var days = Month.Days;
        for (var i = 0; i < days.Count; i++)
            if (days[i] == day)
                return i;
        throw new InvalidOperationException($"{day} is not a working day of the planned month.");
    }

    /// Gives today's steps distinct times in [08:00, 17:30], increasing in the order they were simulated.
    private List<SampleStep> StampTimes()
    {
        var minutes = new SortedSet<int>();
        while (minutes.Count < _today.Count)
            minutes.Add(_random.Between(FirstMinute, LastMinute));
        var stamped = _today.Zip(minutes, (step, minute) => step with { At = SampleCalendar.At(_date, minute) }).ToList();
        _today.Clear();
        return stamped;
    }

    private void Write(SampleStep step) => _today.Add(step);

    private int? OwnerOf(string partnerCode) => _partners[partnerCode].OwnerIndex;

    /// A payment date before the window moves to one of its first days, so catch-up payments do not bunch.
    private DateOnly IntoWindow(DateOnly date) =>
        date >= _plan.OpeningDate ? date : _plan.OpeningDate.AddDays(_random.Between(0, CatchUpDays));

    private static string MonthText(DateOnly day) => $"{day.Month:00}/{day.Year}";

    /// Receivable invoices are collected by habit (the bad debt customer never pays its opening items); payables are paid
    /// near their due date. Prepayments get no date: only the refund targets the receivable one.
    private void TrackOpeningDebts()
    {
        foreach (var debt in _plan.OpeningDebts)
        {
            if (debt.Kind == OpeningDebtKind.Prepayment)
            {
                if (debt.Side == DebtSide.Receivable)
                    _ledger.Items.Add(new SampleOpenItem(debt.Key, debt.PartnerCode, debt.Side, "OD", true, debt.Amount, null, null));
                continue;
            }

            var due = debt.DueDate!.Value;
            var payDate = debt.Side == DebtSide.Receivable
                ? CollectionDate(_profiles[debt.PartnerCode], due, opening: true)
                : due.AddDays(-_random.Between(SupplierDaysBeforeDueMin, SupplierDaysBeforeDueMax));
            _ledger.Items.Add(new SampleOpenItem(debt.Key, debt.PartnerCode, debt.Side, "OD", false, debt.Amount, due,
                payDate is { } date ? IntoWindow(date) : null));
        }
    }

    /// On time: shortly before the due date. Late: well after it. Bad debt: never for an opening item, very late for a new one.
    private DateOnly? CollectionDate(SampleCustomerProfile customer, DateOnly due, bool opening) => customer.Habit switch
    {
        PaymentHabit.OnTime => due.AddDays(-_random.Between(OnTimeDaysBeforeDueMin, OnTimeDaysBeforeDueMax)),
        PaymentHabit.BadDebt when opening => null,
        PaymentHabit.BadDebt => due.AddDays(_random.Between(BadDebtDaysAfterDueMin, BadDebtDaysAfterDueMax)),
        _ => due.AddDays(_random.Between(LateDaysAfterDueMin, LateDaysAfterDueMax)),
    };

    /// Planned credit sales (single line each):
    /// - placed relative to T (the day after the last document), so the overdue report at T has an item in the 1–30 and
    ///   the 31–60 day buckets whatever the window: one to the first late payer due LateSaleOverdueDays before T, one to
    ///   the bad debt customer due BadDebtSaleOverdueDays before T. Neither customer pays them before T (their habits pay
    ///   later, and the invoices are held: never settled early). CreditDays + overdue days (at most 45 + 40 = 85) stay
    ///   within the shortest window (89 days before T), so the sale date is never before the opening date; a unit test
    ///   checks it;
    /// - one early sale to each customer with a credit limit.
    private void PlanCreditSales()
    {
        var t = _plan.LastDocumentDate.AddDays(1);
        foreach (var (habit, overdueDays) in new[] { (PaymentHabit.Late, LateSaleOverdueDays), (PaymentHabit.BadDebt, BadDebtSaleOverdueDays) })
        {
            var customer = SampleCatalog.PlacedSaleCustomer(habit);
            var date = t.AddDays(-(_partners[customer.Code].CreditDays ?? 0) - overdueDays);
            if (date.DayOfWeek == DayOfWeek.Sunday)
                date = date.AddDays(1);
            _plannedSales.Add((date, customer, true));
        }

        // Customers with a credit limit buy large and would otherwise pay at once against the receivables ceiling: each
        // gets one early credit sale (single line, within 80% of its limit), so the credit limit report has one to show.
        var early = _plan.OpeningDate.AddDays(LimitedCustomerSaleDay);
        if (early.DayOfWeek == DayOfWeek.Sunday)
            early = early.AddDays(1);
        foreach (var customer in SampleCatalog.CustomerProfiles.Where(p => p.CreditLimit != null))
            _plannedSales.Add((early, customer, false));

        // Stable sort: same-day sales keep the order above.
        var ordered = _plannedSales.OrderBy(s => s.Date).ToList();
        _plannedSales.Clear();
        _plannedSales.AddRange(ordered);
    }

    /// From EndHoldDays before T: receipts take only invoices that are due or due within OnTimeDaysBeforeDueMax days (no
    /// gathering), the sales desk steers the receivables expected at T, and every owner keeps an open invoice at T. An
    /// owner's invoice still open at T is held (never settled early); an owner without one gets a planned credit sale to
    /// its first on-time, non-retail customer without a credit limit, held as well.
    private void StartEndHold()
    {
        if (_holdStarted || _date < _plan.LastDocumentDate.AddDays(1 - EndHoldDays))
            return;
        _holdStarted = true;
        var t = _plan.LastDocumentDate.AddDays(1);
        var owners = _plan.Partners.Where(p => p.IsCustomer && p.OwnerIndex != null).Select(p => p.OwnerIndex!.Value)
            .Distinct().OrderBy(o => o).ToList();
        foreach (var owner in owners)
        {
            var customers = SampleCatalog.CustomerProfiles.Where(c => _partners[c.Code].OwnerIndex == owner).ToList();
            var openAtT = _ledger.Items.FirstOrDefault(i => i.Side == DebtSide.Receivable && !i.IsPrepayment && i.Open > 0
                && customers.Any(c => c.Code == i.PartnerCode) && (i.PayDate is null || i.PayDate >= t));
            if (openAtT != null)
            {
                _heldKeys.Add(openAtT.Key);
                continue;
            }
            var customer = customers.FirstOrDefault(c => c.Habit == PaymentHabit.OnTime && c.Group != CustomerGroup.Retail
                && c.CreditLimit == null && c.Code != DualRolePartner);
            if (customer != null)
                _plannedSales.Add((_date, customer, true));
        }
    }
}
