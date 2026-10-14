using OrderMgmt.Application.Inventory.Common;

namespace OrderMgmt.Application.Organization.SampleData.Generation;

/// Date rules of the sample dataset: opening on the 1st of the month three months back, documents on working days up to yesterday.
public static class SampleCalendar
{
    public static DateOnly OpeningDate(DateOnly today)
    {
        var back = today.AddMonths(-3);
        return new DateOnly(back.Year, back.Month, 1);
    }

    public static DateOnly LastDocumentDate(DateOnly today) => today.AddDays(-1);

    /// Monday to Saturday days in [from, to], ascending.
    public static IReadOnlyList<DateOnly> WorkingDays(DateOnly from, DateOnly to)
    {
        var days = new List<DateOnly>();
        for (var d = from; d <= to; d = d.AddDays(1))
            if (d.DayOfWeek != DayOfWeek.Sunday) days.Add(d);
        return days;
    }

    /// UTC instant of `minutesFromMidnight` (minutes after VN midnight, 0-1439) on `date`.
    public static DateTimeOffset At(DateOnly date, int minutesFromMidnight) =>
        VnTime.StartOfDay(date).AddMinutes(minutesFromMidnight);
}
