using FluentAssertions;
using OrderMgmt.Application.CashDebt.Common;
using Xunit;

namespace OrderMgmt.IntegrationTests.CashDebt.Unit;

public class RunningBalanceTests
{
    private static DateTimeOffset At(int month, int day, int hour = 9) =>
        new DateTimeOffset(2026, month, day, hour, 0, 0, TimeSpan.FromHours(7)).ToUniversalTime();

    private static BalanceMovement In(DateTimeOffset at, decimal amount, string code = "PT00001") =>
        new(at, 0, code, amount);

    private static BalanceMovement Out(DateTimeOffset at, decimal amount, string code = "PC00001") =>
        new(at, 1, code, -amount);

    [Fact]
    public void Same_instant_inflow_sorts_before_outflow()
    {
        var instant = At(10, 1);

        // The payment is listed first; sorting must still apply the receipt before it.
        var result = RunningBalance.Evaluate(0m, new[] { Out(instant, 100m), In(instant, 100m) }, At(10, 1, 0));

        result.Should().Be(new RunningBalanceResult(Min: 0m, FirstNegativeAt: null, Closing: 0m));
    }

    [Fact]
    public void Min_includes_start_balance_and_later_movements()
    {
        var movements = new[]
        {
            Out(At(10, 5), 400m, "PC00002"),
            In(At(10, 3), 100m),
            Out(At(10, 1), 300m, "PC00001"),
        };

        var result = RunningBalance.Evaluate(500m, movements, At(10, 2, 0));

        result.Should().Be(new RunningBalanceResult(Min: -100m, FirstNegativeAt: At(10, 5), Closing: -100m));
    }

    [Fact]
    public void Negative_start_balance_is_first_negative_at_from()
    {
        var from = At(10, 2, 0);

        var result = RunningBalance.Evaluate(0m, new[] { Out(At(10, 1), 50m), In(At(10, 3), 80m) }, from);

        result.Should().Be(new RunningBalanceResult(Min: -50m, FirstNegativeAt: from, Closing: 30m));
    }

    [Fact]
    public void Deleting_a_receipt_is_worse_when_a_later_payment_depends_on_it()
    {
        var from = At(10, 1, 0);
        var before = RunningBalance.Evaluate(0m, new[] { In(At(10, 1), 100m), Out(At(10, 2), 100m) }, from);
        var after = RunningBalance.Evaluate(0m, new[] { Out(At(10, 2), 100m) }, from);

        before.Min.Should().Be(0m);
        after.Min.Should().Be(-100m);
        RunningBalance.IsWorse(before, after).Should().BeTrue();
    }

    [Fact]
    public void Reducing_an_existing_deficit_is_not_worse()
    {
        var from = At(10, 1, 0);
        var before = RunningBalance.Evaluate(0m, new[] { Out(At(10, 2), 300m) }, from);
        var after = RunningBalance.Evaluate(0m, new[] { Out(At(10, 2), 100m) }, from);

        before.Min.Should().Be(-300m);
        after.Min.Should().Be(-100m);
        RunningBalance.IsWorse(before, after).Should().BeFalse();
    }

    [Fact]
    public void Moving_first_negative_earlier_is_worse_even_with_same_min()
    {
        var from = At(10, 1, 0);
        var before = RunningBalance.Evaluate(0m, new[] { Out(At(10, 5), 100m) }, from);
        var after = RunningBalance.Evaluate(0m, new[] { Out(At(10, 3), 100m) }, from);

        after.Min.Should().Be(before.Min);
        after.FirstNegativeAt.Should().Be(At(10, 3));
        RunningBalance.IsWorse(before, after).Should().BeTrue();
        RunningBalance.IsWorse(after, before).Should().BeFalse();
    }
}
