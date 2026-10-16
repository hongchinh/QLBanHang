using OrderMgmt.Application.Common;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Sales.Quotations.Interfaces;
using OrderMgmt.Application.Sales.Quotations.Models;
using OrderMgmt.Domain.Common;

namespace OrderMgmt.Application.Sales.Quotations.Services;

public class QuotationSystemSettingsService : IQuotationSystemSettingsService
{
    private readonly IAppDbContext _db;
    private readonly IDateTime _clock;
    private readonly ICurrentUser _currentUser;

    public QuotationSystemSettingsService(IAppDbContext db, IDateTime clock, ICurrentUser currentUser)
    {
        _db = db;
        _clock = clock;
        _currentUser = currentUser;
    }

    public async Task<QuotationSystemSettingsDto> GetAsync(CancellationToken ct = default)
    {
        var settings = await _db.QuotationSystemSettings.AsNoTracking().FirstAsync(s => s.Id == 1, ct);
        var updatedByName = await ResolveUserNameAsync(settings.UpdatedBy, ct);
        return MapToDto(settings, updatedByName);
    }

    public async Task<QuotationSystemSettingsDto> UpdateAsync(
        UpdateQuotationSystemSettingsRequest request, CancellationToken ct = default)
    {
        if (!RevenueDateField.AllowedValues.Contains(request.RevenueReportingDateField))
            throw new DomainException("VALIDATION",
                $"Giá trị '{request.RevenueReportingDateField}' không hợp lệ. " +
                $"Chấp nhận: {string.Join(", ", RevenueDateField.AllowedValues)}");

        var settings = await _db.QuotationSystemSettings.FirstAsync(s => s.Id == 1, ct);
        settings.RevenueReportingDateField = request.RevenueReportingDateField;
        settings.UpdatedAt = _clock.UtcNow;
        settings.UpdatedBy = _currentUser.UserId;
        await _db.SaveChangesAsync(ct);

        var updatedByName = await ResolveUserNameAsync(settings.UpdatedBy, ct);
        return MapToDto(settings, updatedByName);
    }

    private async Task<string?> ResolveUserNameAsync(Guid? userId, CancellationToken ct)
    {
        if (!userId.HasValue) return null;
        return await _db.Users.IgnoreQueryFilters([QueryFilters.SoftDelete])
            .AsNoTracking()
            .Where(u => u.Id == userId.Value)
            .Select(u => u.FullName)
            .FirstOrDefaultAsync(ct);
    }

    private static QuotationSystemSettingsDto MapToDto(
        OrderMgmt.Domain.Entities.Sales.QuotationSystemSettings s, string? updatedByName) => new()
    {
        RevenueReportingDateField = s.RevenueReportingDateField,
        UpdatedAt = s.UpdatedAt,
        UpdatedByName = updatedByName,
    };
}
