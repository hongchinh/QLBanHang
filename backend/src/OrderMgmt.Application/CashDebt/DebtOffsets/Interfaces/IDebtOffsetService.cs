using OrderMgmt.Application.CashDebt.CashVouchers.Models;
using OrderMgmt.Application.CashDebt.DebtOffsets.Models;

namespace OrderMgmt.Application.CashDebt.DebtOffsets.Interfaces;

/// Debt offset vouchers (chứng từ bù trừ công nợ, P23) of the working branch: create, view, cancel and delete; no edit
/// and no restore. The controller checks the debt_offset.* permissions.
public interface IDebtOffsetService
{
    Task<DebtOffsetListResult> ListAsync(DebtOffsetListRequest request, CancellationToken ct = default);
    Task<DebtOffsetDefaultsDto> GetDefaultsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<CashDocumentActivityDto>> ListActivitiesAsync(Guid id, CancellationToken ct = default);
    Task<DebtOffsetDto> GetAsync(Guid id, CancellationToken ct = default);

    /// Posts R− and P− of the amount and settles each item against the offset's entry of its side (origin Offset,
    /// batch = the voucher id).
    Task<DebtOffsetDto> CreateAsync(CreateDebtOffsetRequest request, CancellationToken ct = default);

    /// Removes the two entries and their Offset settlements without confirmation, then recomputes the Auto settlement
    /// of the stock vouchers they were settled against.
    Task<DebtOffsetDto> CancelAsync(Guid id, CashDocumentActionRequest request, CancellationToken ct = default);

    /// Soft delete in any status; an Active offset first loses its debt as on cancel.
    Task DeleteAsync(Guid id, CashDocumentActionRequest request, CancellationToken ct = default);
}
