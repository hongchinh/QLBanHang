using FluentAssertions;
using OrderMgmt.Application.Inventory.Common;
using OrderMgmt.Domain.Enums;
using Xunit;

namespace OrderMgmt.IntegrationTests.Inventory.Unit;

public class CostProvisionalTests
{
    private static DateOnly D(int month, int day) => new(2026, month, day);

    [Fact]
    public void Open_month_is_provisional() =>
        CostProvisional.IsProvisional(D(10, 31), CostingPeriod.Month, D(10, 13)).Should().BeTrue();

    [Fact]
    public void Ended_month_is_final() =>
        CostProvisional.IsProvisional(D(9, 30), CostingPeriod.Month, D(10, 13)).Should().BeFalse();

    [Fact]
    public void Ended_quarter_is_final() =>
        CostProvisional.IsProvisional(D(9, 30), CostingPeriod.Quarter, D(10, 13)).Should().BeFalse();

    [Fact]
    public void Open_quarter_is_provisional() =>
        CostProvisional.IsProvisional(D(10, 1), CostingPeriod.Quarter, D(10, 13)).Should().BeTrue();

    [Fact]
    public void Period_that_ends_today_is_provisional() =>
        CostProvisional.IsProvisional(D(10, 13), CostingPeriod.Month, D(10, 31)).Should().BeTrue();
}
