using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.CashDebt.AutoVouchers;
using OrderMgmt.Application.CashDebt.Common;
using OrderMgmt.Application.CashDebt.Guards;
using OrderMgmt.Application.CashDebt.Interfaces;
using OrderMgmt.Application.CashDebt.Ledger;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Inventory.Common;
using OrderMgmt.Application.Inventory.StockVouchers.Interfaces;
using OrderMgmt.Domain.Common;
using OrderMgmt.Domain.Entities.CashDebt;
using OrderMgmt.Domain.Entities.Inventory;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Inventory.StockVouchers.Services;

/// The cash and debt steps of a stock voucher save, called by StockVoucherService at the section-03 §5 steps.
public class StockVoucherCashDebt : IStockVoucherCashDebt
{
    private readonly IAppDbContext _db;
    private readonly ICashDebtLock _cashDebtLock;
    private readonly IDebtLedgerService _ledger;
    private readonly IAutoCashVoucherService _auto;
    private readonly ICreditLimitGuard _creditGuard;
    private readonly IFundBalanceGuard _fundGuard;

    public StockVoucherCashDebt(IAppDbContext db, ICashDebtLock cashDebtLock, IDebtLedgerService ledger,
        IAutoCashVoucherService auto, ICreditLimitGuard creditGuard, IFundBalanceGuard fundGuard)
    {
        _db = db;
        _cashDebtLock = cashDebtLock;
        _ledger = ledger;
        _auto = auto;
        _creditGuard = creditGuard;
        _fundGuard = fundGuard;
    }

    /// Save step 3 (P14): after the branch gate and the product keys, the partner and fund keys of the voucher's old
    /// and new state, then the shared key of each stock reason it is saved with. A reason edit or delete takes that key
    /// exclusively, so its in-use check (P19) waits for this save, and this save reads the reason only after it.
    public async Task AcquireLocksAsync(Guid branchId, IEnumerable<Guid?> partnerIds, IEnumerable<Guid?> fundIds,
        IEnumerable<Guid> reasonIds, CancellationToken ct)
    {
        await _cashDebtLock.AcquireAsync(branchId, partnerIds.OfType<Guid>().Distinct(), fundIds.OfType<Guid>().Distinct(), ct);
        // One order for every save, like the other key sets.
        foreach (var reasonId in reasonIds.Distinct().OrderBy(id => id))
            await _cashDebtLock.AcquireStockReasonAsync(reasonId, exclusive: false, ct);
    }

    /// Save steps 11–13 (cancel / delete step 7): the voucher's own Auto settlements go first (no confirmation), then
    /// its entry is upserted in place, or removed when the reason has no debt effect or the total is 0 (P11, P21), or
    /// on cancel / delete (`remove`); then the automatic voucher follows (SyncAsync).
    /// Other settlements of the stock entry and of the automatic entry need one confirmation (P10). Without it, a 422
    /// from the stock entry is held while the steps go on as confirmed, so that the 422 also lists the automatic
    /// entry's settlements; throwing it then rolls the transaction back.
    /// The entries those settlements joined may belong to other stock vouchers, whose Auto settlements are then
    /// recomputed (this voucher's own is set by SyncAsync).
    public async Task SyncDebtAsync(StockVoucher voucher, StockReason reason, bool remove, bool confirmRemoveSettlements,
        CancellationToken ct)
    {
        var sourceType = DebtSourceOf(voucher.Type);
        await _ledger.RemoveOwnSettlementsAsync(sourceType, voucher.Id, SettlementOrigin.Auto, ct);
        // Read once the own Auto settlement is gone, so this voucher's own entries are not among them (only a Manual
        // settlement between the two could bring them back, and recomputing this voucher again changes nothing).
        var counterparts = await CounterpartEntryIdsAsync(voucher, sourceType, ct);

        Task PostOrRemoveAsync(bool confirm) => remove
            ? _ledger.RemoveSourceAsync(sourceType, voucher.Id, confirm, ct)
            : _ledger.PostAsync(sourceType, voucher.Id, DebtDraftsOf(voucher, reason), confirm, ct);

        ConfirmationRequiredException? stockRemovals = null;
        try
        {
            await PostOrRemoveAsync(confirmRemoveSettlements);
        }
        catch (ConfirmationRequiredException e) when (e.Code == SettlementRemovalConfirmation.Code)
        {
            stockRemovals = e;
            await PostOrRemoveAsync(confirm: true);
        }

        try
        {
            await _auto.SyncAsync(voucher, reason, confirmRemoveSettlements, ct);
        }
        catch (ConfirmationRequiredException e) when (stockRemovals is not null
                                                      && e.Code == SettlementRemovalConfirmation.Code)
        {
            throw SettlementRemovalConfirmation.Merge(stockRemovals, e);
        }

        if (stockRemovals is not null)
            throw stockRemovals;

        await _auto.RecomputeAutoAsync(counterparts, ct);
    }

