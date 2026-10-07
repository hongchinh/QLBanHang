using FluentAssertions;
using OrderMgmt.Application.Inventory.StockVouchers.Services;
using OrderMgmt.Domain.Enums;
using Xunit;

namespace OrderMgmt.IntegrationTests.Inventory.Unit;

public class StockVoucherCalculatorTests
{
    private static StockLineInput Line(decimal quantity, decimal unitPrice, decimal vatRate,
        PricingMode mode = PricingMode.PerUnit, decimal? sheets = null, decimal? length = null,
        decimal? width = null, decimal? thickness = null, decimal discountRate = 0m,
        decimal? discountAmount = null, bool discountManual = false, bool priceIncludesVat = false,
        bool trackInventory = true) =>
        new(trackInventory, mode, priceIncludesVat, sheets, length, width, thickness, quantity,
            unitPrice, discountRate, discountAmount, discountManual, vatRate);

    private static StockHeaderInput Header(StockDirection direction, decimal freight = 0m,
        decimal orderDiscount = 0m, bool netExcludesVat = false) =>
        new(direction, freight, orderDiscount, netExcludesVat);

    [Fact]
    public void Out_example_allocates_order_discount_before_vat_and_handles_vat_inclusive_price()
    {
        var result = StockVoucherCalculator.Compute(
            Header(StockDirection.Out, freight: 20_000m, orderDiscount: 30_000m),
            new[]
            {
                Line(10m, 100_000m, 8m, discountRate: 10m),
                Line(0m, 50_000m, 10m, PricingMode.PerSquareMeter, sheets: 2m, length: 2000m, width: 1000m, priceIncludesVat: true),
            });

        result.Lines[0].Should().Be(new StockLineResult(
            Quantity: 10m, Amount: 1_000_000m, DiscountAmount: 100_000m, OrderDiscountAllocated: 24_545m,
            VatAmount: 70_036m, NetAmount: 945_491m, FreightAllocated: 0m, InboundValue: 0m));
        result.Lines[1].Should().Be(new StockLineResult(
            Quantity: 4m, Amount: 200_000m, DiscountAmount: 0m, OrderDiscountAllocated: 5_455m,
            VatAmount: 17_686m, NetAmount: 194_545m, FreightAllocated: 0m, InboundValue: 0m));
        result.Totals.Should().Be(new StockVoucherTotals(
            GoodsAmount: 1_200_000m, LineDiscountTotal: 100_000m, DiscountTotal: 130_000m,
            VatTotal: 87_722m, Total: 1_160_036m));
    }

    [Fact]
    public void In_example_allocates_freight_to_tracked_lines_only()
    {
        var result = StockVoucherCalculator.Compute(
            Header(StockDirection.In, freight: 200_000m, orderDiscount: 100_000m),
            new[]
            {
                Line(100m, 50_000m, 10m),
                Line(0m, 1_500_000m, 8m, PricingMode.PerCubicMeter, sheets: 10m, length: 2000m, width: 1000m, thickness: 50m, discountRate: 5m),
                Line(1m, 300_000m, 8m, trackInventory: false),
            });

        result.Lines.Select(l => l.Amount - l.DiscountAmount).Should().Equal(5_000_000m, 1_425_000m, 300_000m);
        result.Lines.Select(l => l.OrderDiscountAllocated).Should().Equal(74_349m, 21_190m, 4_461m);
        result.Lines.Select(l => l.Amount - l.DiscountAmount - l.OrderDiscountAllocated)
            .Should().Equal(4_925_651m, 1_403_810m, 295_539m);
        result.Lines.Select(l => l.VatAmount).Should().Equal(492_565m, 112_305m, 23_643m);
        result.Lines.Select(l => l.NetAmount).Should().Equal(5_418_216m, 1_516_115m, 319_182m);
        result.Lines.Select(l => l.FreightAllocated).Should().Equal(155_642m, 44_358m, 0m);
        result.Lines.Select(l => l.InboundValue).Should().Equal(5_081_293m, 1_448_168m, 0m);
        result.Totals.Should().Be(new StockVoucherTotals(
            GoodsAmount: 6_800_000m, LineDiscountTotal: 75_000m, DiscountTotal: 175_000m,
            VatTotal: 628_513m, Total: 7_453_513m));
    }

