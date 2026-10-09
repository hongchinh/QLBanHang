using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderMgmt.Application.CashDebt.CashVouchers.Models;
using OrderMgmt.Application.CashDebt.Common;
using OrderMgmt.Application.CashDebt.FundTransfers.Models;
using OrderMgmt.Application.CashDebt.Ledger;
using OrderMgmt.Application.CashDebt.OpeningDebts.Models;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Application.Inventory.Common;
using OrderMgmt.Application.Inventory.Numbering;
using OrderMgmt.Domain.Entities.CashDebt;
using OrderMgmt.Domain.Enums;
using OrderMgmt.Infrastructure.Persistence;
using OrderMgmt.IntegrationTests.Fixtures;
using OrderMgmt.IntegrationTests.Inventory;

namespace OrderMgmt.IntegrationTests.CashDebt;

public abstract class CashDebtTestBase : InventoryTestBase
{
    // Opening sources posted straight to the ledger by this test, with no opening debt row (see LedgerOnlyOpeningSource).
    private readonly HashSet<Guid> _ledgerOnlyOpeningSources = new();

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

            // 6. Every Opening entry belongs to a non-deleted opening debt, except the ledger-only sources this test posted.
            var rowIds = openingDebts.Select(o => o.Id).ToHashSet();
            entries.Values
                .Where(e => e.SourceType == DebtSourceType.Opening && !rowIds.Contains(e.SourceId)
                            && !_ledgerOnlyOpeningSources.Contains(e.SourceId))
                .Select(e => e.DocCode)
                .Should().BeEmpty("every Opening entry belongs to a non-deleted opening debt");

            // 7. Cash vouchers: Total = Σ non-deleted lines; an Active, non-deleted voucher whose reason affects debt
            // has exactly one matching entry; every other voucher (no debt effect, Cancelled or deleted) has none.
            var vouchers = await db.CashVouchers.IgnoreQueryFilters().AsNoTracking()
                .Select(v => new
                {
                    v.Id, v.Type, v.Code, v.BranchId, v.PartnerId, v.VoucherAt, v.Total, v.Status, v.IsDeleted,
                    v.Reason!.DebtEffect,
                    LineTotal = v.Lines.Where(l => !l.IsDeleted).Sum(l => l.Amount),
                })
                .ToListAsync();
            foreach (var v in vouchers)
            {
                var own = entries.Values.Where(e => e.SourceType == CashSourceType(v.Type) && e.SourceId == v.Id).ToList();
                if (!v.IsDeleted)
                    v.Total.Should().Be(v.LineTotal, $"Total of cash voucher {v.Code} is the sum of its lines");

                if (v.IsDeleted || v.Status != DocumentStatus.Active || DebtEffectRules.Resolve(v.DebtEffect) is not { } effect)
                {
                    own.Should().BeEmpty($"cash voucher {v.Code} posts no debt");
                    continue;
                }
                own.Should().ContainSingle($"cash voucher {v.Code} has one entry")
                    .Which.Should().BeEquivalentTo(new
                    {
                        effect.Side,
                        effect.Direction,
                        Amount = v.Total,
                        PartnerId = v.PartnerId,
                        v.BranchId,
                        PostedAt = v.VoucherAt,
                    }, $"entry of cash voucher {v.Code}");
            }

