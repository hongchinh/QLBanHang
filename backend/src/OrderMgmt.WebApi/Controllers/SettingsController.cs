using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderMgmt.Application.Branding.Interfaces;
using OrderMgmt.Application.Branding.Models;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.WebApi.Authorization;

namespace OrderMgmt.WebApi.Controllers;

[Route("api/settings")]
public class SettingsController : ApiControllerBase
{
    private readonly IBrandingService _branding;

    public SettingsController(IBrandingService branding)
    {
        _branding = branding;
    }

    [HttpGet("branding")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<BrandingDto>>> GetBranding(CancellationToken ct)
        => Success(await _branding.GetMetaAsync(ct));

    // Logo is fetched as <img src> directly by the browser; no Bearer header attached.
    // Branding assets are equivalent to a favicon — public-facing visual identity, not sensitive data.
    [HttpGet("branding/logo")]
    [AllowAnonymous]
    public async Task<IActionResult> GetLogo([FromQuery] string variant, CancellationToken ct)
    {
        if (variant != "full" && variant != "mark")
            return BadRequest();

        var result = await _branding.GetLogoAsync(variant, ct);
        if (result is null) return NotFound();

        var ifNoneMatch = Request.Headers.IfNoneMatch.ToString();
        if (!string.IsNullOrEmpty(ifNoneMatch) && ifNoneMatch == result.ETag)
            return StatusCode(StatusCodes.Status304NotModified);

        Response.Headers.ETag = result.ETag;
        Response.Headers.CacheControl = "private, max-age=300";
        return File(result.Content, result.ContentType);
    }

    [HttpGet("branding/icon/{size:int}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPwaIcon(int size, CancellationToken ct)
    {
        // 180 is the iOS apple-touch-icon; 192 and 512 are the manifest icons.
        if (size is not (180 or 192 or 512))
            return BadRequest();

        var result = await _branding.GetPwaIconAsync(size, ct);

        var ifNoneMatch = Request.Headers.IfNoneMatch.ToString();
        if (!string.IsNullOrEmpty(ifNoneMatch) && ifNoneMatch == result.ETag)
            return StatusCode(StatusCodes.Status304NotModified);

        Response.Headers.ETag = result.ETag;
        Response.Headers.CacheControl = "public, max-age=3600";
        return File(result.Content, result.ContentType);
    }

    [HttpPut("branding")]
    [HasPermission(Permissions.UserSettings.Manage)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<BrandingDto>>> UpdateBranding(
        IFormFile? logoFull,
        IFormFile? logoMark,
        CancellationToken ct)
    {
        var fullUpload = logoFull is null ? null : new LogoUpload(
            logoFull.FileName, logoFull.ContentType, logoFull.Length, () => logoFull.OpenReadStream());
        var markUpload = logoMark is null ? null : new LogoUpload(
            logoMark.FileName, logoMark.ContentType, logoMark.Length, () => logoMark.OpenReadStream());

        var dto = await _branding.UpdateAsync(fullUpload, markUpload, ct);
        return Success(dto);
    }
}
