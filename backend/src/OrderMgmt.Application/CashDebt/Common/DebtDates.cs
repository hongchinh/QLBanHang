using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.CashDebt.Common;

/// Pure due-date and settlement-instant rules for debt entries.
public static class DebtDates
{
    /// docDate + creditDays; null when creditDays is null (P27).
    public static DateOnly? DefaultDueDate(DateOnly docDate, int? creditDays)
        => creditDays is { } days ? docDate.AddDays(days) : null;

    /// Increase → dueDate ?? docDate (C9: no term = due on the document date). Decrease → null.
    public static DateOnly? EntryDueDate(DebtDirection direction, DateOnly docDate, DateOnly? dueDate)
        => direction == DebtDirection.Increase ? dueDate ?? docDate : null;

    /// max(a, b), returned as UTC.
    public static DateTimeOffset EffectiveAt(DateTimeOffset a, DateTimeOffset b)
        => (a >= b ? a : b).ToUniversalTime();

    /// Days since the document date, never negative (B5). Shared by the documents and aging reports.
    public static int DaysOutstanding(DateOnly at, DateOnly docDate)
        => Math.Max(0, at.DayNumber - docDate.DayNumber);

    /// Days past the due date; 0 when not yet overdue (due today is not overdue).
    public static int DaysOverdue(DateOnly at, DateOnly dueDate)
        => dueDate < at ? at.DayNumber - dueDate.DayNumber : 0;
}
