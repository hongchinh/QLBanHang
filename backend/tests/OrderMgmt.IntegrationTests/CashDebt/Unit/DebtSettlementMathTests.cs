using FluentAssertions;
using OrderMgmt.Application.CashDebt.Common;
using Xunit;

namespace OrderMgmt.IntegrationTests.CashDebt.Unit;

public class DebtSettlementMathTests
{
    [Theory]
    [InlineData(1_000, 3_000, 1_000, 1_000)]
    [InlineData(5_000, 3_000, 5_000, 3_000)] // overpaid
    [InlineData(1_000, 0, 1_000, 0)] // stock fully matched elsewhere
    [InlineData(1_000, 3_000, 400, 400)] // auto matched elsewhere
    [InlineData(0, 3_000, 0, 0)]
    [InlineData(1_000, -200, 1_000, 0)] // never negative
    public void Auto_amount_cases(int paid, int stockOpen, int autoOpen, int expected)
        => DebtSettlementMath.AutoAmount(paid, stockOpen, autoOpen).Should().Be(expected);

    [Fact]
    public void Open_never_negative()
    {
        DebtSettlementMath.Open(1_000m, 300m).Should().Be(700m);
        DebtSettlementMath.Open(1_000m, 1_000m).Should().Be(0m);
        DebtSettlementMath.Open(1_000m, 1_200m).Should().Be(0m);
    }
}
