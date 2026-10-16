using FluentAssertions;
using Xunit;

namespace OrderMgmt.IntegrationTests.Platform;

public class RuntimeVersionTests
{
    [Fact]
    public void Runs_on_dotnet_10() => Environment.Version.Major.Should().Be(10);

    [Fact]
    public void Uses_ef_core_10() =>
        typeof(Microsoft.EntityFrameworkCore.DbContext).Assembly.GetName().Version!.Major.Should().Be(10);
}
