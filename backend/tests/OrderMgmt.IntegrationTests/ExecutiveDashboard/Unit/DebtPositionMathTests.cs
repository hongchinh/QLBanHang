using FluentAssertions;
using OrderMgmt.Application.CashDebt.Common;
using OrderMgmt.Application.Dashboards.Executive.Common;
using OrderMgmt.Domain.Enums;
using Xunit;

namespace OrderMgmt.IntegrationTests.ExecutiveDashboard.Unit;

public class DebtPositionMathTests
{
    private static readonly DateOnly At = new(2026, 10, 31);
    private static readonly IReadOnlyList<int> Boundaries = DebtAging.DefaultBoundaries;
    private static readonly Guid P1 = Guid.NewGuid();
    private static readonly Guid P2 = Guid.NewGuid();

    private static DateOnly D(int month, int day) => new(2026, month, day);

    /// A document of the partner still owed, dated 09-01 unless said otherwise.
    private static OpenDebtItem Increase(Guid partner, decimal open, DateOnly? due, DateOnly? docDate = null) =>
        new(Guid.Empty, partner, DebtDirection.Increase, DebtSourceType.StockOut, Guid.NewGuid(), "PX00001",
            docDate ?? D(9, 1), due, null, open);

    /// Money of the partner not yet matched to a document.
    private static OpenDebtItem Decrease(Guid partner, decimal open, DateOnly? due = null) =>
        new(Guid.Empty, partner, DebtDirection.Decrease, DebtSourceType.Receipt, Guid.NewGuid(), "PT00001",
            D(9, 1), due, null, open);

    [Fact]
    public void Totals_split_open_overdue_and_due_soon()
    {
        var items = new[]
        {
            Increase(P1, 1_000m, D(9, 20)),
            Increase(P1, 2_000m, D(10, 3)),
            Increase(P2, 500m, D(10, 31)),
            Increase(P2, 700m, D(11, 7)),
            Increase(P2, 900m, D(11, 8)),
            Decrease(P1, 1_500m),
        };

        var totals = DebtPositionMath.Totals(items, At, Boundaries);

        totals.TotalOpen.Should().Be(5_100m);
        totals.Unallocated.Should().Be(1_500m);
        totals.Balance.Should().Be(3_600m);
        totals.Overdue.Should().Be(3_000m);
        totals.OverdueAmounts.Should().Equal(2_000m, 1_000m, 0m, 0m);
        totals.NotDue.Should().Be(2_100m);
        totals.OverduePartnerCount.Should().Be(1);
        totals.DueSoon.Should().Be(1_200m);
        totals.DueSoonCount.Should().Be(2);
    }

    [Fact]
    public void Due_falls_back_to_the_document_date()
    {
        var item = Increase(P1, 400m, due: null, docDate: D(10, 1));

        item.Due.Should().Be(D(10, 1));
        DebtPositionMath.IsOverdue(item, At).Should().BeTrue();
        DebtPositionMath.Totals(new[] { item }, At, Boundaries).OverdueAmounts.Should().Equal(400m, 0m, 0m, 0m);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(30, 0)]
    [InlineData(31, 1)]
    [InlineData(90, 2)]
    [InlineData(91, 3)]
    public void Bucket_edges(int daysOverdue, int expectedBucket)
    {
        var item = Increase(P1, 250m, At.AddDays(-daysOverdue));

        var amounts = DebtPositionMath.Totals(new[] { item }, At, Boundaries).OverdueAmounts;

        amounts.Should().Equal(Enumerable.Range(0, 4).Select(bucket => bucket == expectedBucket ? 250m : 0m));
    }

    [Fact]
    public void Custom_boundaries_give_one_more_bucket_than_boundaries()
    {
        var item = Increase(P1, 250m, At.AddDays(-15));

        DebtPositionMath.Totals(new[] { item }, At, new[] { 10, 20 }).OverdueAmounts.Should().Equal(0m, 250m, 0m);
    }

    [Theory]
    [InlineData(-1, true)]
    [InlineData(0, false)]
    [InlineData(1, false)]
    public void Overdue_starts_the_day_after_the_due_date(int dueOffsetDays, bool expected) =>
        DebtPositionMath.IsOverdue(Increase(P1, 100m, At.AddDays(dueOffsetDays)), At).Should().Be(expected);

    [Theory]
    [InlineData(-1, 7, false)]
    [InlineData(0, 7, true)]
    [InlineData(7, 7, true)]
    [InlineData(8, 7, false)]
    [InlineData(30, 30, true)]
    [InlineData(31, 30, false)]
    public void Due_within_covers_today_and_the_days_after(int dueOffsetDays, int days, bool expected) =>
        DebtPositionMath.IsDueWithin(Increase(P1, 100m, At.AddDays(dueOffsetDays)), At, days).Should().Be(expected);

    [Fact]
    public void Due_soon_looks_seven_days_ahead() => DebtPositionMath.DueSoonDays.Should().Be(7);

    [Fact]
    public void Decrease_items_are_never_overdue_or_due_soon()
    {
        // Whatever date a Decrease item carries, it is not a document that falls due.
        var items = new[] { Decrease(P1, 300m, D(9, 20)), Decrease(P1, 200m, D(11, 2)), Decrease(P2, 100m) };

        foreach (var item in items)
        {
            DebtPositionMath.IsOverdue(item, At).Should().BeFalse();
            DebtPositionMath.IsDueWithin(item, At, 30).Should().BeFalse();
        }

        var totals = DebtPositionMath.Totals(items, At, Boundaries);
        totals.Overdue.Should().Be(0m);
        totals.OverduePartnerCount.Should().Be(0);
        totals.DueSoon.Should().Be(0m);
        totals.DueSoonCount.Should().Be(0);
        totals.TotalOpen.Should().Be(0m);
        totals.Unallocated.Should().Be(600m);
    }

    [Fact]
    public void Balance_subtracts_decrease_items() =>
        DebtPositionMath.Balance(new[] { Increase(P1, 300m, D(9, 20)), Decrease(P1, 100m) }).Should().Be(200m);

    [Fact]
    public void Empty_input_gives_zero_filled_amounts()
    {
        var totals = DebtPositionMath.Totals(Array.Empty<OpenDebtItem>(), At, Boundaries);

        totals.OverdueAmounts.Should().Equal(0m, 0m, 0m, 0m);
        (totals.TotalOpen, totals.Unallocated, totals.Overdue, totals.DueSoon).Should().Be((0m, 0m, 0m, 0m));
        (totals.OverduePartnerCount, totals.DueSoonCount).Should().Be((0, 0));
    }
}
