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

        var first = Enumerable.Range(0, 20).Select(_ => a.Next(0, 1000)).ToList();
        var second = Enumerable.Range(0, 20).Select(_ => b.Next(0, 1000)).ToList();
        var other = Enumerable.Range(0, 20).Select(_ => c.Next(0, 1000)).ToList();

        second.Should().Equal(first);
        other.Should().NotEqual(first);
    }
}
