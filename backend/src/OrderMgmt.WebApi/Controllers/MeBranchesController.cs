using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Application.Organization.Branches.Interfaces;
using OrderMgmt.Application.Organization.Branches.Models;

namespace OrderMgmt.WebApi.Controllers;

[Authorize]
[Route("api/me/branches")]
public class MeBranchesController : ApiControllerBase
{
    private readonly IBranchService _service;

    public MeBranchesController(IBranchService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<MyBranchesDto>>> Get(CancellationToken ct)
        => Success(await _service.GetMyBranchesAsync(ct));
}
