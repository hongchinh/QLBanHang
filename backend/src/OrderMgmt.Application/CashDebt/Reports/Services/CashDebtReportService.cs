using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.CashDebt.Common;
using OrderMgmt.Application.CashDebt.Guards;
using OrderMgmt.Application.CashDebt.Reports.Interfaces;
using OrderMgmt.Application.CashDebt.Reports.Models;
using OrderMgmt.Application.Common;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Inventory.Common;
using OrderMgmt.Domain.Common;
using OrderMgmt.Domain.Entities.CashDebt;
using OrderMgmt.Domain.Entities.Catalog;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.CashDebt.Reports.Services;

/// Cash and debt reports of the working branch, read from the debt ledger and the voucher tables.
public class CashDebtReportService : ICashDebtReportService
{
    // The escape character EscapeLike uses; every ILike must pass it (see CashVoucherService).
    private const string LikeEscape = "\\";

    private const string OffsetReasonName = "Bù trừ công nợ";

    private readonly IAppDbContext _db;
    private readonly ICurrentBranch _currentBranch;
    private readonly ITransactionRunner _transaction;
    private readonly ICurrentUser _currentUser;

    public CashDebtReportService(
        IAppDbContext db, ICurrentBranch currentBranch, ITransactionRunner transaction, ICurrentUser currentUser)
    {
        _db = db;
        _currentBranch = currentBranch;
        _transaction = transaction;
        _currentUser = currentUser;
    }

    /// One REPEATABLE READ snapshot, so the opening and period queries see the same ledger.
    public Task<DebtBalanceReportDto> GetDebtBalanceAsync(DebtBalanceReportRequest request, CancellationToken ct = default) =>
        _transaction.RunSnapshotAsync(c => BuildDebtBalanceAsync(request, c), ct);

    private async Task<DebtBalanceReportDto> BuildDebtBalanceAsync(DebtBalanceReportRequest request, CancellationToken ct)
    {
        ReportScopeGuard.EnsureAllowed(_currentUser, request.Scope);
        var branchId = await _currentBranch.GetIdAsync(ct);
        var start = VnTime.ToUtc(VnTime.StartOfDay(request.From));
        var end = VnTime.ToUtc(VnTime.StartOfNextDay(request.To));

        // Soft-deleted partners keep their ledger rows, so they stay in the report.
        var partners = _db.Customers.IgnoreQueryFilters([QueryFilters.SoftDelete]).AsNoTracking();
        var entries = _db.DebtEntries.AsNoTracking().Where(e => e.Side == request.Side);
        if (request.Scope == ReportScope.Branch)
        {
            entries = entries.Where(e => e.BranchId == branchId);
        }
        else
        {
            // S15: company scope skips demo branches, soft-deleted ones included.
            entries = entries.Where(e => !_db.Branches.IgnoreQueryFilters(new[] { QueryFilters.SoftDelete }).Any(b => b.Id == e.BranchId && b.IsDemo));
        }
        if (request.PartnerId is { } partnerId)
            entries = entries.Where(e => e.PartnerId == partnerId);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var pattern = $"%{EscapeLike(request.Search.Trim())}%";
            entries = entries.Where(e => partners.Any(c => c.Id == e.PartnerId
                && (EF.Functions.ILike(EF.Functions.Unaccent(c.Code), EF.Functions.Unaccent(pattern), LikeEscape)
                    || EF.Functions.ILike(EF.Functions.Unaccent(c.Name), EF.Functions.Unaccent(pattern), LikeEscape))));
        }

        // P29: Opening entries are opening balance in every window, whatever their PostedAt (no upper bound),
        // so a window ending before the opening date still carries them and its closing is the next opening.
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
            .Where(e => e.SourceType != DebtSourceType.Opening && e.PostedAt >= start && e.PostedAt < end)
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
            Scope = request.Scope,
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

