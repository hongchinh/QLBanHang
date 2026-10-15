using OrderMgmt.Application.Organization.SampleData.Models;
using OrderMgmt.Domain.Enums;
using static OrderMgmt.Application.Organization.SampleData.Generation.SampleDataVolumes;

namespace OrderMgmt.Application.Organization.SampleData.Generation;

/// Cash documents: receipts (TKH), supplier payments (TNCC), expenses (CK), other income (TK), fund transfers, and the
/// once-per-period refund (CHKH), debt offset and cancelled receipt.
internal sealed partial class SampleDocumentSimulator
{
    private static readonly IReadOnlyList<SampleAllocationTarget> NoTargets = Array.Empty<SampleAllocationTarget>();

    private DateOnly _refundDate;
    private DateOnly _mistakeDate;
    private DateOnly _offsetDate;
    private DateOnly[] _partialDates = Array.Empty<DateOnly>();

    private bool _refundDone;
    private bool _offsetDone;
    private bool _cancelDone;
    private string? _mistakeKey;
    private decimal _mistakeAmount;
    private int _partialsDone;

    private void PlanOncePerPeriod()
    {
        _refundDate = _plan.OpeningDate.AddDays(_random.Between(RefundDayMin, RefundDayMax));
        _mistakeDate = _plan.OpeningDate.AddDays(_random.Between(MistakenIncomeDayMin, MistakenIncomeDayMax));
        _offsetDate = _plan.OpeningDate.AddDays(_random.Between(OffsetDayMin, OffsetDayMax));
        _partialDates = new[] { _plan.OpeningDate.AddDays(PartialPaymentFirstDay), _plan.OpeningDate.AddDays(PartialPaymentSecondDay) };
    }

    /// One receipt per customer and day once an invoice's collection date has come. Besides the invoices due, it takes
    /// (oldest first, never a held invoice nor one the customer never pays):
    /// - on the deposit day with a short till, invoices to collect within EarlyCollectionDays (during the end hold only
    ///   invoices already due or due within OnTimeDaysBeforeDueMax days, as an on-time customer pays); before the hold
    ///   this early collection can still take an invoice that would otherwise be open at T;
    /// - before the end hold, the invoices to collect within ReceiptGatherDays and by the last working day (gathering),
    ///   so gathering never takes an invoice that would still be open at T.
    /// The dual-role partner pays nothing before its debt offset, so the offset finds both sides open.
    private void CustomerReceipts()
    {
        foreach (var customer in SampleCatalog.CustomerProfiles)
        {
            if (customer.Code == DualRolePartner && !_offsetDone)
                continue;
            var shortTill = DepositIsShort();
            bool Due(SampleOpenItem i) => i.PayDate <= _date;
            bool Early(SampleOpenItem i) => shortTill && !_heldKeys.Contains(i.Key) && i.PayDate != null && (_holdStarted
                ? i.DueDate <= _date.AddDays(OnTimeDaysBeforeDueMax)
                : i.PayDate <= _date.AddDays(EarlyCollectionDays));
            bool Gathered(SampleOpenItem i) => !_holdStarted && !_heldKeys.Contains(i.Key)
                && i.PayDate <= _date.AddDays(ReceiptGatherDays) && i.PayDate <= LastWorkingDay;

            var due = _ledger.Items
                .Where(i => i.PartnerCode == customer.Code && i.Side == DebtSide.Receivable && !i.IsPrepayment && i.Open > 0
                    && (Due(i) || Early(i) || Gathered(i)))
                .ToList();
            if (!due.Any(i => Due(i) || Early(i)))
                continue;

            var partial = _partialsDone < _partialDates.Length && _date >= _partialDates[_partialsDone]
                && customer.Habit == PaymentHabit.OnTime && due[0].Open >= PartialPaymentMinOpen;
            var targets = new List<SampleAllocationTarget>();
            var total = 0m;
            foreach (var item in due)
            {
                decimal? max = null;
                DateOnly? payRestOn = null;
                if (partial && targets.Count == 0)
                {
                    max = Math.Floor(item.Open * PartialPaymentPercent / 100m / PartialPaymentStep) * PartialPaymentStep;
                    payRestOn = _date.AddDays(_random.Between(PartialRestDaysMin, PartialRestDaysMax));
                }
                var amount = Math.Min(item.Open, max ?? item.Open);
                item.Settle(amount, payRestOn);
                total += amount;
                targets.Add(new SampleAllocationTarget(item.Key, max));
            }
            if (partial)
                _partialsDone++;

            var fund = DepositIsShort() ? CashFund : _random.Chance(ReceiptBankPercent) ? _random.Pick(BankFunds) : CashFund;
            Write(new SampleCashVoucherStep(_ledger.NextKey("TKH"), default, OwnerOf(customer.Code), CashVoucherType.Receipt, "TKH",
                fund, customer.Code, null, partial ? "Thu tiền hàng (trả một phần)" : "Thu tiền hàng", null, targets));
            _ledger.MoveFund(fund, total);
        }
    }

