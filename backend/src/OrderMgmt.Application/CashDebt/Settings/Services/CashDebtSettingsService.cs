using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.CashDebt.Settings.Interfaces;
using OrderMgmt.Application.CashDebt.Settings.Models;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Domain.Entities.CashDebt;

namespace OrderMgmt.Application.CashDebt.Settings.Services;

public class CashDebtSettingsService : ICashDebtSettingsService
{
    private readonly IAppDbContext _db;
    private readonly IDateTime _clock;
    private readonly ICurrentUser _currentUser;

    public CashDebtSettingsService(IAppDbContext db, IDateTime clock, ICurrentUser currentUser)
    {
        _db = db;
        _clock = clock;
        _currentUser = currentUser;
    }

    public async Task<CashDebtSettingsDto> GetAsync(CancellationToken ct = default)
        => ToDto(await _db.CashDebtSettings.AsNoTracking().SingleAsync(s => s.Id == 1, ct));

    public async Task<CashDebtSettingsDto> UpdateAsync(UpdateCashDebtSettingsRequest request, CancellationToken ct = default)
    {
        var settings = await _db.CashDebtSettings.SingleAsync(s => s.Id == 1, ct);
        settings.CreditLimitPolicy = request.CreditLimitPolicy;
        settings.NegativeFundPolicy = request.NegativeFundPolicy;
        settings.UpdatedAt = _clock.UtcNow;
        settings.UpdatedBy = _currentUser.UserId;
        await _db.SaveChangesAsync(ct);
        return ToDto(settings);
    }

    private static CashDebtSettingsDto ToDto(CashDebtSettings s) => new()
    {
        CreditLimitPolicy = s.CreditLimitPolicy,
        NegativeFundPolicy = s.NegativeFundPolicy,
        UpdatedAt = s.UpdatedAt,
    };
}
