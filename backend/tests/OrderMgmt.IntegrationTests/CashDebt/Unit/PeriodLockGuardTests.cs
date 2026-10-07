using FluentAssertions;
using OrderMgmt.Application.Organization.Branches;
using OrderMgmt.Domain.Common;
using Xunit;

namespace OrderMgmt.IntegrationTests.CashDebt.Unit;

public class PeriodLockGuardTests
{
    private static readonly DateOnly Lock = new(2026, 10, 5);

    [Fact]
    public void Null_lock_never_throws()
    {
        var instants = new[] { DateTimeOffset.Parse("2020-01-01T00:00:00Z") };
        var dates = new[] { new DateOnly(2020, 1, 1) };

        PeriodLockGuard.EnsureNotLocked(null, instants);
        PeriodLockGuard.EnsureNotLocked(null, dates);
    }

    [Fact]
    public void Instant_on_lock_date_in_vn_time_throws()
    {
        // 16:59Z = 23:59 VN on 10-05 -> locked; 17:00Z = 00:00 VN on 10-06 -> allowed.
        var locked = () => PeriodLockGuard.EnsureNotLocked(Lock, new[] { DateTimeOffset.Parse("2026-10-05T16:59:00Z") });
        var open = () => PeriodLockGuard.EnsureNotLocked(Lock, new[] { DateTimeOffset.Parse("2026-10-05T17:00:00Z") });

        locked.Should().Throw<DomainException>().Where(e => e.Code == "PERIOD_LOCKED")
            .WithMessage("*05/10/2026*");
        open.Should().NotThrow();
    }

    [Fact]
    public void Date_overload_throws_on_or_before_lock()
    {
        var onLock = () => PeriodLockGuard.EnsureNotLocked(Lock, new[] { new DateOnly(2026, 10, 5) });
        var before = () => PeriodLockGuard.EnsureNotLocked(Lock, new[] { new DateOnly(2026, 10, 1) });
        var after = () => PeriodLockGuard.EnsureNotLocked(Lock, new[] { new DateOnly(2026, 10, 6) });

        onLock.Should().Throw<DomainException>().Where(e => e.Code == "PERIOD_LOCKED");
        before.Should().Throw<DomainException>().Where(e => e.Code == "PERIOD_LOCKED");
        after.Should().NotThrow();
    }
}
