# Phase 04 — Debt ledger, locks, credit-limit guard & opening debts

**Status:** [x] complete
**Complexity:** XL

## Objective

Build the debt engine and its first source:

- `DebtEntry` / `DebtSettlement` schema;
- partner/fund advisory locks (P14);
- `IDebtLedgerService`: post in place, remove a source, add/remove settlements with confirmations (P10, P11, P21);
- `IDebtQueryService`: balances and open items;
- settlement-removal activity logging;
- `ICreditLimitGuard`;
- the invariant helper;
- opening debts per legacy document, including moving their entries when the branch opening date changes.

The fund-balance guard and the fund opening balance come in Phase 05, together with the first fund movements.

## Files

- `backend/src/OrderMgmt.Domain/Enums/CashDebtEnums.cs` (modify — `DebtSourceType`, `SettlementOrigin`, `OpeningDebtKind`)
- `backend/src/OrderMgmt.Domain/Enums/InventoryEnums.cs` (modify — `StockVoucherActivityAction.SettlementRemoved = 6`, `AutoVoucherChanged = 7`)
- `backend/src/OrderMgmt.Domain/Entities/CashDebt/{DebtEntry,DebtSettlement,OpeningDebt}.cs` (new)
- `backend/src/OrderMgmt.Application/CashDebt/Interfaces/ICashDebtLock.cs` (new)
- `backend/src/OrderMgmt.Application/CashDebt/Ledger/{IDebtLedgerService,DebtLedgerService,IDebtQueryService,DebtQueryService,IDebtActivityLog,DebtActivityLog,DebtLedgerModels}.cs` (new)
- `backend/src/OrderMgmt.Application/CashDebt/Guards/{ICreditLimitGuard,CreditLimitGuard}.cs` (new)
- `backend/src/OrderMgmt.Application/CashDebt/OpeningDebts/{Interfaces,Models,Services,Validators}/*` (new)
- `backend/src/OrderMgmt.Application/Organization/Branches/Services/BranchService.cs` (modify — opening-date change re-posts opening debts)
- `backend/src/OrderMgmt.Application/DependencyInjection.cs`, `backend/src/OrderMgmt.Infrastructure/DependencyInjection.cs` (modify)
- `backend/src/OrderMgmt.Infrastructure/Persistence/AdvisoryLocks.cs` (new — extracted helper)
- `backend/src/OrderMgmt.Infrastructure/Inventory/PostgresInventoryLock.cs` (modify — use the helper)
- `backend/src/OrderMgmt.Infrastructure/CashDebt/PostgresCashDebtLock.cs` (new)
- `backend/src/OrderMgmt.Infrastructure/Persistence/Configurations/CashDebtConfiguration.cs` (modify)
- `backend/src/OrderMgmt.Infrastructure/Persistence/Migrations/<ts>_{AddDebtLedger,AddOpeningDebts}.cs` (generated)
- `backend/src/OrderMgmt.WebApi/Controllers/OpeningDebtsController.cs` (new)
- `backend/tests/OrderMgmt.IntegrationTests/CashDebt/CashDebtTestBase.cs` (modify — engine helpers, invariants)
- `backend/tests/OrderMgmt.IntegrationTests/CashDebt/{DebtSchemaTests,CashDebtLockTests,DebtLedgerPostTests,DebtSettlementTests,DebtQueryTests,CreditLimitGuardTests,OpeningDebtTests}.cs` (new)

## Reference files (read-only)

- `backend/src/OrderMgmt.Infrastructure/Inventory/PostgresInventoryLock.cs` — the single-statement lock SQL
- `backend/src/OrderMgmt.Application/Inventory/Ledger/InventoryLedgerService.cs` — derived-data write style
- `backend/tests/OrderMgmt.IntegrationTests/Inventory/{InventoryLockTests,OpeningStockLargeGridTests,InventoryEngineTestBase}.cs` — lock tests, lock counting, `InTransactionAsync`

## Data model