    /// One payment per supplier and day once an invoice's payment date has come. It pays, oldest first, the invoices to pay
    /// within SupplierGatherDays from the bank fund with the most room; invoices that do not fit wait for the next day.
    private void SupplierPayments()
    {
        foreach (var supplier in _plan.Partners.Where(p => p.IsSupplier))
        {
            if (supplier.Code == DualRolePartner && !_offsetDone)
                continue;
            var due = _ledger.Items
                .Where(i => i.PartnerCode == supplier.Code && i.Side == DebtSide.Payable && i.Open > 0
                    && i.PayDate <= _date.AddDays(SupplierGatherDays))
                .ToList();
            if (!due.Any(i => i.PayDate <= _date))
                continue;

            var fund = BanksByRoom().First();
            var room = Room(fund, postponable: true);
            var paid = new List<SampleOpenItem>();
            var total = 0m;
            foreach (var item in due)
            {
                if (total + item.Open > room)
                    break;
                paid.Add(item);
                total += item.Open;
            }
            if (paid.Count == 0)
                continue;

            Write(new SampleCashVoucherStep(_ledger.NextKey("TNCC"), default, null, CashVoucherType.Payment, "TNCC", fund,
                supplier.Code, null, "Trả tiền hàng", null, paid.Select(i => new SampleAllocationTarget(i.Key, null)).ToList()));
            foreach (var item in paid)
                item.Settle(item.Open);
            _ledger.MoveFund(fund, -total);
        }
    }

    /// Deposit day and the till holds less than the deposit needs: money coming in is taken in cash.
    private bool DepositIsShort() => IsDepositDay() && _ledger.Fund(CashFund) < CashKeep + CashDepositMin + CashFloat;

    /// Bank funds, most room for a postponable payment first (VCB on a tie).
    private IEnumerable<string> BanksByRoom() =>
        Room(Tcb, postponable: true) > Room(Vcb, postponable: true) ? new[] { Tcb, Vcb } : BankFunds;

    /// The bank fund that pays a postponable `amount` and keeps the margin, or null.
    private string? PayingBank(decimal amount) =>
        BanksByRoom().FirstOrDefault(f => Room(f, postponable: true) >= amount);

    /// A fixed expense from `preferred`, or from the first fund that keeps the margin. False when no fund can pay it.
    private bool Expense(string preferred, decimal amount, string description)
    {
        var fund = new[] { preferred }.Concat(_plan.Funds.Select(f => f.Code))
            .FirstOrDefault(f => Room(f, postponable: false) >= amount);
        if (fund is null)
            return false;
        Write(new SampleCashVoucherStep(_ledger.NextKey("CK"), default, null, CashVoucherType.Payment, "CK", fund, null, null,
            description, amount, NoTargets));
        _ledger.MoveFund(fund, -amount);
        return true;
    }

    private bool ScrapIncome()
    {
        Income(_random.NextAmount(ScrapMin, ScrapMax, CashAmountStep), "Bán phế liệu");
        return true;
    }

    private string Income(decimal amount, string description)
    {
        var key = _ledger.NextKey("TK");
        Write(new SampleCashVoucherStep(key, default, null, CashVoucherType.Receipt, "TK", CashFund, null, null, description, amount,
            NoTargets));
        _ledger.MoveFund(CashFund, amount);
        return key;
    }