            // 8. Stock vouchers: a non-deleted Active voucher whose reason affects debt and whose Total > 0 has exactly
            // one matching entry; no other stock entry exists.
            var stockVouchers = await db.StockVouchers.IgnoreQueryFilters().AsNoTracking()
                .Where(v => !v.IsDeleted && v.Status == StockVoucherStatus.Active && v.Total > 0
                            && v.Reason!.DebtEffect != DebtEffect.None)
                .Select(v => new
                {
                    v.Id, v.Type, v.Code, v.BranchId, v.PartnerId, v.VoucherAt, v.DueDate, v.Total, v.Reason!.DebtEffect,
                })
                .ToListAsync();
            var stockSources = new HashSet<(DebtSourceType, Guid)>();
            foreach (var v in stockVouchers)
            {
                var sourceType = v.Type == StockDirection.In ? DebtSourceType.StockIn : DebtSourceType.StockOut;
                stockSources.Add((sourceType, v.Id));
                var effect = DebtEffectRules.Resolve(v.DebtEffect)!.Value;
                entries.Values.Where(e => e.SourceType == sourceType && e.SourceId == v.Id)
                    .Should().ContainSingle($"stock voucher {v.Code} has one entry")
                    .Which.Should().BeEquivalentTo(new
                    {
                        effect.Side,
                        effect.Direction,
                        Amount = v.Total,
                        PartnerId = v.PartnerId,
                        v.BranchId,
                        PostedAt = v.VoucherAt,
                        DueDate = DebtDates.EntryDueDate(effect.Direction, VnTime.ToVnDate(v.VoucherAt), v.DueDate),
                    }, $"entry of stock voucher {v.Code}");
            }
            entries.Values
                .Where(e => e.SourceType is DebtSourceType.StockIn or DebtSourceType.StockOut
                            && !stockSources.Contains((e.SourceType, e.SourceId)))
                .Select(e => e.DocCode)
                .Should().BeEmpty("every stock entry belongs to an Active, non-deleted stock voucher whose reason affects debt");
        });

    private static DebtSourceType CashSourceType(CashVoucherType type) =>
        type == CashVoucherType.Receipt ? DebtSourceType.Receipt : DebtSourceType.Payment;

    protected Task<Guid> CashReasonIdAsync(string code) =>
        InDbAsync(db => db.CashReasons.Where(r => r.Code == code).Select(r => r.Id).SingleAsync());

    /// A fund of the branch (default: the main branch) by code.
    protected Task<Guid> FundIdAsync(string code, Guid? branchId = null) =>
        InDbAsync(db => db.Funds.Where(f => f.BranchId == (branchId ?? MainBranchId) && f.Code == code)
            .Select(f => f.Id).SingleAsync());

    /// A cash voucher request at VN time `at` (via Vn) with one line per amount ("Dòng 1", "Dòng 2" …).
    protected static UpsertCashVoucherRequest CashRequest(CashVoucherType type, Guid reasonId, Guid fundId, string at,
        Guid? partnerId, params decimal[] lineAmounts) => new()
    {
        Type = type,
        VoucherAt = Vn(at),
        FundId = fundId,
        ReasonId = reasonId,
        PartnerId = partnerId,
        Lines = lineAmounts.Select((amount, i) => new UpsertCashVoucherLineRequest
        {
            Description = $"Dòng {i + 1}",
            Amount = amount,
        }).ToList(),
    };

    protected static async Task<(HttpStatusCode Status, CashVoucherDto? Voucher, ApiError? Error)> PostCashAsync(
        HttpClient c, UpsertCashVoucherRequest r) =>
        await ReadCashResponseAsync(await c.PostAsJsonAsync("/api/cash-vouchers", r));

    /// Creates a cash voucher through the API and asserts 200.
    protected async Task<CashVoucherDto> CreateCashAsync(HttpClient c, UpsertCashVoucherRequest r)
    {
        var (status, voucher, error) = await PostCashAsync(c, r);
        status.Should().Be(HttpStatusCode.OK, error?.Message);
        return voucher!;
    }

    protected static async Task<(HttpStatusCode Status, CashVoucherDto? Voucher, ApiError? Error)> GetCashAsync(
        HttpClient c, Guid id) =>
        await ReadCashResponseAsync(await c.GetAsync($"/api/cash-vouchers/{id}"));

    protected static async Task<(HttpStatusCode Status, CashVoucherDto? Voucher, ApiError? Error)> PutCashAsync(
        HttpClient c, Guid id, UpsertCashVoucherRequest r) =>
        await ReadCashResponseAsync(await c.PutAsJsonAsync($"/api/cash-vouchers/{id}", r));

    protected static async Task<(HttpStatusCode Status, CashVoucherDto? Voucher, ApiError? Error)> CancelCashAsync(
        HttpClient c, Guid id, CashDocumentActionRequest r) =>
        await ReadCashResponseAsync(await c.PostAsJsonAsync($"/api/cash-vouchers/{id}/cancel", r));

    protected static async Task<(HttpStatusCode Status, CashVoucherDto? Voucher, ApiError? Error)> RestoreCashAsync(
        HttpClient c, Guid id, CashDocumentActionRequest r) =>
        await ReadCashResponseAsync(await c.PostAsJsonAsync($"/api/cash-vouchers/{id}/restore", r));

    /// DELETE sends the action request in the query string; a success carries no voucher.
    protected static async Task<(HttpStatusCode Status, CashVoucherDto? Voucher, ApiError? Error)> DeleteCashAsync(
        HttpClient c, Guid id, CashDocumentActionRequest r) =>
        await ReadCashResponseAsync(await c.DeleteAsync(
            $"/api/cash-vouchers/{id}?version={r.Version}&confirmRemoveSettlements={r.ConfirmRemoveSettlements}"
            + $"&acknowledgeNegativeFund={r.AcknowledgeNegativeFund}"));

    private static Task<(HttpStatusCode Status, CashVoucherDto? Voucher, ApiError? Error)> ReadCashResponseAsync(
        HttpResponseMessage response) =>
        ReadDocumentResponseAsync<CashVoucherDto>(response);

    /// Status, data and error of a document endpoint; a success without a body (DELETE) carries no data.
    protected static async Task<(HttpStatusCode Status, T? Data, ApiError? Error)> ReadDocumentResponseAsync<T>(
        HttpResponseMessage response) where T : class
    {
        var body = await response.Content.ReadAsStringAsync();
        var parsed = string.IsNullOrEmpty(body)
            ? null
            : System.Text.Json.JsonSerializer.Deserialize<ApiResponse<T>>(body, TestJson.Options);
        return (response.StatusCode, parsed?.Data, parsed?.Error);
    }

    /// A fund transfer request at VN time `at` (via Vn).
    protected static UpsertFundTransferRequest TransferRequest(Guid from, Guid to, decimal amount, string at) => new()
    {
        VoucherAt = Vn(at),
        FromFundId = from,
        ToFundId = to,
        Amount = amount,
    };

    protected static async Task<(HttpStatusCode Status, FundTransferDto? Transfer, ApiError? Error)> PostTransferAsync(
        HttpClient c, UpsertFundTransferRequest r) =>
        await ReadDocumentResponseAsync<FundTransferDto>(await c.PostAsJsonAsync("/api/fund-transfers", r));

    protected static async Task<(HttpStatusCode Status, FundTransferDto? Transfer, ApiError? Error)> GetTransferAsync(
        HttpClient c, Guid id) =>
        await ReadDocumentResponseAsync<FundTransferDto>(await c.GetAsync($"/api/fund-transfers/{id}"));

    protected static async Task<(HttpStatusCode Status, FundTransferDto? Transfer, ApiError? Error)> PutTransferAsync(
        HttpClient c, Guid id, UpsertFundTransferRequest r) =>
        await ReadDocumentResponseAsync<FundTransferDto>(await c.PutAsJsonAsync($"/api/fund-transfers/{id}", r));

    protected static async Task<(HttpStatusCode Status, FundTransferDto? Transfer, ApiError? Error)> CancelTransferAsync(
        HttpClient c, Guid id, CashDocumentActionRequest r) =>
        await ReadDocumentResponseAsync<FundTransferDto>(await c.PostAsJsonAsync($"/api/fund-transfers/{id}/cancel", r));

    protected static async Task<(HttpStatusCode Status, FundTransferDto? Transfer, ApiError? Error)> RestoreTransferAsync(
        HttpClient c, Guid id, CashDocumentActionRequest r) =>
        await ReadDocumentResponseAsync<FundTransferDto>(await c.PostAsJsonAsync($"/api/fund-transfers/{id}/restore", r));

    /// DELETE sends the action request in the query string; a success carries no transfer.
    protected static async Task<(HttpStatusCode Status, FundTransferDto? Transfer, ApiError? Error)> DeleteTransferAsync(
        HttpClient c, Guid id, CashDocumentActionRequest r) =>
        await ReadDocumentResponseAsync<FundTransferDto>(await c.DeleteAsync(
            $"/api/fund-transfers/{id}?version={r.Version}&acknowledgeNegativeFund={r.AcknowledgeNegativeFund}"));

    /// An update request that resends the stored voucher unchanged (line ids and version included, allocations null).
    protected static UpsertCashVoucherRequest CashUpdateFrom(CashVoucherDto v) => new()
    {
        Type = v.Type,
        VoucherAt = v.VoucherAt,
        FundId = v.FundId,
        ReasonId = v.ReasonId,
        PartnerId = v.PartnerId,
        PartnerName = v.PartnerName,
        PartnerAddress = v.PartnerAddress,
        PartnerTaxCode = v.PartnerTaxCode,
        PersonName = v.PersonName,
        Description = v.Description,
        AttachedDocs = v.AttachedDocs,
        Note = v.Note,
        Version = v.Version,
        Lines = v.Lines.OrderBy(l => l.SortOrder).Select(l => new UpsertCashVoucherLineRequest
        {
            Id = l.Id,
            Description = l.Description,
            Amount = l.Amount,
            Note = l.Note,
        }).ToList(),
    };

    /// Inserts a cash voucher row straight into the DB: owner admin, no partner, one line of `total`.
    /// Uses the DebtEffect=None reasons TK / CK so the row needs no debt entry and stays valid business data.
    protected Task<Guid> InsertCashVoucherAsync(Guid branchId, Guid fundId, CashVoucherType type, decimal total,
        DateTimeOffset at, DocumentStatus status = DocumentStatus.Active) =>
        InDbAsync(async db =>
        {
            var reasonCode = type == CashVoucherType.Receipt ? "TK" : "CK";
            var voucher = new CashVoucher
            {
                Type = type,
                Code = (type == CashVoucherType.Receipt ? "PT" : "PC") + Guid.NewGuid().ToString("N")[..8],
                VoucherAt = at,
                BranchId = branchId,
                FundId = fundId,
                ReasonId = await db.CashReasons.Where(r => r.Code == reasonCode).Select(r => r.Id).SingleAsync(),
                Total = total,
                Status = status,
                OwnerUserId = await db.Users.Where(u => u.Username == "admin").Select(u => u.Id).SingleAsync(),
            };
            db.CashVouchers.Add(voucher);
            db.CashVoucherLines.Add(new CashVoucherLine
            {
                CashVoucherId = voucher.Id, SortOrder = 0, Description = "Dòng 1", Amount = total,
            });
            await db.SaveChangesAsync();
            return voucher.Id;
        });

    /// Inserts a fund transfer row straight into the DB (owner admin); the code is random unless given.
    protected Task<Guid> InsertFundTransferAsync(Guid branchId, Guid fromFundId, Guid toFundId, decimal amount,
        DateTimeOffset at, DocumentStatus status = DocumentStatus.Active, string? code = null) =>
        InDbAsync(async db =>
        {
            var transfer = new FundTransfer
            {
                Code = code ?? "CQ" + Guid.NewGuid().ToString("N")[..8],
                VoucherAt = at,
                BranchId = branchId,
                FromFundId = fromFundId,
                ToFundId = toFundId,
                Amount = amount,
                Status = status,
                OwnerUserId = await db.Users.Where(u => u.Username == "admin").Select(u => u.Id).SingleAsync(),
            };
            db.FundTransfers.Add(transfer);
            await db.SaveChangesAsync();
            return transfer.Id;
        });

    /// The seeded cash fund QTM of a branch.
    protected Task<Guid> CashFundIdAsync(Guid branchId) =>
        InDbAsync(db => db.Funds.Where(f => f.BranchId == branchId && f.Code == "QTM").Select(f => f.Id).SingleAsync());

    /// Sets a fund's OpeningAmount straight in the DB.
    protected Task SetFundOpeningAmountAsync(Guid fundId, decimal amount) =>
        InDbAsync(db => db.Funds.Where(f => f.Id == fundId)
            .ExecuteUpdateAsync(s => s.SetProperty(f => f.OpeningAmount, amount)));

    /// Creates an opening debt through the API in the client's working branch (which needs an opening date) and
    /// returns the row id and the id of its debt entry.
    protected async Task<(Guid OpeningDebtId, Guid EntryId)> CreateOpeningDebtAsync(HttpClient c, Guid partnerId,
        DebtSide side, OpeningDebtKind kind, string docNo, DateOnly docDate, decimal amount, DateOnly? dueDate = null)
    {
        var debt = await ReadDataAsync<OpeningDebtDto>(await c.PostAsJsonAsync("/api/opening-debts",
            new UpsertOpeningDebtRequest
            {
                PartnerId = partnerId,
                Side = side,
                Kind = kind,
                DocNo = docNo,
                DocDate = docDate,
                DueDate = dueDate,
                Amount = amount,
            }, TestJson.Options));
        return (debt.Id, (await EntriesOfAsync(DebtSourceType.Opening, debt.Id)).Single().Id);
    }

    /// Ledger-level tests post Opening entries for fresh Guids without an opening debt row (Task 4.3).
    /// Registers `sourceId` as such a source and returns it; invariant 6 exempts exactly these ids,
    /// so any other Opening entry without a row still fails.
    protected Guid LedgerOnlyOpeningSource(Guid sourceId)
    {
        _ledgerOnlyOpeningSources.Add(sourceId);
        return sourceId;
    }

    /// Runs two requests against each other. A transaction of the test holds a row (`hold`) that `first` writes only
    /// after its locks and checks, so `first` stops in flight there; `second` starts then, and the row is released
    /// once `second` has finished or waits too.
    protected async Task<(TFirst First, TSecond Second)> RaceAsync<TFirst, TSecond>(Func<IServiceProvider, Task> hold,
        Func<Task<TFirst>> first, Func<Task<TSecond>> second)
    {
        Task<TFirst> firstTask;
        Task<TSecond> secondTask;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await using var transaction = await db.Database.BeginTransactionAsync();
            await hold(scope.ServiceProvider);

            firstTask = first();
            await WaitForBlockedAsync(firstTask, count: 1);
            firstTask.IsCompleted.Should().BeFalse("the first request waits for the held row");
            secondTask = second();
            await WaitForBlockedAsync(secondTask, count: 2);
            await transaction.RollbackAsync();
        }
        return (await firstTask, await secondTask);
    }

    /// Waits until `task` has finished or `count` backends of this test's database wait on a lock. Polled through
    /// new connections: pg_stat_activity stays frozen inside a transaction.
    protected async Task WaitForBlockedAsync(Task task, int count)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (!task.IsCompleted && await InDbAsync(db => db.Database.SqlQuery<int>(
                   $"SELECT count(*)::int AS \"Value\" FROM pg_stat_activity WHERE datname = current_database() AND cardinality(pg_blocking_pids(pid)) > 0")
                   .SingleAsync()) < count)
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException($"Expected {count} request(s) waiting on a lock.");
            await Task.Delay(20);
        }
    }

    /// Locks a row in the race's transaction. FOR NO KEY UPDATE: an UPDATE of the row waits, a foreign-key check
    /// does not.
    protected static Func<IServiceProvider, Task> HoldRow(string table, Guid id) => sp =>
        sp.GetRequiredService<AppDbContext>().Database.ExecuteSqlRawAsync(
            "SELECT 1 FROM " + table + " WHERE id = {0} FOR NO KEY UPDATE", id);

    /// Takes the branch's next `docType` number in the transaction that holds a race: a create of that type then
    /// waits on the counter row, the last lock of its save (P14).
    protected static Func<IServiceProvider, Task> HoldNextCode(DocumentType docType, Guid branchId) => sp =>
        sp.GetRequiredService<IDocumentCodeAllocator>().NextAsync(docType, branchId, Vn("2026-10-05 09:00"),
            peek: false, (_, _) => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>()));
}
