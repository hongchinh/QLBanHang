using FluentAssertions;
using OrderMgmt.Application.Common;
using Xunit;

namespace OrderMgmt.IntegrationTests.CashDebt.Unit;

public class LikePatternTests
{
    [Fact]
    public void Escape_prefixes_percent_underscore_and_backslash()
    {
        LikePattern.Escape("50%_a\\b").Should().Be("50\\%\\_a\\\\b");
    }

    [Fact]
    public void Escape_character_is_a_backslash()
    {
        LikePattern.EscapeCharacter.Should().Be("\\");
    }
}
