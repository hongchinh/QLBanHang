using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.CashDebt.Common;
using OrderMgmt.Application.CashDebt.Ledger;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Inventory.Common;
using OrderMgmt.Application.Inventory.Numbering;
using OrderMgmt.Domain.Entities.CashDebt;
using OrderMgmt.Domain.Entities.Inventory;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.CashDebt.AutoVouchers;

public class AutoCashVoucherService : IAutoCashVoucherService
{
    // Seeded system reasons (IsAuto): they cannot be edited or deleted, so no reason key is needed.
    private const string ReceiptReasonCode = "TPX";
    private const string PaymentReasonCode = "CPN";

    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTime _clock;
    private readonly IDocumentCodeAllocator _codes;
    private readonly IDebtLedgerService _ledger;

    public AutoCashVoucherService(IAppDbContext db, ICurrentUser currentUser, IDateTime clock, IDocumentCodeAllocator codes,
        IDebtLedgerService ledger)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _codes = codes;
        _ledger = ledger;
    }

    public async Task SyncAsync(StockVoucher voucher, StockReason reason, bool confirmRemoveSettlements,
        CancellationToken ct = default)
    {
        // The live automatic voucher; one soft-deleted earlier (P3) is hidden by the query filter.
        var auto = await _db.CashVouchers.Include(v => v.Lines)
            .FirstOrDefaultAsync(v => v.SourceStockVoucherId == voucher.Id, ct);

        if (!voucher.IsDeleted && voucher.Status == StockVoucherStatus.Active && voucher.PaidAmount > 0)
        {
            await UpsertAsync(voucher, reason, auto, confirmRemoveSettlements, ct);
            return;
        }
        if (auto is null)
            return;

        // A Cancelled automatic voucher has no entry left, so this removes nothing for it.
        await _ledger.RemoveSourceAsync(SourceTypeOf(auto.Type), auto.Id, confirmRemoveSettlements, ct);
        if (!voucher.IsDeleted && voucher.Status == StockVoucherStatus.Cancelled)
        {
            auto.Status = DocumentStatus.Cancelled;
            auto.CancelledAt = _clock.UtcNow;
            auto.CancelledBy = _currentUser.UserId;
            AddActivity(auto, voucher, CashDocumentActivityAction.Cancelled);
        }
        else
        {
            // Deleted with its stock voucher, or nothing is paid any more (P3); its number is not reused.
            auto.IsDeleted = true;
            AddActivity(auto, voucher, CashDocumentActivityAction.Deleted);
            AddStockActivity(voucher, $"Xóa {NounOf(auto.Type)} tự động {auto.Code}");
        }
        await _db.SaveChangesAsync(ct);
    }

    /// Creates or updates the automatic voucher of a paid, Active stock voucher, then its entry and Auto settlement.
    private async Task UpsertAsync(StockVoucher voucher, StockReason reason, CashVoucher? auto, bool confirmRemoveSettlements,
        CancellationToken ct)
    {
        var type = DebtEffectRules.AutoVoucherType(voucher.Type);
        var action = auto is null ? CashDocumentActivityAction.Created
            : auto.Status == DocumentStatus.Cancelled ? CashDocumentActivityAction.Restored
            : CashDocumentActivityAction.Updated;
        if (auto is null)
        {
            auto = new CashVoucher
            {
                Type = type,
                // Kept when the stock voucher's date moves (P20).
                Code = await NextCodeAsync(type, voucher.BranchId, voucher.VoucherAt, ct),
                BranchId = voucher.BranchId,
                ReasonId = await ReasonIdAsync(type, ct),
                SourceStockVoucherId = voucher.Id,
            };
            _db.CashVouchers.Add(auto);
        }

        var description = (type == CashVoucherType.Receipt ? "Thu tiền theo phiếu xuất " : "Chi tiền theo phiếu nhập ")
                          + voucher.Code;
        auto.VoucherAt = voucher.VoucherAt;
        auto.FundId = voucher.FundId
            ?? throw new InvalidOperationException("A stock voucher with a paid amount has a fund (P9).");
        auto.PartnerId = voucher.PartnerId;
        auto.PartnerName = voucher.PartnerName;
        auto.PartnerAddress = voucher.PartnerAddress;
        auto.PartnerTaxCode = voucher.PartnerTaxCode;
        auto.PersonName = voucher.HandlerName;
        auto.Description = description;
        auto.OwnerUserId = voucher.OwnerUserId;
        auto.Status = DocumentStatus.Active;
        auto.CancelledAt = null;
        auto.CancelledBy = null;
        auto.Total = voucher.PaidAmount;

        // One line, updated in place; a new one goes through its DbSet.
        var line = auto.Lines.FirstOrDefault(l => !l.IsDeleted);
        if (line is null)
        {
            line = new CashVoucherLine { CashVoucherId = auto.Id };
            _db.CashVoucherLines.Add(line);
        }
        line.SortOrder = 0;
        line.Description = description;
        line.Amount = voucher.PaidAmount;
        await _db.SaveChangesAsync(ct);

        var entries = await _ledger.PostAsync(SourceTypeOf(type), auto.Id, DraftsOf(auto, reason),
            confirmRemoveSettlements, ct);
        if (entries.Count > 0)
            await SettleAsync(voucher, entries.Single(), ct);

        AddActivity(auto, voucher, action);
        if (action == CashDocumentActivityAction.Created)
            AddStockActivity(voucher, $"Tạo {NounOf(type)} tự động {auto.Code}");
        await _db.SaveChangesAsync(ct);
    }

    /// The automatic voucher's entry: the stock reason's side with the opposite direction, when the reason affects
    /// debt (P20); none otherwise.
    private static IReadOnlyList<DebtEntryDraft> DraftsOf(CashVoucher auto, StockReason reason)
    {
        if (DebtEffectRules.Resolve(reason.DebtEffect) is not { } effect)
            return Array.Empty<DebtEntryDraft>();

        var partnerId = auto.PartnerId
            ?? throw new InvalidOperationException("A stock voucher whose reason affects debt needs a partner.");
        var direction = DebtEffectRules.Opposite(effect.Direction);
        var docDate = VnTime.ToVnDate(auto.VoucherAt);
        return new[]
        {
            new DebtEntryDraft(auto.BranchId, partnerId, effect.Side, direction, auto.Total, auto.VoucherAt, docDate,
                DebtDates.EntryDueDate(direction, docDate, null), auto.OwnerUserId, auto.Code, auto.Description),
        };
    }

    public async Task RecomputeAutoAsync(IEnumerable<Guid> entryIds, CancellationToken ct = default)
    {
        var ids = entryIds.Distinct().ToList();
        if (ids.Count == 0)
            return;

        // The stock vouchers behind these entries: as the source of a stock entry, or as the manager of the automatic
        // voucher that is the source of a receipt or payment entry.
        var sources = await _db.DebtEntries.Where(e => ids.Contains(e.Id))
            .Select(e => new { e.SourceType, e.SourceId })
            .ToListAsync(ct);
        var cashIds = sources.Where(s => s.SourceType is DebtSourceType.Receipt or DebtSourceType.Payment)
            .Select(s => s.SourceId)
            .ToList();
        var managers = await _db.CashVouchers
            .Where(v => cashIds.Contains(v.Id) && v.SourceStockVoucherId != null)
            .Select(v => v.SourceStockVoucherId!.Value)
            .ToListAsync(ct);
        var stockIds = sources.Where(s => s.SourceType is DebtSourceType.StockIn or DebtSourceType.StockOut)
            .Select(s => s.SourceId)
            .Concat(managers)
            .Distinct()
            .ToList();

        // Non-deleted (query filter), Active and paid: the stock vouchers that hold an Auto settlement (P20).
        var vouchers = await _db.StockVouchers.AsNoTracking()
            .Where(v => stockIds.Contains(v.Id) && v.Status == StockVoucherStatus.Active && v.PaidAmount > 0)
            .OrderBy(v => v.Id)
            .ToListAsync(ct);
        foreach (var voucher in vouchers)
        {
            var auto = await _db.CashVouchers.AsNoTracking()
                .Where(v => v.SourceStockVoucherId == voucher.Id && v.Status == DocumentStatus.Active)
                .Select(v => new { v.Id, v.Type })
                .FirstOrDefaultAsync(ct);
            if (auto is null)
                continue;

            var autoSource = SourceTypeOf(auto.Type);
            // None when the stock reason has no debt effect.
            var autoEntry = await _db.DebtEntries
                .FirstOrDefaultAsync(e => e.SourceType == autoSource && e.SourceId == auto.Id, ct);
            if (autoEntry is not null)
                await SettleAsync(voucher, autoEntry, ct);
        }
    }

    /// The Auto settlement between the stock voucher's entry and the automatic voucher's entry (P20), batch = the stock
    /// voucher: DebtSettlementMath.AutoAmount of the current rows, replacing the one in place when it differs. On a
    /// save the stock voucher removed it before posting its entry (P21), so it is simply added.
    private async Task SettleAsync(StockVoucher voucher, DebtEntry autoEntry, CancellationToken ct)
    {
        var stockSource = voucher.Type == StockDirection.In ? DebtSourceType.StockIn : DebtSourceType.StockOut;
        // None when the stock voucher's total is 0.
        var stockEntry = await _db.DebtEntries.FirstOrDefaultAsync(e => e.SourceType == stockSource
            && e.SourceId == voucher.Id && e.Side == autoEntry.Side, ct);
        if (stockEntry is null)
            return;

        var current = await _db.DebtSettlements
            .Where(s => s.Origin == SettlementOrigin.Auto
                        && (s.IncreaseEntryId == stockEntry.Id || s.DecreaseEntryId == stockEntry.Id))
            .SumAsync(s => s.Amount, ct);
        var settled = await _ledger.SettledAmountsAsync(new[] { stockEntry.Id, autoEntry.Id }, SettlementOrigin.Auto, ct);
        var amount = DebtSettlementMath.AutoAmount(voucher.PaidAmount,
            stockEntry.Amount - settled[stockEntry.Id], autoEntry.Amount - settled[autoEntry.Id]);
        if (amount == current)
            return;

        if (current > 0)
            await _ledger.RemoveOwnSettlementsAsync(stockSource, voucher.Id, SettlementOrigin.Auto, ct);
        if (amount <= 0)
            return;

        var draft = stockEntry.Direction == DebtDirection.Increase
            ? new SettlementDraft(stockEntry.Id, autoEntry.Id, amount)
            : new SettlementDraft(autoEntry.Id, stockEntry.Id, amount);
        await _ledger.AddSettlementsAsync(new[] { draft }, SettlementOrigin.Auto, voucher.Id, "paidAmount", ct);
    }

    /// Same series as manual receipts and payments (P4); codes already used in the (type, branch) are skipped.
    private Task<string> NextCodeAsync(CashVoucherType type, Guid branchId, DateTimeOffset voucherAt, CancellationToken ct) =>
        _codes.NextAsync(DocTypeOf(type), branchId, voucherAt, peek: false, async (codes, c) =>
            // Same scope as the unique index (type, branch_id, code) WHERE is_deleted = false: the query filter matches it.
            (await _db.CashVouchers
                .Where(v => v.Type == type && v.BranchId == branchId && codes.Contains(v.Code))
                .Select(v => v.Code)
                .ToListAsync(c)).ToHashSet(), ct);

    private Task<Guid> ReasonIdAsync(CashVoucherType type, CancellationToken ct)
    {
        var code = type == CashVoucherType.Receipt ? ReceiptReasonCode : PaymentReasonCode;
        return _db.CashReasons.Where(r => r.Code == code && r.Type == type && r.IsAuto).Select(r => r.Id).SingleAsync(ct);
    }

    /// "Theo phiếu PX00001" on the automatic voucher.
    private void AddActivity(CashVoucher auto, StockVoucher voucher, CashDocumentActivityAction action) =>
        _db.CashDocumentActivities.Add(new CashDocumentActivity
        {
            DocumentType = DocTypeOf(auto.Type),
            DocumentId = auto.Id,
            Action = action,
            ActorUserId = _currentUser.UserId,
            OccurredAt = _clock.UtcNow,
            Description = $"Theo phiếu {voucher.Code}",
        });

    /// AutoVoucherChanged on the stock voucher ("Tạo phiếu thu tự động PT00001").
    private void AddStockActivity(StockVoucher voucher, string description) =>
        _db.StockVoucherActivities.Add(new StockVoucherActivity
        {
            StockVoucherId = voucher.Id,
            Action = StockVoucherActivityAction.AutoVoucherChanged,
            ActorUserId = _currentUser.UserId,
            OccurredAt = _clock.UtcNow,
            Description = description,
        });

    private static string NounOf(CashVoucherType type) => type == CashVoucherType.Receipt ? "phiếu thu" : "phiếu chi";

    private static DocumentType DocTypeOf(CashVoucherType type) =>
        type == CashVoucherType.Receipt ? DocumentType.Receipt : DocumentType.Payment;

    private static DebtSourceType SourceTypeOf(CashVoucherType type) =>
        type == CashVoucherType.Receipt ? DebtSourceType.Receipt : DebtSourceType.Payment;
}
