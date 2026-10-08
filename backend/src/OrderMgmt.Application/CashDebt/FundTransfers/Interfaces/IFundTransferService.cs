using OrderMgmt.Application.CashDebt.FundTransfers.Models;

namespace OrderMgmt.Application.CashDebt.FundTransfers.Interfaces;

/// Fund transfers (phiếu chuyển quỹ) of the working branch. The controller checks the fund_transfers.* permissions.
public interface IFundTransferService
{
    Task<FundTransferDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<FundTransferDto> CreateAsync(UpsertFundTransferRequest request, CancellationToken ct = default);
}
