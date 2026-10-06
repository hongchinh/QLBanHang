using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Application.Inventory.Settings.Interfaces;
using OrderMgmt.Application.Inventory.Settings.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Enums;
using OrderMgmt.WebApi.Authorization;

namespace OrderMgmt.WebApi.Controllers;

[Route("api/inventory")]
public class InventorySettingsController : ApiControllerBase
{
    private readonly IInventorySettingsService _settings;
    private readonly IValidator<UpdateInventorySettingsRequest> _settingsValidator;

    public InventorySettingsController(
        IInventorySettingsService settings,
        IValidator<UpdateInventorySettingsRequest> settingsValidator)
    {
        _settings = settings;
        _settingsValidator = settingsValidator;
    }

    [HttpGet("settings")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<InventorySettingsDto>>> GetSettings(CancellationToken ct)
        => Success(await _settings.GetAsync(ct));

    [HttpPut("settings")]
    [HasPermission(Permissions.Inventory.Settings)]
    public async Task<ActionResult<ApiResponse<InventorySettingsDto>>> UpdateSettings(
        [FromBody] UpdateInventorySettingsRequest request, CancellationToken ct)
    {
        await _settingsValidator.ValidateAndThrowAsync(request, ct);
        return Success(await _settings.UpdateAsync(request, ct));
    }

    [HttpGet("numbering")]
    [HasPermission(Permissions.Inventory.Settings)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<DocumentNumberingDto>>>> ListNumbering(CancellationToken ct)
        => Success(await _settings.ListNumberingAsync(ct));

    [HttpPut("numbering/{docType}")]
    [HasPermission(Permissions.Inventory.Settings)]
    public async Task<ActionResult<ApiResponse<DocumentNumberingDto>>> UpdateNumbering(
        DocumentType docType, [FromBody] UpdateNumberingRequest request, CancellationToken ct)
        => Success(await _settings.UpdateNumberingAsync(docType, request, ct));
}
