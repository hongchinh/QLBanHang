using OrderMgmt.Application.CashDebt.AutoVouchers;
using OrderMgmt.Application.CashDebt.Common;
using OrderMgmt.Application.CashDebt.Guards;
using OrderMgmt.Application.CashDebt.Interfaces;
using OrderMgmt.Application.CashDebt.Ledger;
using OrderMgmt.Application.Inventory.Common;
using OrderMgmt.Application.Inventory.StockVouchers.Interfaces;
using OrderMgmt.Domain.Common;
using OrderMgmt.Domain.Entities.Inventory;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Inventory.StockVouchers.Services;

/// The cash and debt steps of a stock voucher save (section-03 §5).
public class StockVoucherCashDebt : IStockVoucherCashDebt
{
    private readonly ICashDebtLock _cashDebtLock;
    private readonly IDebtLedgerService _ledger;
    private readonly IAutoCashVoucherService _auto;
    private readonly ICreditLimitGuard _creditGuard;
    private readonly IFundBalanceGuard _fundGuard;

    public StockVoucherCashDebt(ICashDebtLock cashDebtLock, IDebtLedgerService ledger,
        IAutoCashVoucherService auto, ICreditLimitGuard creditGuard, IFundBalanceGuard fundGuard)
    {
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
    /// Other settlements of the stock entry and of the automatic entry need one confirmation (P10). The stock step
    /// always runs unconfirmed first: a 422 from it is held while the steps go on as confirmed, so that the 422 also
    /// lists the automatic entry's settlements; throwing it then rolls the transaction back. A confirmed token must
    /// cover the settlements of both entries; a stale one gets a 422 that lists all of them, with a token covering all.
    /// The entries those settlements joined may belong to other stock vouchers, whose Auto settlements are then
    /// recomputed before the commit (AutoRecomputeHook; this voucher's own is set by SyncAsync).
    public async Task SyncDebtAsync(StockVoucher voucher, StockReason reason, bool remove, bool confirmRemoveSettlements,
        string? confirmedSettlements, CancellationToken ct)
    {
        var sourceType = DebtSourceOf(voucher.Type);
        await _ledger.RemoveOwnSettlementsAsync(sourceType, voucher.Id, SettlementOrigin.Auto, ct);

        Task PostOrRemoveAsync(bool confirm, string? token) => remove
            ? _ledger.RemoveSourceAsync(sourceType, voucher.Id, confirm, token, ct)
            : _ledger.PostAsync(sourceType, voucher.Id, DebtDraftsOf(voucher, reason), confirm, token, ct);

        // The stock step first runs unconfirmed, so its pending settlements are always known: a 422 is held, and the
        // steps go on as confirmed with no token (that retry cannot throw a second 422).
        ConfirmationRequiredException? stockRemovals = null;
        try
        {
            await PostOrRemoveAsync(confirm: false, token: null);
        }
        catch (ConfirmationRequiredException e) when (e.Code == SettlementRemovalConfirmation.Code)
        {
            stockRemovals = e;
            await PostOrRemoveAsync(confirm: true, token: null);
        }

        // The client's confirmation covers the stock entry when it lists what was held (a request without a token
        // keeps its old meaning: confirmed is confirmed).
        var stockCovered = stockRemovals is null
                           || (confirmRemoveSettlements
                               && SettlementRemovalConfirmation.Covers(confirmedSettlements, stockRemovals));

        try
        {
            // Auto is confirmed only when the stock entry was too; otherwise its pending settlements are thrown and merged.
            // So the one 422 lists the settlements of both entries, and a stale token converges on the next resend.
            await _auto.SyncAsync(voucher, reason, confirmRemoveSettlements && stockCovered,
                stockCovered ? confirmedSettlements : null, ct);
        }
        catch (ConfirmationRequiredException e) when (stockRemovals is not null
                                                      && e.Code == SettlementRemovalConfirmation.Code)
        {
            throw SettlementRemovalConfirmation.Merge(stockRemovals, e);
        }

        if (stockRemovals is not null && !stockCovered)
            throw stockRemovals;
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

    /// The customer's receivable balance before the save (save step 7), read after the partner keys. Captured only when
    /// the reason raises the receivable and a partner is set; null otherwise, and the credit check is skipped.
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

    /// The fund-balance check after the automatic voucher is posted, against the balances captured at step 8.
    public Task CheckFundsAsync(FundCapture capture, bool acknowledge, CancellationToken ct) =>
        _fundGuard.CheckAsync(capture, acknowledge, ct);

    private static DebtSourceType DebtSourceOf(StockDirection type) =>
        type == StockDirection.In ? DebtSourceType.StockIn : DebtSourceType.StockOut;
}
