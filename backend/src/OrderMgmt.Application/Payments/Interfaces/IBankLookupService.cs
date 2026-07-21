using OrderMgmt.Application.Payments.Models;

namespace OrderMgmt.Application.Payments.Interfaces;

public interface IBankLookupService
{
    Task<IReadOnlyList<BankDto>> ListAsync(CancellationToken ct = default);
}
