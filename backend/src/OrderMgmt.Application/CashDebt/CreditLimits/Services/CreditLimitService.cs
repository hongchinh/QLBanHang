using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.CashDebt.CreditLimits.Interfaces;
using OrderMgmt.Application.CashDebt.CreditLimits.Models;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Domain.Common;
using OrderMgmt.Domain.Entities.CashDebt;
using OrderMgmt.Domain.Entities.Catalog;

namespace OrderMgmt.Application.CashDebt.CreditLimits.Services;

public class CreditLimitService : ICreditLimitService
{
    private readonly IAppDbContext _db;
    private readonly IDateTime _clock;
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentBranch _currentBranch;

    public CreditLimitService(IAppDbContext db, IDateTime clock, ICurrentUser currentUser, ICurrentBranch currentBranch)
    {
        _db = db;
        _clock = clock;
        _currentUser = currentUser;
        _currentBranch = currentBranch;
    }

    public async Task<CreditLimitListResult> ListAsync(PageRequest request, CancellationToken ct = default)
    {
        var branchId = await _currentBranch.GetIdAsync(ct);
        var query = _db.CreditLimits.AsNoTracking()
            .Where(l => l.BranchId == branchId && !l.IsDeleted && !l.Customer!.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var pattern = $"%{EscapeLike(request.Search.Trim())}%";
            query = query.Where(l =>
                EF.Functions.ILike(l.Customer!.Code, pattern) || EF.Functions.ILike(l.Customer.Name, pattern));
        }

        var totalItems = await query.CountAsync(ct);
        var items = await query
            .OrderBy(l => l.Customer!.Code)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(l => new CreditLimitDto
            {
                CustomerId = l.CustomerId,
                CustomerCode = l.Customer!.Code,
                CustomerName = l.Customer.Name,
                CreditDays = l.Customer.CreditDays,
                Amount = l.Amount,
            })
            .ToListAsync(ct);

        return new CreditLimitListResult
        {
            Items = items,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalItems = totalItems,
        };
    }

    public async Task<CreditLimitDto> UpsertAsync(Guid customerId, UpsertCreditLimitRequest request, CancellationToken ct = default)
    {
        var branchId = await _currentBranch.GetIdAsync(ct);
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == customerId && !c.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Customer), customerId);
        if (!customer.IsCustomer)
            throw new ValidationDomainException(
                new Dictionary<string, string[]> { ["customerId"] = new[] { "Đối tượng phải là khách hàng." } }, null);

        var limit = await _db.CreditLimits.FirstOrDefaultAsync(
            l => l.CustomerId == customerId && l.BranchId == branchId && !l.IsDeleted, ct);
        if (limit is null)
        {
            limit = new CreditLimit { CustomerId = customerId, BranchId = branchId };
            _db.CreditLimits.Add(limit);
        }
        limit.Amount = request.Amount;

        await _db.SaveChangesAsync(ct);
        return new CreditLimitDto
        {
            CustomerId = customer.Id,
            CustomerCode = customer.Code,
            CustomerName = customer.Name,
            CreditDays = customer.CreditDays,
            Amount = limit.Amount,
        };
    }

    public async Task DeleteAsync(Guid customerId, CancellationToken ct = default)
    {
        var branchId = await _currentBranch.GetIdAsync(ct);
        var limit = await _db.CreditLimits.FirstOrDefaultAsync(
            l => l.CustomerId == customerId && l.BranchId == branchId && !l.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(CreditLimit), customerId);

        limit.IsDeleted = true;
        limit.DeletedAt = _clock.UtcNow;
        limit.DeletedBy = _currentUser.UserId;
        await _db.SaveChangesAsync(ct);
    }

    private static string EscapeLike(string input) =>
        input.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
