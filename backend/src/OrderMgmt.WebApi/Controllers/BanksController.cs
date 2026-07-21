using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Application.Payments.Interfaces;
using OrderMgmt.Application.Payments.Models;

namespace OrderMgmt.WebApi.Controllers;

[Authorize]
[Route("api/banks")]
public class BanksController : ApiControllerBase
{
    private readonly IBankLookupService _banks;

    public BanksController(IBankLookupService banks) => _banks = banks;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<BankDto>>>> List(CancellationToken ct)
        => Success(await _banks.ListAsync(ct));
}
