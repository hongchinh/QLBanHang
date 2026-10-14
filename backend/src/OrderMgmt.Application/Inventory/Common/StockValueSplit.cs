namespace OrderMgmt.Application.Inventory.Common;

/// Shares the stock value of a costing scope out over its warehouses.
public static class StockValueSplit
{
    /// Under the branch costing scope a product has one value for the whole branch. Each warehouse gets
    /// R0(scopeValue × qty / scopeQty) and the rounding residual goes to the last warehouse holding stock, so the parts
    /// add up to the scope value. The quantities come in warehouse code order. A zero scope quantity gives every
    /// warehouse 0.
    public static List<decimal> ByQuantity(decimal scopeValue, IReadOnlyList<decimal> quantities)
    {
        var scopeQty = quantities.Sum();
        if (scopeQty == 0m)
            return quantities.Select(_ => 0m).ToList();

        var values = quantities.Select(q => R0(scopeValue * q / scopeQty)).ToList();
        var last = quantities.Count - 1;
        while (quantities[last] == 0m)
            last--;
        values[last] += scopeValue - values.Sum();
        return values;
    }

    private static decimal R0(decimal x) => Math.Round(x, 0, MidpointRounding.AwayFromZero);
}
