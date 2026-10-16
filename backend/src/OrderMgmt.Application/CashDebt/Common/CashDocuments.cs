using OrderMgmt.Application.Common;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.CashDebt.CashVouchers.Models;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Domain.Common;
using OrderMgmt.Domain.Entities.CashDebt;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.CashDebt.Common;

/// Rules shared by the cash vouchers, fund transfers and debt offsets: the voucher date range, the activity log and
/// the working-branch guard.
public static class CashDocuments
{
    public static readonly DateTimeOffset MinVoucherAt = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// Same range as stock vouchers. An out-of-range date is reported by the rules (400 `voucherAt`), so the period
    /// lock check skips it: VnTime cannot convert dates near DateTimeOffset.MaxValue.
    public static bool IsValidVoucherAt(DateTimeOffset voucherAt, DateTimeOffset utcNow) =>
        voucherAt >= MinVoucherAt && voucherAt <= utcNow.AddYears(1);

    public static CashDocumentActivity NewActivity(
        DocumentType type, Guid documentId, CashDocumentActivityAction action, Guid? actorUserId,
        DateTimeOffset occurredAt, string description) =>
        new()
        {
            DocumentType = type,
            DocumentId = documentId,
            Action = action,
            ActorUserId = actorUserId,
            OccurredAt = occurredAt,
            Description = description,
        };

    /// Newest first.
    public static Task<List<CashDocumentActivityDto>> ListActivitiesAsync(
        IAppDbContext db, DocumentType type, Guid documentId, CancellationToken ct) =>
        db.CashDocumentActivities.AsNoTracking()
            .Where(a => a.DocumentType == type && a.DocumentId == documentId)
            .OrderByDescending(a => a.OccurredAt)
            .Select(a => new CashDocumentActivityDto
            {
                Id = a.Id,
                Action = a.Action,
                ActorUserId = a.ActorUserId,
                ActorName = a.ActorUserId == null
                    ? "Hệ thống"
                    : db.Users.IgnoreQueryFilters(new[] { QueryFilters.SoftDelete })
                        .Where(u => u.Id == a.ActorUserId)
                        .Select(u => u.FullName)
                        .FirstOrDefault() ?? "Người dùng không xác định",
                OccurredAt = a.OccurredAt,
                Description = a.Description,
            })
            .ToListAsync(ct);

    /// Throws ForbiddenException(message) when the document belongs to another branch than the working one.
    public static async Task EnsureWorkingBranchAsync(
        ICurrentBranch currentBranch, Guid branchId, string message, CancellationToken ct)
    {
        if (branchId != await currentBranch.GetIdAsync(ct))
            throw new ForbiddenException(message);
    }
}
