using OrderMgmt.Domain.Entities.Catalog;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Inventory.StockVouchers.Services;

public sealed record StockLineInput(
    bool TrackInventory, PricingMode PricingMode, bool PriceIncludesVat,
    decimal? SheetCount, decimal? Length, decimal? Width, decimal? Thickness, decimal Quantity,
    decimal UnitPrice, decimal DiscountRate, decimal? DiscountAmount, bool DiscountManual, decimal VatRate);

public sealed record StockHeaderInput(StockDirection Direction, decimal Freight, decimal OrderDiscount, bool NetExcludesVat);

public sealed record StockLineResult(
    decimal Quantity, decimal Amount, decimal DiscountAmount, decimal OrderDiscountAllocated,
    decimal VatAmount, decimal NetAmount, decimal FreightAllocated, decimal InboundValue);

public sealed record StockVoucherTotals(
    decimal GoodsAmount, decimal LineDiscountTotal, decimal DiscountTotal, decimal VatTotal, decimal Total);

public sealed record StockVoucherComputation(IReadOnlyList<StockLineResult> Lines, StockVoucherTotals Totals);

/// Line amounts, discounts, VAT and freight of a stock voucher (D6, D9, D10).
/// Callers guarantee 0 <= manual DiscountAmount <= Amount and 0 <= OrderDiscount <= ΣNet.
public static class StockVoucherCalculator
{
    // `lines` must already be in SortOrder.
    public static StockVoucherComputation Compute(StockHeaderInput header, IReadOnlyList<StockLineInput> lines)
    {
        var quantities = new decimal[lines.Count];
        var amounts = new decimal[lines.Count];
        var discounts = new decimal[lines.Count];
        var nets = new decimal[lines.Count];
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            quantities[i] = R6(PricingQuantity.Compute(line.PricingMode, line.SheetCount, line.Length,
                line.Width, line.Thickness, line.Quantity));
            amounts[i] = R0(quantities[i] * line.UnitPrice);
            discounts[i] = line.DiscountManual
                ? R0(line.DiscountAmount ?? 0m)
                : R0(amounts[i] * line.DiscountRate / 100m);
            nets[i] = amounts[i] - discounts[i];
        }

        var orderDiscounts = Allocate(header.OrderDiscount, nets);
        var netsAfterOrderDiscount = nets.Select((net, i) => net - orderDiscounts[i]).ToArray();

        // Freight only raises the value of tracked goods received (VAT is never part of it, D6).
        var isIn = header.Direction == StockDirection.In;
        var freights = isIn
            ? Allocate(header.Freight, netsAfterOrderDiscount.Select((net, i) => lines[i].TrackInventory ? net : 0m).ToArray())
            : new decimal[lines.Count];

        var results = new StockLineResult[lines.Count];
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var net = netsAfterOrderDiscount[i];
            var vatInclusive = header.Direction == StockDirection.Out && line.PriceIncludesVat;
            var vat = vatInclusive
                ? R0(net - net / (1m + line.VatRate / 100m))
                : R0(net * line.VatRate / 100m);
            var netAmount = vatInclusive || header.NetExcludesVat ? net : net + vat;
            var inboundValue = isIn && line.TrackInventory ? net + freights[i] : 0m;

            results[i] = new StockLineResult(quantities[i], amounts[i], discounts[i], orderDiscounts[i],
                vat, netAmount, freights[i], inboundValue);
        }

        var lineDiscountTotal = results.Sum(r => r.DiscountAmount);
        var totals = new StockVoucherTotals(
            GoodsAmount: results.Sum(r => r.Amount),
            LineDiscountTotal: lineDiscountTotal,
            DiscountTotal: lineDiscountTotal + header.OrderDiscount,
            VatTotal: results.Sum(r => r.VatAmount),
            Total: results.Sum(r => r.NetAmount) + header.Freight);

        return new StockVoucherComputation(results, totals);
    }

    /// Splits `total` by `weights`; the last positive weight takes the rounding remainder.
    /// Nothing is allocated when the weights sum to zero.
    private static decimal[] Allocate(decimal total, IReadOnlyList<decimal> weights)
    {
        var allocations = new decimal[weights.Count];
        var weightSum = weights.Sum();
        if (weightSum <= 0m)
            return allocations;

        var last = -1;
        for (var i = 0; i < weights.Count; i++)
            if (weights[i] > 0m)
                last = i;

        var allocated = 0m;
        for (var i = 0; i < weights.Count; i++)
        {
            if (i == last)
                continue;
            allocations[i] = R0(total * weights[i] / weightSum);
            allocated += allocations[i];
        }
        allocations[last] = total - allocated;
        return allocations;
    }

    private static decimal R0(decimal x) => Math.Round(x, 0, MidpointRounding.AwayFromZero);

    private static decimal R6(decimal x) => Math.Round(x, 6, MidpointRounding.AwayFromZero);
}
