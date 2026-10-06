using Mapster;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Catalog.Customers.Interfaces;
using OrderMgmt.Application.Catalog.Customers.Models;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Domain.Common;
using OrderMgmt.Domain.Entities.Catalog;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Catalog.Customers.Services;

public class CustomerService : ICustomerService
{
    private readonly IAppDbContext _db;
    private readonly IDateTime _clock;
    private readonly ICurrentUser _currentUser;

    public CustomerService(IAppDbContext db, IDateTime clock, ICurrentUser currentUser)
    {
        _db = db;
        _clock = clock;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<CustomerListItemDto>> ListAsync(CustomerListRequest request, PartnerRole role, CancellationToken ct = default)
    {
        var query = WithRole(_db.Customers.AsNoTracking().Where(c => !c.IsDeleted), role);

        if (request.Group.HasValue)
            query = query.Where(c => c.Group == request.Group.Value);

        if (request.Status.HasValue)
            query = query.Where(c => c.Status == request.Status.Value);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var pattern = $"%{EscapeLike(request.Search.Trim())}%";
            query = query.Where(c =>
                EF.Functions.ILike(c.Name, pattern)
                || EF.Functions.ILike(c.Code, pattern)
                || (c.PhoneNumber != null && EF.Functions.ILike(c.PhoneNumber, pattern))
                || (c.TaxCode != null && EF.Functions.ILike(c.TaxCode, pattern)));
        }

        query = (request.SortBy?.ToLowerInvariant(), request.SortDirection?.ToLowerInvariant()) switch
        {
            ("name", "desc") => query.OrderByDescending(c => c.Name),
            ("name", _) => query.OrderBy(c => c.Name),
            ("code", "desc") => query.OrderByDescending(c => c.Code),
            ("code", _) => query.OrderBy(c => c.Code),
            _ => query.OrderByDescending(c => c.CreatedAt),
        };

        var totalItems = await query.CountAsync(ct);

        var items = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ProjectToType<CustomerListItemDto>()
            .ToListAsync(ct);

        return new PagedResult<CustomerListItemDto>
        {
            Items = items,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalItems = totalItems,
        };
    }