```csharp
public enum DebtSourceType { Opening = 0, StockIn = 1, StockOut = 2, Receipt = 3, Payment = 4, Offset = 5 }
public enum SettlementOrigin { AtVoucher = 1, Manual = 2, Offset = 3, Auto = 4 }
public enum OpeningDebtKind { Debt = 1, Prepayment = 2 }

public class DebtEntry            // not BaseEntity; hard-deleted
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BranchId { get; set; }
    public Guid PartnerId { get; set; }
    public DebtSide Side { get; set; }
    public DebtDirection Direction { get; set; }
    public decimal Amount { get; set; }            // numeric(18,2), CHECK amount > 0
    public DateTimeOffset PostedAt { get; set; }   // timestamptz, UTC
    public DateOnly DocDate { get; set; }
    public DateOnly? DueDate { get; set; }         // Increase: always set (DebtDates.EntryDueDate); Decrease: null
    public Guid? OwnerUserId { get; set; }
    public DebtSourceType SourceType { get; set; }
    public Guid SourceId { get; set; }
    public string DocCode { get; set; } = default!; // max 50
    public string? Description { get; set; }        // max 500 (P16)
    public DateTimeOffset UpdatedAt { get; set; }
}
// table debt_entries; unique (source_type, source_id, side); index (branch_id, partner_id, side, posted_at);
// FK branch/partner Restrict.

public class DebtSettlement       // not BaseEntity; hard-deleted
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid IncreaseEntryId { get; set; }      // FK debt_entries Restrict
    public Guid DecreaseEntryId { get; set; }      // FK debt_entries Restrict
    public decimal Amount { get; set; }            // CHECK amount > 0
    public SettlementOrigin Origin { get; set; }
    public Guid BatchId { get; set; }
    public DateTimeOffset EffectiveAt { get; set; } // max(PostedAt of both entries)
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
}
// table debt_settlements; indexes (increase_entry_id), (decrease_entry_id), (batch_id).

public class OpeningDebt : BaseEntity
{
    public Guid BranchId { get; set; }
    public Guid PartnerId { get; set; }  public Customer? Partner { get; set; }
    public DebtSide Side { get; set; }
    public OpeningDebtKind Kind { get; set; }
    public string DocNo { get; set; } = default!;  // max 50
    public DateOnly DocDate { get; set; }
    public DateOnly? DueDate { get; set; }         // Debt only
    public decimal Amount { get; set; }            // > 0
    public Guid? OwnerUserId { get; set; }
    public string? Note { get; set; }              // max 500
}
// table opening_debts; index (branch_id, partner_id); query filter !IsDeleted.
```

## Ledger contracts

```csharp
public sealed record DebtEntryDraft(Guid BranchId, Guid PartnerId, DebtSide Side, DebtDirection Direction,
    decimal Amount, DateTimeOffset PostedAt, DateOnly DocDate, DateOnly? DueDate, Guid? OwnerUserId,
    string DocCode, string? Description);
public sealed record SettlementDraft(Guid IncreaseEntryId, Guid DecreaseEntryId, decimal Amount);
public sealed record RemovedSettlement(Guid SettlementId, Guid IncreaseEntryId, Guid DecreaseEntryId, decimal Amount, SettlementOrigin Origin);

public interface IDebtLedgerService
{
    /// Save path (P21, P11). At most one draft per side (else ArgumentException). Upserts in place by
    /// (SourceType, SourceId, Side); a side missing from drafts is deleted.
    Task<IReadOnlyList<DebtEntry>> PostAsync(DebtSourceType sourceType, Guid sourceId,
        IReadOnlyList<DebtEntryDraft> drafts, bool confirmRemoveSettlements, CancellationToken ct = default);
    /// Cancel/delete path: removes every entry of the source after removing its settlements (needs confirmation).
    Task RemoveSourceAsync(DebtSourceType sourceType, Guid sourceId, bool confirmRemoveSettlements, CancellationToken ct = default);
    /// Silently removes settlements of one origin attached to the source's entries (no confirmation, no activity).
    Task RemoveOwnSettlementsAsync(DebtSourceType sourceType, Guid sourceId, SettlementOrigin origin, CancellationToken ct = default);
    /// Validates and inserts settlements. Validation errors use keys "{errorKeyPrefix}[i].amount" / "…increaseEntryId" / "…decreaseEntryId".
    Task<IReadOnlyList<DebtSettlement>> AddSettlementsAsync(IReadOnlyList<SettlementDraft> drafts, SettlementOrigin origin,
        Guid batchId, string errorKeyPrefix, CancellationToken ct = default);
    /// Removes settlements and logs SettlementRemoved on both documents.
    Task<IReadOnlyList<RemovedSettlement>> RemoveSettlementsAsync(IReadOnlyCollection<Guid> settlementIds, CancellationToken ct = default);
    /// Σ settlement amounts per entry, optionally excluding one origin.
    Task<IReadOnlyDictionary<Guid, decimal>> SettledAmountsAsync(IEnumerable<Guid> entryIds, SettlementOrigin? excludeOrigin = null, CancellationToken ct = default);
}

public interface IDebtActivityLog
{
    /// Adds (no save) a "SettlementRemoved" activity to the source document of `entry`:
    /// StockIn/StockOut → StockVoucherActivity; Opening → nothing. Receipt/Payment (Phase 05) and Offset (Phase 08) extend it.
    Task SettlementRemovedAsync(DebtEntry entry, DebtEntry other, decimal amount, CancellationToken ct = default);
}
```

