using OrderMgmt.Application.Organization.SampleData.Models;
using OrderMgmt.Domain.Enums;
using static OrderMgmt.Application.Organization.SampleData.Generation.SampleDataVolumes;

namespace OrderMgmt.Application.Organization.SampleData.Generation;

/// Simulates the documents of the window day by day (working days only). It tracks stock per warehouse, estimated fund
/// balances and open debt items, so the plan never makes stock negative, keeps every fund at or above the margin and
/// keeps credit balances at or below 80% of the limit (S12). Draws continue the generator's random stream.
/// Split by concern: this file has the day loop and the schedule, `.Stock` the stock vouchers, `.Cash` the cash documents.
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

    /// The month's working days (whole month, also outside the window), its sales per day and its pending events.
    /// An event fires on the first working day on or after its date where it can; it is dropped at the end of the month.
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
    }

    private readonly SampleRandom _random;
    private readonly SampleDataPlan _plan;
    private readonly SampleLedger _ledger;
    private readonly Dictionary<string, SamplePartnerSpec> _partners;
    private readonly Dictionary<string, SampleProductSpec> _products;
    private readonly Dictionary<string, SampleCustomerProfile> _profiles;
    private readonly List<SampleStep> _today = new();

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
    /// `netExcludesVat` is the branch's inventory setting the voucher totals are estimated with.
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
                    _ledger.Items.Add(new SampleOpenItem(debt.Key, debt.PartnerCode, debt.Side, "OD", true, debt.Amount, null));
                continue;
            }

            var due = debt.DueDate!.Value;
            var payDate = debt.Side == DebtSide.Receivable
                ? CollectionDate(_profiles[debt.PartnerCode], due, opening: true)
                : due.AddDays(-_random.Between(SupplierDaysBeforeDueMin, SupplierDaysBeforeDueMax));
            _ledger.Items.Add(new SampleOpenItem(debt.Key, debt.PartnerCode, debt.Side, "OD", false, debt.Amount,
                payDate is { } date ? IntoWindow(date) : null));
        }
    }

    private DateOnly? CollectionDate(SampleCustomerProfile customer, DateOnly due, bool opening) => customer.Habit switch
    {
        PaymentHabit.OnTime => due.AddDays(-_random.Between(OnTimeDaysBeforeDueMin, OnTimeDaysBeforeDueMax)),
        PaymentHabit.BadDebt when opening => null,
        _ => due.AddDays(_random.Between(LateDaysAfterDueMin, LateDaysAfterDueMax)),
    };
}
