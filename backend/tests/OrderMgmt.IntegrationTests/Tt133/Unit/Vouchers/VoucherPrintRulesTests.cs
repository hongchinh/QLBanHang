using FluentAssertions;
using OrderMgmt.Application.CashDebt.PaymentQr;
using OrderMgmt.Application.Tt133.Vouchers;
using OrderMgmt.Domain.Enums;
using Xunit;

namespace OrderMgmt.IntegrationTests.Tt133.Unit.Vouchers;

public class VoucherPrintRulesTests
{
    [Fact]
    public void FirstFilled_returns_first_non_blank_trimmed()
    {
        VoucherPrintRules.FirstFilled(null, "  ", " B ").Should().Be("B");
        VoucherPrintRules.FirstFilled(null, " ").Should().BeNull();
    }

    [Theory]
    [InlineData("Thu tiền hàng", new[] { "Dòng 1" }, "Thu khác", "Thu tiền hàng")]
    [InlineData(null, new[] { "Dòng 1", "Dòng 2" }, "Thu khác", "Dòng 1; Dòng 2")]
    [InlineData(" ", new[] { "", " ", "Dòng 3" }, "Thu khác", "Dòng 3")]
    [InlineData("", new[] { "", " " }, "Thu khác", "Thu khác")]
    [InlineData(null, new string[0], null, null)]
    public void CashReason_falls_back(string? description, string[] lines, string? reasonName, string? expected)
        => VoucherPrintRules.CashReason(description, lines, reasonName).Should().Be(expected);

    [Theory]
    [InlineData("2", "Kèm theo: 2 chứng từ gốc")]
    [InlineData(" 02 ", "Kèm theo: 2 chứng từ gốc")]
    [InlineData("HĐ 0001234", "Kèm theo: HĐ 0001234")]
    [InlineData(null, "Kèm theo: …………………… chứng từ gốc")]
    [InlineData("  ", "Kèm theo: …………………… chứng từ gốc")]
    public void AttachedDocsLine_formats(string? input, string expected)
        => VoucherPrintRules.AttachedDocsLine(input).Should().Be(expected);

    [Theory]
    [InlineData(1_500_000, "1.500.000")]
    [InlineData(0, "0")]
    [InlineData(1234.6, "1.235")]
    public void Money_uses_vi_VN_grouping(double amount, string expected)
        => VoucherPrintRules.Money((decimal)amount).Should().Be(expected);

    [Fact]
    public void BatchFileBase_uses_vn_time()
        => VoucherPrintRules.BatchFileBase("PhieuThu", new DateTimeOffset(2026, 10, 14, 9, 5, 0, TimeSpan.Zero))
            .Should().Be("PhieuThu_20261014-1605");

    [Fact]
    public void Dots_is_the_frozen_literal()
        => VoucherPrintRules.Dots.Should().Be("……………………");

    public static TheoryData<string, PricingMode, double?, double?, double?, double?, string> LineNameCases => new()
    {
        { "Kính", PricingMode.PerUnit, 1200, 2400, 5, 10, "Kính" },
        { "Kính", PricingMode.PerSquareMeter, 1200, 2400, null, 10, "Kính (1200×2400 mm, 10 tấm)" },
        { "Xốp", PricingMode.PerCubicMeter, 2000, 1000, 50, 10, "Xốp (2000×1000×50 mm, 10 tấm)" },
        { "Thép", PricingMode.PerLinearMeter, 6000, null, null, 4, "Thép (6000 mm, 4 tấm)" },
        { "Kính", PricingMode.PerSquareMeter, 1200.5, 0, null, 2, "Kính (1200,5 mm, 2 tấm)" },
        { "Kính", PricingMode.PerSquareMeter, null, null, null, 3, "Kính (3 tấm)" },
        { "Kính", PricingMode.PerSquareMeter, null, null, null, null, "Kính" },
    };

    [Theory]
    [MemberData(nameof(LineNameCases))]
    public void LineName_adds_dimensions_for_dimensional_modes(string name, PricingMode mode, double? length,
        double? width, double? thickness, double? sheets, string expected)
        => VoucherPrintRules.LineName(name, mode, (decimal?)length, (decimal?)width, (decimal?)thickness, (decimal?)sheets)
            .Should().Be(expected);

    [Fact]
    public void VoucherTotals_single_cong_row_when_nothing_extra()
    {
        VoucherPrintRules.VoucherTotals(1_000_000, 0, 0, 0, 1_000_000)
            .Should().Equal(new StockVoucherPrintTotal("Cộng", 1_000_000));
        VoucherPrintRules.VoucherTotals(2_200_000, 0, 0, 0, 2_200_000)
            .Should().Equal(new StockVoucherPrintTotal("Cộng", 2_200_000));
    }

    [Fact]
    public void VoucherTotals_lists_discount_vat_and_freight()
    {
        var rows = VoucherPrintRules.VoucherTotals(1_500_000, 50_000, 10_000, 20_000, 1_604_000);
        rows.Should().Equal(
            new StockVoucherPrintTotal("Cộng tiền hàng", 1_500_000),
            new StockVoucherPrintTotal("Chiết khấu", 60_000),
            new StockVoucherPrintTotal("Thuế GTGT", 144_000),
            new StockVoucherPrintTotal("Cước vận chuyển", 20_000),
            new StockVoucherPrintTotal("Tổng cộng", 1_604_000));
        (rows[0].Amount - rows[1].Amount + rows[2].Amount + rows[3].Amount).Should().Be(rows[^1].Amount);
    }

    [Fact]
    public void VoucherTotals_freight_only()
    {
        var rows = VoucherPrintRules.VoucherTotals(500_000, 0, 0, 30_000, 530_000);
        rows.Should().Equal(
            new StockVoucherPrintTotal("Cộng tiền hàng", 500_000),
            new StockVoucherPrintTotal("Cước vận chuyển", 30_000),
            new StockVoucherPrintTotal("Tổng cộng", 530_000));
        (rows[0].Amount + rows[1].Amount).Should().Be(rows[^1].Amount);
    }

    private static VoucherPaymentQrDto Qr(decimal amount, decimal total, decimal cashPaid) => new()
    {
        Amount = amount,
        Total = total,
        CashPaid = cashPaid,
        BankShortName = "Vietcombank",
        AccountNumber = "0123456789",
    };

    [Fact]
    public void QrAmountLine_formats_the_amount()
        => VoucherPrintRules.QrAmountLine(Qr(1_000_000, 1_000_000, 0)).Should().Be("Số tiền: 1.000.000 đồng");

    [Fact]
    public void QrBreakdownLine_is_null_without_a_cash_part()
        => VoucherPrintRules.QrBreakdownLine(Qr(1_000_000, 1_000_000, 0)).Should().BeNull();

    [Fact]
    public void QrBreakdownLine_shows_total_cash_and_transfer()
        => VoucherPrintRules.QrBreakdownLine(Qr(1_000_000, 1_500_000, 500_000))
            .Should().Be("Tổng 1.500.000 · Đã trả tiền mặt 500.000 · Cần chuyển khoản 1.000.000");

    [Fact]
    public void QrBankCaption_and_scan_caption()
    {
        VoucherPrintRules.QrBankCaption(Qr(1, 1, 0)).Should().Be("Vietcombank · 0123456789");
        VoucherPrintRules.QrScanCaption.Should().Be("Quét để chuyển khoản");
    }
}
