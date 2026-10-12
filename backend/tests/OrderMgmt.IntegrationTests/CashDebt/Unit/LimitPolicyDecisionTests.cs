using FluentAssertions;
using OrderMgmt.Application.CashDebt.Common;
using OrderMgmt.Domain.Enums;
using Xunit;

namespace OrderMgmt.IntegrationTests.CashDebt.Unit;

public class LimitPolicyDecisionTests
{
    [Theory]
    [InlineData(LimitPolicy.Allow, false, false, PolicyOutcome.Pass)]
    [InlineData(LimitPolicy.Allow, false, true, PolicyOutcome.Pass)]
    [InlineData(LimitPolicy.Allow, true, false, PolicyOutcome.Pass)]
    [InlineData(LimitPolicy.Allow, true, true, PolicyOutcome.Pass)]
    [InlineData(LimitPolicy.Warn, false, false, PolicyOutcome.Pass)]
    [InlineData(LimitPolicy.Warn, false, true, PolicyOutcome.Pass)]
    [InlineData(LimitPolicy.Warn, true, false, PolicyOutcome.Warn)]
    [InlineData(LimitPolicy.Warn, true, true, PolicyOutcome.Pass)]
    [InlineData(LimitPolicy.Block, false, false, PolicyOutcome.Pass)]
    [InlineData(LimitPolicy.Block, false, true, PolicyOutcome.Pass)]
    [InlineData(LimitPolicy.Block, true, false, PolicyOutcome.Block)]
    [InlineData(LimitPolicy.Block, true, true, PolicyOutcome.Block)]
    public void Decide_matrix(LimitPolicy policy, bool violated, bool acknowledged, PolicyOutcome expected)
        => LimitPolicyDecision.Decide(policy, violated, acknowledged).Should().Be(expected);

    [Fact]
    public void Credit_limit_violation_cases()
    {
        CreditLimitRule.IsViolation(limit: null, before: 0m, after: 1_000_000m).Should().BeFalse("no limit is set");
        CreditLimitRule.IsViolation(limit: 50m, before: 40m, after: 60m).Should().BeTrue("the debt rises above the limit");
        CreditLimitRule.IsViolation(limit: 50m, before: 70m, after: 60m).Should().BeFalse("the edit lowers the debt");
        CreditLimitRule.IsViolation(limit: 50m, before: 40m, after: 50m).Should().BeFalse("reaching the limit is allowed");
    }
}
