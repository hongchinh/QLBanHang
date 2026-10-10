using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.CashDebt.Common;
using OrderMgmt.Application.CashDebt.Reports.Interfaces;
using OrderMgmt.Application.CashDebt.Reports.Models;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Inventory.Common;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.CashDebt.Reports.Services;

/// Cash and debt reports of the working branch, read from the debt ledger and the voucher tables.
public class CashDebtReportService : ICashDebtReportService
{
    // The escape character EscapeLike uses; every ILike must pass it (see CashVoucherService).
    private const string LikeEscape = "\\";

    private readonly IAppDbContext _db;
    private readonly ICurrentBranch _currentBranch;
    private readonly ITransactionRunner _transaction;

    public CashDebtReportService(IAppDbContext db, ICurrentBranch currentBranch, ITransactionRunner transaction)
    {
        _db = db;
        _currentBranch = currentBranch;
        _transaction = transaction;
    }

    /// One REPEATABLE READ snapshot, so the opening and period queries see the same ledger.
    public Task<DebtBalanceReportDto> GetDebtBalanceAsync(DebtBalanceReportRequest request, CancellationToken ct = default) =>
        _transaction.RunSnapshotAsync(c => BuildDebtBalanceAsync(request, c), ct);

    private async Task<DebtBalanceReportDto> BuildDebtBalanceAsync(DebtBalanceReportRequest request, CancellationToken ct)
    {
        var branchId = await _currentBranch.GetIdAsync(ct);
        var start = VnTime.ToUtc(VnTime.StartOfDay(request.From));
        var end = VnTime.ToUtc(VnTime.StartOfNextDay(request.To));

        // Soft-deleted partners keep their ledger rows, so they stay in the report.
        var partners = _db.Customers.IgnoreQueryFilters().AsNoTracking();
        var entries = _db.DebtEntries.AsNoTracking()
            .Where(e => e.BranchId == branchId && e.Side == request.Side && e.PostedAt < end);
        if (request.PartnerId is { } partnerId)
            entries = entries.Where(e => e.PartnerId == partnerId);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var pattern = $"%{EscapeLike(request.Search.Trim())}%";
            entries = entries.Where(e => partners.Any(c => c.Id == e.PartnerId
                && (EF.Functions.ILike(EF.Functions.Unaccent(c.Code), EF.Functions.Unaccent(pattern), LikeEscape)
                    || EF.Functions.ILike(EF.Functions.Unaccent(c.Name), EF.Functions.Unaccent(pattern), LikeEscape))));
        }

        // P29: Opening entries are opening balance even when they sit inside the window.
        var opening = await entries
            .Where(e => e.PostedAt < start || e.SourceType == DebtSourceType.Opening)
            .GroupBy(e => e.PartnerId)
            .Select(g => new
            {
                PartnerId = g.Key,
                Balance = g.Sum(e => e.Direction == DebtDirection.Increase ? e.Amount : -e.Amount),
            })
            .ToDictionaryAsync(x => x.PartnerId, x => x.Balance, ct);
        var period = await entries
            .Where(e => e.PostedAt >= start && e.SourceType != DebtSourceType.Opening)
            .GroupBy(e => e.PartnerId)
            .Select(g => new
            {
                PartnerId = g.Key,
                Increase = g.Sum(e => e.Direction == DebtDirection.Increase ? e.Amount : 0m),
                Decrease = g.Sum(e => e.Direction == DebtDirection.Decrease ? e.Amount : 0m),
            })
            .ToDictionaryAsync(x => x.PartnerId, x => (x.Increase, x.Decrease), ct);

        var partnerIds = opening.Keys.Union(period.Keys).ToList();
        var info = await partners
            .Where(c => partnerIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Code, c.Name })
            .ToDictionaryAsync(c => c.Id, ct);

        var rows = new List<DebtBalanceRowDto>();
        foreach (var id in partnerIds)
        {
            var openingBalance = opening.GetValueOrDefault(id);
            var (increase, decrease) = period.GetValueOrDefault(id);
            if (openingBalance == 0m && increase == 0m && decrease == 0m)
                continue;

            var (openingDebit, openingCredit) = DebtBalanceSign.Split(request.Side, openingBalance);
            var (closingDebit, closingCredit) = DebtBalanceSign.Split(request.Side, openingBalance + increase - decrease);
            var partner = info.GetValueOrDefault(id);
            rows.Add(new DebtBalanceRowDto
            {
                PartnerId = id,
                PartnerCode = partner?.Code,
                PartnerName = partner?.Name,
                OpeningDebit = openingDebit,
                OpeningCredit = openingCredit,
                Increase = increase,
                Decrease = decrease,
                ClosingDebit = closingDebit,
                ClosingCredit = closingCredit,
            });
        }
        rows = rows.OrderBy(r => r.PartnerCode, StringComparer.Ordinal).ToList();

        return new DebtBalanceReportDto
        {
            Side = request.Side,
            From = request.From,
            To = request.To,
            Rows = rows,
            Totals = new DebtBalanceRowDto
            {
                OpeningDebit = rows.Sum(r => r.OpeningDebit),
                OpeningCredit = rows.Sum(r => r.OpeningCredit),
                Increase = rows.Sum(r => r.Increase),
                Decrease = rows.Sum(r => r.Decrease),
                ClosingDebit = rows.Sum(r => r.ClosingDebit),
                ClosingCredit = rows.Sum(r => r.ClosingCredit),
            },
        };
    }

    private static string EscapeLike(string input) =>
        input.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
