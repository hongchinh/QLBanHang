using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using OrderMgmt.Application.Catalog.Customers.Interfaces;
using OrderMgmt.Application.Catalog.Customers.Models;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Enums;
using OrderMgmt.WebApi.Authorization;

namespace OrderMgmt.WebApi.Controllers;

[Route("api/suppliers")]
public class SuppliersController : ApiControllerBase
{
    private readonly ICustomerService _customers;
    private readonly IValidator<CreateCustomerRequest> _createValidator;
    private readonly IValidator<UpdateCustomerRequest> _updateValidator;
    private readonly IValidator<CustomerListRequest> _listValidator;

    public SuppliersController(
        ICustomerService customers,
        IValidator<CreateCustomerRequest> createValidator,
        IValidator<UpdateCustomerRequest> updateValidator,
        IValidator<CustomerListRequest> listValidator)
    {
        _customers = customers;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _listValidator = listValidator;
    }

    [HttpGet]
    [HasPermission(Permissions.Suppliers.View)]
    public async Task<ActionResult<ApiResponse<PagedResult<CustomerListItemDto>>>> List(
        [FromQuery] CustomerListRequest request, CancellationToken ct)
    {
        await _listValidator.ValidateAndThrowAsync(request, ct);
        var result = await _customers.ListAsync(request, PartnerRole.Supplier, ct);
        return Success(result);
    }

    [HttpGet("search")]
    [HasPermission(Permissions.Suppliers.View)]
    public async Task<ActionResult<ApiResponse<List<CustomerSearchItemDto>>>> Search(
        [FromQuery] string keyword = "",
        [FromQuery] bool activeOnly = true,
        [FromQuery] int limit = 20,
        CancellationToken ct = default)
    {
        var result = await _customers.SearchAsync(
            new CustomerSearchRequest { Keyword = keyword, ActiveOnly = activeOnly, Limit = limit }, PartnerType.Supplier, ct);
        return Success(result);
    }

    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.Suppliers.View)]
    public async Task<ActionResult<ApiResponse<CustomerDto>>> Get(Guid id, CancellationToken ct)
    {
        var result = await _customers.GetAsync(id, PartnerRole.Supplier, ct);
        return Success(result);
    }

    [HttpPost]
    [HasPermission(Permissions.Suppliers.Create)]
    public async Task<ActionResult<ApiResponse<CustomerDto>>> Create([FromBody] CreateCustomerRequest request, CancellationToken ct)
    {
        await _createValidator.ValidateAndThrowAsync(request, ct);
        var result = await _customers.CreateAsync(request, PartnerRole.Supplier, ct);
        return Success(result);
    }

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.Suppliers.Update)]
    public async Task<ActionResult<ApiResponse<CustomerDto>>> Update(Guid id, [FromBody] UpdateCustomerRequest request, CancellationToken ct)
    {
        await _updateValidator.ValidateAndThrowAsync(request, ct);
        var result = await _customers.UpdateAsync(id, request, PartnerRole.Supplier, ct);
        return Success(result);
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.Suppliers.Delete)]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id, CancellationToken ct)
    {
        await _customers.DeleteAsync(id, PartnerRole.Supplier, ct);
        return Success();
    }
}