**`PostAsync` rules per side**: E = existing entry, D = draft, S = Σ settlements of E.

| E | D | Rule |
|---|---|---|
| — | — | nothing |
| — | D | insert |
| E | — | S > 0 → `ConflictException("DEBT_HAS_SETTLEMENTS", "Chứng từ đã được đối trừ, hãy bỏ đối trừ trước.")`; else delete E |
| E | D | `BranchId`, `PartnerId` or `Direction` changes and S > 0 → 409 `DEBT_HAS_SETTLEMENTS`. `D.Amount < S` → every settlement of E is **pending removal**. Then copy every field of D onto E (`UpdatedAt = now`). If `PostedAt` changed, set `EffectiveAt = max(PostedAt)` on E's remaining settlements. |

- After both sides are evaluated, if anything is pending removal and `!confirmRemoveSettlements`:
  - throw `ConfirmationRequiredException("DEBT_SETTLEMENTS_WILL_BE_REMOVED", "Chứng từ đã được đối trừ. Lưu sẽ bỏ các đối trừ sau.", details, canConfirm: true)`;
  - `details` key = the other entry's `DocCode`; value = `["Bỏ đối trừ {amount:N0} với {E.DocCode}"]`.
- Otherwise remove them through the same path as `RemoveSettlementsAsync` (activity on both documents), then save.
- `RemoveSourceAsync` uses the same confirmation and details for all settlements of the source's entries.

**`AddSettlementsAsync` rules**: errors are collected into one `ValidationDomainException`.
- Both entries exist, share `BranchId`, `PartnerId` and `Side`. The first is `Increase` and the second `Decrease`.
- `Amount > 0`.
- Per entry, Σ (existing + new) ≤ `Amount`.
- `EffectiveAt = max(PostedAt)` must be after the branch `LockedUntil` (`PeriodLockGuard`).
- `CreatedAt = IDateTime.UtcNow`, `CreatedBy = ICurrentUser.UserId`.

## Tasks

### Task 4.1 — Debt schema

1. **Write the failing tests** `CashDebt/DebtSchemaTests.cs`:
   - `Entry_unique_per_source_and_side` (two entries with the same `(SourceType, SourceId, Side)` → `DbUpdateException`)
   - `Amount_check_constraints` (an entry with 0 → `DbUpdateException`; a settlement with 0 → `DbUpdateException`)
   - `Settlement_fk_restricts_entry_delete`
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~DebtSchemaTests"`. Expected: FAIL (compile).
3. **Write the minimal implementation:**
   - enums, entities;
   - configurations: `HasCheckConstraint("ck_debt_entries_amount_positive", "amount > 0")` and the same for settlements;
   - DbSets `DebtEntries`, `DebtSettlements` on `IAppDbContext`/`AppDbContext`;
   - migration `AddDebtLedger`;
   - the two new `StockVoucherActivityAction` values.
4. **Run tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git commit -m "feat(debt): debt entry and settlement schema"`

### Task 4.2 — Partner and fund advisory locks

```csharp
// Application/CashDebt/Interfaces/ICashDebtLock.cs
public interface ICashDebtLock
{
    /// Partner keys "debt-partner:{branchId:N}:{partnerId:N}", then fund keys "cash-fund:{fundId:N}".
    /// One statement per non-empty key set (P14). Throws InvalidOperationException without a transaction.
    Task AcquireAsync(Guid branchId, IEnumerable<Guid> partnerIds, IEnumerable<Guid> fundIds, CancellationToken ct = default);
}

