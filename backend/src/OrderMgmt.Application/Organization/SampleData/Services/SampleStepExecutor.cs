using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Inventory.StockVouchers.Interfaces;
using OrderMgmt.Application.Inventory.StockVouchers.Models;
using OrderMgmt.Application.Organization.SampleData.Models;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Organization.SampleData.Services;

/// Skipped: the amount the step computed at run time is 0 (S11), so no document was written.
internal enum SampleStepOutcome
{
    Executed,
    Skipped,
}

/// Runs one plan step inside its item scope, which already acts as the step owner in the branch (S7): one method per
/// step type. The runner counts the outcome.
internal sealed class SampleStepExecutor
{
    private readonly IServiceProvider _services;
    private readonly SampleRunState _run;

    public SampleStepExecutor(IServiceProvider services, SampleRunState run)
    {
        _services = services;
        _run = run;
    }

    public Task<SampleStepOutcome> ExecuteAsync(SampleStep step, CancellationToken ct) => step switch
    {
        SampleStockVoucherStep s => StockVoucherAsync(s, ct),
        // One arm per step type (stock voucher, cash voucher, fund transfer, debt offset, cancel).
        _ => throw new SampleDataPlanException($"Loại bước {step.GetType().Name} chưa được hỗ trợ."),
    };

    /// A purchase or sale through the stock voucher service; the paid amount follows the payment method (P9). The key
    /// then names the voucher's debt entry, when it posted one.
    private async Task<SampleStepOutcome> StockVoucherAsync(SampleStockVoucherStep step, CancellationToken ct)
    {
        var request = new UpsertStockVoucherRequest
        {
            Type = step.Type,
            VoucherAt = step.At,
            WarehouseId = SampleRunState.Lookup(_run.Warehouses, step.WarehouseKey),
            ReasonId = SampleRunState.Lookup(_run.StockReasons, step.ReasonCode),
            PartnerId = step.PartnerCode is { } partner ? SampleRunState.Lookup(_run.Partners, partner) : null,
            PaymentMethodId = step.PaymentMethodCode is { } method ? SampleRunState.Lookup(_run.PaymentMethods, method) : null,
            FundId = step.FundCode is { } fund ? SampleRunState.Lookup(_run.Funds, fund) : null,
            PaidAmount = null,
            Note = step.Note,
            AcknowledgeNegativeStock = true,
            AcknowledgeCreditLimit = true,
            AcknowledgeNegativeFund = true,
            Lines = step.Lines.Select((l, i) => new UpsertStockVoucherLineRequest
            {
                SortOrder = i + 1,
                ProductId = SampleRunState.Lookup(_run.Products, l.ProductCode),
                Quantity = l.Quantity,
                UnitPrice = l.UnitPrice,
                DiscountRate = l.DiscountRate,
                VatRate = l.VatRate,
            }).ToList(),
        };
        await SampleDataRunner.ValidateAsync(_services, request, ct);
        var voucher = await _services.GetRequiredService<IStockVoucherService>().CreateAsync(request, ct);

        var sourceType = step.Type == StockDirection.In ? DebtSourceType.StockIn : DebtSourceType.StockOut;
        var entry = await _services.GetRequiredService<IAppDbContext>().DebtEntries
            .Where(e => e.SourceType == sourceType && e.SourceId == voucher.Id)
            .Select(e => new SampleDebtEntryRef(e.Id, e.Side, e.PartnerId))
            .SingleOrDefaultAsync(ct);
        if (entry is not null)
            _run.Entries[step.Key] = entry;
        return SampleStepOutcome.Executed;
    }
}
