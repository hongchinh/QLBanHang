using OrderMgmt.Application.Catalog.Customers.Models;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Catalog.Customers.Interfaces;

/// Shared partner catalog. Every operation is scoped to one role: /api/customers uses
/// PartnerRole.Customer and /api/suppliers uses PartnerRole.Supplier.
public interface ICustomerService
{
    Task<PagedResult<CustomerListItemDto>> ListAsync(CustomerListRequest request, PartnerRole role, CancellationToken ct = default);
    Task<List<CustomerSearchItemDto>> SearchAsync(CustomerSearchRequest request, PartnerType role, CancellationToken ct = default);
    Task<CustomerDto> GetAsync(Guid id, PartnerRole role, CancellationToken ct = default);
    Task<CustomerDto> CreateAsync(CreateCustomerRequest request, PartnerRole role, CancellationToken ct = default);
    Task<CustomerDto> UpdateAsync(Guid id, UpdateCustomerRequest request, PartnerRole role, CancellationToken ct = default);
    Task DeleteAsync(Guid id, PartnerRole role, CancellationToken ct = default);
}
