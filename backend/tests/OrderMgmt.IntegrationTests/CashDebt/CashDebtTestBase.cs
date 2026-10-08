using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderMgmt.Application.CashDebt.Common;
using OrderMgmt.Application.CashDebt.Ledger;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Inventory.Common;
using OrderMgmt.Domain.Entities.CashDebt;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using OrderMgmt.IntegrationTests.Inventory;

namespace OrderMgmt.IntegrationTests.CashDebt;

public abstract class CashDebtTestBase : InventoryTestBase
{
    protected CashDebtTestBase(PostgresFixture pg) : base(pg) { }

    /// Runs `work` in a new scope inside ITransactionRunner (copied from InventoryEngineTestBase: the class chains differ).
    protected async Task<T> InTransactionAsync<T>(Func<IServiceProvider, CancellationToken, Task<T>> work)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var runner = scope.ServiceProvider.GetRequiredService<ITransactionRunner>();
        return await runner.RunAsync(ct => work(scope.ServiceProvider, ct));
    }

    /// Direct DB update of the settings singleton. Callers that change policies must restore Warn/Warn in a finally block.
    protected Task UpdateCashDebtSettingsAsync(Action<CashDebtSettings> mutate) => InDbAsync(async db =>
    {
        var settings = await db.CashDebtSettings.SingleAsync(s => s.Id == 1);
        mutate(settings);
        await db.SaveChangesAsync();
    });

    protected Task<IReadOnlyList<DebtEntry>> EntriesOfAsync(DebtSourceType type, Guid sourceId) =>
        InDbAsync<IReadOnlyList<DebtEntry>>(async db => await db.DebtEntries.AsNoTracking()
            .Where(e => e.SourceType == type && e.SourceId == sourceId)
            .OrderBy(e => e.Side)
            .ToListAsync());

    /// Settlements where the entry is either the Increase or the Decrease side.
    protected Task<IReadOnlyList<DebtSettlement>> SettlementsOfAsync(Guid entryId) =>
        InDbAsync<IReadOnlyList<DebtSettlement>>(async db => await db.DebtSettlements.AsNoTracking()
            .Where(s => s.IncreaseEntryId == entryId || s.DecreaseEntryId == entryId)
            .OrderBy(s => s.CreatedAt)
            .ToListAsync());

    /// A ledger draft at VN time `at` (via Vn); DueDate follows DebtDates.EntryDueDate (Increase: dueDate ?? doc date).
    protected static DebtEntryDraft Draft(Guid partnerId, DebtSide side, DebtDirection dir, decimal amount, string at,
        string code = "CT", Guid? branchId = null, DateOnly? dueDate = null)
    {
        var postedAt = Vn(at);
        var docDate = VnTime.ToVnDate(postedAt);
        return new DebtEntryDraft(branchId ?? MainBranchId, partnerId, side, dir, amount, postedAt, docDate,
            DebtDates.EntryDueDate(dir, docDate, dueDate), null, code, null);
    }

    /// Debt engine invariants. Call at the end of every ledger, settlement and debt-source scenario.
    protected Task AssertCashDebtInvariantsAsync() =>
        InDbAsync(async db =>
        {
            var entries = await db.DebtEntries.AsNoTracking().ToDictionaryAsync(e => e.Id);
            var settlements = await db.DebtSettlements.AsNoTracking().ToListAsync();

            // 1. Per entry, Σ settlements ≤ Amount.
            foreach (var entry in entries.Values)
            {
                var settled = settlements.Where(s => s.IncreaseEntryId == entry.Id || s.DecreaseEntryId == entry.Id)
                    .Sum(s => s.Amount);
                settled.Should().BeLessThanOrEqualTo(entry.Amount, $"settled amount of {entry.DocCode} ({entry.Side})");
            }

            foreach (var s in settlements)
            {
                var increase = entries[s.IncreaseEntryId];
                var decrease = entries[s.DecreaseEntryId];

                // 2. An Increase and a Decrease entry of the same branch, partner and side.
                increase.Direction.Should().Be(DebtDirection.Increase, $"increase side of settlement {s.Id}");
                decrease.Direction.Should().Be(DebtDirection.Decrease, $"decrease side of settlement {s.Id}");
                decrease.BranchId.Should().Be(increase.BranchId, $"branch of settlement {s.Id}");
                decrease.PartnerId.Should().Be(increase.PartnerId, $"partner of settlement {s.Id}");
                decrease.Side.Should().Be(increase.Side, $"side of settlement {s.Id}");

                // 3. EffectiveAt = max(PostedAt) of its two entries.
                s.EffectiveAt.Should().Be(DebtDates.EffectiveAt(increase.PostedAt, decrease.PostedAt),
                    $"EffectiveAt of settlement {s.Id}");
            }

            // 4. At most one entry per (SourceType, SourceId, Side).
            entries.Values.GroupBy(e => (e.SourceType, e.SourceId, e.Side))
                .Where(g => g.Count() > 1).Select(g => g.Key)
                .Should().BeEmpty("a source has at most one entry per side");

            // 5. Every non-deleted opening debt has exactly one entry matching it, posted at the branch opening date.
            var openingDebts = await db.OpeningDebts.AsNoTracking().ToListAsync();
            var openingDates = await db.Branches.IgnoreQueryFilters().AsNoTracking()
                .ToDictionaryAsync(b => b.Id, b => b.OpeningDate);
            foreach (var row in openingDebts)
            {
                var direction = row.Kind == OpeningDebtKind.Debt ? DebtDirection.Increase : DebtDirection.Decrease;
                var openingDate = openingDates[row.BranchId];
                openingDate.Should().NotBeNull($"branch of opening debt {row.DocNo} has an opening date");
                entries.Values.Where(e => e.SourceType == DebtSourceType.Opening && e.SourceId == row.Id)
                    .Should().ContainSingle($"opening debt {row.DocNo} has one entry")
                    .Which.Should().BeEquivalentTo(new
                    {
                        row.BranchId,
                        row.PartnerId,
                        row.Side,
                        Direction = direction,
                        row.Amount,
                        PostedAt = VnTime.StartOfDay(openingDate!.Value),
                        row.DocDate,
                        DueDate = DebtDates.EntryDueDate(direction, row.DocDate, row.DueDate),
                        row.OwnerUserId,
                        DocCode = row.DocNo,
                        Description = row.Note ?? "Nợ đầu kỳ",
                    }, $"entry of opening debt {row.DocNo}");
            }

            // 6. Every Opening entry belongs to a non-deleted opening debt.
            if (!PostsSyntheticOpeningEntries)
            {
                var rowIds = openingDebts.Select(o => o.Id).ToHashSet();
                entries.Values.Where(e => e.SourceType == DebtSourceType.Opening && !rowIds.Contains(e.SourceId))
                    .Select(e => e.DocCode)
                    .Should().BeEmpty("every Opening entry belongs to a non-deleted opening debt");
            }
        });

    /// Ledger-level tests post Opening entries for fresh Guids without an opening debt row (Task 4.3).
    /// They return true, which skips only invariant 6 (every Opening entry has a row).
    protected virtual bool PostsSyntheticOpeningEntries => false;
}
