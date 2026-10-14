using FluentAssertions;
using OrderMgmt.Application.Tt133.Vouchers;
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
}
