using FluentAssertions;
using OrderMgmt.Application.CashDebt.Common;
using Xunit;

namespace OrderMgmt.IntegrationTests.CashDebt.Unit;

public class DebtAgingTests
{
    [Fact]
    public void Parse_defaults_and_valid_input()
    {
        DebtAging.ParseBoundaries(null).Should().Equal(30, 60, 90);
        DebtAging.ParseBoundaries("").Should().Equal(30, 60, 90);
        DebtAging.ParseBoundaries("  ").Should().Equal(30, 60, 90);
        DebtAging.ParseBoundaries("15, 45").Should().Equal(15, 45);
        DebtAging.ParseBoundaries("7").Should().Equal(7);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("30,,60")]
    [InlineData("0,30")]
    [InlineData("-5")]
    [InlineData("60,30")]
    [InlineData("30,30")]
    [InlineData("1,2,3,4,5,6,7")]
    public void Parse_rejects_invalid_input(string text)
    {
        DebtAging.ParseBoundaries(text).Should().BeNull();
    }

    [Fact]
    public void Bucket_index_edges()
    {
        var b = new[] { 30, 60, 90 };
        DebtAging.BucketIndex(-4, b).Should().Be(0);
        DebtAging.BucketIndex(0, b).Should().Be(0);
        DebtAging.BucketIndex(30, b).Should().Be(0);
        DebtAging.BucketIndex(31, b).Should().Be(1);
        DebtAging.BucketIndex(60, b).Should().Be(1);
        DebtAging.BucketIndex(61, b).Should().Be(2);
        DebtAging.BucketIndex(90, b).Should().Be(2);
        DebtAging.BucketIndex(91, b).Should().Be(3);
        DebtAging.BucketIndex(400, b).Should().Be(3);

        var one = new[] { 10 };
        DebtAging.BucketIndex(10, one).Should().Be(0);
        DebtAging.BucketIndex(11, one).Should().Be(1);
    }

    [Fact]
    public void Ranges_for_aging_and_overdue()
    {
        var b = new[] { 30, 60, 90 };
        DebtAging.Ranges(b, 0).Should().Equal((0, (int?)30), (31, 60), (61, 90), (91, null));
        DebtAging.Ranges(b, 1).Should().Equal((1, (int?)30), (31, 60), (61, 90), (91, null));
    }
}
