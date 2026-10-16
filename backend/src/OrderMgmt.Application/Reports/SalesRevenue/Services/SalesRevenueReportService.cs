using OrderMgmt.Application.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Common.Options;
using OrderMgmt.Application.Reports.Common;
using OrderMgmt.Application.Reports.SalesRevenue.Interfaces;
using OrderMgmt.Application.Reports.SalesRevenue.Models;
using OrderMgmt.Application.Sales.Quotations.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Entities.Sales;

namespace OrderMgmt.Application.Reports.SalesRevenue.Services;

public class SalesRevenueReportService : ISalesRevenueReportService
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IOptionsMonitor<FeatureOptions> _features;

    public SalesRevenueReportService(
        IAppDbContext db,
        ICurrentUser currentUser,
        IOptionsMonitor<FeatureOptions> features)
    {
        _db = db;
        _currentUser = currentUser;
        _features = features;
    }

    public async Task<SalesRevenueReportDto> GetAsync(SalesRevenueReportRequest request, CancellationToken ct = default)
    {
        var dateMode = await RevenueFilterHelper.GetDateModeAsync(_db, ct);

        var fromDate = DateOnly.FromDateTime(request.From.Date);
        var toDate = DateOnly.FromDateTime(request.To.Date);

        var baseQuery = ApplyScope(request.SaleUserId);

        var quotations = RevenueFilterHelper.ApplyRevenueFilter(baseQuery, dateMode, fromDate, toDate);

        var grouped = await quotations
            .GroupBy(q => q.OwnerUserId)
            .Select(g => new
            {
                OwnerUserId = g.Key,
                Count = g.Count(),
                Gross = g.Sum(x => x.Total),
                Net = g.Sum(x => x.Subtotal - x.Discount),
            })
            .ToListAsync(ct);

        var ownerIds = grouped.Select(x => x.OwnerUserId).ToList();
        var owners = await _db.Users.IgnoreQueryFilters([QueryFilters.SoftDelete])
            .AsNoTracking()
            .Where(u => ownerIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName, u.IsDeleted })
            .ToListAsync(ct);
        var ownerMap = owners.ToDictionary(o => o.Id);

        var items = grouped
            .Select(g =>
            {
                ownerMap.TryGetValue(g.OwnerUserId, out var owner);
                return new SalesRevenueReportItem
                {
                    SaleUserId = g.OwnerUserId,
                    SaleName = owner?.FullName ?? "(không xác định)",
                    IsSaleDeleted = owner?.IsDeleted ?? false,
                    QuotationCount = g.Count,
                    TotalRevenueGross = g.Gross,
                    TotalRevenueNet = g.Net,
                };
            })
            .OrderByDescending(i => i.TotalRevenueGross)
            .ToList();

        return new SalesRevenueReportDto
        {
            From = request.From.Date,
            To = request.To.Date,
            Items = items,
            TotalQuotationCount = items.Sum(i => i.QuotationCount),
            GrandTotalGross = items.Sum(i => i.TotalRevenueGross),
            GrandTotalNet = items.Sum(i => i.TotalRevenueNet),
        };
    }

    public async Task<List<SalesRevenueLineItemDto>> GetLineItemsAsync(
        Guid saleUserId,
        SalesRevenueLineItemsRequest request,
        CancellationToken ct = default)
    {
        var dateMode = await RevenueFilterHelper.GetDateModeAsync(_db, ct);
        return await GetLineItemsCoreAsync(request.From, request.To, saleUserId, newestFirst: true, dateMode, ct);
    }

    public async Task<List<SalesRevenueLineItemDto>> GetLineItemsAsync(
        SalesRevenueLineItemsRequest request,
        CancellationToken ct = default)
    {
        var dateMode = await RevenueFilterHelper.GetDateModeAsync(_db, ct);
        return await GetLineItemsCoreAsync(request.From, request.To, request.SaleUserId, newestFirst: false, dateMode, ct);
    }

    private IQueryable<Quotation> ApplyScope(Guid? requestedSaleUserId)
    {
        var q = _db.Quotations.AsNoTracking().Where(x => !x.IsDeleted);

        if (!_features.CurrentValue.QuotationOwnerScope)
            return requestedSaleUserId.HasValue
                ? q.Where(x => x.OwnerUserId == requestedSaleUserId.Value)
                : q;

        if (_currentUser.HasPermission(Permissions.Quotations.ViewAll))
            return requestedSaleUserId.HasValue
                ? q.Where(x => x.OwnerUserId == requestedSaleUserId.Value)
                : q;

        var uid = _currentUser.UserId ?? Guid.Empty;
        return q.Where(x => x.OwnerUserId == uid);
    }

    private async Task<List<SalesRevenueLineItemDto>> GetLineItemsCoreAsync(
        DateTime from,
        DateTime to,
        Guid? saleUserId,
        bool newestFirst,
        string dateMode,
        CancellationToken ct)
    {
        var fromDate = DateOnly.FromDateTime(from.Date);
        var toDate = DateOnly.FromDateTime(to.Date);
        var canViewCost = _currentUser.HasPermission(Permissions.Quotations.ViewCost);

        var baseQuery = ApplyScope(saleUserId);
        var filtered = RevenueFilterHelper.ApplyRevenueFilter(baseQuery, dateMode, fromDate, toDate);

        var query = filtered.Include(q => q.Lines);

        var ordered = newestFirst
            ? dateMode switch
            {
                RevenueDateField.AccountingConfirmedAt => query.OrderByDescending(q => q.AccountingConfirmedAt).ThenBy(q => q.Id),
                RevenueDateField.ConfirmedAt => query.OrderByDescending(q => q.ConfirmedAt).ThenBy(q => q.Id),
                _ => query.OrderByDescending(q => q.QuotationDate).ThenBy(q => q.Id),
            }
            : dateMode switch
            {
                RevenueDateField.AccountingConfirmedAt => query.OrderBy(q => q.AccountingConfirmedAt).ThenBy(q => q.Id),
                RevenueDateField.ConfirmedAt => query.OrderBy(q => q.ConfirmedAt).ThenBy(q => q.Id),
                _ => query.OrderBy(q => q.QuotationDate).ThenBy(q => q.Id),
            };

        var quotations = await ordered.ToListAsync(ct);

        var result = new List<SalesRevenueLineItemDto>();
        foreach (var q in quotations)
        {
            DateTime? revenueDate = dateMode switch
            {
                RevenueDateField.AccountingConfirmedAt => q.AccountingConfirmedAt,
                RevenueDateField.ConfirmedAt => q.ConfirmedAt,
                _ => q.QuotationDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            };

            var lines = q.Lines.OrderBy(l => l.SortOrder).ToList();
            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                result.Add(new SalesRevenueLineItemDto
                {
                    QuotationId              = q.Id,
                    QuotationCode            = q.Code,
                    QuotationDate            = q.QuotationDate,
                    ConfirmedAt              = q.ConfirmedAt,
                    RevenueDate              = revenueDate,
                    CustomerName             = q.CustomerName,
                    CustomerAddress          = q.CustomerAddress,
                    ContactPhone             = q.ContactPhone,
                    DeliveryAddress          = q.DeliveryAddress,
                    DeliveryPhone            = q.DeliveryPhone,
                    Freight                  = q.Freight,
                    TaxAmount                = q.TaxAmount,
                    Total                    = q.Total,
                    IsFirstLineOfQuotation   = i == 0,
                    ProductName              = line.ProductName,
                    Specification            = line.Specification,
                    UnitName                 = line.UnitName,
                    Length                   = line.Length,
                    Width                    = line.Width,
                    Thickness                = line.Thickness,
                    Density                  = line.Density,
                    SheetCount               = line.SheetCount,
                    Quantity                 = line.Quantity,
                    UnitPrice                = line.UnitPrice,
                    LineTotal                = line.LineTotal,
                    UnitCost                 = canViewCost ? line.UnitCost   : null,
                    LineCost                 = canViewCost ? line.LineCost   : null,
                    LineProfit               = canViewCost ? line.LineProfit : null,
                });
            }
        }
        return result;
    }
}
