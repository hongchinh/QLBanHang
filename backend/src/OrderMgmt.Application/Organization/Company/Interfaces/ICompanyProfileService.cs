using OrderMgmt.Application.Organization.Company.Models;

namespace OrderMgmt.Application.Organization.Company.Interfaces;

public interface ICompanyProfileService
{
    Task<CompanyProfileDto> GetAsync(CancellationToken ct = default);
    Task<CompanyProfileDto> UpdateAsync(UpdateCompanyProfileRequest request, CancellationToken ct = default);
}
