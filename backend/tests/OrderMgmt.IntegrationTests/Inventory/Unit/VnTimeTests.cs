using FluentAssertions;
using OrderMgmt.Application.Inventory.Common;
using Xunit;

namespace OrderMgmt.IntegrationTests.Inventory.Unit;

public class VnTimeTests
{
    [Fact]
    public void ToVnDate_crosses_midnight_at_17_utc() =>
        VnTime.ToVnDate(new DateTimeOffset(2026, 10, 6, 17, 30, 0, TimeSpan.Zero)).Should().Be(new DateOnly(2026, 10, 7));

    [Fact]
    public void StartOfDay_is_midnight_vn_as_utc_instant()
    {
        var start = VnTime.StartOfDay(new DateOnly(2026, 10, 7));

        start.Should().Be(new DateTimeOffset(2026, 10, 6, 17, 0, 0, TimeSpan.Zero));
        start.Offset.Should().Be(TimeSpan.Zero); // Npgsql rejects non-zero offsets for timestamptz (D27)
    }

    [Fact]
    public void StartOfNextDay()
    {
        var next = VnTime.StartOfNextDay(new DateOnly(2026, 10, 7));

        next.Should().Be(VnTime.StartOfDay(new DateOnly(2026, 10, 8)));
        next.Offset.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void ToUtc_keeps_the_instant_and_drops_the_offset()
    {
        var utc = VnTime.ToUtc(new DateTimeOffset(2026, 10, 2, 8, 0, 0, TimeSpan.FromHours(7)));

        utc.Should().Be(new DateTimeOffset(2026, 10, 2, 1, 0, 0, TimeSpan.Zero));
        utc.Offset.Should().Be(TimeSpan.Zero);
    }
}
