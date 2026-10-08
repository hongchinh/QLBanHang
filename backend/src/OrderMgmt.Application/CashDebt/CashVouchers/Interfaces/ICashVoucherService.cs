using OrderMgmt.Application.CashDebt.CashVouchers.Models;

namespace OrderMgmt.Application.CashDebt.CashVouchers.Interfaces;

/// Manual receipts and payments (Phase 05 save rules).
public interface ICashVoucherService
{
    Task<CashVoucherDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<CashVoucherDto> CreateAsync(UpsertCashVoucherRequest request, CancellationToken ct = default);
    Task<CashVoucherDto> UpdateAsync(Guid id, UpsertCashVoucherRequest request, CancellationToken ct = default);
    Task<CashVoucherDto> CancelAsync(Guid id, CashDocumentActionRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CashDocumentActionRequest request, CancellationToken ct = default);
}