    /// One REPEATABLE READ snapshot, so the opening balance and the rows see the same ledger.
    public Task<DebtLedgerReportDto> GetDebtLedgerAsync(DebtLedgerReportRequest request, CancellationToken ct = default) =>
        _transaction.RunSnapshotAsync(c => BuildDebtLedgerAsync(request, c), ct);

    private async Task<DebtLedgerReportDto> BuildDebtLedgerAsync(DebtLedgerReportRequest request, CancellationToken ct)
    {
        ReportScopeGuard.EnsureAllowed(_currentUser, request.Scope);
        var branchId = await _currentBranch.GetIdAsync(ct);
        var start = VnTime.ToUtc(VnTime.StartOfDay(request.From));
        var end = VnTime.ToUtc(VnTime.StartOfNextDay(request.To));

        // Soft-deleted partners keep their ledger rows, so their ledger can still be read.
        var partner = await _db.Customers.IgnoreQueryFilters([QueryFilters.SoftDelete]).AsNoTracking()
            .Where(c => c.Id == request.PartnerId)
            .Select(c => new { c.Code, c.Name })
            .SingleOrDefaultAsync(ct)
            ?? throw new NotFoundException(nameof(Customer), request.PartnerId);

        var entries = _db.DebtEntries.AsNoTracking()
            .Where(e => e.PartnerId == request.PartnerId && e.Side == request.Side);
        if (request.Scope == ReportScope.Branch)
        {
            entries = entries.Where(e => e.BranchId == branchId);
        }
        else
        {
            // S15: company scope skips demo branches, soft-deleted ones included.
            entries = entries.Where(e => !_db.Branches.IgnoreQueryFilters(new[] { QueryFilters.SoftDelete }).Any(b => b.Id == e.BranchId && b.IsDemo));
        }

        // Same window rule as the balance report (P29): Opening entries are opening balance in every window.
        var openingBalance = await entries
            .Where(e => e.PostedAt < start || e.SourceType == DebtSourceType.Opening)
            .SumAsync(e => e.Direction == DebtDirection.Increase ? e.Amount : -e.Amount, ct);
        var periodEntries = await entries
            .Where(e => e.SourceType != DebtSourceType.Opening && e.PostedAt >= start && e.PostedAt < end)
            .ToListAsync(ct);

        var reasonNames = await LoadReasonNamesAsync(periodEntries, ct);
        var branchCodes = request.Scope == ReportScope.Company
            ? await LoadBranchCodesAsync(periodEntries, ct)
            : new Dictionary<Guid, string>();

        var running = openingBalance;
        var rows = new List<DebtLedgerRowDto>();
        foreach (var e in periodEntries
                     .OrderBy(e => e.PostedAt)
                     .ThenBy(e => e.SourceType)
                     .ThenBy(e => e.DocCode, StringComparer.Ordinal)
                     .ThenBy(e => e.Id))
        {
            var increase = e.Direction == DebtDirection.Increase ? e.Amount : 0m;
            var decrease = e.Direction == DebtDirection.Decrease ? e.Amount : 0m;
            running += increase - decrease;
            rows.Add(new DebtLedgerRowDto
            {
                EntryId = e.Id,
                PostedAt = e.PostedAt,
                DocDate = e.DocDate,
                DocCode = e.DocCode,
                SourceType = e.SourceType,
                SourceId = e.SourceId,
                Description = e.Description,
                ReasonName = reasonNames.GetValueOrDefault((e.SourceType, e.SourceId)),
                DueDate = e.DueDate,
                BranchCode = request.Scope == ReportScope.Company ? branchCodes.GetValueOrDefault(e.BranchId) : null,
                Increase = increase,
                Decrease = decrease,
                RunningBalance = running,
            });
        }

        var (openingDebit, openingCredit) = DebtBalanceSign.Split(request.Side, openingBalance);
        var (closingDebit, closingCredit) = DebtBalanceSign.Split(request.Side, running);
        return new DebtLedgerReportDto
        {
            Side = request.Side,
            Scope = request.Scope,
            PartnerId = request.PartnerId,
            PartnerCode = partner.Code,
            PartnerName = partner.Name,
            From = request.From,
            To = request.To,
            OpeningBalance = openingBalance,
            OpeningDebit = openingDebit,
            OpeningCredit = openingCredit,
            Rows = rows,
            TotalIncrease = rows.Sum(r => r.Increase),
            TotalDecrease = rows.Sum(r => r.Decrease),
            ClosingBalance = running,
            ClosingDebit = closingDebit,
            ClosingCredit = closingCredit,
        };
    }

