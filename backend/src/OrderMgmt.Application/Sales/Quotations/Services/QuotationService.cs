using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Reports.Common;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Application.Common.Options;
using OrderMgmt.Application.Notifications.Interfaces;
using OrderMgmt.Application.Sales.Quotations.Helpers;
using OrderMgmt.Application.Sales.Quotations.Interfaces;
using OrderMgmt.Application.Sales.Quotations.Models;
using OrderMgmt.Domain.Common;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Entities.Catalog;
using OrderMgmt.Domain.Entities.Sales;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Sales.Quotations.Services;

public class QuotationService : IQuotationService
{
    private const string CodePrefix = "BG";
    private const int MaxCreateAttempts = 5;
    private const string DefaultTransportVehicleNumber = "Xe khác";

    // (current, action) -> next status
    private static readonly Dictionary<(QuotationStatus, QuotationAction), QuotationStatus> Transitions = new()
    {
        { (QuotationStatus.Draft, QuotationAction.Send), QuotationStatus.Sent },
        { (QuotationStatus.Draft, QuotationAction.Cancel), QuotationStatus.Cancelled },
        { (QuotationStatus.Sent, QuotationAction.Confirm), QuotationStatus.Confirmed },
        { (QuotationStatus.Sent, QuotationAction.Cancel), QuotationStatus.Cancelled },
        { (QuotationStatus.Confirmed, QuotationAction.Cancel), QuotationStatus.Cancelled },
        { (QuotationStatus.Confirmed, QuotationAction.AccountingConfirm), QuotationStatus.AccountingConfirmed },
        { (QuotationStatus.AccountingConfirmed, QuotationAction.Cancel), QuotationStatus.Cancelled },
    };

    private readonly IAppDbContext _db;
    private readonly IDateTime _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IQuotationExcelRenderer _excelRenderer;
    private readonly IQuotationSpreadsheetPdfConverter _pdfConverter;
    private readonly IOptionsMonitor<FeatureOptions> _features;
    private readonly IQuotationExportPathResolver _templatePathResolver;
    private readonly IHandoverExcelRenderer _handoverRenderer;
    private readonly INotificationService _notifications;

    public QuotationService(
        IAppDbContext db,
        IDateTime clock,
        ICurrentUser currentUser,
        IQuotationExcelRenderer excelRenderer,
        IQuotationSpreadsheetPdfConverter pdfConverter,
        IOptionsMonitor<FeatureOptions> features,
        IQuotationExportPathResolver templatePathResolver,
        IHandoverExcelRenderer handoverRenderer,
        INotificationService notifications)
    {
        _db = db;
        _clock = clock;
        _currentUser = currentUser;
        _excelRenderer = excelRenderer;
        _pdfConverter = pdfConverter;
        _features = features;
        _templatePathResolver = templatePathResolver;
        _handoverRenderer = handoverRenderer;
        _notifications = notifications;
    }

    private IQueryable<Quotation> ApplyOwnerScope(IQueryable<Quotation> query)
    {
        if (!_features.CurrentValue.QuotationOwnerScope) return query;
        if (_currentUser.HasPermission(Permissions.Quotations.ViewAll)) return query;
        var uid = _currentUser.UserId ?? Guid.Empty;
        return query.Where(x => x.OwnerUserId == uid);
    }

    private bool CanViewCost() => _currentUser.HasPermission(Permissions.Quotations.ViewCost);

    private QuotationActivity AddActivity(
        Quotation quotation,
        QuotationActivityAction action,
        string description,
        object? metadata = null)
    {
        var activity = new QuotationActivity
        {
            QuotationId = quotation.Id,
            Action = action,
            ActorUserId = _currentUser.UserId,
            OccurredAt = _clock.UtcNow,
            Description = description,
            MetadataJson = metadata is null ? null : JsonSerializer.Serialize(metadata),
        };
        _db.QuotationActivities.Add(activity);
        return activity;
    }

    private void EnsureCanAccess(Quotation quotation)
    {
        if (!_features.CurrentValue.QuotationOwnerScope) return;
        if (_currentUser.HasPermission(Permissions.Quotations.ViewAll)) return;
        if (quotation.OwnerUserId != _currentUser.UserId)
            throw new ForbiddenException("Bạn không có quyền truy cập báo giá này.");
    }

