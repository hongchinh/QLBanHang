using FluentAssertions;
using OrderMgmt.Application.CashDebt.Ledger;
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

        var error = SettlementRemovalConfirmation.Create(details);

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
        });
        var second = SettlementRemovalConfirmation.Create(new Dictionary<string, string[]>
        {
            ["PT-1"] = new[] { "c" },
            ["PT-3"] = new[] { "d" },
        });

        var merged = SettlementRemovalConfirmation.Merge(first, second);

        merged.Code.Should().Be(SettlementRemovalConfirmation.Code);
        merged.CanConfirm.Should().BeTrue();
        merged.Details["PT-1"].Should().Equal("a", "c");
        merged.Details["PT-2"].Should().Equal("b");
        merged.Details["PT-3"].Should().Equal("d");
        merged.Details.Should().HaveCount(3);
    }
}