    // Branch code of each period row for the Company scope (T21), one query; a deleted branch keeps its history.
    private async Task<Dictionary<Guid, string>> LoadBranchCodesAsync(
        IReadOnlyCollection<DebtEntry> entries, CancellationToken ct)
    {
        var branchIds = entries.Select(e => e.BranchId).Distinct().ToList();
        if (branchIds.Count == 0)
            return new Dictionary<Guid, string>();
        return await _db.Branches.IgnoreQueryFilters([QueryFilters.SoftDelete]).AsNoTracking()
            .Where(b => branchIds.Contains(b.Id))
            .ToDictionaryAsync(b => b.Id, b => b.Code, ct);
    }

    // Reason of each period row (T8), read in two queries. Soft-deleted vouchers and reasons keep their history.
    private async Task<Dictionary<(DebtSourceType, Guid), string>> LoadReasonNamesAsync(
        IReadOnlyCollection<DebtEntry> entries, CancellationToken ct)
    {
        var names = new Dictionary<(DebtSourceType, Guid), string>();
        foreach (var e in entries.Where(e => e.SourceType == DebtSourceType.Offset))
            names[(e.SourceType, e.SourceId)] = OffsetReasonName;

        var stockIds = entries
            .Where(e => e.SourceType is DebtSourceType.StockIn or DebtSourceType.StockOut)
            .Select(e => e.SourceId).Distinct().ToList();
        if (stockIds.Count > 0)
        {
            var stock = await _db.StockVouchers.IgnoreQueryFilters([QueryFilters.SoftDelete]).AsNoTracking()
                .Where(v => stockIds.Contains(v.Id))
                .Select(v => new
                {
                    v.Id,
                    Name = _db.StockReasons.IgnoreQueryFilters(new[] { QueryFilters.SoftDelete })
                        .Where(r => r.Id == v.ReasonId).Select(r => r.Name).FirstOrDefault(),
                })
                .ToListAsync(ct);
            foreach (var v in stock.Where(v => v.Name is not null))
            {
                names[(DebtSourceType.StockIn, v.Id)] = v.Name!;
                names[(DebtSourceType.StockOut, v.Id)] = v.Name!;
            }
        }

        var cashIds = entries
            .Where(e => e.SourceType is DebtSourceType.Receipt or DebtSourceType.Payment)
            .Select(e => e.SourceId).Distinct().ToList();
        if (cashIds.Count > 0)
        {
            var cash = await _db.CashVouchers.IgnoreQueryFilters([QueryFilters.SoftDelete]).AsNoTracking()
                .Where(v => cashIds.Contains(v.Id))
                .Select(v => new
                {
                    v.Id,
                    Name = _db.CashReasons.IgnoreQueryFilters(new[] { QueryFilters.SoftDelete })
                        .Where(r => r.Id == v.ReasonId).Select(r => r.Name).FirstOrDefault(),
                })
                .ToListAsync(ct);
            foreach (var v in cash.Where(v => v.Name is not null))
            {
                names[(DebtSourceType.Receipt, v.Id)] = v.Name!;
                names[(DebtSourceType.Payment, v.Id)] = v.Name!;
            }
        }

        return names;
    }

    /// One REPEATABLE READ snapshot, so both ledgers see the same state.
    public Task<DebtReconciliationReportDto> GetDebtReconciliationAsync(DebtReconciliationReportRequest request, CancellationToken ct = default) =>
        _transaction.RunSnapshotAsync(c => BuildDebtReconciliationAsync(request, c), ct);

