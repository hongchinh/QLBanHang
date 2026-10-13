using OrderMgmt.Application.CashDebt.Common;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Dashboards.Executive.Common;

/// The figures of one side (receivable or payable) of the open items. OverdueAmounts has one entry per bucket.
public sealed record DebtSideTotals(decimal TotalOpen, decimal Unallocated, decimal Overdue, int OverduePartnerCount,
    IReadOnlyList<decimal> OverdueAmounts, decimal DueSoon, int DueSoonCount)
{
    public decimal Balance => TotalOpen - Unallocated;
    public decimal NotDue => TotalOpen - Overdue;
}

/// The rules of the debt tiles on the open items of a side. Only an Increase item (a document still owed) is ever
/// overdue or due soon; a Decrease item is money not yet matched to a document.
public static class DebtPositionMath
{
    public const int DueSoonDays = 7;

    /// Totals of one side. `items` are the open items (Open > 0) of that side; the overdue amounts are split by days
    /// past the due date with the boundaries of the overdue report.
    public static DebtSideTotals Totals(IReadOnlyCollection<OpenDebtItem> items, DateOnly at, IReadOnlyList<int> boundaries)
    {
        var increases = items.Where(i => i.Direction == DebtDirection.Increase).ToList();
        var overdue = increases.Where(i => IsOverdue(i, at)).ToList();
        var overdueAmounts = new decimal[boundaries.Count + 1];
        foreach (var item in overdue)
            overdueAmounts[DebtAging.BucketIndex(DebtDates.DaysOverdue(at, item.Due), boundaries)] += item.Open;
        var dueSoon = increases.Where(i => IsDueWithin(i, at, DueSoonDays)).ToList();

        return new DebtSideTotals(
            TotalOpen: increases.Sum(i => i.Open),
            Unallocated: items.Where(i => i.Direction == DebtDirection.Decrease).Sum(i => i.Open),
            Overdue: overdue.Sum(i => i.Open),
            OverduePartnerCount: overdue.Select(i => i.PartnerId).Distinct().Count(),
            OverdueAmounts: overdueAmounts,
            DueSoon: dueSoon.Sum(i => i.Open),
            DueSoonCount: dueSoon.Count);
    }

    /// Due before `at`; due on `at` is not overdue yet.
    public static bool IsOverdue(OpenDebtItem item, DateOnly at) =>
        item.Direction == DebtDirection.Increase && item.Due < at;

    /// Due from `at` up to and including `days` days after it.
    public static bool IsDueWithin(OpenDebtItem item, DateOnly at, int days) =>
        item.Direction == DebtDirection.Increase && item.Due >= at && item.Due <= at.AddDays(days);

    /// Sum of Open of the Increase items minus the sum of Open of the Decrease items.
    public static decimal Balance(IEnumerable<OpenDebtItem> items) => items.Sum(Signed);

    private static decimal Signed(OpenDebtItem item) => item.Direction switch
    {
        DebtDirection.Increase => item.Open,
        DebtDirection.Decrease => -item.Open,
        _ => throw new ArgumentOutOfRangeException(nameof(item), item.Direction, "Unknown debt direction."),
    };
}