// Infrastructure/Persistence/AdvisoryLocks.cs (extracted from PostgresInventoryLock)
internal static class AdvisoryLocks
{
    public static Task AcquireAsync(DbContext db, IEnumerable<string> keys, bool shared, CancellationToken ct);
}
```

1. **Write the failing tests** `CashDebt/CashDebtLockTests.cs`. Copy the 55P03 / `SET LOCAL lock_timeout = '300ms'` approach from `InventoryLockTests`.
   - `Partner_key_blocks_same_partner_and_branch_only` (A holds partner P in branch 1; B gets 55P03 for P in branch 1, and succeeds for P in branch 2 and for partner Q)
   - `Fund_key_blocks_same_fund`
   - `One_lock_per_key_and_requires_transaction`:
     - inside `InTransactionAsync`, acquire 3 partners + 2 funds → `pg_locks` advisory count for the backend = 5;
     - calling outside a transaction → `InvalidOperationException`.
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~CashDebtLockTests"`. Expected: FAIL (compile).
3. **Write the minimal implementation:**
   - extract `AdvisoryLocks.AcquireAsync` (same SQL: `SELECT pg_advisory_xact_lock[_shared](hashtextextended(k, 0)) FROM unnest(@keys::text[]) AS k ORDER BY k COLLATE "C"`);
   - make `PostgresInventoryLock` use it;
   - add `PostgresCashDebtLock`; DI registration.
   - Move `InTransactionAsync` into `CashDebtTestBase` by copying the `InventoryEngineTestBase` implementation (the class chains differ).
4. **Run tests to verify they pass:** `--filter "FullyQualifiedName~CashDebtLockTests|FullyQualifiedName~InventoryLockTests|FullyQualifiedName~OpeningStockLargeGridTests"`. Expected: PASS.
5. **Commit:** `git commit -m "feat(debt): partner and fund advisory locks with shared lock helper"`

### Task 4.3 — `PostAsync` in place, and the invariant helper

`CashDebtTestBase` gains:
- `Task<IReadOnlyList<DebtEntry>> EntriesOfAsync(DebtSourceType type, Guid sourceId)`
- `Task<IReadOnlyList<DebtSettlement>> SettlementsOfAsync(Guid entryId)`
- `static DebtEntryDraft Draft(Guid partnerId, DebtSide side, DebtDirection dir, decimal amount, string at, string code = "CT", Guid? branchId = null, DateOnly? dueDate = null)` (branch defaults to `MainBranchId`; `at` goes through `Vn(...)`)
- `Task AssertCashDebtInvariantsAsync()`, which checks for now:
  1. per entry, Σ settlements ≤ `Amount`;
  2. each settlement links an Increase and a Decrease entry with the same branch, partner and side;
  3. `EffectiveAt == max(PostedAt)` of its two entries;
  4. no two entries share `(SourceType, SourceId, Side)`.

  Phases 04–08 add source checks to it.

1. **Write the failing tests** `CashDebt/DebtLedgerPostTests.cs : CashDebtTestBase`. Each test ends with `AssertCashDebtInvariantsAsync()`. Sources use `DebtSourceType.Opening` with fresh Guids (no activity is written for Opening).
   - `Insert_update_in_place_and_delete_side` (post R+ 1,000 → 1 row; post R+ 1,500 → same `Id`, amount 1,500; post with no drafts → row deleted)
   - `Two_sides_for_one_source` (R− 300 and P− 300 for one source → 2 rows)
   - `Changing_posted_at_recomputes_effective_at`:
     - X = R+ 1,000 @10-01; Y = R− 400 @10-03;
     - settlement X↔Y → `EffectiveAt` 10-03;
     - re-post X @10-05 → `EffectiveAt` 10-05.
   - `Amount_below_settled_needs_confirmation`:
     - with the settlement above, re-post X with 300 → 422 `DEBT_SETTLEMENTS_WILL_BE_REMOVED`, details key = Y's `DocCode`, nothing changed;
     - with confirm → the settlement is gone and X = 300.
   - `Amount_above_settled_keeps_settlements` (re-post X with 600 → 200, settlement kept)
   - `Partner_or_direction_change_with_settlements_is_409` (change partner → `DEBT_HAS_SETTLEMENTS`; change direction → same)
   - `Removing_a_settled_side_on_save_is_409`
   - `Two_drafts_for_one_side_throw_argument_exception`
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~DebtLedgerPostTests"`. Expected: FAIL.
3. **Write the minimal implementation:**
   - `DebtLedgerService.PostAsync` with the rules table, the private `RemoveCoreAsync`, and `AddSettlementsAsync` (needed by the test setup, without the validation rules of Task 4.4: inserts only);
   - `IDebtActivityLog` with the stock-voucher arm: activity `SettlementRemoved` with description `"Bỏ đối trừ {amount:N0} với {other.DocCode}"`;
   - DI. (`ConflictException(code, message)` exists since Phase 01 Task 1.5.)