    public async Task<CustomerDto> GetAsync(Guid id, PartnerRole role, CancellationToken ct = default)
    {
        var customer = await WithRole(_db.Customers.AsNoTracking(), role)
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Customer), id);

        return customer.Adapt<CustomerDto>();
    }

    public async Task<List<CustomerSearchItemDto>> SearchAsync(CustomerSearchRequest request, PartnerType role, CancellationToken ct = default)
    {
        var keyword = request.Keyword?.Trim() ?? string.Empty;
        if (keyword.Length == 0 || role == PartnerType.None)
            return new List<CustomerSearchItemDto>();

        var limit = Math.Clamp(request.Limit, 1, 50);
        var pattern = $"%{EscapeLike(keyword)}%";

        var query = _db.Customers.AsNoTracking().Where(c => !c.IsDeleted);
        if (role == PartnerType.Customer)
            query = query.Where(c => c.IsCustomer);
        else if (role == PartnerType.Supplier)
            query = query.Where(c => c.IsSupplier);

        if (request.ActiveOnly)
            query = query.Where(c => c.Status == CustomerStatus.Active);

        query = query.Where(c =>
            EF.Functions.ILike(EF.Functions.Unaccent(c.Code), EF.Functions.Unaccent(pattern))
            || EF.Functions.ILike(EF.Functions.Unaccent(c.Name), EF.Functions.Unaccent(pattern))
            || (c.TaxCode != null && EF.Functions.ILike(EF.Functions.Unaccent(c.TaxCode), EF.Functions.Unaccent(pattern)))
            || (c.CompanyAddress != null && EF.Functions.ILike(EF.Functions.Unaccent(c.CompanyAddress), EF.Functions.Unaccent(pattern)))
            || (c.PhoneNumber != null && EF.Functions.ILike(EF.Functions.Unaccent(c.PhoneNumber), EF.Functions.Unaccent(pattern))));

        return await query
            .OrderBy(c => c.Code)
            .ThenBy(c => c.Name)
            .Take(limit)
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
    }

    private const int MaxCreateAttempts = 5;

    public async Task<CustomerDto> CreateAsync(CreateCustomerRequest request, PartnerRole role, CancellationToken ct = default)
    {
        // The endpoint's own role is forced on; the other flag needs that role's create permission (D2).
        var isCustomer = role == PartnerRole.Customer || (request.IsCustomer ?? false);
        var isSupplier = role == PartnerRole.Supplier || (request.IsSupplier ?? false);
        PartnerPermissionGuard.EnsureCanCreate(_currentUser, isCustomer, isSupplier);

        var explicitCode = !string.IsNullOrWhiteSpace(request.Code);

        for (var attempt = 1; attempt <= MaxCreateAttempts; attempt++)
        {
            var code = explicitCode ? request.Code!.Trim() : await GenerateCodeAsync(ct);

            if (await _db.Customers.AnyAsync(c => c.Code == code, ct))
            {
                if (explicitCode)
                    throw new ConflictException($"Mã khách hàng '{code}' đã tồn tại.");
                continue;
            }

            var customer = new Customer
            {
                Code = code,
                Name = request.Name.Trim(),
                TaxCode = request.TaxCode?.Trim(),
                CompanyAddress = request.CompanyAddress?.Trim(),
                DefaultShippingAddress = request.DefaultShippingAddress?.Trim(),
                ContactPerson = request.ContactPerson?.Trim(),
                PhoneNumber = request.PhoneNumber?.Trim(),
                Email = request.Email?.Trim(),
                Group = request.Group,
                Note = request.Note,
                Status = CustomerStatus.Active,
                IsCustomer = isCustomer,
                IsSupplier = isSupplier,
            };

            _db.Customers.Add(customer);

            try
            {
                await _db.SaveChangesAsync(ct);
                return customer.Adapt<CustomerDto>();
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex) && !explicitCode && attempt < MaxCreateAttempts)
            {
                // Lost race: another request reserved the same generated code. Detach and retry.
                _db.Entry(customer).State = EntityState.Detached;
            }
        }

        throw new ConflictException("Không thể tạo mã khách hàng tự động sau nhiều lần thử. Vui lòng thử lại.");
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        // Npgsql wraps Postgres errors; 23505 = unique_violation.
        var inner = ex.InnerException;
        var sqlState = inner?.GetType().GetProperty("SqlState")?.GetValue(inner) as string;
        return sqlState == "23505";
    }

    private static string EscapeLike(string input) =>
        input.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    public async Task<CustomerDto> UpdateAsync(Guid id, UpdateCustomerRequest request, PartnerRole role, CancellationToken ct = default)
    {
        var customer = await WithRole(_db.Customers, role).FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Customer), id);

        var isCustomer = request.IsCustomer ?? customer.IsCustomer;
        var isSupplier = request.IsSupplier ?? customer.IsSupplier;
        if (!isCustomer && !isSupplier)
            throw new DomainException("PARTNER_ROLE_REQUIRED", "Đối tượng phải là khách hàng hoặc nhà cung cấp.");
        PartnerPermissionGuard.EnsureCanUpdate(_currentUser, customer.IsCustomer, customer.IsSupplier, isCustomer, isSupplier);

        customer.Name = request.Name.Trim();
        customer.TaxCode = request.TaxCode?.Trim();
        customer.CompanyAddress = request.CompanyAddress?.Trim();
        customer.DefaultShippingAddress = request.DefaultShippingAddress?.Trim();
        customer.ContactPerson = request.ContactPerson?.Trim();
        customer.PhoneNumber = request.PhoneNumber?.Trim();
        customer.Email = request.Email?.Trim();
        customer.Group = request.Group;
        customer.Note = request.Note;
        customer.Status = request.Status;
        customer.IsCustomer = isCustomer;
        customer.IsSupplier = isSupplier;

        await _db.SaveChangesAsync(ct);
        return customer.Adapt<CustomerDto>();
    }

    public async Task DeleteAsync(Guid id, PartnerRole role, CancellationToken ct = default)
    {
        var customer = await WithRole(_db.Customers, role).FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Customer), id);
        PartnerPermissionGuard.EnsureCanDelete(_currentUser, customer.IsCustomer, customer.IsSupplier);
        if (await _db.StockVouchers.AnyAsync(v => v.PartnerId == id, ct))
            throw new ConflictException("Đối tượng đã có phiếu nhập/xuất kho, không thể xóa.");

        customer.IsDeleted = true;
        customer.DeletedAt = _clock.UtcNow;
        customer.DeletedBy = _currentUser.UserId;

        await _db.SaveChangesAsync(ct);
    }

    private static IQueryable<Customer> WithRole(IQueryable<Customer> query, PartnerRole role) =>
        role == PartnerRole.Supplier ? query.Where(c => c.IsSupplier) : query.Where(c => c.IsCustomer);

    private async Task<string> GenerateCodeAsync(CancellationToken ct)
    {
        var prefix = "KH";
        var date = _clock.Now.ToString("yyMMdd");
        var todayCount = await _db.Customers
            .CountAsync(c => c.Code.StartsWith($"{prefix}-{date}"), ct);
        return $"{prefix}-{date}-{todayCount + 1:D4}";
    }
}
