using OrderMgmt.Application.CashDebt.Settings.Models;

namespace OrderMgmt.Application.CashDebt.Settings.Interfaces;

public interface ICashDebtSettingsService
{
    Task<CashDebtSettingsDto> GetAsync(CancellationToken ct = default);
    Task<CashDebtSettingsDto> UpdateAsync(UpdateCashDebtSettingsRequest request, CancellationToken ct = default);
}
