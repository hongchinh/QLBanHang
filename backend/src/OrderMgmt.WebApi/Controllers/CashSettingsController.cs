using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderMgmt.Application.CashDebt.Settings.Interfaces;
using OrderMgmt.Application.CashDebt.Settings.Models;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.WebApi.Authorization;

namespace OrderMgmt.WebApi.Controllers;

[Route("api/cash")]
public class CashSettingsController : ApiControllerBase
{
    private readonly ICashDebtSettingsService _settings;
    private readonly IValidator<UpdateCashDebtSettingsRequest> _validator;

    public CashSettingsController(ICashDebtSettingsService settings, IValidator<UpdateCashDebtSettingsRequest> validator)
    {
        _settings = settings;
        _validator = validator;
    }

    [HttpGet("settings")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<CashDebtSettingsDto>>> GetSettings(CancellationToken ct)
        => Success(await _settings.GetAsync(ct));

    [HttpPut("settings")]
    [HasPermission(Permissions.Cash.Settings)]
    public async Task<ActionResult<ApiResponse<CashDebtSettingsDto>>> UpdateSettings(
        [FromBody] UpdateCashDebtSettingsRequest request, CancellationToken ct)
    {
        await _validator.ValidateAndThrowAsync(request, ct);
        return Success(await _settings.UpdateAsync(request, ct));
    }
}
