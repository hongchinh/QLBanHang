using FluentAssertions;
using OrderMgmt.Application.CashDebt.Common;
using OrderMgmt.Domain.Enums;
using Xunit;

namespace OrderMgmt.IntegrationTests.CashDebt.Unit;

public class DebtDatesTests
{
    private static readonly DateOnly DocDate = new(2026, 10, 5);

    [Fact]
    public void Default_due_date_adds_credit_days()
    {
        DebtDates.DefaultDueDate(DocDate, 30).Should().Be(new DateOnly(2026, 11, 4));
        DebtDates.DefaultDueDate(DocDate, null).Should().BeNull();
        DebtDates.DefaultDueDate(DocDate, 0).Should().Be(DocDate);
    }

    [Fact]
    public void Entry_due_date_falls_back_to_doc_date_only_for_increase()
    {
        var due = new DateOnly(2026, 11, 4);

        DebtDates.EntryDueDate(DebtDirection.Increase, DocDate, due).Should().Be(due);
        DebtDates.EntryDueDate(DebtDirection.Increase, DocDate, null).Should().Be(DocDate);
        DebtDates.EntryDueDate(DebtDirection.Decrease, DocDate, due).Should().BeNull();
        DebtDates.EntryDueDate(DebtDirection.Decrease, DocDate, null).Should().BeNull();
    }

    [Fact]
    public void Effective_at_is_the_later_instant_in_utc()
    {
        // 10:00 +07:00 = 03:00 UTC, later than 02:00 UTC.
        var vn = new DateTimeOffset(2026, 10, 5, 10, 0, 0, TimeSpan.FromHours(7));
        var utc = new DateTimeOffset(2026, 10, 5, 2, 0, 0, TimeSpan.Zero);

        var expected = new DateTimeOffset(2026, 10, 5, 3, 0, 0, TimeSpan.Zero);

        // BeExactly also compares the offset.
        DebtDates.EffectiveAt(vn, utc).Should().BeExactly(expected);
        DebtDates.EffectiveAt(utc, vn).Should().BeExactly(expected);

        var laterUtc = new DateTimeOffset(2026, 10, 5, 4, 0, 0, TimeSpan.Zero);
        DebtDates.EffectiveAt(vn, laterUtc).Should().BeExactly(laterUtc);
    }
}
