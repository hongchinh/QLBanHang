using System.Globalization;
using OrderMgmt.Application.Inventory.Common;
using OrderMgmt.Domain.Common;

namespace OrderMgmt.Application.Organization.Branches;

/// Shared period-lock check: a document dated on or before the branch's LockedUntil (VN date) is rejected.
public static class PeriodLockGuard
{
    public static void EnsureNotLocked(DateOnly? lockedUntil, IEnumerable<DateTimeOffset> instants)
    {
        if (lockedUntil is { } locked && instants.Any(at => IsLocked(locked, at)))
            throw Locked(locked);
    }

    /// True when `instant` falls on or before the branch's LockedUntil (VN date), the case EnsureNotLocked rejects.
    public static bool IsLocked(DateOnly? lockedUntil, DateTimeOffset instant) =>
        lockedUntil is { } locked && VnTime.ToVnDate(instant) <= locked;

    public static void EnsureNotLocked(DateOnly? lockedUntil, IEnumerable<DateOnly> dates)
    {
        if (lockedUntil is { } locked && dates.Any(d => d <= locked))
            throw Locked(locked);
    }

    private static DomainException Locked(DateOnly locked) =>
        new("PERIOD_LOCKED",
            string.Create(CultureInfo.InvariantCulture, $"Ngày chứng từ đã khóa sổ (đến {locked:dd/MM/yyyy})."));
}
