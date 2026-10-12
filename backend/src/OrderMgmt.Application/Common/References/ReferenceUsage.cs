using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Inventory.Common;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Common.References;

public class ReferenceUsage : IReferenceUsage
{
    private readonly IAppDbContext _db;

    public ReferenceUsage(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<bool> PartnerHasCashOrDebtDataAsync(Guid partnerId, CancellationToken ct) =>
        await _db.DebtEntries.AnyAsync(e => e.PartnerId == partnerId, ct)
        || await _db.CashVouchers.AnyAsync(v => v.PartnerId == partnerId, ct)
        || await _db.OpeningDebts.AnyAsync(d => d.PartnerId == partnerId, ct)
        || await _db.DebtOffsetVouchers.AnyAsync(o => o.PartnerId == partnerId, ct);

    public Task<bool> PartnerHasDebtOnSideAsync(Guid partnerId, DebtSide side, CancellationToken ct) =>
        _db.DebtEntries.AnyAsync(e => e.PartnerId == partnerId && e.Side == side, ct);

    public Task<bool> PartnerHasStockVouchersAsync(Guid partnerId, CancellationToken ct) =>
        _db.StockVouchers.AnyAsync(v => v.PartnerId == partnerId, ct);

    public Task<bool> PaymentMethodUsedByStockVouchersAsync(Guid methodId, CancellationToken ct) =>
        _db.StockVouchers.AnyAsync(v => v.PaymentMethodId == methodId, ct);

    public Task<bool> PaymentMethodUsedByPaidStockVouchersAsync(Guid methodId, CancellationToken ct) =>
        _db.StockVouchers.AnyAsync(v => v.PaymentMethodId == methodId && v.PaidAmount > 0, ct);

    public async Task<bool> BranchHasDocumentsAsync(Guid branchId, CancellationToken ct) =>
        await _db.StockVouchers.AnyAsync(v => v.BranchId == branchId, ct)
        || await _db.CashVouchers.AnyAsync(v => v.BranchId == branchId, ct)
        || await _db.FundTransfers.AnyAsync(t => t.BranchId == branchId, ct)
        || await _db.DebtOffsetVouchers.AnyAsync(o => o.BranchId == branchId, ct);

    public async Task<bool> BranchHasOpeningDataAsync(Guid branchId, CancellationToken ct) =>
        await _db.OpeningStocks.AnyAsync(o => o.BranchId == branchId, ct)
        || await _db.OpeningDebts.AnyAsync(o => o.BranchId == branchId, ct)
        || await _db.Funds.AnyAsync(f => f.BranchId == branchId && f.OpeningAmount != 0, ct);

    public async Task<DateOnly> EarliestDocumentDateAsync(Guid branchId, CancellationToken ct)
    {
        var firsts = new[]
        {
            await _db.StockVouchers.Where(v => v.BranchId == branchId).MinAsync(v => (DateTimeOffset?)v.VoucherAt, ct),
            await _db.CashVouchers.Where(v => v.BranchId == branchId).MinAsync(v => (DateTimeOffset?)v.VoucherAt, ct),
            await _db.FundTransfers.Where(t => t.BranchId == branchId).MinAsync(t => (DateTimeOffset?)t.VoucherAt, ct),
            await _db.DebtOffsetVouchers.Where(o => o.BranchId == branchId).MinAsync(o => (DateTimeOffset?)o.VoucherAt, ct),
        };
        return VnTime.ToVnDate(firsts.OfType<DateTimeOffset>().Min());
    }

    public async Task<FundUsage> FundUsageAsync(Guid fundId, CancellationToken ct)
    {
        if (await _db.CashVouchers.AnyAsync(v => v.FundId == fundId, ct))
            return FundUsage.CashVouchers;
        if (await _db.FundTransfers.AnyAsync(t => t.FromFundId == fundId || t.ToFundId == fundId, ct))
            return FundUsage.FundTransfers;
        if (await _db.StockVouchers.AnyAsync(v => v.FundId == fundId, ct))
            return FundUsage.StockVouchers;
        return FundUsage.None;
    }
}