4. **Run tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git commit -m "feat(debt): post debt entries in place with settlement confirmations"`

### Task 4.4 — Settlement add/remove, own-origin removal, source removal

1. **Write the failing tests** `CashDebt/DebtSettlementTests.cs`:
   - `Add_validates_pairs_amounts_and_open_balance`. Each case → 400 with the key `items[i].…`:
     - two Increase entries;
     - different partner;
     - different side;
     - different branch;
     - amount 0;
     - Σ over the Increase entry's amount.
   - `Add_rejects_effective_at_in_locked_period` (lock 10-04; X @10-01, Y @10-03 → `PERIOD_LOCKED`; Y @10-05 → 200)
   - `Remove_settlements_logs_activity_on_both_stock_vouchers`:
     - create a stock-out and a stock-in voucher through the API (Round 1 helpers);
     - post entries for them with `DebtSourceType.StockOut` / `StockIn` and their real ids, using the same partner and side so a settlement is possible (R+ on the stock-out, R− on the stock-in);
     - settle, then `RemoveSettlementsAsync` → each voucher has a `SettlementRemoved` activity.
   - `Remove_own_settlements_only_touches_that_origin` (Auto + Manual on the same entry → `RemoveOwnSettlementsAsync(…, Auto)` leaves Manual)
   - `Remove_source_requires_confirmation_when_settled` (422 without confirm; with confirm → entries and settlements gone)
   - `Settled_amounts_can_exclude_an_origin`
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~DebtSettlementTests"`. Expected: FAIL.
3. **Write the minimal implementation:** the validation rules in `AddSettlementsAsync` (branch `LockedUntil` read once per call), `RemoveSettlementsAsync`, `RemoveOwnSettlementsAsync`, `RemoveSourceAsync`, `SettledAmountsAsync`.
4. **Run tests to verify they pass:** `--filter "FullyQualifiedName~DebtSettlementTests|FullyQualifiedName~DebtLedgerPostTests"`. Expected: PASS.
5. **Commit:** `git commit -m "feat(debt): add and remove settlements with validation, period lock and activity log"`

### Task 4.5 — `IDebtQueryService`: balance and open items

```csharp
public sealed class OpenItemsQuery
{
    public Guid BranchId { get; set; }
    public Guid PartnerId { get; set; }
    public DebtSide Side { get; set; }
    public DebtDirection? Direction { get; set; }
    public DebtSourceType? ForSourceType { get; set; }  // P26
    public Guid? ForSourceId { get; set; }
}
public sealed class OpenItemDto
{
    public Guid EntryId { get; set; }  public DebtSourceType SourceType { get; set; }  public Guid SourceId { get; set; }
    public string DocCode { get; set; } = default!;  public DateOnly DocDate { get; set; }  public DateOnly? DueDate { get; set; }
    public DateTimeOffset PostedAt { get; set; }  public DebtDirection Direction { get; set; }
    public decimal Amount { get; set; }  public decimal Settled { get; set; }  public decimal Open { get; set; }
    public string? Description { get; set; }  public Guid? OwnerUserId { get; set; }
}
public interface IDebtQueryService
{
    /// Σ Increase − Σ Decrease with PostedAt <= at (all when null).
    Task<decimal> BalanceAsync(Guid branchId, Guid partnerId, DebtSide side, DateTimeOffset? at = null, CancellationToken ct = default);
    /// Entries with Open > 0. The source's own entries are excluded; with ForSource, settlements against the
    /// source's own entries are added back to Open. Ordered by (DueDate ?? DocDate), DocDate, DocCode.
    Task<IReadOnlyList<OpenItemDto>> OpenItemsAsync(OpenItemsQuery query, CancellationToken ct = default);
}
```

