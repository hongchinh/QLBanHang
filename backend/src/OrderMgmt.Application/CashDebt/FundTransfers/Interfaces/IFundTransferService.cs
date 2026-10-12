using OrderMgmt.Application.CashDebt.CashVouchers.Models;
using OrderMgmt.Application.CashDebt.FundTransfers.Models;

namespace OrderMgmt.Application.CashDebt.FundTransfers.Interfaces;

/// Fund transfers (phiếu chuyển quỹ) of the working branch. The controller checks the fund_transfers.* permissions.
public interface IFundTransferService
{
    Task<FundTransferListResult> ListAsync(FundTransferListRequest request, CancellationToken ct = default);
    Task<FundTransferDefaultsDto> GetDefaultsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<CashDocumentActivityDto>> ListActivitiesAsync(Guid id, CancellationToken ct = default);
    Task<FundTransferDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<FundTransferDto> CreateAsync(UpsertFundTransferRequest request, CancellationToken ct = default);
    Task<FundTransferDto> UpdateAsync(Guid id, UpsertFundTransferRequest request, CancellationToken ct = default);
    Task<FundTransferDto> CancelAsync(Guid id, CashDocumentActionRequest request, CancellationToken ct = default);
    Task<FundTransferDto> RestoreAsync(Guid id, CashDocumentActionRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CashDocumentActionRequest request, CancellationToken ct = default);
}