    [Fact]
    public void Manual_discount_amount_overrides_rate()
    {
        var line = StockVoucherCalculator.Compute(
            Header(StockDirection.Out),
            new[] { Line(3m, 33_333m, 0m, discountRate: 50m, discountAmount: 1_000m, discountManual: true) }).Lines[0];

        line.Amount.Should().Be(99_999m);
        line.DiscountAmount.Should().Be(1_000m);
        line.NetAmount.Should().Be(98_999m);
    }

    [Fact]
    public void Net_excludes_vat_keeps_vat_out_of_net_amount()
    {
        var line = StockVoucherCalculator.Compute(
            Header(StockDirection.Out, netExcludesVat: true),
            new[] { Line(1m, 100_000m, 10m) }).Lines[0];

        line.VatAmount.Should().Be(10_000m);
        line.NetAmount.Should().Be(100_000m);
    }

    [Fact]
    public void Vat_inclusive_flag_is_ignored_on_stock_in()
    {
        var line = StockVoucherCalculator.Compute(
            Header(StockDirection.In),
            new[] { Line(1m, 110_000m, 10m, priceIncludesVat: true) }).Lines[0];

        line.VatAmount.Should().Be(11_000m);
        line.NetAmount.Should().Be(121_000m);
    }

    [Fact]
    public void Order_discount_remainder_goes_to_last_line_with_positive_net()
    {
        var result = StockVoucherCalculator.Compute(
            Header(StockDirection.Out, orderDiscount: 10_000m),
            new[] { Line(1m, 100_000m, 0m), Line(1m, 50_000m, 0m), Line(1m, 0m, 0m) });

        result.Lines.Select(l => l.OrderDiscountAllocated).Should().Equal(6_667m, 3_333m, 0m);
    }

    [Fact]
    public void Order_discount_falls_back_to_largest_remainder_when_last_line_would_go_negative()
    {
        // D10 alone gives [0, 0, 0, 0, 2]: the last line (net 1) would end at net -1.
        var result = StockVoucherCalculator.Compute(
            Header(StockDirection.Out, orderDiscount: 2m),
            new[] { Line(1m, 14m, 10m), Line(1m, 14m, 10m), Line(1m, 14m, 10m), Line(1m, 14m, 10m), Line(1m, 1m, 10m) });

        result.Lines.Select(l => l.OrderDiscountAllocated).Should().Equal(0m, 0m, 1m, 1m, 0m);
        result.Lines.Should().OnlyContain(l => l.NetAmount >= 0m && l.VatAmount >= 0m);
        result.Totals.DiscountTotal.Should().Be(2m);
    }

    [Fact]
    public void Freight_falls_back_to_largest_remainder_when_last_line_would_go_negative()
    {
        // D10 alone gives [1, 1, 1, 1, 1, -2].
        var result = StockVoucherCalculator.Compute(
            Header(StockDirection.In, freight: 3m),
            Enumerable.Range(0, 6).Select(_ => Line(1m, 1m, 0m)).ToArray());

        result.Lines.Select(l => l.FreightAllocated).Should().Equal(0m, 0m, 0m, 1m, 1m, 1m);
        result.Lines.Select(l => l.InboundValue).Should().Equal(1m, 1m, 1m, 2m, 2m, 2m);
        result.Totals.Total.Should().Be(9m);
    }

    [Fact]
    public void Quantity_is_rounded_to_six_decimals_before_amount()
    {
        var line = StockVoucherCalculator.Compute(
            Header(StockDirection.In),
            new[] { Line(0m, 10_000_000m, 0m, PricingMode.PerCubicMeter, sheets: 1m, length: 333m, width: 333m, thickness: 33m) }).Lines[0];

        line.Quantity.Should().Be(0.003659m);
        line.Amount.Should().Be(36_590m);
    }

    [Fact]
    public void Freight_without_tracked_lines_is_not_allocated()
    {
        var result = StockVoucherCalculator.Compute(
            Header(StockDirection.In, freight: 50_000m),
            new[] { Line(1m, 100_000m, 0m, trackInventory: false), Line(2m, 30_000m, 0m, trackInventory: false) });

        result.Lines.Select(l => l.FreightAllocated).Should().Equal(0m, 0m);
        result.Lines.Select(l => l.InboundValue).Should().Equal(0m, 0m);
        result.Totals.Total.Should().Be(210_000m);
    }
}