    /// The entries settled against the voucher's stock entry or its automatic voucher's entry.
    private async Task<IReadOnlyList<Guid>> CounterpartEntryIdsAsync(StockVoucher voucher, DebtSourceType sourceType,
        CancellationToken ct)
    {
        var ids = (await _ledger.CounterpartEntryIdsAsync(sourceType, voucher.Id, ct)).ToList();
        var auto = await _db.CashVouchers.Where(v => v.SourceStockVoucherId == voucher.Id)
            .Select(v => new { v.Id, v.Type })
            .FirstOrDefaultAsync(ct);
        if (auto is not null)
            ids.AddRange(await _ledger.CounterpartEntryIdsAsync(
                auto.Type == CashVoucherType.Receipt ? DebtSourceType.Receipt : DebtSourceType.Payment, auto.Id, ct));
        return ids;
    }

    /// The stock draft: one entry when the reason affects debt and the total is positive, none otherwise.
    private static IReadOnlyList<DebtEntryDraft> DebtDraftsOf(StockVoucher voucher, StockReason reason)
    {
        if (DebtEffectRules.Resolve(reason.DebtEffect) is not { } effect)
            return Array.Empty<DebtEntryDraft>();

        var partnerId = voucher.PartnerId
            ?? throw new InvalidOperationException("A stock voucher whose reason affects debt needs a partner.");
        var docDate = VnTime.ToVnDate(voucher.VoucherAt);
        return new[]
        {
            // Total 0 still affects debt: a zero draft is a shrink to 0 for the ledger (P10), not a removed side (P11).
            new DebtEntryDraft(voucher.BranchId, partnerId, effect.Side, effect.Direction, Math.Max(voucher.Total, 0m),
                voucher.VoucherAt, docDate, DebtDates.EntryDueDate(effect.Direction, docDate, voucher.DueDate),
                voucher.OwnerUserId, voucher.Code, reason.Name),
        };
    }

    private static DebtSourceType DebtSourceOf(StockDirection type) =>
        type == StockDirection.In ? DebtSourceType.StockIn : DebtSourceType.StockOut;

    public async Task<StockVoucherCreditCapture?> CaptureCreditAsync(Guid branchId, StockReason reason, Guid? partnerId,
        CancellationToken ct)
    {
        if (reason.DebtEffect != DebtEffect.ReceivableIncrease || partnerId is not { } id)
            return null;
        return new StockVoucherCreditCapture(branchId, id, await _creditGuard.CaptureAsync(branchId, id, ct));
    }

    /// Save step 15 (C12, P10): after the debt entry and the automatic voucher's entry are posted.
    public Task CheckCreditAsync(StockVoucherCreditCapture? capture, bool acknowledge, CancellationToken ct) =>
        capture is null
            ? Task.CompletedTask
            : _creditGuard.CheckAsync(capture.BranchId, capture.PartnerId, capture.Before, acknowledge, ct);

    /// Save step 8: the funds the automatic voucher leaves or enters, held under their keys since step 3. A fund it
    /// ends up not touching keeps its running balance, so capturing it is harmless.
    public Task<FundCapture> CaptureFundsAsync(IEnumerable<(Guid? FundId, DateTimeOffset From)> funds,
        CancellationToken ct) =>
        _fundGuard.CaptureAsync(funds.Where(f => f.FundId is not null).Select(f => (f.FundId!.Value, f.From)), ct);

    public Task CheckFundsAsync(FundCapture capture, bool acknowledge, CancellationToken ct) =>
        _fundGuard.CheckAsync(capture, acknowledge, ct);
}