1. **Write the failing tests** `CashDebt/DebtQueryTests.cs`:
   - `Balance_at_date_and_all_time`:
     - R+ 1,000 @10-01; R− 300 @10-03; R+ 500 @10-05;
     - at 10-04 → 700; all time → 1,200;
     - Payable side → 0.
   - `Open_items_order_and_direction_filter`:
     - three R+ entries with due dates 10-20, 10-10 and none (doc date 10-15);
     - a settlement of 200 on the 10-10 entry;
     - the order is 10-10, 10-15, 10-20, and the 10-10 entry shows `Settled` 200;
     - a Decrease filter returns only Decrease rows.
   - `For_source_adds_back_its_own_settlements` (receipt-like source S with R− 500 settled 500 against X → without ForSource, X open = Amount − 500 and S is not listed as open; with ForSource S, X open = Amount)
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~DebtQueryTests"`. Expected: FAIL.
3. **Write the minimal implementation:** `DebtQueryService` with EF queries (UTC `at`), DI.
4. **Run tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git commit -m "feat(debt): debt balance and open-item queries"`

### Task 4.6 — `ICreditLimitGuard`

```csharp
public interface ICreditLimitGuard
{
    /// Receivable balance of the customer in the branch, all dates (read after the partner lock).
    Task<decimal> CaptureAsync(Guid branchId, Guid partnerId, CancellationToken ct = default);
    /// Reads CreditLimit (branch, customer) and CashDebtSettings.CreditLimitPolicy; after = current balance.
    /// Uses CreditLimitRule + LimitPolicyDecision; Warn → ConfirmationRequiredException("CREDIT_LIMIT_WARNING", …, canConfirm: true),
    /// Block → ("CREDIT_LIMIT_BLOCKED", …, canConfirm: false). Details key "creditLimit":
    /// ["Hạn mức {limit:N0}, dư nợ sau khi lưu {after:N0} (trước {before:N0})."]
    Task CheckAsync(Guid branchId, Guid partnerId, decimal before, bool acknowledge, CancellationToken ct = default);
}
```

1. **Write the failing tests** `CashDebt/CreditLimitGuardTests.cs`. Use `InTransactionAsync`: capture, post entries, check.
   - `No_limit_or_allow_policy_passes`
   - `Warn_requires_acknowledgement` (limit 1,000; before 800; post R+ 500 → 422 `CREDIT_LIMIT_WARNING`; with acknowledge → passes)
   - `Block_cannot_be_acknowledged` (policy Block → `CREDIT_LIMIT_BLOCKED` even with acknowledge)
   - `Lowering_debt_never_triggers` (before 1,500 over the limit; after 1,200 → passes)
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~CreditLimitGuardTests"`. Expected: FAIL.
3. **Write the minimal implementation:** `CreditLimitGuard`, DI.
4. **Run tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git commit -m "feat(debt): credit limit guard with allow/warn/block policy"`

### Task 4.7 — Opening debts API

API `OpeningDebtsController` (route `api/opening-debts`, every action `[HasPermission(Permissions.Debt.OpeningBalance)]`, working branch):

| Method | Route | Request → Response |
|---|---|---|
| GET | `/?partnerId=&side=&search=&page=&pageSize=` | `OpeningDebtListResult : PagedResult<OpeningDebtDto> { DateOnly? OpeningDate }` (the working branch's opening date, for the screen header), ordered by partner code, `DocDate` |
| POST | `/` | `UpsertOpeningDebtRequest` → `OpeningDebtDto` |
| PUT | `/{id}` | `UpsertOpeningDebtRequest` → `OpeningDebtDto` |
| DELETE | `/{id}?confirmRemoveSettlements=` | — |

```csharp
public sealed class UpsertOpeningDebtRequest
{
    public Guid PartnerId { get; set; }  public DebtSide Side { get; set; }  public OpeningDebtKind Kind { get; set; }
    public string DocNo { get; set; } = default!;  public DateOnly DocDate { get; set; }  public DateOnly? DueDate { get; set; }
    public decimal Amount { get; set; }  public Guid? OwnerUserId { get; set; }  public string? Note { get; set; }
    public bool ConfirmRemoveSettlements { get; set; }
}
public sealed class OpeningDebtDto
{
    public Guid Id { get; set; }  public Guid PartnerId { get; set; }  public string PartnerCode { get; set; } = default!;
    public string PartnerName { get; set; } = default!;  public DebtSide Side { get; set; }  public OpeningDebtKind Kind { get; set; }
    public string DocNo { get; set; } = default!;  public DateOnly DocDate { get; set; }  public DateOnly? DueDate { get; set; }
    public decimal Amount { get; set; }  public decimal Settled { get; set; }  public Guid? OwnerUserId { get; set; }
    public string? OwnerName { get; set; }  public string? Note { get; set; }
}
```

**Write flow.** Inside `ITransactionRunner`:
1. Shared branch gate.
2. `ICashDebtLock.AcquireAsync(branch, {old partner, new partner}, [])`.
3. Read the branch: `OpeningDate` null → 400 `OPENING_DATE_NOT_SET`; then `PeriodLockGuard` on `OpeningDate`.
4. Validate, collecting errors into one `ValidationDomainException`:
   - `docDate` ≤ `OpeningDate` ("Ngày chứng từ phải trước hoặc bằng ngày đầu kỳ.");
   - `dueDate` must be null for Prepayment;
   - `amount` > 0;
   - `partnerId`: exists and has the role of `Side` (`DebtEffectRules.PartnerHasSideRole`);
   - `docNo` NotEmpty, max 50;
   - `ownerUserId` exists if set.
5. Save the row, then `PostAsync(DebtSourceType.Opening, row.Id, [draft], request.ConfirmRemoveSettlements)`. The draft:
   - Direction: Debt → Increase, Prepayment → Decrease;
   - `PostedAt = VnTime.StartOfDay(OpeningDate)`;
   - `DueDate = DebtDates.EntryDueDate(...)`;
   - `DocCode = DocNo`, `Description = Note ?? "Nợ đầu kỳ"`, owner.

Delete soft-deletes the row and calls `RemoveSourceAsync`.

Add `IOpeningDebtService.RepostBranchAsync(Guid branchId, CancellationToken ct)`. It re-posts every row of the branch with the new `PostedAt` (the update in place recomputes `EffectiveAt`). `BranchService.SetOpeningDateAsync` calls it after `RepostBranchAsync` of opening stock. Clearing the date while opening debts exist → 409 `OPENING_DATA_EXISTS`, like opening stock.

Extend `AssertCashDebtInvariantsAsync`: every non-deleted `OpeningDebt` has exactly one entry with matching fields, and every `Opening` entry has a non-deleted row.

1. **Write the failing tests** `CashDebt/OpeningDebtTests.cs`:
   - `Create_list_update_delete` (branch date 10-01; receivable Debt `HD1` 5,000,000, doc date 09-15, due 09-30 → entry R+ at `StartOfDay(10-01)`, `DueDate` 09-30, `DocCode` `HD1`; update amount → same entry id; delete → entry gone)
   - `Prepayment_posts_a_decrease_and_rejects_due_date`
   - `Validation_rules` (no branch date → `OPENING_DATE_NOT_SET`; doc date after opening date → `docDate`; supplier-only partner on Receivable → `partnerId`; locked opening date → `PERIOD_LOCKED`)
   - `Settled_opening_debt_needs_confirmation_to_shrink`:
     - a Debt 1,000 and a Prepayment 400 for the same partner, settled 400;
     - update the Debt to 300 → 422;
     - with `confirmRemoveSettlements` → 200 and the settlement removed.
   - `Changing_branch_opening_date_moves_opening_entries` (branch B without documents: opening debt → PUT opening date 09-01 → entry `PostedAt == StartOfDay(09-01)`, and settlement `EffectiveAt` follows)
   - `Requires_permission` (SALES → 403)
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~OpeningDebtTests"`. Expected: FAIL.
3. **Write the minimal implementation:** entity, config, DbSet, migration `AddOpeningDebts`, service, validator (shape only), controller, DI, `BranchService` call, invariant extension.
4. **Run tests to verify they pass:** `--filter "FullyQualifiedName~OpeningDebtTests|FullyQualifiedName~BranchOpeningDateTests"`. Expected: PASS.
5. **Commit:** `git commit -m "feat(debt): opening debts per legacy document posted at the branch opening date"`

## Verification

```bash
cd backend
dotnet build OrderMgmt.sln
dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~CashDebt|FullyQualifiedName~InventoryLockTests"
dotnet test OrderMgmt.sln   # compare with BASELINE.md
```

## Exit Criteria

- `DebtEntry`/`DebtSettlement` exist. Entries are updated in place, and settlements follow P10/P11/P21.
- Partner/fund locks are taken in one statement each, with lock-count and blocking tests.
- Balance and open-item queries are correct, including P26.
- The credit-limit guard implements the three policies with before/after.
- The opening debt API posts at the branch opening date and moves with it.
- `AssertCashDebtInvariantsAsync` exists and passes in every test of this phase.
