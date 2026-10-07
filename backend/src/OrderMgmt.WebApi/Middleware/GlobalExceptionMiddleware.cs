using System.Net;
using System.Text.Json;
using FluentValidation;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Domain.Common;

namespace OrderMgmt.WebApi.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger, IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleAsync(context, ex);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception ex)
    {
        if (context.Response.HasStarted)
        {
            _logger.LogError(ex, "Response already started; cannot translate exception to API response.");
            throw ex;
        }

        (int statusCode, ApiError error) result = ex switch
        {
            ValidationException ve => (StatusCodes.Status400BadRequest, new ApiError
            {
                Code = "VALIDATION",
                Message = "Dữ liệu không hợp lệ.",
                Details = ve.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()),
            }),
            ValidationDomainException vde => (StatusCodes.Status400BadRequest, new ApiError
            {
                Code = vde.Code,
                Message = vde.Message,
                Details = vde.Errors,
            }),
            NotFoundException nfe => (StatusCodes.Status404NotFound, new ApiError
            {
                Code = nfe.Code,
                Message = nfe.Message,
            }),
            ConflictException ce => (StatusCodes.Status409Conflict, new ApiError
            {
                Code = ce.Code,
                Message = ce.Message,
            }),
            ForbiddenException fe => (StatusCodes.Status403Forbidden, new ApiError
            {
                Code = fe.Code,
                Message = fe.Message,
            }),
            AuthenticationException ae => (StatusCodes.Status401Unauthorized, new ApiError
            {
                Code = ae.Code,
                Message = ae.Message,
            }),
            NegativeStockException nse => (StatusCodes.Status422UnprocessableEntity, new ApiError
            {
                Code = nse.Code,
                Message = nse.Message,
                Details = nse.Shortages,
            }),
            DomainException de => (StatusCodes.Status400BadRequest, new ApiError
            {
                Code = de.Code,
                Message = de.Message,
            }),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, new ApiError
            {
                Code = "UNAUTHORIZED",
                Message = "Bạn cần đăng nhập.",
            }),
            _ => (StatusCodes.Status500InternalServerError, new ApiError
            {
                Code = "INTERNAL_ERROR",
                Message = _env.IsDevelopment() ? ex.Message : "Đã xảy ra lỗi không mong muốn.",
            }),
        };

        // 5xx are bugs/dependency failures — log as Error. 4xx are expected client errors — log as Information.
        if (result.statusCode >= StatusCodes.Status500InternalServerError)
            _logger.LogError(ex, "Unhandled exception → {StatusCode}", result.statusCode);
        else
            _logger.LogInformation("Client error → {StatusCode} {Code}: {Message}",
                result.statusCode, result.error.Code, result.error.Message);

        context.Response.Clear();
        context.Response.StatusCode = result.statusCode;
        context.Response.ContentType = "application/json";

        var payload = JsonSerializer.Serialize(ApiResponse.Fail(result.error), new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });
        await context.Response.WriteAsync(payload);
    }
}
