using FluentAssertions;
using OrderMgmt.Application.Inventory.Costing;
using Xunit;

namespace OrderMgmt.IntegrationTests.Inventory.Unit;

public class PeriodicAverageCalculatorTests
{
    private static readonly DateOnly Sep1 = new(2026, 9, 1), Sep30 = new(2026, 9, 30);
    private static readonly DateOnly Oct1 = new(2026, 10, 1), Oct31 = new(2026, 10, 31);
    private static readonly DateOnly Nov1 = new(2026, 11, 1), Nov30 = new(2026, 11, 30);

    private static CostMovement In(decimal qty, decimal value) => new(Guid.NewGuid(), qty, 0m, value);
    private static CostMovement Out(decimal qty) => new(Guid.NewGuid(), 0m, qty, 0m);

    private static CostPeriodInput Period(DateOnly start, DateOnly end, params CostMovement[] movements) =>
        new(start, end, movements);

    [Fact]
    public void Single_month_average_cost()
    {
        var result = PeriodicAverageCalculator.Run(0m, 0m, 0m,
            new[] { Period(Oct1, Oct31, In(100m, 5_000_000m), Out(30m), In(50m, 3_000_000m), Out(40m)) });

        var period = result.Periods.Single();
        period.AvgCost.Should().Be(53_333.3333m);
        result.OutCosts.Select(c => c.CostAmount).Should().Equal(1_600_000m, 2_133_333m);
        result.OutCosts.Should().OnlyContain(c => c.UnitCost == 53_333.3333m);
        period.OutValue.Should().Be(3_733_333m);
        period.ClosingQty.Should().Be(80m);
        period.ClosingValue.Should().Be(4_266_667m);
    }

    [Fact]
    public void Zero_closing_pushes_rounding_residual_to_last_outbound()
    {
        var result = PeriodicAverageCalculator.Run(0m, 0m, 0m,
            new[] { Period(Oct1, Oct31, In(3m, 100_000m), Out(1m), Out(1m), Out(1m)) });

        result.OutCosts.Select(c => c.CostAmount).Should().Equal(33_333m, 33_333m, 33_334m);
        result.Periods.Single().OutValue.Should().Be(100_000m);
        result.Periods.Single().ClosingQty.Should().Be(0m);
        result.Periods.Single().ClosingValue.Should().Be(0m);
    }

    [Fact]
    public void Non_positive_denominator_uses_previous_period_average()
    {
        var result = PeriodicAverageCalculator.Run(0m, 0m, 0m, new[]
        {
            Period(Sep1, Sep30, In(10m, 1_000_000m), Out(10m)),
            Period(Oct1, Oct31, Out(5m)),
            Period(Nov1, Nov30, In(10m, 1_200_000m)),
        });

        result.OutCosts.Select(c => c.CostAmount).Should().Equal(1_000_000m, 500_000m);
        result.Periods[0].Should().Match<CostPeriodResult>(p => p.ClosingQty == 0m && p.ClosingValue == 0m);
        result.Periods[1].AvgCost.Should().Be(100_000m);
        result.Periods[1].Should().Match<CostPeriodResult>(p => p.ClosingQty == -5m && p.ClosingValue == -500_000m);
        result.Periods[2].AvgCost.Should().Be(140_000m);
        result.Periods[2].Should().Match<CostPeriodResult>(p => p.ClosingQty == 5m && p.ClosingValue == 700_000m);
    }

    [Fact]
    public void First_period_without_stock_uses_supplied_fallback()
    {
        var result = PeriodicAverageCalculator.Run(0m, 0m, 60_000m, new[] { Period(Oct1, Oct31, Out(2m)) });

        result.Periods.Single().AvgCost.Should().Be(60_000m);
        result.OutCosts.Single().CostAmount.Should().Be(120_000m);
        result.Periods.Single().ClosingQty.Should().Be(-2m);
        result.Periods.Single().ClosingValue.Should().Be(-120_000m);
    }

    [Fact]
    public void Zero_closing_without_outbound_keeps_value()
    {
        var result = PeriodicAverageCalculator.Run(-5m, -500_000m, 90_000m,
            new[] { Period(Oct1, Oct31, In(5m, 600_000m)) });

        var period = result.Periods.Single();
        period.AvgCost.Should().Be(90_000m);
        period.ClosingQty.Should().Be(0m);
        period.ClosingValue.Should().Be(100_000m);
        result.OutCosts.Should().BeEmpty();
    }

    [Fact]
    public void Closing_of_period_is_opening_of_next()
    {
        var result = PeriodicAverageCalculator.Run(2m, 150_000m, 0m, new[]
        {
            Period(Sep1, Sep30, In(10m, 1_000_000m), Out(4m)),
            Period(Oct1, Oct31, In(3m, 270_000m), Out(6m)),
        });

        result.Periods[1].OpeningQty.Should().Be(result.Periods[0].ClosingQty);
        result.Periods[1].OpeningValue.Should().Be(result.Periods[0].ClosingValue);
    }

    [Fact]
    public void Negative_numerator_uses_fallback()
    {
        var result = PeriodicAverageCalculator.Run(-5m, -500_000m, 100_000m,
            new[] { Period(Oct1, Oct31, In(7m, 140_000m), Out(1m)) });

        var period = result.Periods.Single();
        period.AvgCost.Should().Be(100_000m); // numerator -360,000 over 2 would give -180,000 (D37)
        result.OutCosts.Single().CostAmount.Should().Be(100_000m);
        period.ClosingQty.Should().Be(1m);
        period.ClosingValue.Should().Be(-460_000m);
    }
}
