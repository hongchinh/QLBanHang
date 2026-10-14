using FluentAssertions;
using OrderMgmt.Application.Organization.SampleData.Generation;
using Xunit;

namespace OrderMgmt.IntegrationTests.SampleData.Unit;

public class SampleRandomTests
{
    [Fact]
    public void Fnv1a_of_branch_code()
    {
        SampleRandom.Fnv1a32("DEMO01").Should().Be(1082858617u);
        SampleRandom.Fnv1a32("CN-DEMO").Should().Be(3232239808u);
    }

    [Fact]
    public void Same_code_gives_same_sequence()
    {
        var a = SampleRandom.ForBranch("DEMO01");
        var b = SampleRandom.ForBranch("DEMO01");
        var c = SampleRandom.ForBranch("DEMO02");

        var first = Enumerable.Range(0, 20).Select(_ => a.Between(0, 999)).ToList();
        var second = Enumerable.Range(0, 20).Select(_ => b.Between(0, 999)).ToList();
        var other = Enumerable.Range(0, 20).Select(_ => c.Between(0, 999)).ToList();

        second.Should().Equal(first);
        other.Should().NotEqual(first);
    }

    [Fact]
    public void Between_is_inclusive_on_both_ends()
    {
        var random = SampleRandom.ForBranch("DEMO01");
        var values = Enumerable.Range(0, 1000).Select(_ => random.Between(1, 6)).ToList();

        values.Should().OnlyContain(v => v >= 1 && v <= 6);
        values.Should().Contain(1).And.Contain(6);
    }

    [Fact]
    public void NextAmount_stays_on_the_step_grid_and_reaches_both_ends()
    {
        var random = SampleRandom.ForBranch("DEMO01");
        var values = Enumerable.Range(0, 1000).Select(_ => random.NextAmount(300_000, 1_500_000, 100_000)).ToList();

        values.Should().OnlyContain(v => v >= 300_000 && v <= 1_500_000 && v % 100_000 == 0);
        values.Should().Contain(300_000).And.Contain(1_500_000);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void NextAmount_rejects_a_non_positive_step(int step)
    {
        var random = SampleRandom.ForBranch("DEMO01");
        var act = () => random.NextAmount(1, 10, step);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void NextAmount_rejects_max_below_min()
    {
        var random = SampleRandom.ForBranch("DEMO01");
        var act = () => random.NextAmount(10, 1, 1);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Pick_returns_list_elements()
    {
        var random = SampleRandom.ForBranch("DEMO01");
        var items = new[] { "a", "b", "c" };
        var picked = Enumerable.Range(0, 200).Select(_ => random.Pick(items)).ToList();

        picked.Should().OnlyContain(p => items.Contains(p));
        picked.Distinct().Should().HaveCount(3);
    }

    [Fact]
    public void Chance_zero_never_and_hundred_always()
    {
        var random = SampleRandom.ForBranch("DEMO01");
        Enumerable.Range(0, 500).Select(_ => random.Chance(0)).Should().OnlyContain(x => !x);
        Enumerable.Range(0, 500).Select(_ => random.Chance(100)).Should().OnlyContain(x => x);
    }

    [Fact]
    public void NextDouble_is_in_unit_range()
    {
        var random = SampleRandom.ForBranch("DEMO01");
        Enumerable.Range(0, 200).Select(_ => random.NextDouble()).Should().OnlyContain(x => x >= 0 && x < 1);
    }
}
