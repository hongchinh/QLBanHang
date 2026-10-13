using System.Globalization;
using FluentAssertions;
using OrderMgmt.Application.Common;
using Xunit;

namespace OrderMgmt.IntegrationTests.Tt133.Unit;

public class VndAmountInWordsTests
{
    [Theory]
    [InlineData("0", "Không đồng")]
    [InlineData("5", "Năm đồng")]
    [InlineData("10", "Mười đồng")]
    [InlineData("11", "Mười một đồng")]
    [InlineData("15", "Mười lăm đồng")]
    [InlineData("20", "Hai mươi đồng")]
    [InlineData("21", "Hai mươi mốt đồng")]
    [InlineData("24", "Hai mươi bốn đồng")]
    [InlineData("25", "Hai mươi lăm đồng")]
    [InlineData("101", "Một trăm linh một đồng")]
    [InlineData("105", "Một trăm linh năm đồng")]
    [InlineData("110", "Một trăm mười đồng")]
    [InlineData("115", "Một trăm mười lăm đồng")]
    [InlineData("1000", "Một nghìn đồng")]
    [InlineData("1500", "Một nghìn năm trăm đồng")]
    [InlineData("1000005", "Một triệu không trăm linh năm đồng")]
    [InlineData("1005000", "Một triệu không trăm linh năm nghìn đồng")]
    [InlineData("1500000", "Một triệu năm trăm nghìn đồng")]
    [InlineData("2000000000", "Hai tỷ đồng")]
    [InlineData("2000000005", "Hai tỷ không trăm linh năm đồng")]
    [InlineData("1000000000000", "Một nghìn tỷ đồng")]
    [InlineData("1000000000000000000", "Một tỷ tỷ đồng")]
    [InlineData("-105", "Một trăm linh năm đồng")]
    [InlineData("1499.5", "Một nghìn năm trăm đồng")]
    public void Reads_amount(string amount, string expected) =>
        VndAmountInWords.Read(decimal.Parse(amount, CultureInfo.InvariantCulture)).Should().Be(expected);
}
