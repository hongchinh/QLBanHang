using System.Globalization;
using OrderMgmt.Application.Inventory.Common;
using OrderMgmt.Domain.Common;

namespace OrderMgmt.Application.CashDebt.Common;

/// Helper to check period locks and throw a standardized DomainException.
/// Mirrors the period-lock behavior from Round 1 (Inventory): checks if a date is on or before the branch's LockedUntil.
public static class PeriodLockGuard
{
    /// Ensures that the given instant is not locked. Throws if the VN date is on or before the branch's LockedUntil.
    public static void EnsureNotLocked(DateTimeOffset instant, DateOnly? lockedUntil)
    {
        if (lockedUntil is { } locked && IsLocked(locked, instant))
            throw new DomainException("PERIOD_LOCKED",
                string.Create(CultureInfo.InvariantCulture, $"Ngày chứng từ đã khóa sổ (đến {locked:dd/MM/yyyy})."));
    }

    /// Ensures that all given instants are not locked. Throws if any VN date is on or before the branch's LockedUntil.
    public static void EnsureNotLockedAny(IEnumerable<DateTimeOffset> instants, DateOnly? lockedUntil)
    {
        if (lockedUntil is { } locked && instants.Any(at => IsLocked(locked, at)))
            throw new DomainException("PERIOD_LOCKED",
                string.Create(CultureInfo.InvariantCulture, $"Ngày chứng từ đã khóa sổ (đến {locked:dd/MM/yyyy})."));
    }

    /// Returns true if the VN date of `at` is on or before the branch's LockedUntil (D14, D18).
    private static bool IsLocked(DateOnly lockedUntil, DateTimeOffset at) =>
        VnTime.ToVnDate(at) <= lockedUntil;
}
