using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Application.Payments.Interfaces;
using OrderMgmt.Application.Payments.Models;

namespace OrderMgmt.WebApi.Controllers;

[Authorize]
[Route("api/payment-qr")]
public class PaymentQrController : ApiControllerBase
{
    private readonly IPaymentQrService _service;
    private readonly IValidator<GenerateQrRequest> _validator;

    public PaymentQrController(IPaymentQrService service, IValidator<GenerateQrRequest> validator)
    {
        _service = service;
        _validator = validator;
    }

    [HttpPost("generate")]
    public async Task<ActionResult<ApiResponse<GenerateQrResponse>>> Generate(
        [FromBody] GenerateQrRequest request, CancellationToken ct)
    {
        await _validator.ValidateAndThrowAsync(request, ct);
        return Success(await _service.GenerateAsync(request, ct));
    }
}
