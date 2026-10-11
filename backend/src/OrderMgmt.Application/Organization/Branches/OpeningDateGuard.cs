using System.Globalization;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Inventory.Common;
using OrderMgmt.Domain.Common;

namespace OrderMgmt.Application.Organization.Branches;

/// Shared opening-date check: once a branch has an opening date, a document dated before it (VN date) is rejected, so
/// the opening balances always come first in the ledgers (P8, P29). A branch without an opening date accepts any date;
/// its first opening date must then be on or before its earliest document (BranchService.SetOpeningDateAsync).
public static class OpeningDateGuard
{
    /// Reads the opening date: call it after the shared branch gate, like the period lock check (P14).
    public static async Task EnsureOnOrAfterAsync(IAppDbContext db, Guid branchId, DateTimeOffset voucherAt,
        CancellationToken ct)
    {
        var openingDate = await db.Branches.Where(b => b.Id == branchId).Select(b => b.OpeningDate).SingleAsync(ct);
        if (openingDate is { } opening && VnTime.ToVnDate(voucherAt) < opening)
            throw new ValidationDomainException(new Dictionary<string, string[]>
            {
                ["voucherAt"] = new[]
                {
                    string.Create(CultureInfo.InvariantCulture,
                        $"Ngày chứng từ trước ngày đầu kỳ của chi nhánh ({opening:dd/MM/yyyy})."),
                },
            }, null);
    }
}
