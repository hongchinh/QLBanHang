using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderMgmt.Application.CashDebt.Debt.Interfaces;
using OrderMgmt.Application.CashDebt.Debt.Models;
using OrderMgmt.Application.Common.Models;

namespace OrderMgmt.WebApi.Controllers;

/// Permissions (debt.partner_summary or reports.debt) are checked by the service.
[Route("api/debt")]
[Authorize]
public class DebtController : ApiControllerBase
{
    private readonly IDebtService _service;

    public DebtController(IDebtService service)
    {
        _service = service;
    }

    [HttpGet("partner-summary")]
    public async Task<ActionResult<ApiResponse<PartnerDebtSummaryDto>>> PartnerSummary(
        [FromQuery] Guid partnerId, CancellationToken ct)
        => Success(await _service.GetPartnerSummaryAsync(partnerId, ct));
}
