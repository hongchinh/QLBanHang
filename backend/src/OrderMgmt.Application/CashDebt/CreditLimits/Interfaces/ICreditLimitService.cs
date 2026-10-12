using OrderMgmt.Application.CashDebt.CreditLimits.Models;
using OrderMgmt.Application.Common.Models;

namespace OrderMgmt.Application.CashDebt.CreditLimits.Interfaces;

public interface ICreditLimitService
{
    Task<CreditLimitListResult> ListAsync(PageRequest request, CancellationToken ct = default);
    Task<CreditLimitDto> UpsertAsync(Guid customerId, UpsertCreditLimitRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid customerId, CancellationToken ct = default);
}
