using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Application.Payments.Interfaces;
using OrderMgmt.Application.Payments.Models;

namespace OrderMgmt.WebApi.Controllers;

[Authorize]
[Route("api/me/bank-accounts")]
public class MeBankAccountsController : ApiControllerBase
{
    private readonly IUserBankAccountService _service;
    private readonly IValidator<CreateUserBankAccountRequest> _createValidator;
    private readonly IValidator<UpdateUserBankAccountRequest> _updateValidator;

    public MeBankAccountsController(
        IUserBankAccountService service,
        IValidator<CreateUserBankAccountRequest> createValidator,
        IValidator<UpdateUserBankAccountRequest> updateValidator)
    {
        _service = service;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<UserBankAccountDto>>>> List(CancellationToken ct)
        => Success(await _service.ListForCurrentUserAsync(ct));

    [HttpPost]
    public async Task<ActionResult<ApiResponse<UserBankAccountDto>>> Create(
        [FromBody] CreateUserBankAccountRequest request, CancellationToken ct)
    {
        await _createValidator.ValidateAndThrowAsync(request, ct);
        return Success(await _service.CreateAsync(request, ct));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<UserBankAccountDto>>> Update(
        Guid id, [FromBody] UpdateUserBankAccountRequest request, CancellationToken ct)
    {
        await _updateValidator.ValidateAndThrowAsync(request, ct);
        return Success(await _service.UpdateAsync(id, request, ct));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return Success();
    }

    [HttpPut("{id:guid}/default")]
    public async Task<ActionResult<ApiResponse<UserBankAccountDto>>> SetDefault(Guid id, CancellationToken ct)
        => Success(await _service.SetDefaultAsync(id, ct));
}