    private async Task<DebtReconciliationReportDto> BuildDebtReconciliationAsync(DebtReconciliationReportRequest request, CancellationToken ct)
    {
        ReportScopeGuard.EnsureAllowed(_currentUser, request.Scope);
        var branchId = await _currentBranch.GetIdAsync(ct);
        var partner = await _db.Customers.IgnoreQueryFilters([QueryFilters.SoftDelete]).AsNoTracking()
            .Where(c => c.Id == request.PartnerId)
            .Select(c => new { c.Code, c.Name, c.TaxCode, c.CompanyAddress, c.IsCustomer, c.IsSupplier })
            .SingleOrDefaultAsync(ct)
            ?? throw new NotFoundException(nameof(Customer), request.PartnerId);
        var branchName = request.Scope == ReportScope.Branch
            ? await _db.Branches.AsNoTracking()
                .Where(b => b.Id == branchId)
                .Select(b => b.Name)
                .SingleAsync(ct)
            : null;
        var partnerEntries = _db.DebtEntries.AsNoTracking().Where(e => e.PartnerId == request.PartnerId);
        if (request.Scope == ReportScope.Branch)
        {
            partnerEntries = partnerEntries.Where(e => e.BranchId == branchId);
        }
        else
        {
            // S15: company scope skips demo branches, soft-deleted ones included.
            partnerEntries = partnerEntries.Where(
                e => !_db.Branches.IgnoreQueryFilters(new[] { QueryFilters.SoftDelete }).Any(b => b.Id == e.BranchId && b.IsDemo));
        }
        var sidesWithEntries = await partnerEntries
            .Select(e => e.Side)
            .Distinct()
            .ToListAsync(ct);

        // B8: a side is listed for the partner's role or when it has any entry there (a former customer keeps its history).
        var sides = new List<DebtLedgerReportDto>();
        foreach (var side in new[] { DebtSide.Receivable, DebtSide.Payable })
        {
            var hasRole = side == DebtSide.Receivable ? partner.IsCustomer : partner.IsSupplier;
            if (!hasRole && !sidesWithEntries.Contains(side))
                continue;
            sides.Add(await BuildDebtLedgerAsync(new DebtLedgerReportRequest
            {
                Side = side, PartnerId = request.PartnerId, From = request.From, To = request.To, Scope = request.Scope,
            }, ct));
        }

        var receivable = sides.SingleOrDefault(s => s.Side == DebtSide.Receivable);
        var payable = sides.SingleOrDefault(s => s.Side == DebtSide.Payable);
        return new DebtReconciliationReportDto
        {
            Scope = request.Scope,
            PartnerId = request.PartnerId,
            PartnerCode = partner.Code,
            PartnerName = partner.Name,
            TaxCode = partner.TaxCode,
            Address = partner.CompanyAddress,
            BranchName = branchName,
            From = request.From,
            To = request.To,
            Sides = sides,
            NetBalance = receivable is not null && payable is not null ? receivable.ClosingBalance - payable.ClosingBalance : null,
        };
    }

    /// One REPEATABLE READ snapshot, so the funds and their movements are read from the same state.
    public Task<CashBookReportDto> GetCashBookAsync(CashBookReportRequest request, CancellationToken ct = default) =>
        _transaction.RunSnapshotAsync(c => BuildCashBookAsync(request, c), ct);

