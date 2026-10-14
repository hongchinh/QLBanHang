using Microsoft.Extensions.DependencyInjection;
using OrderMgmt.Application.CashDebt.CashVouchers.Interfaces;
using OrderMgmt.Application.CashDebt.CashVouchers.Models;
using OrderMgmt.Application.CashDebt.DebtOffsets.Interfaces;
using OrderMgmt.Application.CashDebt.DebtOffsets.Models;
using OrderMgmt.Application.CashDebt.FundTransfers.Interfaces;
using OrderMgmt.Application.CashDebt.FundTransfers.Models;
using OrderMgmt.Application.CashDebt.Ledger;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Inventory.StockVouchers.Interfaces;
using OrderMgmt.Application.Inventory.StockVouchers.Models;
using OrderMgmt.Application.Organization.SampleData.Models;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Organization.SampleData.Services;

/// Skipped: nothing to do (amount 0 or a skipped target, S11), so no document was written.
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

    /// A skipped step's key is remembered, so a later step naming it finds nothing there instead of an unknown key.
    public async Task<SampleStepOutcome> ExecuteAsync(SampleStep step, CancellationToken ct)
    {
        var outcome = await (step switch
        {
            SampleStockVoucherStep s => StockVoucherAsync(s, ct),
            SampleCashVoucherStep s => CashVoucherAsync(s, ct),
            SampleFundTransferStep s => FundTransferAsync(s, ct),
            SampleDebtOffsetStep s => DebtOffsetAsync(s, ct),
            SampleCancelStep s => CancelAsync(s, ct),
            _ => throw new SampleDataPlanException($"Loại bước {step.GetType().Name} chưa được hỗ trợ."),
        });
        if (outcome == SampleStepOutcome.Skipped)
            _run.SkippedKeys.Add(step.Key);
        return outcome;
    }

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
        await MapEntryAsync(step.Key, sourceType, voucher.Id, ct);
        return SampleStepOutcome.Executed;
    }

    /// A receipt or payment with one line. A fixed amount allocates nothing; targets allocate what is open on each of
    /// them now, capped by MaxAmount, and a step with nothing left to allocate is skipped (S11). The key then names the
    /// voucher and its own debt entry, when it posted one.
    private async Task<SampleStepOutcome> CashVoucherAsync(SampleCashVoucherStep step, CancellationToken ct)
    {
        var hasFixedAmount = step.FixedAmount is not null;
        var hasTargets = step.Targets.Count > 0;
        if (hasFixedAmount == hasTargets)
            throw new SampleDataPlanException(
                $"Bước {step.Key} phải có đúng một trong hai: số tiền cố định hoặc chứng từ cần đối trừ.");
        // The service would refuse it too, without naming the key.
        if (step.Targets.GroupBy(t => t.TargetKey).FirstOrDefault(g => g.Count() > 1) is { } twice)
            throw new SampleDataPlanException($"Bước {step.Key} chọn khóa {twice.Key} hai lần.");

        var request = new UpsertCashVoucherRequest
        {
            Type = step.Type,
            VoucherAt = step.At,
            FundId = SampleRunState.Lookup(_run.Funds, step.FundCode),
            ReasonId = SampleRunState.Lookup(_run.CashReasons, step.ReasonCode),
            PartnerId = step.PartnerCode is { } partner ? SampleRunState.Lookup(_run.Partners, partner) : null,
            PersonName = step.PersonName,
            Description = step.Description,
            AcknowledgeNegativeFund = true,
        };

        decimal amount;
        if (step.FixedAmount is { } fixedAmount)
        {
            amount = fixedAmount;
        }
        else
        {
            // A skipped target has nothing open, so it allocates nothing.
            var targets = step.Targets
                .Where(t => !_run.SkippedKeys.Contains(t.TargetKey))
                .Select(t => (Entry: SampleRunState.Lookup(_run.Entries, t.TargetKey), t.MaxAmount))
                .ToList();
            var open = await OpenAmountsAsync(targets.Select(t => t.Entry), ct);
            request.Allocations = targets
                .Select(t => new AllocationRequest
                {
                    EntryId = t.Entry.Id,
                    Amount = Math.Min(open[t.Entry.Id], t.MaxAmount ?? open[t.Entry.Id]),
                })
                .Where(a => a.Amount > 0)
                .ToList();
            amount = request.Allocations.Sum(a => a.Amount);
            if (amount <= 0)
                return SampleStepOutcome.Skipped;
        }

        request.Lines.Add(new UpsertCashVoucherLineRequest { Description = step.Description, Amount = amount });
        await SampleDataRunner.ValidateAsync(_services, request, ct);
        var voucher = await _services.GetRequiredService<ICashVoucherService>().CreateAsync(request, ct);

        _run.CashVouchers[step.Key] = voucher.Id;
        var sourceType = step.Type == CashVoucherType.Receipt ? DebtSourceType.Receipt : DebtSourceType.Payment;
        await MapEntryAsync(step.Key, sourceType, voucher.Id, ct);
        return SampleStepOutcome.Executed;
    }

    private async Task<SampleStepOutcome> FundTransferAsync(SampleFundTransferStep step, CancellationToken ct)
    {
        var request = new UpsertFundTransferRequest
        {
            VoucherAt = step.At,
            FromFundId = SampleRunState.Lookup(_run.Funds, step.FromFundCode),
            ToFundId = SampleRunState.Lookup(_run.Funds, step.ToFundCode),
            Amount = step.Amount,
            Description = step.Description,
            AcknowledgeNegativeFund = true,
        };
        await SampleDataRunner.ValidateAsync(_services, request, ct);
        await _services.GetRequiredService<IFundTransferService>().CreateAsync(request, ct);
        return SampleStepOutcome.Executed;
    }

    /// Offsets what is open on both entries now: min(open receivable, open payable); 0 skips the step (S11), as does a
    /// skipped target, once the partner and the other target are known.
    private async Task<SampleStepOutcome> DebtOffsetAsync(SampleDebtOffsetStep step, CancellationToken ct)
    {
        var partnerId = SampleRunState.Lookup(_run.Partners, step.PartnerCode);
        var receivable = EntryOrSkipped(step.ReceivableTargetKey);
        var payable = EntryOrSkipped(step.PayableTargetKey);
        if (receivable is null || payable is null)
            return SampleStepOutcome.Skipped;
        var open = await OpenAmountsAsync(new[] { receivable, payable }, ct);
        var amount = Math.Min(open[receivable.Id], open[payable.Id]);
        if (amount <= 0)
            return SampleStepOutcome.Skipped;

        var request = new CreateDebtOffsetRequest
        {
            VoucherAt = step.At,
            PartnerId = partnerId,
            Receivables = { new AllocationRequest { EntryId = receivable.Id, Amount = amount } },
            Payables = { new AllocationRequest { EntryId = payable.Id, Amount = amount } },
        };
        await SampleDataRunner.ValidateAsync(_services, request, ct);
        await _services.GetRequiredService<IDebtOffsetService>().CreateAsync(request, ct);
        return SampleStepOutcome.Executed;
    }

    /// Cancels the cash voucher of step TargetKey at its current version; its settlements go with it, so what it
    /// settled is open again. A skipped target wrote no voucher, so the cancel is skipped too.
    private async Task<SampleStepOutcome> CancelAsync(SampleCancelStep step, CancellationToken ct)
    {
        if (_run.SkippedKeys.Contains(step.TargetKey))
            return SampleStepOutcome.Skipped;
        var voucherId = SampleRunState.Lookup(_run.CashVouchers, step.TargetKey);
        var service = _services.GetRequiredService<ICashVoucherService>();
        var voucher = await service.GetAsync(voucherId, ct);
        var request = new CashDocumentActionRequest
        {
            Version = voucher.Version,
            ConfirmRemoveSettlements = true,
            AcknowledgeNegativeFund = true,
        };
        await SampleDataRunner.ValidateAsync(_services, request, ct);
        await service.CancelAsync(voucherId, request, ct);
        return SampleStepOutcome.Executed;
    }

    /// The entry the plan names by `key`, or null when that step was skipped: it wrote nothing, so nothing is open
    /// there. A key the plan never defined still fails the item.
    private SampleDebtEntryRef? EntryOrSkipped(string key) =>
        _run.SkippedKeys.Contains(key) ? null : SampleRunState.Lookup(_run.Entries, key);

    /// Lets later steps name the document's debt entry by the step key.
    private async Task MapEntryAsync(string key, DebtSourceType sourceType, Guid sourceId, CancellationToken ct)
    {
        var db = _services.GetRequiredService<IAppDbContext>();
        if (await SampleDebtEntryRef.OfSourceAsync(db, sourceType, sourceId, ct) is { } entry)
            _run.Entries[key] = entry;
    }

    /// Each entry's open amount now. OpenItemsAsync leaves out an entry with nothing open (a cash sale's entry is
    /// settled by its automatic receipt; a cancelled voucher's entry is gone), so a missing entry counts as 0.
    private async Task<Dictionary<Guid, decimal>> OpenAmountsAsync(IEnumerable<SampleDebtEntryRef> entries,
        CancellationToken ct)
    {
        var debtQuery = _services.GetRequiredService<IDebtQueryService>();
        var open = new Dictionary<Guid, decimal>();
        foreach (var group in entries.GroupBy(e => (e.PartnerId, e.Side)))
        {
            var items = await debtQuery.OpenItemsAsync(new OpenItemsQuery
            {
                BranchId = _run.BranchId,
                PartnerId = group.Key.PartnerId,
                Side = group.Key.Side,
                Direction = null,
            }, ct);
            var openById = items.ToDictionary(i => i.EntryId, i => i.Open);
            foreach (var entry in group)
                open[entry.Id] = openById.GetValueOrDefault(entry.Id);
        }
        return open;
    }
}
