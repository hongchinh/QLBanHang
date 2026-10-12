using OrderMgmt.Domain.Common;
using OrderMgmt.Domain.Entities.Catalog;

namespace OrderMgmt.Domain.Entities.CashDebt;

/// Receivable limit of one customer in one branch. At most one non-deleted row per (customer, branch).
public class CreditLimit : BaseEntity
{
    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public Guid BranchId { get; set; }

    public decimal Amount { get; set; }
}
