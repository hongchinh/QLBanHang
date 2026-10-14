using FluentAssertions;
using OrderMgmt.Application.Inventory.Common;
using Xunit;

namespace OrderMgmt.IntegrationTests.Inventory.Unit;

/// The value of a product under the branch costing scope, shared out over its warehouses by quantity.
public class StockValueSplitTests
{
    [Fact]
    public void Rounding_residual_goes_to_the_last_warehouse() =>
        StockValueSplit.ByQuantity(100m, new[] { 1m, 1m, 1m }).Should().Equal(33m, 33m, 34m);

    [Fact]
    public void Value_follows_the_quantities() =>
        StockValueSplit.ByQuantity(100m, new[] { 1m, 3m }).Should().Equal(25m, 75m);

    [Fact]
    public void Warehouse_without_stock_gets_nothing() =>
        StockValueSplit.ByQuantity(100m, new[] { 2m, 0m }).Should().Equal(100m, 0m);

    [Fact]
    public void Zero_scope_quantity_gives_every_warehouse_zero() =>
        StockValueSplit.ByQuantity(100m, new[] { 0m, 0m }).Should().Equal(0m, 0m);

    [Fact]
    public void Value_of_a_product_held_in_two_warehouses() =>
        StockValueSplit.ByQuantity(1600m, new[] { 6m, 10m }).Should().Equal(600m, 1000m);

    [Fact]
    public void Residual_skips_warehouses_without_stock() =>
        StockValueSplit.ByQuantity(100m, new[] { 1m, 1m, 1m, 0m }).Should().Equal(33m, 33m, 34m, 0m);

    [Fact]
    public void Quantities_that_cancel_out_give_every_warehouse_zero() =>
        StockValueSplit.ByQuantity(100m, new[] { 2m, -2m }).Should().Equal(0m, 0m);

    [Fact]
    public void Halves_round_away_from_zero() =>
        StockValueSplit.ByQuantity(5m, new[] { 1m, 1m }).Should().Equal(3m, 2m);
}
