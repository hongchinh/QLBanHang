using FluentAssertions;
using OrderMgmt.Application.Inventory.Numbering;
using OrderMgmt.Domain.Enums;
using Xunit;

namespace OrderMgmt.IntegrationTests.Inventory.Unit;

public class DocumentNumberFormatterTests
{
    [Fact]
    public void Format_default_pattern() =>
        DocumentNumberFormatter.Format("{KH}{STT}", "PN", 5, 12, new DateOnly(2026, 10, 6)).Should().Be("PN00012");

    [Fact]
    public void Format_with_year_and_month() =>
        DocumentNumberFormatter.Format("{KH}{NAM}{THANG}-{STT}", "PX", 4, 7, new DateOnly(2026, 3, 1)).Should().Be("PX202603-0007");

    [Fact]
    public void Counter_longer_than_length_is_not_truncated() =>
        DocumentNumberFormatter.Format("{KH}{STT}", "PN", 3, 12345, new DateOnly(2026, 10, 6)).Should().Be("PN12345");

    [Fact]
    public void PeriodKey_per_policy()
    {
        var date = new DateOnly(2026, 3, 15);
        DocumentNumberFormatter.PeriodKey(NumberingResetPolicy.None, date).Should().Be("");
        DocumentNumberFormatter.PeriodKey(NumberingResetPolicy.Monthly, date).Should().Be("2026-03");
        DocumentNumberFormatter.PeriodKey(NumberingResetPolicy.Yearly, date).Should().Be("2026");
    }

    [Theory]
    [InlineData("{KH}", 5, NumberingResetPolicy.None)]            // missing {STT}
    [InlineData("{KH}{NAM}{STT}", 5, NumberingResetPolicy.Monthly)] // monthly without {THANG}
    [InlineData("{KH}{THANG}{STT}", 5, NumberingResetPolicy.Monthly)] // monthly without {NAM}
    [InlineData("{KH}{STT}", 5, NumberingResetPolicy.Yearly)]     // yearly without {NAM}
    [InlineData("{KH}{STT}", 0, NumberingResetPolicy.None)]       // length too small
    [InlineData("{KH}{STT}", 11, NumberingResetPolicy.None)]      // length too large
    public void Validate_rejects_missing_tokens_and_bad_length(string pattern, int length, NumberingResetPolicy policy) =>
        DocumentNumberFormatter.Validate(pattern, length, policy).Should().NotBeEmpty();

    [Fact]
    public void Validate_accepts_valid_monthly_pattern() =>
        DocumentNumberFormatter.Validate("{KH}{NAM}{THANG}{STT}", 5, NumberingResetPolicy.Monthly).Should().BeEmpty();
}