    private bool BankTransfer()
    {
        if (Room(Vcb, postponable: true) < BankTransferAmount)
            return false;
        Transfer(Vcb, Tcb, BankTransferAmount, "Chuyển tiền sang tài khoản Techcombank");
        return true;
    }

    /// Saturday, or the last day of the window: the week's cash is deposited at the end of the day.
    private bool IsDepositDay() => _date.DayOfWeek == DayOfWeek.Saturday || _date == _plan.LastDocumentDate;

    /// Deposits the cash above the amount kept in the till.
    private void CashDeposit()
    {
        if (!IsDepositDay())
            return;
        var amount = Math.Floor((_ledger.Fund(CashFund) - CashKeep) / CashAmountStep) * CashAmountStep;
        if (amount >= CashDepositMin)
            Transfer(CashFund, Vcb, amount, "Nộp tiền mặt vào tài khoản Vietcombank");
    }

    private void Transfer(string from, string to, decimal amount, string description)
    {
        Write(new SampleFundTransferStep(_ledger.NextKey("CQ"), default, null, from, to, amount, description));
        _ledger.MoveFund(from, -amount);
        _ledger.MoveFund(to, amount);
    }

    /// Refunds part of the retail customer's prepayment, in cash when the till keeps the margin.
    private void RefundPrepayment()
    {
        if (_refundDone || _date < _refundDate)
            return;
        var prepayment = _ledger.Items.First(i => i.IsPrepayment && i.Side == DebtSide.Receivable);
        var amount = Math.Min(prepayment.Open, PrepaymentRefund);
        var fund = new[] { CashFund }.Concat(BanksByRoom()).FirstOrDefault(f => Room(f, postponable: false) >= amount);
        if (fund is null)
            return;

        Write(new SampleCashVoucherStep(_ledger.NextKey("CHKH"), default, OwnerOf(prepayment.PartnerCode), CashVoucherType.Payment,
            "CHKH", fund, prepayment.PartnerCode, null, "Hoàn một phần tiền trả trước", null,
            new[] { new SampleAllocationTarget(prepayment.Key, PrepaymentRefund) }));
        prepayment.Settle(amount);
        _ledger.MoveFund(fund, -amount);
        _refundDone = true;
    }

    /// An income receipt entered by mistake; it is cancelled later the same day, so the day's cash is unchanged.
    private void MistakenIncome()
    {
        if (_mistakeKey != null || _date < _mistakeDate)
            return;
        _mistakeAmount = _random.NextAmount(MistakenIncomeMin, MistakenIncomeMax, CashAmountStep);
        _mistakeKey = Income(_mistakeAmount, "Thu khác (lập nhầm)");
    }

    private void CancelMistakenIncome()
    {
        if (_mistakeKey is null || _cancelDone || Room(CashFund, postponable: false) < _mistakeAmount)
            return;
        Write(new SampleCancelStep(_ledger.NextKey("HUY"), default, null, _mistakeKey));
        _ledger.MoveFund(CashFund, -_mistakeAmount);
        _cancelDone = true;
    }

    /// From some days before the offset date until the offset, the dual-role partner's next sale is a credit sale.
    private bool OffsetNeedsSale() =>
        !_offsetDone && _date >= _offsetDate.AddDays(-OffsetSaleLeadDays) && OpenItem(DualRolePartner, "XBH") == null;

    /// Offsets the dual-role partner's oldest open credit sale against its oldest open credit purchase.
    private void DebtOffset()
    {
        if (_offsetDone || _date < _offsetDate)
            return;
        var receivable = OpenItem(DualRolePartner, "XBH");
        var payable = OpenItem(DualRolePartner, "NMH");
        if (receivable is null || payable is null)
            return;

        var amount = Math.Min(receivable.Open, payable.Open);
        Write(new SampleDebtOffsetStep(_ledger.NextKey("BT"), default, null, DualRolePartner, receivable.Key, payable.Key));
        receivable.Settle(amount);
        payable.Settle(amount);
        _offsetDone = true;
    }

    private SampleOpenItem? OpenItem(string partnerCode, string source) =>
        _ledger.Items.FirstOrDefault(i => i.PartnerCode == partnerCode && i.Source == source && i.Open > 0);
}
