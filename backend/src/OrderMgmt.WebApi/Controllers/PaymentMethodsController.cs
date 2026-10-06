using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Application.Inventory.PaymentMethods.Interfaces;
using OrderMgmt.Application.Inventory.PaymentMethods.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.WebApi.Authorization;

namespace OrderMgmt.WebApi.Controllers;

[Route("api/payment-methods")]
public class PaymentMethodsController : ApiControllerBase
{
    private readonly IPaymentMethodService _service;
    private readonly IValidator<CreatePaymentMethodRequest> _createValidator;
    private readonly IValidator<UpdatePaymentMethodRequest> _updateValidator;

    public PaymentMethodsController(
        IPaymentMethodService service,
        IValidator<CreatePaymentMethodRequest> createValidator,
        IValidator<UpdatePaymentMethodRequest> updateValidator)
    {
        _service = service;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    [HttpGet]
    [Authorize]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PaymentMethodDto>>>> List(CancellationToken ct)
        => Success(await _service.ListAsync(ct));

    [HttpGet("{id:guid}")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<PaymentMethodDto>>> Get(Guid id, CancellationToken ct)
        => Success(await _service.GetAsync(id, ct));

    [HttpPost]
    [HasPermission(Permissions.Inventory.ManageCatalogs)]
    public async Task<ActionResult<ApiResponse<PaymentMethodDto>>> Create(
        [FromBody] CreatePaymentMethodRequest request, CancellationToken ct)
    {
        await _createValidator.ValidateAndThrowAsync(request, ct);
        return Success(await _service.CreateAsync(request, ct));
    }

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.Inventory.ManageCatalogs)]
    public async Task<ActionResult<ApiResponse<PaymentMethodDto>>> Update(
        Guid id, [FromBody] UpdatePaymentMethodRequest request, CancellationToken ct)
    {
        await _updateValidator.ValidateAndThrowAsync(request, ct);
        return Success(await _service.UpdateAsync(id, request, ct));
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.Inventory.ManageCatalogs)]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return Success();
    }
}