    private async Task<CashBookReportDto> BuildCashBookAsync(CashBookReportRequest request, CancellationToken ct)
    {
        var branchId = await _currentBranch.GetIdAsync(ct);
        var start = VnTime.ToUtc(VnTime.StartOfDay(request.From));
        var end = VnTime.ToUtc(VnTime.StartOfNextDay(request.To));
        FundKind? kind = request.Kind switch
        {
            FundKindFilter.Cash => FundKind.Cash,
            FundKindFilter.Bank => FundKind.Bank,
            _ => null,
        };

        var funds = _db.Funds.AsNoTracking().Where(f => f.BranchId == branchId);
        if (request.FundId is { } fundId)
        {
            var fundKind = await funds.Where(f => f.Id == fundId).Select(f => (FundKind?)f.Kind).SingleOrDefaultAsync(ct)
                ?? throw new NotFoundException(nameof(Fund), fundId);
            if (kind is not null && fundKind != kind)
                throw new ValidationDomainException(
                    new Dictionary<string, string[]> { ["fundId"] = new[] { "Quỹ không đúng loại quỹ đã chọn." } }, null);
            funds = funds.Where(f => f.Id == fundId);
        }
        else if (kind is not null)
        {
            funds = funds.Where(f => f.Kind == kind);
        }

        // Active or not: an inactive fund keeps its history.
        var selected = (await funds
                .Select(f => new { f.Id, f.Code, f.Name, f.Kind, f.BankName, f.AccountNumber })
                .ToListAsync(ct))
            .OrderBy(f => f.Kind)
            .ThenBy(f => f.Code, StringComparer.Ordinal)
            .ToList();
        var selectedIds = selected.Select(f => f.Id).ToHashSet();
        var movements = await FundMovementQuery.LoadAsync(_db, selectedIds, ct);

        var groups = new List<CashBookGroupDto>();
        decimal grandIn = 0m, grandOut = 0m;
        foreach (var fund in selected)
        {
            var fundMovements = movements[fund.Id];
            // P29: the opening amount is opening balance in every window, whatever the report's dates.
            var opening = fundMovements
                .Where(m => m.Kind == FundMovementKind.Opening || m.At < start)
                .Sum(m => m.Delta);

            var running = opening;
            var rows = new List<CashBookRowDto>();
            foreach (var m in fundMovements.Where(m => m.Kind != FundMovementKind.Opening && m.At >= start && m.At < end))
            {
                var @in = m.Delta > 0 ? m.Delta : 0m;
                var @out = m.Delta < 0 ? -m.Delta : 0m;
                running += m.Delta;
                rows.Add(new CashBookRowDto
                {
                    At = m.At,
                    SourceType = SourceTypeOf(m.Kind),
                    SourceId = m.SourceId,
                    ReceiptCode = m.Kind == FundMovementKind.Receipt ? m.Code : null,
                    PaymentCode = m.Kind == FundMovementKind.Payment ? m.Code : null,
                    TransferCode = m.Kind is FundMovementKind.TransferIn or FundMovementKind.TransferOut ? m.Code : null,
                    PartnerOrPerson = m.PartnerOrPerson,
                    Description = m.Description,
                    ReasonName = m.ReasonName,
                    In = @in,
                    Out = @out,
                    Running = running,
                });

                // P25: a transfer between two selected funds cancels out in the grand totals.
                if (m.OtherFundId is { } other && selectedIds.Contains(other))
                    continue;
                grandIn += @in;
                grandOut += @out;
            }

            groups.Add(new CashBookGroupDto
            {
                FundId = fund.Id,
                FundCode = fund.Code,
                FundName = fund.Name,
                FundKind = fund.Kind,
                BankName = fund.BankName,
                AccountNumber = fund.AccountNumber,
                Opening = opening,
                Rows = rows,
                TotalIn = rows.Sum(r => r.In),
                TotalOut = rows.Sum(r => r.Out),
                Closing = running,
            });
        }

        return new CashBookReportDto
        {
            Kind = request.Kind,
            From = request.From,
            To = request.To,
            Groups = groups,
            GrandOpening = groups.Sum(g => g.Opening),
            GrandIn = grandIn,
            GrandOut = grandOut,
            GrandClosing = groups.Sum(g => g.Closing),
        };
    }

    private static CashBookSourceType SourceTypeOf(FundMovementKind kind) => kind switch
    {
        FundMovementKind.Receipt => CashBookSourceType.Receipt,
        FundMovementKind.Payment => CashBookSourceType.Payment,
        FundMovementKind.TransferIn => CashBookSourceType.TransferIn,
        FundMovementKind.TransferOut => CashBookSourceType.TransferOut,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "The opening amount is not a cash book row."),
    };

    private static string EscapeLike(string input) =>
        input.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
