using OrderMgmt.Application.CashDebt.Funds.Models;

namespace OrderMgmt.Application.CashDebt.Funds.Interfaces;

public interface IFundService
{
    Task<IReadOnlyList<FundDto>> ListAsync(FundListRequest request, CancellationToken ct = default);
    Task<FundDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<FundDto> CreateAsync(CreateFundRequest request, CancellationToken ct = default);
    Task<FundDto> UpdateAsync(Guid id, UpdateFundRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
