using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Application.Organization.Branches.Interfaces;
using OrderMgmt.Application.Organization.Branches.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.WebApi.Authorization;

namespace OrderMgmt.WebApi.Controllers;

[Route("api/branches")]
public class BranchesController : ApiControllerBase
{
    private readonly IBranchService _service;
    private readonly IValidator<CreateBranchRequest> _createValidator;
    private readonly IValidator<UpdateBranchRequest> _updateValidator;

    public BranchesController(
        IBranchService service,
        IValidator<CreateBranchRequest> createValidator,
        IValidator<UpdateBranchRequest> updateValidator)
    {
        _service = service;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    [HttpGet]
    [Authorize]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<BranchDto>>>> List(CancellationToken ct)
        => Success(await _service.ListAsync(ct));

    [HttpGet("{id:guid}")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<BranchDto>>> Get(Guid id, CancellationToken ct)
        => Success(await _service.GetAsync(id, ct));

    [HttpPost]
    [HasPermission(Permissions.Branches.Manage)]
    public async Task<ActionResult<ApiResponse<BranchDto>>> Create(
        [FromBody] CreateBranchRequest request, CancellationToken ct)
    {
        await _createValidator.ValidateAndThrowAsync(request, ct);
        return Success(await _service.CreateAsync(request, ct));
    }

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.Branches.Manage)]
    public async Task<ActionResult<ApiResponse<BranchDto>>> Update(
        Guid id, [FromBody] UpdateBranchRequest request, CancellationToken ct)
    {
        await _updateValidator.ValidateAndThrowAsync(request, ct);
        return Success(await _service.UpdateAsync(id, request, ct));
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.Branches.Manage)]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return Success();
    }

    [HttpPut("{id:guid}/lock")]
    [HasPermission(Permissions.PeriodLock.Manage)]
    public async Task<ActionResult<ApiResponse<BranchDto>>> SetLock(
        Guid id, [FromBody] SetPeriodLockRequest request, CancellationToken ct)
        => Success(await _service.SetLockAsync(id, request, ct));
}
