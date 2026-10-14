using FluentAssertions;
using OrderMgmt.Application.Organization.SampleData.Generation;
using Xunit;

namespace OrderMgmt.IntegrationTests.SampleData.Unit;

public class SampleCalendarTests
{
    [Theory]
    [InlineData("2026-10-14", "2026-07-01")]
    [InlineData("2026-01-05", "2025-10-01")]
    [InlineData("2026-11-01", "2026-08-01")]
    public void Opening_date_is_first_of_month_three_months_back(string today, string expected) =>
        SampleCalendar.OpeningDate(DateOnly.Parse(today)).Should().Be(DateOnly.Parse(expected));

    [Fact]
    public void Last_document_date_is_yesterday() =>
        SampleCalendar.LastDocumentDate(new DateOnly(2026, 10, 14)).Should().Be(new DateOnly(2026, 10, 13));

    [Fact]
    public void Working_days_skip_sundays() =>
        SampleCalendar.WorkingDays(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 7))
            .Should().Equal(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 2), new DateOnly(2026, 7, 3),
                new DateOnly(2026, 7, 4), new DateOnly(2026, 7, 6), new DateOnly(2026, 7, 7));

    [Fact]
    public void At_is_utc_vn_time() =>
        SampleCalendar.At(new DateOnly(2026, 7, 1), 8 * 60)
            .Should().Be(new DateTimeOffset(2026, 7, 1, 1, 0, 0, TimeSpan.Zero));
}
