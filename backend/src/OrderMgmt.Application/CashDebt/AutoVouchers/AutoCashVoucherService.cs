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
        await SettleAsync(voucher, entries, ct);

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

    /// The Auto settlement between the stock voucher's entry and the automatic voucher's entry (P20), batch = the stock
    /// voucher. The stock voucher removed its own Auto settlements before posting its entry (P21).
    private async Task SettleAsync(StockVoucher voucher, IReadOnlyList<DebtEntry> autoEntries, CancellationToken ct)
    {
        if (autoEntries.Count == 0)
            return;

        var autoEntry = autoEntries.Single();
        var stockSource = voucher.Type == StockDirection.In ? DebtSourceType.StockIn : DebtSourceType.StockOut;
        // None when the stock voucher's total is 0.
        var stockEntry = await _db.DebtEntries.FirstOrDefaultAsync(e => e.SourceType == stockSource
            && e.SourceId == voucher.Id && e.Side == autoEntry.Side, ct);
        if (stockEntry is null)
            return;

        var settled = await _ledger.SettledAmountsAsync(new[] { stockEntry.Id, autoEntry.Id }, SettlementOrigin.Auto, ct);
        var amount = DebtSettlementMath.AutoAmount(voucher.PaidAmount,
            stockEntry.Amount - settled[stockEntry.Id], autoEntry.Amount - settled[autoEntry.Id]);
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
