using OrderMgmt.Application.Catalog.Customers.Models;

namespace OrderMgmt.Application.Search.Models;

public sealed class GlobalSearchResultDto
{
    public List<CustomerSearchItemDto> Customers { get; set; } = new();
    /// Supplier-only partners never appear under Customers; this group needs suppliers.view (D38).
    public List<CustomerSearchItemDto> Suppliers { get; set; } = new();
    public List<QuotationSearchItemDto> Quotations { get; set; } = new();
}
