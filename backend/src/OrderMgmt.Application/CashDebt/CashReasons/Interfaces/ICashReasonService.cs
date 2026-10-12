using OrderMgmt.Application.CashDebt.CashReasons.Models;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.CashDebt.CashReasons.Interfaces;

public interface ICashReasonService
{
    Task<IReadOnlyList<CashReasonDto>> ListAsync(CashVoucherType? type, bool includeAuto, CancellationToken ct = default);
    Task<CashReasonDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<CashReasonDto> CreateAsync(CreateCashReasonRequest request, CancellationToken ct = default);
    Task<CashReasonDto> UpdateAsync(Guid id, UpdateCashReasonRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
