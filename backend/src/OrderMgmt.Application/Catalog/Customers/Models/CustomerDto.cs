using OrderMgmt.Application.Common.Models;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Catalog.Customers.Models;

public class CustomerDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? TaxCode { get; set; }
    public string? CompanyAddress { get; set; }
    public string? DefaultShippingAddress { get; set; }
    public string? ContactPerson { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public CustomerGroup Group { get; set; }
    public string? Note { get; set; }
    public CustomerStatus Status { get; set; }
    public bool IsCustomer { get; set; }
    public bool IsSupplier { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class CustomerListItemDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? TaxCode { get; set; }
    public string? PhoneNumber { get; set; }
    public string? ContactPerson { get; set; }
    public CustomerGroup Group { get; set; }
    public CustomerStatus Status { get; set; }
    public bool IsCustomer { get; set; }
    public bool IsSupplier { get; set; }
}

public class CreateCustomerRequest
{
    public string? Code { get; set; }
    public string Name { get; set; } = default!;
    public string? TaxCode { get; set; }
    public string? CompanyAddress { get; set; }
    public string? DefaultShippingAddress { get; set; }
    public string? ContactPerson { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public CustomerGroup Group { get; set; } = CustomerGroup.Company;
    public string? Note { get; set; }
    /// The endpoint's own role is always set; the other flag defaults to false and needs that role's create permission.
    public bool? IsCustomer { get; set; }
    public bool? IsSupplier { get; set; }
}

public class UpdateCustomerRequest
{
    public string Name { get; set; } = default!;
    public string? TaxCode { get; set; }
    public string? CompanyAddress { get; set; }
    public string? DefaultShippingAddress { get; set; }
    public string? ContactPerson { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public CustomerGroup Group { get; set; }
    public string? Note { get; set; }
    public CustomerStatus Status { get; set; }
    /// Null keeps the current value.
    public bool? IsCustomer { get; set; }
    public bool? IsSupplier { get; set; }
}

public class CustomerListRequest : PageRequest
{
    public CustomerGroup? Group { get; set; }
    public CustomerStatus? Status { get; set; }
}

public class CustomerSearchItemDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? TaxCode { get; set; }
    public string? CompanyAddress { get; set; }
    public string? DefaultShippingAddress { get; set; }
    public string? ContactPerson { get; set; }
    public string? PhoneNumber { get; set; }
    public CustomerStatus Status { get; set; }
    public bool IsCustomer { get; set; }
    public bool IsSupplier { get; set; }
}

public class CustomerSearchRequest
{
    public string Keyword { get; set; } = string.Empty;
    public bool ActiveOnly { get; set; } = true;
    public int Limit { get; set; } = 20;
}
