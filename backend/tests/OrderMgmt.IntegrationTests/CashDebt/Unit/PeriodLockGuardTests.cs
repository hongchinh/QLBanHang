using FluentAssertions;
using OrderMgmt.Application.CashDebt.Common;
using OrderMgmt.Application.Inventory.Common;
using OrderMgmt.Domain.Common;
using Xunit;

namespace OrderMgmt.IntegrationTests.CashDebt.Unit;

public class PeriodLockGuardTests
{
    [Fact]
    public void EnsureNotLocked_with_null_locked_until_does_not_throw()
    {
        var instant = new DateTimeOffset(2026, 10, 8, 10, 30, 0, TimeSpan.FromHours(7));

        var action = () => PeriodLockGuard.EnsureNotLocked(instant, null);

        action.Should().NotThrow();
    }

    [Fact]
    public void EnsureNotLocked_with_vn_date_after_locked_until_does_not_throw()
    {
        // Locked until 2026-10-05 (VN date)
        var lockedUntil = new DateOnly(2026, 10, 5);
        // Instant on 2026-10-08 (VN date, which is after locked until)
        var instant = new DateTimeOffset(2026, 10, 8, 10, 30, 0, TimeSpan.FromHours(7));

        var action = () => PeriodLockGuard.EnsureNotLocked(instant, lockedUntil);

        action.Should().NotThrow();
    }

    [Fact]
    public void EnsureNotLocked_with_vn_date_equal_to_locked_until_throws()
    {
        // Locked until 2026-10-08 (VN date)
        var lockedUntil = new DateOnly(2026, 10, 8);
        // Instant on 2026-10-08 (VN date, which equals locked until)
        var instant = new DateTimeOffset(2026, 10, 8, 10, 30, 0, TimeSpan.FromHours(7));

        var action = () => PeriodLockGuard.EnsureNotLocked(instant, lockedUntil);

        action.Should().Throw<DomainException>()
            .Where(e => e.Code == "PERIOD_LOCKED")
            .Where(e => e.Message.Contains("đã khóa sổ"));
    }

    [Fact]
    public void EnsureNotLocked_with_vn_date_before_locked_until_throws()
    {
        // Locked until 2026-10-10 (VN date)
        var lockedUntil = new DateOnly(2026, 10, 10);
        // Instant on 2026-10-08 (VN date, which is before locked until)
        var instant = new DateTimeOffset(2026, 10, 8, 10, 30, 0, TimeSpan.FromHours(7));

        var action = () => PeriodLockGuard.EnsureNotLocked(instant, lockedUntil);

        action.Should().Throw<DomainException>()
            .Where(e => e.Code == "PERIOD_LOCKED")
            .Where(e => e.Message.Contains("đã khóa sổ"));
    }

    [Fact]
    public void EnsureNotLockedAny_with_null_locked_until_does_not_throw()
    {
        var instants = new[]
        {
            new DateTimeOffset(2026, 10, 8, 10, 30, 0, TimeSpan.FromHours(7)),
            new DateTimeOffset(2026, 10, 9, 14, 20, 0, TimeSpan.FromHours(7))
        };

        var action = () => PeriodLockGuard.EnsureNotLockedAny(instants, null);

        action.Should().NotThrow();
    }

    [Fact]
    public void EnsureNotLockedAny_with_all_dates_after_locked_until_does_not_throw()
    {
        var lockedUntil = new DateOnly(2026, 10, 5);
        var instants = new[]
        {
            new DateTimeOffset(2026, 10, 8, 10, 30, 0, TimeSpan.FromHours(7)),
            new DateTimeOffset(2026, 10, 9, 14, 20, 0, TimeSpan.FromHours(7))
        };

        var action = () => PeriodLockGuard.EnsureNotLockedAny(instants, lockedUntil);

        action.Should().NotThrow();
    }

    [Fact]
    public void EnsureNotLockedAny_with_any_date_on_or_before_locked_until_throws()
    {
        var lockedUntil = new DateOnly(2026, 10, 8);
        var instants = new[]
        {
            new DateTimeOffset(2026, 10, 6, 10, 30, 0, TimeSpan.FromHours(7)),  // Before locked until
            new DateTimeOffset(2026, 10, 9, 14, 20, 0, TimeSpan.FromHours(7))   // After locked until
        };

        var action = () => PeriodLockGuard.EnsureNotLockedAny(instants, lockedUntil);

        action.Should().Throw<DomainException>()
            .Where(e => e.Code == "PERIOD_LOCKED")
            .Where(e => e.Message.Contains("đã khóa sổ"));
    }

    [Fact]
    public void Exception_message_contains_formatted_date()
    {
        var lockedUntil = new DateOnly(2026, 10, 8);
        var instant = new DateTimeOffset(2026, 10, 8, 10, 30, 0, TimeSpan.FromHours(7));

        var action = () => PeriodLockGuard.EnsureNotLocked(instant, lockedUntil);

        action.Should().Throw<DomainException>()
            .Where(e => e.Message.Contains("08/10/2026"));
    }

    [Fact]
    public void EnsureNotLocked_respects_vn_timezone()
    {
        // Locked until 2026-10-08 (VN date)
        var lockedUntil = new DateOnly(2026, 10, 8);
        // Instant at 2026-10-08 23:30 UTC (which is 2026-10-09 06:30 VN time, after lock)
        var instant = new DateTimeOffset(2026, 10, 8, 23, 30, 0, TimeSpan.Zero);

        var action = () => PeriodLockGuard.EnsureNotLocked(instant, lockedUntil);

        action.Should().NotThrow();
    }

    [Fact]
    public void EnsureNotLocked_with_non_vn_timezone_converts_correctly()
    {
        // Locked until 2026-10-08 (VN date)
        var lockedUntil = new DateOnly(2026, 10, 8);
        // Instant at 2026-10-09 10:00 UTC-5
        // UTC time: 2026-10-09 15:00
        // VN time: 2026-10-09 22:00 (which is after locked until)
        var instant = new DateTimeOffset(2026, 10, 9, 10, 0, 0, TimeSpan.FromHours(-5));

        var action = () => PeriodLockGuard.EnsureNotLocked(instant, lockedUntil);

        action.Should().NotThrow();
    }
}
