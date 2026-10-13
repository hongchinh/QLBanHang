using FluentAssertions;
using OrderMgmt.Application.Dashboards.Executive.Common;
using Xunit;

namespace OrderMgmt.IntegrationTests.ExecutiveDashboard.Unit;

public class ExecutiveMathTests
{
    [Fact]
    public void DeltaPct_of_an_increase() => ExecutiveMath.DeltaPct(110m, 100m).Should().Be(10m);

    [Fact]
    public void DeltaPct_of_a_decrease() => ExecutiveMath.DeltaPct(90m, 100m).Should().Be(-10m);

    [Fact]
    public void DeltaPct_is_null_without_a_previous_value() => ExecutiveMath.DeltaPct(5m, 0m).Should().BeNull();

    [Fact]
    public void DeltaPct_rounds_to_two_decimals() => ExecutiveMath.DeltaPct(-500m, 3000m).Should().Be(-116.67m);

    [Fact]
    public void DeltaPct_divides_by_the_absolute_previous_value() => ExecutiveMath.DeltaPct(100m, -200m).Should().Be(150m);

    [Fact]
    public void MarginPct_of_a_sale() => ExecutiveMath.MarginPct(600m, 200m).Should().Be(33.33m);

    [Fact]
    public void MarginPct_is_null_without_revenue() => ExecutiveMath.MarginPct(0m, 0m).Should().BeNull();

    [Fact]
    public void MarginPct_of_a_net_return() => ExecutiveMath.MarginPct(-100m, -20m).Should().Be(20m);
}
