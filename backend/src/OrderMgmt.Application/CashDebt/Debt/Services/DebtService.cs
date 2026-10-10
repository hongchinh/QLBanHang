using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.CashDebt.Debt.Interfaces;
using OrderMgmt.Application.CashDebt.Debt.Models;
using OrderMgmt.Application.CashDebt.Ledger;
using OrderMgmt.Application.Catalog.Customers.Models;
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

    public async Task<IReadOnlyList<OpenItemDto>> GetOpenItemsAsync(OpenItemsRequest request,
        CancellationToken ct = default)
    {
        EnsureAnyPermission(Permissions.Receipts.Create, Permissions.Receipts.Edit, Permissions.Payments.Create,
            Permissions.Payments.Edit, Permissions.Debt.Matching, Permissions.DebtOffsets.Create);

        var branchId = await _currentBranch.GetIdAsync(ct);
        return await _debtQuery.OpenItemsAsync(new OpenItemsQuery
        {
            BranchId = branchId,
            PartnerId = request.PartnerId,
            Side = request.Side,
            Direction = request.Direction,
            ForSourceType = request.ForSourceType,
            ForSourceId = request.ForSourceId,
        }, ct);
    }

    public async Task<List<CustomerSearchItemDto>> SearchPartnersAsync(DebtPartnerSearchRequest request,
        CancellationToken ct = default)
    {
        EnsureAnyPermission(Permissions.Debt.Matching, Permissions.Debt.OpeningBalance, Permissions.DebtOffsets.View,
            Permissions.Reports.Debt, Permissions.CreditLimits.Manage);

        var query = _db.Customers.AsNoTracking().Where(c => !c.IsDeleted);
        if (request.BothRoles)
            query = query.Where(c => c.IsCustomer && c.IsSupplier);
        else if (request.Side == DebtSide.Receivable)
            query = query.Where(c => c.IsCustomer);
        else if (request.Side == DebtSide.Payable)
            query = query.Where(c => c.IsSupplier);

        if (request.ActiveOnly)
            query = query.Where(c => c.Status == CustomerStatus.Active);

        var keyword = request.Keyword?.Trim() ?? string.Empty;
        if (keyword.Length > 0)
        {
            var pattern = $"%{EscapeLike(keyword)}%";
            query = query.Where(c =>
                EF.Functions.ILike(EF.Functions.Unaccent(c.Code), EF.Functions.Unaccent(pattern), LikeEscape)
                || EF.Functions.ILike(EF.Functions.Unaccent(c.Name), EF.Functions.Unaccent(pattern), LikeEscape)
                || (c.TaxCode != null
                    && EF.Functions.ILike(EF.Functions.Unaccent(c.TaxCode), EF.Functions.Unaccent(pattern), LikeEscape)));
        }

        return await query
            .OrderBy(c => c.Code)
            .Take(Math.Clamp(request.Limit, 1, 50))
            .Select(c => new CustomerSearchItemDto
            {
                Id = c.Id,
                Code = c.Code,
                Name = c.Name,
                TaxCode = c.TaxCode,
                CompanyAddress = c.CompanyAddress,
                DefaultShippingAddress = c.DefaultShippingAddress,
                ContactPerson = c.ContactPerson,
                PhoneNumber = c.PhoneNumber,
                Status = c.Status,
                IsCustomer = c.IsCustomer,
                IsSupplier = c.IsSupplier,
                CreditDays = c.CreditDays,
            })
            .ToListAsync(ct);
    }

    private void EnsureAnyPermission(params string[] codes)
    {
        if (!codes.Any(_currentUser.HasPermission))
            throw new ForbiddenException();
    }

    // The escape character EscapeLike uses. Every ILike must pass it: without an escape argument Npgsql sends
    // ESCAPE '' (no escape character), so the added backslashes would stay literal.
    private const string LikeEscape = "\\";

    private static string EscapeLike(string input) =>
        input.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
