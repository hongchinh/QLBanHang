using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Application.Organization.Company.Interfaces;
using OrderMgmt.Application.Organization.Company.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.WebApi.Authorization;

namespace OrderMgmt.WebApi.Controllers;

[Route("api/settings")]
public class CompanyProfileController : ApiControllerBase
{
    private readonly ICompanyProfileService _company;
    private readonly IValidator<UpdateCompanyProfileRequest> _validator;

    public CompanyProfileController(ICompanyProfileService company, IValidator<UpdateCompanyProfileRequest> validator)
    {
        _company = company;
        _validator = validator;
    }

    [HttpGet("company")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<CompanyProfileDto>>> Get(CancellationToken ct)
        => Success(await _company.GetAsync(ct));

    [HttpPut("company")]
    [HasPermission(Permissions.CompanySettings.Manage)]
    public async Task<ActionResult<ApiResponse<CompanyProfileDto>>> Update(
        [FromBody] UpdateCompanyProfileRequest request, CancellationToken ct)
    {
        await _validator.ValidateAndThrowAsync(request, ct);
        return Success(await _company.UpdateAsync(request, ct));
    }
}
