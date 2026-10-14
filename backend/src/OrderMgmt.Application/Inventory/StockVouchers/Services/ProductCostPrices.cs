using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Inventory.StockVouchers.Services;

public static class ProductCostPrices
{
    /// D35: CostPrice follows the latest Active, non-deleted stock-in line (by VoucherAt, then the voucher's
    /// CreatedAt and Code so vouchers with the same VoucherAt resolve deterministically, then SortOrder).
    /// It only changes tracked Product entities: the caller must SaveChangesAsync. A product with no active stock-in
    /// left keeps its price and only has CostPriceUpdatedOn cleared (S17).
    public static async Task RecomputeAsync(IAppDbContext db, IEnumerable<Guid> productIds, CancellationToken ct)
    {
        foreach (var productId in productIds.Distinct())
        {
            var latest = await db.StockVoucherLines
                .Where(l => l.ProductId == productId
                    && l.StockVoucher!.Type == StockDirection.In
                    && l.StockVoucher.Status == StockVoucherStatus.Active)
                .OrderByDescending(l => l.StockVoucher!.VoucherAt)
                .ThenByDescending(l => l.StockVoucher!.CreatedAt)
                .ThenByDescending(l => l.StockVoucher!.Code)
                .ThenByDescending(l => l.SortOrder)
                .Select(l => new { l.UnitPrice, l.StockVoucher!.VoucherAt })
                .FirstOrDefaultAsync(ct);
            var product = await db.Products.FirstOrDefaultAsync(p => p.Id == productId, ct);
            if (product is null)
                continue;

            if (latest is null)
            {
                product.CostPriceUpdatedOn = null;
            }
            else
            {
                product.CostPrice = latest.UnitPrice;
                product.CostPriceUpdatedOn = latest.VoucherAt;
            }
        }
    }
}
