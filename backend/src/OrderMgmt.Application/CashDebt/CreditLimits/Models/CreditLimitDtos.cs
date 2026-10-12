using OrderMgmt.Application.Common.Models;

namespace OrderMgmt.Application.CashDebt.CreditLimits.Models;

public class CreditLimitDto
{
    public Guid CustomerId { get; set; }
    public string CustomerCode { get; set; } = default!;
    public string CustomerName { get; set; } = default!;
    public int? CreditDays { get; set; }
    public decimal Amount { get; set; }
}

public class CreditLimitListResult : PagedResult<CreditLimitDto>
{
}

public class UpsertCreditLimitRequest
{
    public decimal Amount { get; set; }
}
