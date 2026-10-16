using OrderMgmt.Application.Common;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Entities.Inventory;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Dashboards.Common;

/// The counted sales vouchers of the dashboards: Active, not deleted, system reason XBH (sale, positive) or NTL (sale
/// return, negative), VoucherAt in [from, to). Revenue is before VAT; freight is not revenue.
public sealed class SalesRevenueSource
{
    private readonly IAppDbContext _db;

    public SalesRevenueSource(IAppDbContext db)
    {
        _db = db;
    }

    /// The query of the counted vouchers of the branches with VoucherAt in [from, to). `ownerUserId` null = every owner.
    /// Only the reason ids are read here; the vouchers are read when the query is run.
    public async Task<CountedSales> BuildAsync(IReadOnlyCollection<Guid> branchIds, Guid? ownerUserId,
        DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        // The reason ids come from their own query: IgnoreQueryFilters would also reveal deleted vouchers here.
        var reasons = await _db.StockReasons.IgnoreQueryFilters([QueryFilters.SoftDelete]).AsNoTracking()
            .Where(r => r.IsSystem && (r.Code == StockReasonCodes.Sale || r.Code == StockReasonCodes.SaleReturn))
            .Select(r => new { r.Id, r.Code })
            .ToListAsync(ct);
        var saleIds = reasons.Where(r => r.Code == StockReasonCodes.Sale).Select(r => r.Id).ToList();
        var returnIds = reasons.Where(r => r.Code == StockReasonCodes.SaleReturn).Select(r => r.Id).ToList();

        var vouchers = _db.StockVouchers.AsNoTracking()
            .Where(v => v.Status == StockVoucherStatus.Active
                && branchIds.Contains(v.BranchId)
                && (saleIds.Contains(v.ReasonId) || returnIds.Contains(v.ReasonId))
                && v.VoucherAt >= from && v.VoucherAt < to);
        if (ownerUserId is { } owner)
            vouchers = vouchers.Where(v => v.OwnerUserId == owner);
        return new CountedSales(vouchers, returnIds);
    }
}

/// The query of counted vouchers and the ids of the NTL reason (vouchers that count negative).
public sealed record CountedSales(IQueryable<StockVoucher> Vouchers, IReadOnlyList<Guid> ReturnIds)
{
    /// Signed revenue of each voucher: a sale counts positive and a return negative. Written as a group over the
    /// lines, not as a sum inside the select, so that a caller can aggregate the result again in the database.
    /// DefaultIfEmpty keeps a voucher without lines as one row with revenue 0, as the old COALESCE(SUM) did.
    public IQueryable<SalesVoucherRevenue> VoucherRevenues()
    {
        var returnIds = ReturnIds;
        return Vouchers
            .SelectMany(v => v.Lines.DefaultIfEmpty(), (v, l) => new { Voucher = v, Line = l })
            .GroupBy(x => new
            {
                x.Voucher.Id,
                x.Voucher.VoucherAt,
                x.Voucher.BranchId,
                x.Voucher.PartnerId,
                x.Voucher.OwnerUserId,
                x.Voucher.ReasonId,
                x.Voucher.Type,
            })
            .Select(g => new SalesVoucherRevenue
            {
                Id = g.Key.Id,
                VoucherAt = g.Key.VoucherAt,
                BranchId = g.Key.BranchId,
                PartnerId = g.Key.PartnerId,
                OwnerUserId = g.Key.OwnerUserId,
                IsReturn = returnIds.Contains(g.Key.ReasonId),
                Revenue = (returnIds.Contains(g.Key.ReasonId) ? -1m : 1m)
                    * g.Sum(x => x.Line == null ? 0m
                        : x.Line.Amount - x.Line.DiscountAmount - x.Line.OrderDiscountAllocated
                            - (g.Key.Type == StockDirection.Out && x.Line.PriceIncludesVat ? x.Line.VatAmount : 0m)),
            });
    }

    /// Signed revenue and quantity of each line of the counted vouchers, from `lines` (the caller's line set).
    public IQueryable<SalesLineRevenue> LineRevenues(IQueryable<StockVoucherLine> lines)
    {
        var vouchers = Vouchers;
        var returnIds = ReturnIds;
        return lines.AsNoTracking()
            .Where(l => vouchers.Any(v => v.Id == l.StockVoucherId))
            .Select(l => new SalesLineRevenue
            {
                VoucherId = l.StockVoucherId,
                VoucherAt = l.StockVoucher!.VoucherAt,
                ProductId = l.ProductId,
                Quantity = (returnIds.Contains(l.StockVoucher.ReasonId) ? -1m : 1m) * l.Quantity,
                Revenue = (returnIds.Contains(l.StockVoucher.ReasonId) ? -1m : 1m)
                    * (l.Amount - l.DiscountAmount - l.OrderDiscountAllocated
                        - (l.StockVoucher.Type == StockDirection.Out && l.PriceIncludesVat ? l.VatAmount : 0m)),
            });
    }
}

/// Properties, not constructor arguments: a query can filter and group on them after the projection.
public sealed record SalesVoucherRevenue
{
    public Guid Id { get; init; }
    public DateTimeOffset VoucherAt { get; init; }
    public Guid BranchId { get; init; }
    public Guid? PartnerId { get; init; }
    public Guid OwnerUserId { get; init; }
    public bool IsReturn { get; init; }
    public decimal Revenue { get; init; }
}

public sealed record SalesLineRevenue
{
    public Guid VoucherId { get; init; }
    public DateTimeOffset VoucherAt { get; init; }
    public Guid ProductId { get; init; }
    public decimal Quantity { get; init; }
    public decimal Revenue { get; init; }
}
