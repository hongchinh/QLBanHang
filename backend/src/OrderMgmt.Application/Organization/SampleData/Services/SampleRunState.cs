using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Organization.SampleData.Services;

/// A debt entry posted by a plan item: what an allocation or an offset needs to read its open amount.
internal sealed record SampleDebtEntryRef(Guid Id, DebtSide Side, Guid PartnerId)
{
    /// The entry the document posted, or null when it posted none (a sale without partner, a reason without debt
    /// effect). Opening debts, stock vouchers and cash vouchers post at most one.
    public static Task<SampleDebtEntryRef?> OfSourceAsync(IAppDbContext db, DebtSourceType sourceType, Guid sourceId,
        CancellationToken ct) =>
        db.DebtEntries
            .Where(e => e.SourceType == sourceType && e.SourceId == sourceId)
            .Select(e => new SampleDebtEntryRef(e.Id, e.Side, e.PartnerId))
            .SingleOrDefaultAsync(ct);
}

/// What one run has learned so far: people, ids by plan code or key, counters and the item being run.
internal sealed class SampleRunState
{
    public SampleRunState(Guid branchId, int total, string currentKey)
    {
        BranchId = branchId;
        Total = total;
        CurrentKey = currentKey;
    }

    public Guid BranchId { get; }
    public string BranchCode { get; set; } = "";
    public int Total { get; }
    public int Done { get; set; }
    public int StepsExecuted { get; set; }
    public int StepsSkipped { get; set; }
    /// Key of the item being run; it starts the failure message.
    public string CurrentKey { get; set; }

    public Guid RequestedBy { get; set; }
    public IReadOnlyList<Guid> Owners { get; set; } = Array.Empty<Guid>();
    public Dictionary<Guid, ActingIdentity> Identities { get; } = new();

    public Dictionary<string, Guid> Products { get; } = new();
    public Dictionary<string, Guid> Partners { get; } = new();
    /// Warehouse ids by plan key (K1, K2).
    public Dictionary<string, Guid> Warehouses { get; } = new();
    /// Fund ids of the branch by code.
    public Dictionary<string, Guid> Funds { get; } = new();
    public Dictionary<string, Guid> StockReasons { get; } = new();
    /// Manual cash reasons only (!IsAuto).
    public Dictionary<string, Guid> CashReasons { get; } = new();
    public Dictionary<string, Guid> PaymentMethods { get; } = new();
    /// Debt entries by plan key: opening debts, then the documents that post one.
    public Dictionary<string, SampleDebtEntryRef> Entries { get; } = new();
    /// Cash voucher ids by plan key (for SampleCancelStep).
    public Dictionary<string, Guid> CashVouchers { get; } = new();

    /// S5: `index` picks among the owners (wrapping); null is the requesting user.
    public Guid OwnerOf(int? index) => index is { } i ? Owners[i % Owners.Count] : RequestedBy;

    /// The value the plan names by `key`; a key the plan never defined fails the item.
    public static T Lookup<T>(IReadOnlyDictionary<string, T> map, string key) =>
        map.TryGetValue(key, out var value) ? value : throw new SampleDataPlanException($"Kế hoạch tham chiếu khóa không tồn tại: {key}");
}
