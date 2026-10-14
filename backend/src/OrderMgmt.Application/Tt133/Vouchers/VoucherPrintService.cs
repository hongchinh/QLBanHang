using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.CashDebt.Reports.Models;
using OrderMgmt.Application.Common;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Inventory.Common;
using OrderMgmt.Application.Tt133.Models;
using OrderMgmt.Application.Tt133.Services;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Tt133.Vouchers;

public class VoucherPrintService : IVoucherPrintService
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentBranch _currentBranch;
    private readonly IDateTime _clock;
    private readonly ITransactionRunner _transaction;
    private readonly Tt133ExportSupport _support;
    private readonly IVoucherFormRenderer _renderer;

    public VoucherPrintService(
        IAppDbContext db,
        ICurrentUser currentUser,
        ICurrentBranch currentBranch,
        IDateTime clock,
        ITransactionRunner transaction,
        Tt133ExportSupport support,
        IVoucherFormRenderer renderer)
    {
        _db = db;
        _currentUser = currentUser;
        _currentBranch = currentBranch;
        _clock = clock;
        _transaction = transaction;
        _support = support;
        _renderer = renderer;
    }

    public async Task<ExportFile> PrintCashVouchersAsync(IReadOnlyList<Guid> ids, ExportFormat format, CancellationToken ct = default)
    {
        var distinct = ids.Distinct().ToList();

        var (context, models) = await _transaction.RunSnapshotAsync(async token =>
        {
            var formContext = await _support.BuildContextAsync(ReportScope.Branch, token);

            // Soft-deleted reasons, partners and owners keep their names; the voucher and its lines are filtered by hand.
            var rows = await _db.CashVouchers.IgnoreQueryFilters().AsNoTracking()
                .Where(v => distinct.Contains(v.Id) && !v.IsDeleted)
                .Select(v => new
                {
                    v.Id,
                    v.Type,
                    v.Code,
                    v.VoucherAt,
                    v.BranchId,
                    v.Status,
                    v.PersonName,
                    v.PartnerName,
                    v.PartnerAddress,
                    v.Description,
                    v.AttachedDocs,
                    v.Total,
                    ReasonName = v.Reason!.Name,
                    OwnerName = v.Owner != null ? v.Owner.FullName : null,
                    LineDescriptions = v.Lines.Where(l => !l.IsDeleted).OrderBy(l => l.SortOrder)
                        .Select(l => l.Description).ToList(),
                })
                .ToListAsync(token);

            var list = rows
                .OrderBy(r => r.VoucherAt)
                .ThenBy(r => r.Code, StringComparer.Ordinal)
                .Select(r => new CashVoucherPrintModel
                {
                    Type = r.Type,
                    Code = r.Code,
                    Date = VnTime.ToVnDate(r.VoucherAt),
                    IsCancelled = r.Status == DocumentStatus.Cancelled,
                    PersonName = VoucherPrintRules.FirstFilled(r.PersonName, r.PartnerName),
                    Address = VoucherPrintRules.FirstFilled(r.PartnerAddress),
                    Reason = VoucherPrintRules.CashReason(r.Description, r.LineDescriptions, r.ReasonName),
                    Amount = r.Total,
                    AmountInWords = VndAmountInWords.Read(r.Total),
                    AttachedDocsLine = VoucherPrintRules.AttachedDocsLine(r.AttachedDocs),
                    PreparerName = r.OwnerName,
                })
                .ToList();
            return (formContext, list);
        }, ct);

        var xlsx = _renderer.RenderCashVouchers(context, models);
        return await _support.ToFileAsync(xlsx, models[0].Code, format, ct);
    }
}
