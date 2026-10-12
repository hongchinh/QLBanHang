using OrderMgmt.Application.Catalog.Customers.Models;
using OrderMgmt.Application.CashDebt.CashVouchers.Models;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.CashDebt.CashVouchers.Interfaces;

/// Manual receipts and payments (Phase 05 save rules).
public interface ICashVoucherService
{
    Task<CashVoucherListResult> ListAsync(CashVoucherListRequest request, CancellationToken ct = default);
    Task<CashVoucherDefaultsDto> GetDefaultsAsync(CashVoucherType type, CancellationToken ct = default);
    Task<List<CustomerSearchItemDto>> SearchPartnersAsync(CashVoucherType type, string? keyword, int limit, Guid? reasonId,
        CancellationToken ct = default);
    Task<IReadOnlyList<CashDocumentActivityDto>> ListActivitiesAsync(Guid id, CancellationToken ct = default);
    Task<CashVoucherDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<CashVoucherDto> CreateAsync(UpsertCashVoucherRequest request, CancellationToken ct = default);
    Task<CashVoucherDto> UpdateAsync(Guid id, UpsertCashVoucherRequest request, CancellationToken ct = default);
    Task<CashVoucherDto> CancelAsync(Guid id, CashDocumentActionRequest request, CancellationToken ct = default);
    Task<CashVoucherDto> RestoreAsync(Guid id, CashDocumentActionRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CashDocumentActionRequest request, CancellationToken ct = default);
}
