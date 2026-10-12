using OrderMgmt.Application.CashDebt.Funds.Models;

namespace OrderMgmt.Application.CashDebt.Funds.Interfaces;

public interface IFundService
{
    Task<IReadOnlyList<FundDto>> ListAsync(FundListRequest request, CancellationToken ct = default);
    Task<FundDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<FundDto> CreateAsync(CreateFundRequest request, CancellationToken ct = default);
    Task<FundDto> UpdateAsync(Guid id, UpdateFundRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    /// PUT api/funds/{id}/opening-balance: needs the branch opening date (400 OPENING_DATE_NOT_SET) outside the
    /// locked period, then applies the negative-fund policy to the fund's whole history.
    Task<FundDto> SetOpeningBalanceAsync(Guid id, SetFundOpeningBalanceRequest request, CancellationToken ct = default);
}
