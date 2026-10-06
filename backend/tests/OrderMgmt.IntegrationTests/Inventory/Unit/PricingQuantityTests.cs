using FluentAssertions;
using OrderMgmt.Domain.Entities.Catalog;
using OrderMgmt.Domain.Enums;
using Xunit;

namespace OrderMgmt.IntegrationTests.Inventory.Unit;

public class PricingQuantityTests
{
    [Fact]
    public void PerUnit_returns_quantity() =>
        PricingQuantity.Compute(PricingMode.PerUnit, null, null, null, null, 7m).Should().Be(7m);

    [Fact]
    public void PerLinearMeter_is_sheets_times_length_over_1000() =>
        PricingQuantity.Compute(PricingMode.PerLinearMeter, 4m, 2500m, null, null, 0m).Should().Be(10m);

    [Fact]
    public void PerSquareMeter_is_sheets_times_length_times_width_over_1e6() =>
        PricingQuantity.Compute(PricingMode.PerSquareMeter, 2m, 2000m, 1000m, null, 0m).Should().Be(4m);

    [Fact]
    public void PerCubicMeter_is_sheets_times_dimensions_over_1e9() =>
        PricingQuantity.Compute(PricingMode.PerCubicMeter, 1m, 1000m, 1000m, 500m, 0m).Should().Be(0.5m);

    [Fact]
    public void Missing_dimensions_yield_zero() =>
        PricingQuantity.Compute(PricingMode.PerSquareMeter, 2m, 2000m, null, null, 5m).Should().Be(0m);
}
