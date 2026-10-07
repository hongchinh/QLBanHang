using OrderMgmt.Domain.Common;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Domain.Entities.Catalog;

public class Customer : BaseEntity
{
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? TaxCode { get; set; }
    public string? CompanyAddress { get; set; }
    public string? DefaultShippingAddress { get; set; }
    public string? ContactPerson { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public CustomerGroup Group { get; set; } = CustomerGroup.Company;
    public string? Note { get; set; }
    public CustomerStatus Status { get; set; } = CustomerStatus.Active;

    /// Shared partner catalog: a partner can be a customer, a supplier, or both.
    public bool IsCustomer { get; set; } = true;
    public bool IsSupplier { get; set; }

    public ICollection<CustomerAddress> Addresses { get; set; } = new List<CustomerAddress>();
}
