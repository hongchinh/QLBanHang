using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.CashDebt.Funds.Models;

public class FundDto
{
    public Guid Id { get; set; }
    public Guid BranchId { get; set; }
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public FundKind Kind { get; set; }
    public string? BankName { get; set; }
    public string? AccountNumber { get; set; }
    public bool IsDefault { get; set; }
    public decimal OpeningAmount { get; set; }
    public bool IsActive { get; set; }
}

public class FundListRequest
{
    public FundKind? Kind { get; set; }
    public bool? IsActive { get; set; }
}

public class CreateFundRequest
{
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public FundKind Kind { get; set; }
    public string? BankName { get; set; }
    public string? AccountNumber { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UpdateFundRequest
{
    public string Name { get; set; } = default!;
    public FundKind Kind { get; set; }
    public string? BankName { get; set; }
    public string? AccountNumber { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; }
}

public class SetFundOpeningBalanceRequest
{
    public decimal Amount { get; set; }
    public bool AcknowledgeNegativeFund { get; set; }
}
