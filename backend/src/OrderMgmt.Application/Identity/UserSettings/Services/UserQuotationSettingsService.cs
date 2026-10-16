using OrderMgmt.Application.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Identity.UserSettings.Interfaces;
using OrderMgmt.Application.Identity.UserSettings.Models;
using OrderMgmt.Application.Sales.Quotations.Interfaces;
using OrderMgmt.Application.Sales.Quotations.Models;
using OrderMgmt.Domain.Common;
using OrderMgmt.Domain.Entities.Identity;

namespace OrderMgmt.Application.Identity.UserSettings.Services;

public class UserQuotationSettingsService : IUserQuotationSettingsService
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<UserQuotationSettingsService> _logger;
    private readonly IOptionsMonitor<TemplateUploadOptions> _uploadOptions;
    private readonly IDateTime _clock;
    private readonly IQuotationExportPathResolver _pathResolver;

    public UserQuotationSettingsService(
        IAppDbContext db,
        ICurrentUser currentUser,
        ILogger<UserQuotationSettingsService> logger,
        IOptionsMonitor<TemplateUploadOptions> uploadOptions,
        IDateTime clock,
        IQuotationExportPathResolver pathResolver)
    {
        _db = db;
        _currentUser = currentUser;
        _logger = logger;
        _uploadOptions = uploadOptions;
        _clock = clock;
        _pathResolver = pathResolver;
    }

    public async Task<UserQuotationSettingsDto> GetForCurrentUserAsync(CancellationToken ct = default)
    {
        var userId = _currentUser.UserId
            ?? throw new UnauthorizedAccessException("User not authenticated.");
        return await GetOrCreateDtoAsync(userId, ct);
    }

    public async Task<UserQuotationSettingsDto> GetForUserAsync(Guid userId, CancellationToken ct = default)
    {
        if (!await _db.Users.AnyAsync(u => u.Id == userId, ct))
            throw new NotFoundException(nameof(User), userId);
        return await GetOrCreateDtoAsync(userId, ct);
    }

    public async Task<UserQuotationSettingsDto> SetLockAtAsync(Guid userId, UpdateLockAtRequest request, CancellationToken ct = default)
    {
        if (!await _db.Users.AnyAsync(u => u.Id == userId, ct))
            throw new NotFoundException(nameof(User), userId);

        var settings = await EnsureSettingsAsync(userId, ct);
        var oldValue = settings.LockAtStatus;
        settings.LockAtStatus = request.LockAtStatus;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "User settings lock-at changed: TargetUserId={TargetUserId}, OldValue={Old}, NewValue={New}, ActorUserId={ActorUserId}",
            userId, oldValue, request.LockAtStatus, _currentUser.UserId);

        return await ToDtoAsync(settings, ct);
    }

    public async Task<UserQuotationSettingsDto> UploadTemplateAsync(UploadedFile file, CancellationToken ct = default)
    {
        var userId = _currentUser.UserId
            ?? throw new UnauthorizedAccessException("User not authenticated.");

        TemplateUploadValidator.Validate(file, _uploadOptions.CurrentValue);

        var dir = _pathResolver.GetUserTemplatesDirectory();
        var fileName = $"{userId}.xlsx";
        var finalPath = Path.Combine(dir, fileName);
        var tempPath = finalPath + ".tmp";

        using (var source = file.OpenReadStream())
        using (var dest = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await source.CopyToAsync(dest, ct);
        }
        if (File.Exists(finalPath)) File.Delete(finalPath);
        File.Move(tempPath, finalPath);

        var settings = await EnsureSettingsAsync(userId, ct);
        settings.TemplateFileName = fileName;
        settings.TemplateOriginalName = Path.GetFileName(file.FileName);
        settings.TemplateUploadedAt = _clock.UtcNow;
        await _db.SaveChangesAsync(ct);

        return await ToDtoAsync(settings, ct);
    }

    public async Task<UserQuotationSettingsDto> DeleteTemplateAsync(CancellationToken ct = default)
    {
        var userId = _currentUser.UserId
            ?? throw new UnauthorizedAccessException("User not authenticated.");

        var settings = await EnsureSettingsAsync(userId, ct);
        if (!string.IsNullOrWhiteSpace(settings.TemplateFileName))
        {
            var dir = _pathResolver.GetUserTemplatesDirectory();
            var filePath = Path.Combine(dir, settings.TemplateFileName);
            if (File.Exists(filePath)) File.Delete(filePath);
        }
        settings.TemplateFileName = null;
        settings.TemplateOriginalName = null;
        settings.TemplateUploadedAt = null;
        await _db.SaveChangesAsync(ct);

        return await ToDtoAsync(settings, ct);
    }

    public async Task<(Stream Stream, string FileName)?> GetCurrentUserTemplateStreamAsync(CancellationToken ct = default)
    {
        var userId = _currentUser.UserId
            ?? throw new UnauthorizedAccessException("User not authenticated.");

        var settings = await _db.UserQuotationSettings
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .Select(s => new { s.TemplateFileName, s.TemplateOriginalName })
            .FirstOrDefaultAsync(ct);
        if (settings is null || string.IsNullOrWhiteSpace(settings.TemplateFileName)) return null;

        var dir = _pathResolver.GetUserTemplatesDirectory();
        var path = Path.Combine(dir, settings.TemplateFileName);
        if (!File.Exists(path)) return null;

        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return (stream, settings.TemplateOriginalName ?? settings.TemplateFileName);
    }

    private async Task<UserQuotationSettingsDto> GetOrCreateDtoAsync(Guid userId, CancellationToken ct)
    {
        var settings = await EnsureSettingsAsync(userId, ct);
        return await ToDtoAsync(settings, ct);
    }

    private async Task<UserQuotationSettings> EnsureSettingsAsync(Guid userId, CancellationToken ct)
    {
        var existing = await _db.UserQuotationSettings
            .FirstOrDefaultAsync(s => s.UserId == userId, ct);
        if (existing is not null) return existing;

        var created = new UserQuotationSettings { UserId = userId };
        _db.UserQuotationSettings.Add(created);
        await _db.SaveChangesAsync(ct);
        return created;
    }

    private async Task<UserQuotationSettingsDto> ToDtoAsync(UserQuotationSettings settings, CancellationToken ct)
    {
        var fullName = await _db.Users.IgnoreQueryFilters([QueryFilters.SoftDelete])
            .AsNoTracking()
            .Where(u => u.Id == settings.UserId)
            .Select(u => u.FullName)
            .FirstOrDefaultAsync(ct);

        return new UserQuotationSettingsDto
        {
            UserId = settings.UserId,
            UserFullName = fullName,
            LockAtStatus = settings.LockAtStatus,
            TemplateFileName = settings.TemplateFileName,
            TemplateOriginalName = settings.TemplateOriginalName,
            TemplateUploadedAt = settings.TemplateUploadedAt,
            HandoverWithPriceTemplateFileName = settings.HandoverWithPriceTemplateFileName,
            HandoverWithPriceTemplateOriginalName = settings.HandoverWithPriceTemplateOriginalName,
            HandoverWithPriceTemplateUploadedAt = settings.HandoverWithPriceTemplateUploadedAt,
            HandoverNoPriceTemplateFileName = settings.HandoverNoPriceTemplateFileName,
            HandoverNoPriceTemplateOriginalName = settings.HandoverNoPriceTemplateOriginalName,
            HandoverNoPriceTemplateUploadedAt = settings.HandoverNoPriceTemplateUploadedAt,
        };
    }

    public async Task<UserQuotationSettingsDto> UploadHandoverTemplateAsync(
        UploadedFile file,
        QuotationTemplateType type,
        CancellationToken ct = default)
    {
        var userId = _currentUser.UserId
            ?? throw new UnauthorizedAccessException("User not authenticated.");

        TemplateUploadValidator.Validate(file, _uploadOptions.CurrentValue);

        var dir = _pathResolver.GetUserTemplatesDirectory();
        var (fileName, fieldUpdater) = type switch
        {
            QuotationTemplateType.HandoverWithPrice =>
                ($"{userId}_handover_with_price.xlsx",
                (Action<UserQuotationSettings, string, DateTimeOffset>)((s, fn, at) =>
                {
                    s.HandoverWithPriceTemplateFileName = fn;
                    s.HandoverWithPriceTemplateOriginalName = Path.GetFileName(file.FileName);
                    s.HandoverWithPriceTemplateUploadedAt = at;
                })),
            QuotationTemplateType.HandoverNoPrice =>
                ($"{userId}_handover_no_price.xlsx",
                (Action<UserQuotationSettings, string, DateTimeOffset>)((s, fn, at) =>
                {
                    s.HandoverNoPriceTemplateFileName = fn;
                    s.HandoverNoPriceTemplateOriginalName = Path.GetFileName(file.FileName);
                    s.HandoverNoPriceTemplateUploadedAt = at;
                })),
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };

        var finalPath = Path.Combine(dir, fileName);
        var tempPath = finalPath + ".tmp";

        using (var source = file.OpenReadStream())
        using (var dest = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await source.CopyToAsync(dest, ct);
        }
        if (File.Exists(finalPath)) File.Delete(finalPath);
        File.Move(tempPath, finalPath);

        var settings = await EnsureSettingsAsync(userId, ct);
        fieldUpdater(settings, fileName, _clock.UtcNow);
        await _db.SaveChangesAsync(ct);

        return await ToDtoAsync(settings, ct);
    }

    public async Task<UserQuotationSettingsDto> DeleteHandoverTemplateAsync(
        QuotationTemplateType type,
        CancellationToken ct = default)
    {
        var userId = _currentUser.UserId
            ?? throw new UnauthorizedAccessException("User not authenticated.");

        var settings = await EnsureSettingsAsync(userId, ct);
        var dir = _pathResolver.GetUserTemplatesDirectory();

        var (fileName, fieldClearer) = type switch
        {
            QuotationTemplateType.HandoverWithPrice =>
                (settings.HandoverWithPriceTemplateFileName,
                (Action<UserQuotationSettings>)(s =>
                {
                    s.HandoverWithPriceTemplateFileName = null;
                    s.HandoverWithPriceTemplateOriginalName = null;
                    s.HandoverWithPriceTemplateUploadedAt = null;
                })),
            QuotationTemplateType.HandoverNoPrice =>
                (settings.HandoverNoPriceTemplateFileName,
                (Action<UserQuotationSettings>)(s =>
                {
                    s.HandoverNoPriceTemplateFileName = null;
                    s.HandoverNoPriceTemplateOriginalName = null;
                    s.HandoverNoPriceTemplateUploadedAt = null;
                })),
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };

        if (!string.IsNullOrWhiteSpace(fileName))
        {
            var filePath = Path.Combine(dir, fileName);
            if (File.Exists(filePath)) File.Delete(filePath);
        }
        fieldClearer(settings);
        await _db.SaveChangesAsync(ct);

        return await ToDtoAsync(settings, ct);
    }

    public async Task<(Stream Stream, string FileName)?> GetCurrentUserHandoverTemplateStreamAsync(
        QuotationTemplateType type,
        CancellationToken ct = default)
    {
        var userId = _currentUser.UserId
            ?? throw new UnauthorizedAccessException("User not authenticated.");

        var settings = await _db.UserQuotationSettings
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .Select(s => new
            {
                s.HandoverWithPriceTemplateFileName,
                s.HandoverWithPriceTemplateOriginalName,
                s.HandoverNoPriceTemplateFileName,
                s.HandoverNoPriceTemplateOriginalName,
            })
            .FirstOrDefaultAsync(ct);

        var (fileName, originalName) = type switch
        {
            QuotationTemplateType.HandoverWithPrice =>
                (settings?.HandoverWithPriceTemplateFileName,
                 settings?.HandoverWithPriceTemplateOriginalName),
            QuotationTemplateType.HandoverNoPrice =>
                (settings?.HandoverNoPriceTemplateFileName,
                 settings?.HandoverNoPriceTemplateOriginalName),
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };

        if (string.IsNullOrWhiteSpace(fileName)) return null;

        var dir = _pathResolver.GetUserTemplatesDirectory();
        var path = Path.Combine(dir, fileName);
        if (!File.Exists(path)) return null;

        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return (stream, originalName ?? fileName);
    }
}
