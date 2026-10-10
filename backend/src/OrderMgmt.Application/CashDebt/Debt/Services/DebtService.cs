using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.CashDebt.Debt.Interfaces;
using OrderMgmt.Application.CashDebt.Debt.Models;
using OrderMgmt.Application.CashDebt.Ledger;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Inventory.Common;
using OrderMgmt.Domain.Common;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Entities.Catalog;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.CashDebt.Debt.Services;

public class DebtService : IDebtService
{
    private readonly IAppDbContext _db;
    private readonly IDebtQueryService _debtQuery;
    private readonly IDateTime _clock;
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentBranch _currentBranch;

    public DebtService(IAppDbContext db, IDebtQueryService debtQuery, IDateTime clock, ICurrentUser currentUser,
        ICurrentBranch currentBranch)
    {
        _db = db;
        _debtQuery = debtQuery;
        _clock = clock;
        _currentUser = currentUser;
        _currentBranch = currentBranch;
    }

    public async Task<PartnerDebtSummaryDto> GetPartnerSummaryAsync(Guid partnerId, CancellationToken ct = default)
    {
        if (!_currentUser.HasPermission(Permissions.Debt.PartnerSummary)
            && !_currentUser.HasPermission(Permissions.Reports.Debt))
            throw new ForbiddenException();

        var branchId = await _currentBranch.GetIdAsync(ct);
        var partner = await _db.Customers.AsNoTracking()
            .Where(c => c.Id == partnerId)
            .Select(c => new { c.CreditDays })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(nameof(Customer), partnerId);

        var balance = await _debtQuery.BalanceAsync(branchId, partnerId, DebtSide.Receivable, null, ct);
        var limit = await _db.CreditLimits.AsNoTracking()
            .Where(l => l.BranchId == branchId && l.CustomerId == partnerId)
            .Select(l => (decimal?)l.Amount)
            .SingleOrDefaultAsync(ct);

        var today = VnTime.ToVnDate(_clock.UtcNow);
        var openIncreases = await _debtQuery.OpenItemsAsync(new OpenItemsQuery
        {
            BranchId = branchId,
            PartnerId = partnerId,
            Side = DebtSide.Receivable,
            Direction = DebtDirection.Increase,
        }, ct);
        var overdue = openIncreases.Where(i => i.DueDate < today).Sum(i => i.Open);

        return new PartnerDebtSummaryDto
        {
            PartnerId = partnerId,
            ReceivableBalance = balance,
            CreditLimit = limit,
            Available = limit - balance,
            OverdueAmount = overdue,
            CreditDays = partner.CreditDays,
        };
    }
}