    private async Task EnsureCanModifyAsync(Quotation q, CancellationToken ct)
    {
        if (q.Status == QuotationStatus.Cancelled)
            throw new DomainException("CONFLICT", "Báo giá đã hủy không thể chỉnh sửa.");

        var isOwnerDeleted = await _db.Users.IgnoreQueryFilters()
            .Where(u => u.Id == q.OwnerUserId)
            .Select(u => (bool?)u.IsDeleted)
            .FirstOrDefaultAsync(ct) ?? false;
        if (isOwnerDeleted)
            throw new DomainException("CONFLICT", "Báo giá có chủ sở hữu đã ngừng hoạt động — chỉ có thể clone.");

        if (_currentUser.HasPermission(Permissions.Quotations.BypassLock)) return;

        var settings = await _db.UserQuotationSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == _currentUser.UserId, ct);
        if (settings?.LockAtStatus is { } threshold && CompareStatus(q.Status, threshold) >= 0)
            throw new DomainException("CONFLICT",
                $"Báo giá ở trạng thái '{q.Status}' đã bị khoá theo cấu hình của bạn.");
    }

    private void ApplyStatusTimestamps(Quotation q, QuotationStatus newStatus)
    {
        var nowUtc = _clock.UtcNow.UtcDateTime;
        if (newStatus == QuotationStatus.Confirmed && q.ConfirmedAt == null)
        {
            q.ConfirmedAt = nowUtc;
            q.ConfirmedByUserId = _currentUser.UserId;
        }
        if (newStatus == QuotationStatus.Cancelled && q.CancelledAt == null)
        {
            q.CancelledAt = nowUtc;
        }
        if (newStatus == QuotationStatus.AccountingConfirmed && q.AccountingConfirmedAt == null)
        {
            q.AccountingConfirmedAt = nowUtc;
            q.AccountingConfirmedByUserId = _currentUser.UserId;
        }
    }

    private static int CompareStatus(QuotationStatus a, QuotationStatus b)
    {
        // Order: Draft(0) < Sent(1) < Confirmed(2) < AccountingConfirmed(3). Cancelled handled separately.
        static int Rank(QuotationStatus s) => s switch
        {
            QuotationStatus.Draft => 0,
            QuotationStatus.Sent => 1,
            QuotationStatus.Confirmed => 2,
            QuotationStatus.AccountingConfirmed => 3,
            _ => -1,
        };
        return Rank(a).CompareTo(Rank(b));
    }

    public async Task<QuotationListResult> ListAsync(QuotationListRequest request, CancellationToken ct = default)
    {
        var canViewCost = CanViewCost();
        var dateMode = await RevenueFilterHelper.GetDateModeAsync(_db, ct);
        var query = ApplyOwnerScope(_db.Quotations
            .AsNoTracking()
            .Where(q => !q.IsDeleted));

        var statuses = QuotationStatusListParser.Parse(request.Status);
        if (statuses.Count > 0)
            query = query.Where(q => statuses.Contains(q.Status));
        if (request.CustomerId.HasValue)
            query = query.Where(q => q.CustomerId == request.CustomerId.Value);
        if (request.From.HasValue)
            query = query.Where(q => q.QuotationDate >= request.From.Value);
        if (request.To.HasValue)
            query = query.Where(q => q.QuotationDate <= request.To.Value);

        // Honor only for view_all callers; for non-privileged callers ApplyOwnerScope has already
        // restricted to self, and stacking this filter would yield an empty list on forged URLs.
        if (_currentUser.HasPermission(Permissions.Quotations.ViewAll)
            && !string.IsNullOrWhiteSpace(request.OwnerUserIds))
        {
            var ownerIds = OwnerIdListParser.Parse(request.OwnerUserIds);
            if (ownerIds.Count > 0)
                query = query.Where(q => ownerIds.Contains(q.OwnerUserId));
        }

        query = RevenueFilterHelper.ApplyRevenueDateRangeFilter(
            query, dateMode, request.RevenueDateFrom, request.RevenueDateTo);

        if (request.DeliveryDateFrom.HasValue)
            query = query.Where(q => q.DeliveryDate >= request.DeliveryDateFrom.Value);
        if (request.DeliveryDateTo.HasValue)
            query = query.Where(q => q.DeliveryDate <= request.DeliveryDateTo.Value);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var pattern = $"%{EscapeLike(request.Search.Trim())}%";
            query = query.Where(q =>
                EF.Functions.ILike(q.Code, pattern)
                || EF.Functions.ILike(q.CustomerName, pattern));
        }

        // Loại trừ Cancelled khi user không filter status explicit (financial convention).
        var aggregateQuery = statuses.Count == 0
            ? query.Where(q => q.Status != QuotationStatus.Cancelled)
            : query;

        var aggregates = await aggregateQuery
            .GroupBy(_ => 1)
            .Select(g => new QuotationListAggregates
            {
                Subtotal = g.Sum(q => q.Subtotal),
                Discount = g.Sum(q => q.Discount),
                Freight  = g.Sum(q => q.Freight),
                Total    = g.Sum(q => q.Total),
                AdvancePayment = g.Sum(q => q.AdvancePayment),
                TotalCost = canViewCost ? g.Sum(q => q.TotalCost) : null,
                GrossProfit = canViewCost ? g.Sum(q => q.GrossProfit) : null,
            })
            .FirstOrDefaultAsync(ct) ?? new QuotationListAggregates();

        query = (request.SortBy?.ToLowerInvariant(), request.SortDirection?.ToLowerInvariant()) switch
        {
            ("code", "desc") => query.OrderByDescending(q => q.Code),
            ("code", _) => query.OrderBy(q => q.Code),
            ("date", "desc") => query.OrderByDescending(q => q.QuotationDate),
            ("date", _) => query.OrderBy(q => q.QuotationDate),
            ("total", "desc") => query.OrderByDescending(q => q.Total),
            ("total", _) => query.OrderBy(q => q.Total),
            _ => query.OrderByDescending(q => q.CreatedAt),
        };

        var totalItems = await query.CountAsync(ct);

        var items = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(q => new QuotationListItemDto
            {
                Id = q.Id,
                Code = q.Code,
                QuotationDate = q.QuotationDate,
                CustomerName = q.CustomerName,
                ContactPhone = q.ContactPhone,
                Subtotal = q.Subtotal,
                Discount = q.Discount,
                Freight = q.Freight,
                TaxRate = q.TaxRate,
                TaxAmount = q.TaxAmount,
                Total = q.Total,
                AdvancePayment = q.AdvancePayment,
                TotalCost = canViewCost ? q.TotalCost : null,
                GrossProfit = canViewCost ? q.GrossProfit : null,
                Status = q.Status,
                ConfirmedAt = q.ConfirmedAt,
                AccountingConfirmedAt = q.AccountingConfirmedAt,
                OwnerUserId = q.OwnerUserId,
                OwnerFullName = _db.Users.IgnoreQueryFilters()
                    .Where(u => u.Id == q.OwnerUserId)
                    .Select(u => u.FullName)
                    .FirstOrDefault(),
                IsOwnerDeleted = _db.Users.IgnoreQueryFilters()
                    .Where(u => u.Id == q.OwnerUserId)
                    .Select(u => (bool?)u.IsDeleted)
                    .FirstOrDefault() ?? false,
                CanClone = true,
                CreatedByName = q.CreatedBy.HasValue
                    ? _db.Users.IgnoreQueryFilters()
                        .Where(u => u.Id == q.CreatedBy)
                        .Select(u => u.FullName)
                        .FirstOrDefault()
                    : null,
                CreatedAt = q.CreatedAt,
                DeliveryDate = q.DeliveryDate,
            })
            .ToListAsync(ct);

        foreach (var item in items)
        {
            item.RevenueDate = dateMode switch
            {
                RevenueDateField.ConfirmedAt => item.ConfirmedAt.HasValue
                    ? DateOnly.FromDateTime(item.ConfirmedAt.Value)
                    : null,
                RevenueDateField.AccountingConfirmedAt => item.AccountingConfirmedAt.HasValue
                    ? DateOnly.FromDateTime(item.AccountingConfirmedAt.Value)
                    : null,
                _ => item.QuotationDate,
            };
        }

        return new QuotationListResult
        {
            Items = items,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalItems = totalItems,
            Aggregates = aggregates,
        };
    }

    public async Task<IReadOnlyList<QuotationOwnerOptionDto>> ListOwnersAsync(bool includeDeleted, CancellationToken ct = default)
    {
        var ownerStats = await _db.Quotations
            .AsNoTracking()
            .Where(q => !q.IsDeleted)
            .GroupBy(q => q.OwnerUserId)
            .Select(g => new { OwnerUserId = g.Key, QuotationCount = g.Count() })
            .ToListAsync(ct);

        if (ownerStats.Count == 0) return Array.Empty<QuotationOwnerOptionDto>();

        var ownerIds = ownerStats.Select(s => s.OwnerUserId).ToList();
        var usersQuery = _db.Users.IgnoreQueryFilters()
            .AsNoTracking()
            .Where(u => ownerIds.Contains(u.Id));
        if (!includeDeleted)
            usersQuery = usersQuery.Where(u => !u.IsDeleted);

        var users = await usersQuery
            .Select(u => new { u.Id, u.FullName, u.IsDeleted })
            .ToListAsync(ct);

        var vietnameseComparer = StringComparer.Create(new System.Globalization.CultureInfo("vi-VN"), ignoreCase: true);
        return users
            .Join(ownerStats, u => u.Id, s => s.OwnerUserId, (u, s) => new QuotationOwnerOptionDto
            {
                Id = u.Id,
                FullName = u.FullName,
                IsDeleted = u.IsDeleted,
                QuotationCount = s.QuotationCount,
            })
            .OrderBy(o => o.IsDeleted)
            .ThenBy(o => o.FullName, vietnameseComparer)
            .ToList();
    }

    public async Task<QuotationDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var quotation = await _db.Quotations
            .AsNoTracking()
            .Include(q => q.Lines.OrderBy(l => l.SortOrder))
                .ThenInclude(l => l.Product)
                    .ThenInclude(p => p!.ProductGroup)
            .FirstOrDefaultAsync(q => q.Id == id && !q.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Quotation), id);

        EnsureCanAccess(quotation);

        var owner = await _db.Users.IgnoreQueryFilters()
            .AsNoTracking()
            .Where(u => u.Id == quotation.OwnerUserId)
            .Select(u => new { u.FullName, u.IsDeleted })
            .FirstOrDefaultAsync(ct);

        string? confirmedByName = null;
        if (quotation.ConfirmedByUserId.HasValue)
        {
            confirmedByName = await _db.Users.IgnoreQueryFilters()
                .AsNoTracking()
                .Where(u => u.Id == quotation.ConfirmedByUserId.Value)
                .Select(u => u.FullName)
                .FirstOrDefaultAsync(ct);
        }

        string? accountingConfirmedByName = null;
        if (quotation.AccountingConfirmedByUserId.HasValue)
        {
            accountingConfirmedByName = await _db.Users.IgnoreQueryFilters()
                .AsNoTracking()
                .Where(u => u.Id == quotation.AccountingConfirmedByUserId.Value)
                .Select(u => u.FullName)
                .FirstOrDefaultAsync(ct);
        }

        var canEdit = await ComputeCanEditAsync(quotation, owner?.IsDeleted ?? false, ct);
        return MapToDto(quotation, owner?.FullName, owner?.IsDeleted ?? false, canEdit, confirmedByName, CanViewCost(), accountingConfirmedByName);
    }

    public async Task<IReadOnlyList<QuotationActivityDto>> ListActivitiesAsync(Guid id, CancellationToken ct = default)
    {
        var quotation = await _db.Quotations
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == id && !q.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Quotation), id);

        EnsureCanAccess(quotation);

        var activities = await _db.QuotationActivities
            .AsNoTracking()
            .Where(a => a.QuotationId == id && !a.IsDeleted)
            .OrderByDescending(a => a.OccurredAt)
            .Select(a => new
            {
                a.Id,
                a.QuotationId,
                a.Action,
                a.ActorUserId,
                a.OccurredAt,
                a.Description,
                a.MetadataJson,
            })
            .ToListAsync(ct);

        var actorIds = activities
            .Where(a => a.ActorUserId.HasValue)
            .Select(a => a.ActorUserId!.Value)
            .Distinct()
            .ToList();

        var actors = actorIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _db.Users.IgnoreQueryFilters()
                .AsNoTracking()
                .Where(u => actorIds.Contains(u.Id))
                .Select(u => new { u.Id, u.FullName })
                .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

        return activities.Select(a => new QuotationActivityDto
        {
            Id = a.Id,
            QuotationId = a.QuotationId,
            Action = a.Action,
            ActorUserId = a.ActorUserId,
            ActorName = a.ActorUserId.HasValue
                ? actors.GetValueOrDefault(a.ActorUserId.Value) ?? "Người dùng không xác định"
                : "Hệ thống",
            OccurredAt = a.OccurredAt,
            Description = a.Description,
            MetadataJson = a.MetadataJson,
        }).ToList();
    }

    private async Task<bool> ComputeCanEditAsync(Quotation q, bool isOwnerDeleted, CancellationToken ct)
    {
        if (q.Status == QuotationStatus.Cancelled) return false;
        if (isOwnerDeleted) return false;
        if (_currentUser.HasPermission(Permissions.Quotations.BypassLock)) return true;

        var lockAt = await _db.UserQuotationSettings
            .AsNoTracking()
            .Where(s => s.UserId == _currentUser.UserId)
            .Select(s => s.LockAtStatus)
            .FirstOrDefaultAsync(ct);
        if (lockAt is { } threshold && CompareStatus(q.Status, threshold) >= 0) return false;
        return true;
    }

    public async Task<QuotationDto> CreateAsync(UpsertQuotationRequest request, CancellationToken ct = default)
    {
        var customer = await EnsureCustomerAsync(request.CustomerId, ct);

        for (var attempt = 1; attempt <= MaxCreateAttempts; attempt++)
        {
            var code = await GenerateCodeAsync(ct);

            if (await _db.Quotations.AnyAsync(q => q.Code == code, ct))
                continue;

            var quotation = new Quotation
            {
                Code = code,
                QuotationDate = request.QuotationDate,
                OwnerUserId = _currentUser.UserId
                    ?? throw new UnauthorizedAccessException("User not authenticated."),
                CustomerId = customer.Id,
                CustomerName = string.IsNullOrWhiteSpace(request.CustomerName)
                    ? customer.Name
                    : request.CustomerName.Trim(),
                CustomerTaxCode = customer.TaxCode,
                CustomerAddress = customer.CompanyAddress,
                ContactPerson = customer.ContactPerson,
                ContactPhone = customer.PhoneNumber,
                DeliveryAddress = request.DeliveryAddress ?? customer.DefaultShippingAddress,
                DeliveryRecipient = request.DeliveryRecipient,
                DeliveryPhone = request.DeliveryPhone,
                TransportVehicleNumber = NormalizeTransportVehicleNumber(request.TransportVehicleNumber),
                DeliveryDate = request.DeliveryDate,
                DeliveryNote = request.DeliveryNote,
                TaxRate = request.TaxRate,
                Discount = request.Discount,
                Freight = request.Freight,
                AdvancePayment = request.AdvancePayment,
                InternalNote = request.InternalNote,
                Status = QuotationStatus.Draft,
            };

            await PopulateLinesAsync(quotation, request.Lines, isNew: true, ct);
            Recompute(quotation);
            _db.Quotations.Add(quotation);
            var activity = AddActivity(quotation, QuotationActivityAction.Created, "Tạo báo giá");

            try
            {
                await _db.SaveChangesAsync(ct);
                return await GetAsync(quotation.Id, ct);
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex) && attempt < MaxCreateAttempts)
            {
                _db.Entry(quotation).State = EntityState.Detached;
                _db.Entry(activity).State = EntityState.Detached;
                foreach (var line in quotation.Lines)
                    _db.Entry(line).State = EntityState.Detached;
            }
        }

        throw new ConflictException("Không thể tạo mã báo giá tự động sau nhiều lần thử. Vui lòng thử lại.");
    }

    public async Task<QuotationDto> UpdateAsync(Guid id, UpsertQuotationRequest request, CancellationToken ct = default)
    {
        var quotation = await _db.Quotations
            .Include(q => q.Lines)
            .FirstOrDefaultAsync(q => q.Id == id && !q.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Quotation), id);

        EnsureCanAccess(quotation);
        await EnsureCanModifyAsync(quotation, ct);

        var customer = await EnsureCustomerAsync(request.CustomerId, ct);

        quotation.QuotationDate = request.QuotationDate;
        quotation.CustomerId = customer.Id;
        quotation.CustomerName = string.IsNullOrWhiteSpace(request.CustomerName)
            ? customer.Name
            : request.CustomerName.Trim();
        quotation.CustomerTaxCode = customer.TaxCode;
        quotation.CustomerAddress = customer.CompanyAddress;
        quotation.ContactPerson = customer.ContactPerson;
        quotation.ContactPhone = customer.PhoneNumber;
        quotation.DeliveryAddress = request.DeliveryAddress;
        quotation.DeliveryRecipient = request.DeliveryRecipient;
        quotation.DeliveryPhone = request.DeliveryPhone;
        quotation.TransportVehicleNumber = NormalizeTransportVehicleNumber(request.TransportVehicleNumber);
        quotation.DeliveryDate = request.DeliveryDate;
        quotation.DeliveryNote = request.DeliveryNote;
        quotation.TaxRate = request.TaxRate;
        quotation.Discount = request.Discount;
        quotation.Freight = request.Freight;
        quotation.AdvancePayment = request.AdvancePayment;
        quotation.InternalNote = request.InternalNote;

        await PopulateLinesAsync(quotation, request.Lines, isNew: false, ct);
        Recompute(quotation);
        AddActivity(quotation, QuotationActivityAction.Updated, "Cập nhật báo giá");

        await _db.SaveChangesAsync(ct);
        return await GetAsync(quotation.Id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var quotation = await _db.Quotations
            .Include(q => q.Lines)
            .FirstOrDefaultAsync(q => q.Id == id && !q.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Quotation), id);

        EnsureCanAccess(quotation);
        // Delete must respect the same lock-at and orphan-owner rules as Update. Without
        // this check a user could bypass their own lock-at threshold by deleting instead
        // of editing — a privilege escalation route. Cancelled quotations are also blocked,
        // matching the "cancelled = read-only" rule; use a dedicated admin purge if cleanup
        // of cancelled rows is needed.
        await EnsureCanModifyAsync(quotation, ct);

        quotation.IsDeleted = true;
        quotation.DeletedAt = _clock.UtcNow;
        quotation.DeletedBy = _currentUser.UserId;

        await _db.SaveChangesAsync(ct);
    }

    public async Task<(byte[] Excel, string FileName)> RenderExcelAsync(Guid id, CancellationToken ct = default)
    {
        var dto = await GetAsync(id, ct);
        var templatePath = await _templatePathResolver.ResolveTemplatePathAsync(_currentUser.UserId!.Value, ct);
        var bytes = await _excelRenderer.RenderAsync(dto, templatePath, ct);
        return (bytes, $"BaoGia_{dto.Code}.xlsx");
    }

    public async Task<(byte[] Pdf, string FileName)> RenderPdfAsync(Guid id, CancellationToken ct = default)
    {
        var dto = await GetAsync(id, ct);
        var templatePath = await _templatePathResolver.ResolveTemplatePathAsync(_currentUser.UserId!.Value, ct);
        var excelBytes = await _excelRenderer.RenderAsync(dto, templatePath, ct);
        var pdfBytes = await _pdfConverter.ConvertAsync(excelBytes, ct);
        return (pdfBytes, $"BaoGia_{dto.Code}.pdf");
    }

    public async Task<(byte[] Excel, string FileName)> RenderHandoverExcelAsync(
        Guid id, bool withPrice, CancellationToken ct = default)
    {
        var dto = await GetAsync(id, ct);
        var type = withPrice
            ? QuotationTemplateType.HandoverWithPrice
            : QuotationTemplateType.HandoverNoPrice;
        var templatePath = await _templatePathResolver.ResolveHandoverTemplatePathAsync(
            _currentUser.UserId!.Value, type, ct);
        var bytes = await _handoverRenderer.RenderAsync(dto, templatePath, withPrice, ct);
        return (bytes, $"BBBG_{dto.Code}.xlsx");
    }

    public async Task<(byte[] Pdf, string FileName)> RenderHandoverPdfAsync(
        Guid id, bool withPrice, CancellationToken ct = default)
    {
        var dto = await GetAsync(id, ct);
        var type = withPrice
            ? QuotationTemplateType.HandoverWithPrice
            : QuotationTemplateType.HandoverNoPrice;
        var templatePath = await _templatePathResolver.ResolveHandoverTemplatePathAsync(
            _currentUser.UserId!.Value, type, ct);
        var excelBytes = await _handoverRenderer.RenderAsync(dto, templatePath, withPrice, ct);
        var pdfBytes = await _pdfConverter.ConvertAsync(excelBytes, ct);
        return (pdfBytes, $"BBBG_{dto.Code}.pdf");
    }

    public async Task<QuotationDto> TransitionAsync(Guid id, QuotationAction action, CancellationToken ct = default)
    {
        var quotation = await _db.Quotations
            .FirstOrDefaultAsync(q => q.Id == id && !q.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Quotation), id);

        EnsureCanAccess(quotation);

        if (!Transitions.TryGetValue((quotation.Status, action), out var next))
            throw new DomainException("CONFLICT", $"Không thể chuyển trạng thái '{quotation.Status}' bằng hành động '{action}'.");

        // Cancel and AccountingConfirm skip lock-at: both are transitions (not content edits),
        // each gated by its own dedicated permission.
        if (action != QuotationAction.Cancel && action != QuotationAction.AccountingConfirm)
            await EnsureCanModifyAsync(quotation, ct);

        if (action == QuotationAction.Cancel
            && quotation.Status == QuotationStatus.Confirmed
            && !_currentUser.HasPermission(Permissions.Quotations.CancelConfirmed))
        {
            throw new ForbiddenException("Bạn không có quyền hủy báo giá đã xác nhận.");
        }

        if (action == QuotationAction.AccountingConfirm
            && !_currentUser.HasPermission(Permissions.Quotations.AccountingConfirm))
            throw new ForbiddenException("Bạn không có quyền xác nhận kế toán báo giá.");

        if (action == QuotationAction.Cancel
            && quotation.Status == QuotationStatus.AccountingConfirmed
            && !_currentUser.HasPermission(Permissions.Quotations.CancelAccountingConfirmed))
            throw new ForbiddenException("Bạn không có quyền hủy báo giá đã kế toán xác nhận.");

        quotation.Status = next;
        ApplyStatusTimestamps(quotation, next);
        AddActivity(quotation, ActivityActionForTransition(action), ActivityDescriptionForTransition(action));
        await _db.SaveChangesAsync(ct);
        await DispatchQuotationNotificationAsync(quotation, next, ct);
        return await GetAsync(quotation.Id, ct);
    }

    private static readonly HashSet<QuotationStatus> NotifiableStatuses = new()
    {
        QuotationStatus.Confirmed,
        QuotationStatus.AccountingConfirmed,
        QuotationStatus.Cancelled,
    };

    private async Task DispatchQuotationNotificationAsync(
        Quotation quotation, QuotationStatus newStatus, CancellationToken ct)
    {
        if (!NotifiableStatuses.Contains(newStatus)) return;

        var info = $"{quotation.Code} | {quotation.QuotationDate:dd/MM/yyyy} | {quotation.CustomerName} | {quotation.Total:N0} VNĐ";
        var (title, body) = newStatus switch
        {
            QuotationStatus.Confirmed           => ("Báo giá đã xác nhận",   $"{info} — đã được xác nhận."),
            QuotationStatus.AccountingConfirmed => ("Kế toán đã duyệt",      $"{info} — đã được kế toán xác nhận."),
            QuotationStatus.Cancelled           => ("Báo giá bị hủy",         $"{info} — đã bị hủy."),
            _                                   => (string.Empty, string.Empty),
        };

        var link = $"/quotations/{quotation.Id}";
        const string type = "quotation_status";

        var recipients = await _db.UserRoles
            .Where(ur => ur.Role!.Code == RoleCodes.Admin && !ur.User!.IsDeleted)
            .Select(ur => ur.UserId)
            .ToListAsync(ct);

        if (!recipients.Contains(quotation.OwnerUserId))
            recipients.Add(quotation.OwnerUserId);

        foreach (var uid in recipients.Distinct())
            await _notifications.SendAsync(uid, type, title, body, link, ct);
    }

    public async Task<QuotationDto> CloneAsync(Guid id, CancellationToken ct = default)
    {
        var source = await _db.Quotations
            .AsNoTracking()
            .Include(q => q.Lines.OrderBy(l => l.SortOrder))
            .FirstOrDefaultAsync(q => q.Id == id && !q.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Quotation), id);

        var ownerInfo = await _db.Users.IgnoreQueryFilters()
            .AsNoTracking()
            .Where(u => u.Id == source.OwnerUserId)
            .Select(u => new { u.IsDeleted })
            .FirstOrDefaultAsync(ct);
        var isOrphan = ownerInfo?.IsDeleted ?? false;

        if (isOrphan)
        {
            if (!_currentUser.HasPermission(Permissions.Quotations.CloneOrphan))
                throw new ForbiddenException("Bạn không có quyền clone báo giá của user đã ngừng hoạt động.");
        }
        else
        {
            EnsureCanAccess(source);
        }

        var currentUserId = _currentUser.UserId
            ?? throw new UnauthorizedAccessException("User not authenticated.");

        for (var attempt = 1; attempt <= MaxCreateAttempts; attempt++)
        {
            var code = await GenerateCodeAsync(ct);
            if (await _db.Quotations.AnyAsync(q => q.Code == code, ct)) continue;

            var clone = new Quotation
            {
                Code = code,
                QuotationDate = DateOnly.FromDateTime(_clock.Now.DateTime),
                OwnerUserId = currentUserId,
                CustomerId = source.CustomerId,
                CustomerName = source.CustomerName,
                CustomerTaxCode = source.CustomerTaxCode,
                CustomerAddress = source.CustomerAddress,
                ContactPerson = source.ContactPerson,
                ContactPhone = source.ContactPhone,
                DeliveryAddress = source.DeliveryAddress,
                DeliveryRecipient = source.DeliveryRecipient,
                DeliveryPhone = source.DeliveryPhone,
                TransportVehicleNumber = NormalizeTransportVehicleNumber(source.TransportVehicleNumber),
                DeliveryDate = source.DeliveryDate,
                DeliveryNote = source.DeliveryNote,
                TaxRate = source.TaxRate,
                Discount = source.Discount,
                Freight = source.Freight,
                InternalNote = source.InternalNote,
                Status = QuotationStatus.Draft,
            };

            foreach (var srcLine in source.Lines.Where(l => !l.IsDeleted))
            {
                clone.Lines.Add(new QuotationLine
                {
                    SortOrder = srcLine.SortOrder,
                    ProductId = srcLine.ProductId,
                    ProductCode = srcLine.ProductCode,
                    ProductName = srcLine.ProductName,
                    Specification = srcLine.Specification,
                    UnitName = srcLine.UnitName,
                    PricingMode = srcLine.PricingMode,
                    Length = srcLine.Length,
                    Width = srcLine.Width,
                    Thickness = srcLine.Thickness,
                    Density = srcLine.Density,
                    SheetCount = srcLine.SheetCount,
                    Quantity = srcLine.Quantity,
                    UnitPrice = srcLine.UnitPrice,
                    UnitCost = srcLine.UnitCost,
                    Note = srcLine.Note,
                });
            }

            Recompute(clone);
            _db.Quotations.Add(clone);
            var activity = AddActivity(clone, QuotationActivityAction.Cloned, "Clone báo giá", new
            {
                SourceQuotationId = source.Id,
                SourceQuotationCode = source.Code,
            });

            try
            {
                await _db.SaveChangesAsync(ct);
                return await GetAsync(clone.Id, ct);
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex) && attempt < MaxCreateAttempts)
            {
                _db.Entry(clone).State = EntityState.Detached;
                _db.Entry(activity).State = EntityState.Detached;
                foreach (var line in clone.Lines)
                    _db.Entry(line).State = EntityState.Detached;
            }
        }

        throw new ConflictException("Không thể tạo mã báo giá tự động sau nhiều lần thử. Vui lòng thử lại.");
    }

    public async Task<QuotationDto> TransferOwnerAsync(Guid id, TransferOwnerRequest request, CancellationToken ct = default)
    {
        var quotation = await _db.Quotations
            .FirstOrDefaultAsync(q => q.Id == id && !q.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Quotation), id);

        var actorId = _currentUser.UserId
            ?? throw new UnauthorizedAccessException("User not authenticated.");

        var isOwner = quotation.OwnerUserId == actorId;
        var permRequired = isOwner
            ? Permissions.Quotations.TransferOwn
            : Permissions.Quotations.TransferAny;
        if (!_currentUser.HasPermission(permRequired))
            throw new ForbiddenException("Bạn không có quyền chuyển nhượng báo giá này.");

        var currentOwner = await _db.Users.IgnoreQueryFilters()
            .AsNoTracking()
            .Where(u => u.Id == quotation.OwnerUserId)
            .Select(u => new { u.IsDeleted })
            .FirstOrDefaultAsync(ct);
        if (currentOwner?.IsDeleted == true)
            throw new DomainException("CONFLICT",
                "Báo giá có chủ sở hữu đã ngừng hoạt động — dùng bulk-transfer để chuyển toàn bộ.");

        var newOwner = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.NewOwnerUserId, ct)
            ?? throw new NotFoundException(nameof(Domain.Entities.Identity.User), request.NewOwnerUserId);
        if (newOwner.Status != UserStatus.Active)
            throw new DomainException("VALIDATION", "User nhận chuyển nhượng đang bị vô hiệu hoá.");

        var oldOwnerId = quotation.OwnerUserId;
        quotation.OwnerUserId = request.NewOwnerUserId;

        _db.QuotationOwnerHistory.Add(new QuotationOwnerHistory
        {
            QuotationId = quotation.Id,
            OldOwnerUserId = oldOwnerId,
            NewOwnerUserId = request.NewOwnerUserId,
            ActorUserId = actorId,
            Reason = request.Reason,
            ChangedAt = _clock.UtcNow,
        });
        AddActivity(quotation, QuotationActivityAction.OwnerTransferred, "Chuyển chủ sở hữu báo giá", new
        {
            OldOwnerUserId = oldOwnerId,
            NewOwnerUserId = request.NewOwnerUserId,
            request.Reason,
        });

        await _db.SaveChangesAsync(ct);

        // After transfer the caller may no longer have access; map DTO directly without guard.
        var refreshed = await _db.Quotations
            .AsNoTracking()
            .Include(q => q.Lines.OrderBy(l => l.SortOrder))
            .FirstAsync(q => q.Id == quotation.Id, ct);
        var ownerAfter = await _db.Users.IgnoreQueryFilters()
            .AsNoTracking()
            .Where(u => u.Id == refreshed.OwnerUserId)
            .Select(u => new { u.FullName, u.IsDeleted })
            .FirstOrDefaultAsync(ct);
        return MapToDto(refreshed, ownerAfter?.FullName, ownerAfter?.IsDeleted ?? false, canViewCost: CanViewCost());
    }

    private async Task<Customer> EnsureCustomerAsync(Guid customerId, CancellationToken ct)
    {
        return await _db.Customers.FirstOrDefaultAsync(c => c.Id == customerId && !c.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Customer), customerId);
    }

    private async Task PopulateLinesAsync(
        Quotation quotation,
        IReadOnlyList<UpsertQuotationLineRequest> requestedLines,
        bool isNew,
        CancellationToken ct)
    {
        var productIds = requestedLines
            .Where(l => l.ProductId.HasValue)
            .Select(l => l.ProductId!.Value)
            .Distinct()
            .ToList();

        var products = productIds.Count == 0
            ? new Dictionary<Guid, Product>()
            : await _db.Products
                .Include(p => p.Unit)
                .Where(p => productIds.Contains(p.Id) && !p.IsDeleted)
                .ToDictionaryAsync(p => p.Id, ct);

        // When the caller cannot see cost, their request payload also carries no cost
        // (DTO redacted it to null on read). Preserve the stored UnitCost instead of
        // letting the round-trip wipe it out.
        var canWriteCost = CanViewCost();

        if (isNew)
        {
            foreach (var line in requestedLines)
            {
                var entity = new QuotationLine();
                ApplyLine(entity, line, products, canWriteCost);
                quotation.Lines.Add(entity);
            }
            return;
        }

        var existing = quotation.Lines.ToDictionary(l => l.Id);
        var keptIds = new HashSet<Guid>();

        foreach (var line in requestedLines)
        {
            if (line.Id.HasValue && existing.TryGetValue(line.Id.Value, out var entity))
            {
                ApplyLine(entity, line, products, canWriteCost);
                keptIds.Add(entity.Id);
            }
            else
            {
                var newLine = new QuotationLine();
                ApplyLine(newLine, line, products, canWriteCost);
                quotation.Lines.Add(newLine);
                // BaseEntity initializes Id with Guid.NewGuid() in its constructor, so EF's
                // entity-state heuristic (non-default PK ⇒ Modified) would emit an UPDATE
                // instead of an INSERT and fail with 0 rows affected. Force the Added state.
                _db.Entry(newLine).State = EntityState.Added;
            }
        }

        foreach (var existingLine in existing.Values)
        {
            if (keptIds.Contains(existingLine.Id)) continue;
            existingLine.IsDeleted = true;
            existingLine.DeletedAt = _clock.UtcNow;
            existingLine.DeletedBy = _currentUser.UserId;
        }
    }

    private static void ApplyLine(
        QuotationLine entity,
        UpsertQuotationLineRequest req,
        Dictionary<Guid, Product> products,
        bool canWriteCost)
    {
        entity.SortOrder = req.SortOrder;
        entity.ProductId = req.ProductId;
        entity.PricingMode = req.PricingMode;

        if (req.ProductId.HasValue && products.TryGetValue(req.ProductId.Value, out var product))
        {
            entity.ProductCode = product.Code;
            entity.ProductName = string.IsNullOrWhiteSpace(req.ProductName) ? product.Name : req.ProductName;
            entity.Specification = req.Specification ?? product.Specification;
            entity.UnitName = string.IsNullOrWhiteSpace(req.UnitName) ? (product.Unit?.Name ?? string.Empty) : req.UnitName;
            entity.PricingMode = req.PricingMode == default ? product.PricingMode : req.PricingMode;
        }
        else
        {
            entity.ProductCode = req.ProductCode;
            entity.ProductName = req.ProductName;
            entity.Specification = req.Specification;
            entity.UnitName = req.UnitName;
        }

        entity.Length = req.Length;
        entity.Width = req.Width;
        entity.Thickness = req.Thickness;
        entity.Density = req.Density;
        entity.SheetCount = req.SheetCount;

        entity.Quantity = req.Quantity;
        entity.UnitPrice = req.UnitPrice;
        if (canWriteCost)
            entity.UnitCost = req.UnitCost;
        entity.Note = req.Note;
    }

    private static void Recompute(Quotation q)
    {
        decimal subtotal = 0m;
        decimal totalCost = 0m;

        foreach (var line in q.Lines.Where(l => !l.IsDeleted))
        {
            line.Quantity = EffectiveQuantity(line);
            line.LineTotal = Math.Round(line.Quantity * line.UnitPrice, 0, MidpointRounding.AwayFromZero);
            if (line.UnitCost.HasValue)
            {
                line.LineCost = Math.Round(line.Quantity * line.UnitCost.Value, 0, MidpointRounding.AwayFromZero);
                line.LineProfit = line.LineTotal - line.LineCost.Value;
            }
            else
            {
                line.LineCost = null;
                line.LineProfit = null;
            }
            subtotal += line.LineTotal;
            totalCost += line.LineCost ?? 0m;
        }

        q.Subtotal = subtotal;
        q.TotalCost = totalCost;
        q.TaxAmount = Math.Round(subtotal * q.TaxRate / 100m, 0, MidpointRounding.AwayFromZero);
        q.Total = subtotal - q.Discount + q.Freight + q.TaxAmount;
        // Gross profit excludes freight (it's a pass-through) and includes the customer discount.
        q.GrossProfit = subtotal - totalCost - q.Discount;
    }

    private static decimal EffectiveQuantity(QuotationLine line)
    {
        var length = line.Length ?? 0m;
        var width = line.Width ?? 0m;
        var thickness = line.Thickness ?? 0m;
        var sheetCount = line.SheetCount ?? 0m;

        return line.PricingMode switch
        {
            PricingMode.PerLinearMeter => length * sheetCount / 1000m,
            PricingMode.PerSquareMeter => length * width * sheetCount / 1_000_000m,
            PricingMode.PerCubicMeter => length * width * thickness * sheetCount / 1_000_000_000m,
            _ => line.Quantity,
        };
    }

    private async Task<string> GenerateCodeAsync(CancellationToken ct)
    {
        var date = _clock.Now.ToString("yyMMdd");
        var todayCount = await _db.Quotations
            .CountAsync(q => q.Code.StartsWith($"{CodePrefix}-{date}"), ct);
        return $"{CodePrefix}-{date}-{todayCount + 1:D4}";
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        var inner = ex.InnerException;
        var sqlState = inner?.GetType().GetProperty("SqlState")?.GetValue(inner) as string;
        return sqlState == "23505";
    }

    private static string EscapeLike(string input) =>
        input.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    private static string NormalizeTransportVehicleNumber(string? value) =>
        string.IsNullOrWhiteSpace(value) ? DefaultTransportVehicleNumber : value.Trim();

    private static QuotationActivityAction ActivityActionForTransition(QuotationAction action) => action switch
    {
        QuotationAction.Send => QuotationActivityAction.Sent,
        QuotationAction.Confirm => QuotationActivityAction.Confirmed,
        QuotationAction.AccountingConfirm => QuotationActivityAction.AccountingConfirmed,
        QuotationAction.Cancel => QuotationActivityAction.Cancelled,
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, null),
    };

    private static string ActivityDescriptionForTransition(QuotationAction action) => action switch
    {
        QuotationAction.Send => "Gửi báo giá",
        QuotationAction.Confirm => "Xác nhận báo giá",
        QuotationAction.AccountingConfirm => "Kế toán xác nhận đã nhận tiền",
        QuotationAction.Cancel => "Hủy báo giá",
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, null),
    };

    private static QuotationDto MapToDto(
        Quotation q,
        string? ownerFullName = null,
        bool isOwnerDeleted = false,
        bool canEdit = true,
        string? confirmedByName = null,
        bool canViewCost = false,
        string? accountingConfirmedByName = null) => new()
    {
        Id = q.Id,
        Code = q.Code,
        QuotationDate = q.QuotationDate,
        OwnerUserId = q.OwnerUserId,
        OwnerFullName = ownerFullName,
        IsOwnerDeleted = isOwnerDeleted,
        CanEdit = canEdit && q.Status != QuotationStatus.Cancelled && !isOwnerDeleted,
        CanClone = true,
        CustomerId = q.CustomerId,
        CustomerName = q.CustomerName,
        CustomerTaxCode = q.CustomerTaxCode,
        CustomerAddress = q.CustomerAddress,
        ContactPerson = q.ContactPerson,
        ContactPhone = q.ContactPhone,
        DeliveryAddress = q.DeliveryAddress,
        DeliveryRecipient = q.DeliveryRecipient,
        DeliveryPhone = q.DeliveryPhone,
        TransportVehicleNumber = NormalizeTransportVehicleNumber(q.TransportVehicleNumber),
        DeliveryDate = q.DeliveryDate,
        DeliveryNote = q.DeliveryNote,
        Subtotal = q.Subtotal,
        Discount = q.Discount,
        Freight = q.Freight,
        TaxRate = q.TaxRate,
        TaxAmount = q.TaxAmount,
        Total = q.Total,
        AdvancePayment = q.AdvancePayment,
        TotalCost = canViewCost ? q.TotalCost : null,
        GrossProfit = canViewCost ? q.GrossProfit : null,
        Status = q.Status,
        ConfirmedAt = q.ConfirmedAt,
        ConfirmedByUserId = q.ConfirmedByUserId,
        ConfirmedByName = confirmedByName,
        CancelledAt = q.CancelledAt,
        AccountingConfirmedAt = q.AccountingConfirmedAt,
        AccountingConfirmedByUserId = q.AccountingConfirmedByUserId,
        AccountingConfirmedByName = accountingConfirmedByName,
        InternalNote = q.InternalNote,
        CreatedAt = q.CreatedAt,
        CreatedBy = q.CreatedBy,
        Lines = q.Lines
            .OrderBy(l => l.SortOrder)
            .Select(l => new QuotationLineDto
            {
                Id = l.Id,
                SortOrder = l.SortOrder,
                ProductId = l.ProductId,
                ProductCode = l.ProductCode,
                ProductGroupCode = l.Product?.ProductGroup?.Code,
                ProductGroupName = l.Product?.ProductGroup?.Name,
                ProductGroupSortOrder = l.Product?.ProductGroup?.SortOrder,
                ProductName = l.ProductName,
                Specification = l.Specification,
                UnitName = l.UnitName,
                PricingMode = l.PricingMode,
                Length = l.Length,
                Width = l.Width,
                Thickness = l.Thickness,
                Density = l.Density,
                SheetCount = l.SheetCount,
                Quantity = l.Quantity,
                UnitPrice = l.UnitPrice,
                LineTotal = l.LineTotal,
                UnitCost = canViewCost ? l.UnitCost : null,
                LineCost = canViewCost ? l.LineCost : null,
                LineProfit = canViewCost ? l.LineProfit : null,
                Note = l.Note,
            })
            .ToList(),
    };
}
