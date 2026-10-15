using OrderMgmt.Domain.Common;
using OrderMgmt.Domain.Entities.Organization;
using OrderMgmt.Domain.Entities.Payments;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Domain.Entities.CashDebt;

/// A cash box or bank account of one branch. Each (branch, kind) has exactly one default fund (P18).
public class Fund : BaseEntity
{
    public Guid BranchId { get; set; }
    public Branch? Branch { get; set; }

    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public FundKind Kind { get; set; }
    public string? BankName { get; set; }
    public string? AccountNumber { get; set; }

    /// Bank of a Bank-kind fund (null for other kinds); drives the transfer QR.
    public Guid? BankId { get; set; }
    public Bank? Bank { get; set; }
    public string? AccountHolderName { get; set; }
    public bool IsDefault { get; set; }
    public decimal OpeningAmount { get; set; }
    public bool IsActive { get; set; } = true;
}
