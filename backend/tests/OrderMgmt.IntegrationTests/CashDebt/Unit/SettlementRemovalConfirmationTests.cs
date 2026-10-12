using FluentAssertions;
using OrderMgmt.Application.CashDebt.Ledger;
using OrderMgmt.Domain.Common;
using Xunit;

namespace OrderMgmt.IntegrationTests.CashDebt.Unit;

public class SettlementRemovalConfirmationTests
{
    [Fact]
    public void Code_is_the_settlements_will_be_removed_code()
    {
        SettlementRemovalConfirmation.Code.Should().Be("DEBT_SETTLEMENTS_WILL_BE_REMOVED");
    }

    [Fact]
    public void Create_builds_a_confirmable_exception_with_the_details()
    {
        var details = new Dictionary<string, string[]> { ["PT-1"] = new[] { "Bỏ đối trừ 10 với PC-1" } };

        var error = SettlementRemovalConfirmation.Create(details, new[] { Guid.NewGuid() });

        error.Code.Should().Be(SettlementRemovalConfirmation.Code);
        error.CanConfirm.Should().BeTrue();
        error.Message.Should().Be("Chứng từ đã được đối trừ. Lưu sẽ bỏ các đối trừ sau.");
        error.Details.Should().BeEquivalentTo(details);
    }

    [Fact]
    public void Merge_concatenates_the_values_of_the_same_key_first_then_second()
    {
        var first = SettlementRemovalConfirmation.Create(new Dictionary<string, string[]>
        {
            ["PT-1"] = new[] { "a" },
            ["PT-2"] = new[] { "b" },
        }, new[] { Guid.NewGuid() });
        var second = SettlementRemovalConfirmation.Create(new Dictionary<string, string[]>
        {
            ["PT-1"] = new[] { "c" },
            ["PT-3"] = new[] { "d" },
        }, new[] { Guid.NewGuid() });

        var merged = SettlementRemovalConfirmation.Merge(first, second);

        merged.Code.Should().Be(SettlementRemovalConfirmation.Code);
        merged.CanConfirm.Should().BeTrue();
        merged.Details["PT-1"].Should().Equal("a", "c");
        merged.Details["PT-2"].Should().Equal("b");
        merged.Details["PT-3"].Should().Equal("d");
        merged.Details.Should().HaveCount(3);
    }

    private static (Guid Low, Guid High) TwoIds()
    {
        var x = Guid.NewGuid();
        var y = Guid.NewGuid();
        return x.CompareTo(y) < 0 ? (x, y) : (y, x);
    }

    [Fact]
    public void TokenOf_is_distinct_sorted_N_format_and_comma_separated()
    {
        var (a, b) = TwoIds();

        SettlementRemovalConfirmation.TokenOf(new[] { b, a, b }).Should().Be($"{a:N},{b:N}");
    }

    [Fact]
    public void Covers_is_true_without_a_token()
    {
        SettlementRemovalConfirmation.Covers(null, new[] { Guid.NewGuid() }).Should().BeTrue();
    }

    [Fact]
    public void Covers_is_true_when_the_token_lists_every_id()
    {
        var (a, b) = TwoIds();

        SettlementRemovalConfirmation.Covers(SettlementRemovalConfirmation.TokenOf(new[] { a, b }), new[] { a })
            .Should().BeTrue();
    }

    [Fact]
    public void Covers_is_false_when_an_id_is_missing_from_the_token()
    {
        var (a, b) = TwoIds();

        SettlementRemovalConfirmation.Covers(SettlementRemovalConfirmation.TokenOf(new[] { a }), new[] { a, b })
            .Should().BeFalse();
    }

    [Fact]
    public void Covers_is_false_for_an_empty_token()
    {
        SettlementRemovalConfirmation.Covers("", new[] { Guid.NewGuid() }).Should().BeFalse();
    }

    [Fact]
    public void Covers_a_held_confirmation_by_its_token()
    {
        var (a, b) = TwoIds();
        var held = SettlementRemovalConfirmation.Create(new Dictionary<string, string[]>(), new[] { a, b });

        SettlementRemovalConfirmation.Covers(null, held).Should().BeTrue();
        SettlementRemovalConfirmation.Covers(SettlementRemovalConfirmation.TokenOf(new[] { a, b }), held).Should().BeTrue();
        SettlementRemovalConfirmation.Covers(SettlementRemovalConfirmation.TokenOf(new[] { a }), held).Should().BeFalse();
    }

    [Fact]
    public void Merge_with_one_token_keeps_its_ids()
    {
        var a = Guid.NewGuid();
        var details = new Dictionary<string, string[]> { ["PT-1"] = new[] { "x" } };
        var first = SettlementRemovalConfirmation.Create(details, new[] { a });
        var tokenless = new ConfirmationRequiredException(SettlementRemovalConfirmation.Code, "m", details, true);

        SettlementRemovalConfirmation.Merge(first, tokenless).Token.Should().Be(SettlementRemovalConfirmation.TokenOf(new[] { a }));
        SettlementRemovalConfirmation.Merge(tokenless, first).Token.Should().Be(SettlementRemovalConfirmation.TokenOf(new[] { a }));
    }

    [Fact]
    public void Covers_is_false_for_an_unparsable_token()
    {
        SettlementRemovalConfirmation.Covers("garbage", new[] { Guid.NewGuid() }).Should().BeFalse();
    }

    [Fact]
    public void Create_with_ids_sets_the_token()
    {
        var (a, b) = TwoIds();
        var details = new Dictionary<string, string[]> { ["PT-1"] = new[] { "x" } };

        SettlementRemovalConfirmation.Create(details, new[] { a, b }).Token
            .Should().Be(SettlementRemovalConfirmation.TokenOf(new[] { a, b }));
    }

    [Fact]
    public void Merge_unions_the_tokens()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var c = Guid.NewGuid();
        var details = new Dictionary<string, string[]> { ["PT-1"] = new[] { "x" } };
        var first = SettlementRemovalConfirmation.Create(details, new[] { a, b });
        var second = SettlementRemovalConfirmation.Create(details, new[] { b, c });

        SettlementRemovalConfirmation.Merge(first, second).Token
            .Should().Be(SettlementRemovalConfirmation.TokenOf(new[] { a, b, c }));
    }
}
