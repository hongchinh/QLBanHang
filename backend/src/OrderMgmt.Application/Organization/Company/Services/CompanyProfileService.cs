using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Organization.Company.Interfaces;
using OrderMgmt.Application.Organization.Company.Models;
using OrderMgmt.Domain.Entities.Organization;

namespace OrderMgmt.Application.Organization.Company.Services;

public class CompanyProfileService : ICompanyProfileService
{
    private readonly IAppDbContext _db;
    private readonly IDateTime _clock;
    private readonly ICurrentUser _currentUser;

    public CompanyProfileService(IAppDbContext db, IDateTime clock, ICurrentUser currentUser)
    {
        _db = db;
        _clock = clock;
        _currentUser = currentUser;
    }

    public async Task<CompanyProfileDto> GetAsync(CancellationToken ct = default)
        => ToDto(await _db.CompanyProfiles.AsNoTracking().SingleAsync(p => p.Id == 1, ct));

    public async Task<CompanyProfileDto> UpdateAsync(UpdateCompanyProfileRequest request, CancellationToken ct = default)
    {
        var profile = await _db.CompanyProfiles.SingleAsync(p => p.Id == 1, ct);
        profile.Name = Clean(request.Name);
        profile.Address = Clean(request.Address);
        profile.TaxCode = Clean(request.TaxCode);
        profile.ChiefAccountantName = Clean(request.ChiefAccountantName);
        profile.LegalRepresentativeName = Clean(request.LegalRepresentativeName);
        profile.AccountingServiceNote = Clean(request.AccountingServiceNote);
        profile.UpdatedAt = _clock.UtcNow;
        profile.UpdatedBy = _currentUser.UserId;
        await _db.SaveChangesAsync(ct);
        return ToDto(profile);
    }

    private static string? Clean(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static CompanyProfileDto ToDto(CompanyProfile p) => new()
    {
        Name = p.Name,
        Address = p.Address,
        TaxCode = p.TaxCode,
        ChiefAccountantName = p.ChiefAccountantName,
        LegalRepresentativeName = p.LegalRepresentativeName,
        AccountingServiceNote = p.AccountingServiceNote,
        UpdatedAt = p.UpdatedAt,
    };
}
