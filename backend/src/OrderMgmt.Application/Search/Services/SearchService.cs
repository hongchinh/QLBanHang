using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Catalog.Customers.Models;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Search.Interfaces;
using OrderMgmt.Application.Search.Models;
using OrderMgmt.Domain.Constants;

namespace OrderMgmt.Application.Search.Services;

public class SearchService : ISearchService
{
    private const int MinKeywordLength = 3;
    private const int MaxPerGroup = 5;

    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public SearchService(IAppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<GlobalSearchResultDto> GlobalAsync(string keyword, CancellationToken ct = default)
    {
        var trimmed = keyword?.Trim() ?? string.Empty;
        var result = new GlobalSearchResultDto();
        if (trimmed.Length < MinKeywordLength) return result;

        var pattern = $"%{EscapeLike(trimmed)}%";

        // Queries run sequentially on the scoped DbContext. Deviation from plan:
        // parallel execution would require IDbContextFactory which isn't wired in
        // this codebase. Dataset is small enough that sequential latency is fine.
        if (_currentUser.HasPermission(Permissions.Customers.View))
            result.Customers = await SearchPartnersAsync(pattern, suppliers: false, ct);

        if (_currentUser.HasPermission(Permissions.Suppliers.View))
            result.Suppliers = await SearchPartnersAsync(pattern, suppliers: true, ct);

        if (_currentUser.HasPermission(Permissions.Quotations.View))
        {
            var qQuery = _db.Quotations.AsNoTracking()
                .Where(q => !q.IsDeleted
                    && (EF.Functions.ILike(q.Code, pattern)
                        || EF.Functions.ILike(EF.Functions.Unaccent(q.CustomerName), EF.Functions.Unaccent(pattern))));

            if (!_currentUser.HasPermission(Permissions.Quotations.ViewAll))
            {
                var uid = _currentUser.UserId ?? Guid.Empty;
                qQuery = qQuery.Where(q => q.OwnerUserId == uid);
            }

            result.Quotations = await qQuery
                .OrderByDescending(q => q.CreatedAt)
                .Take(MaxPerGroup)
                .Select(q => new QuotationSearchItemDto(
                    q.Id,
                    q.Code,
                    q.CustomerName,
                    q.Total,
                    q.Status,
                    q.CreatedAt))
                .ToListAsync(ct);
        }

        return result;
    }

    private Task<List<CustomerSearchItemDto>> SearchPartnersAsync(string pattern, bool suppliers, CancellationToken ct) =>
        _db.Customers.AsNoTracking()
            .Where(c => !c.IsDeleted && (suppliers ? c.IsSupplier : c.IsCustomer)
                && (EF.Functions.ILike(EF.Functions.Unaccent(c.Name), EF.Functions.Unaccent(pattern))
                    || EF.Functions.ILike(c.Code, pattern)
                    || (c.PhoneNumber != null && EF.Functions.ILike(c.PhoneNumber, pattern))
                    || (c.TaxCode != null && EF.Functions.ILike(c.TaxCode, pattern))))
            .OrderBy(c => c.Name)
            .Take(MaxPerGroup)
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
            })
            .ToListAsync(ct);

    private static string EscapeLike(string input) =>
        input.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
