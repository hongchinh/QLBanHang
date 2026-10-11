# Phase 05 — Cash voucher API

**Status:** [x] complete
**Complexity:** XL

## Objective

Deliver manual receipts and payments end to end on the backend:

- schema;
- shared document-code allocator, extracted from `StockVoucherService`;
- fund-balance guard and fund opening balance;
- create/update/cancel/restore/delete with ownership, `xmin` concurrency, period lock, debt posting, allocation at voucher time (only `AtVoucher` settlements are replaced on edit, C8), settlement confirmations and the negative-fund policy;
- list, get, activities, defaults and partner search;
- in-use locks for reasons and funds;
- the branch "has documents" check;
- the protection of vouchers managed by a stock voucher (`MANAGED_BY_STOCK_VOUCHER`).

## Files

- `backend/src/OrderMgmt.Domain/Enums/CashDebtEnums.cs` (modify — `DocumentStatus`, `DocumentStatusFilter`, `CashDocumentActivityAction`)
- `backend/src/OrderMgmt.Domain/Entities/CashDebt/{CashVoucher,CashVoucherLine,CashDocumentActivity}.cs` (new)
- `backend/src/OrderMgmt.Application/Inventory/Numbering/{IDocumentCodeAllocator,DocumentCodeAllocator}.cs` (new — extracted)
- `backend/src/OrderMgmt.Application/Inventory/StockVouchers/Services/StockVoucherService.cs` (modify — use the allocator)
- `backend/src/OrderMgmt.Application/CashDebt/Guards/{IFundBalanceGuard,FundBalanceGuard}.cs` (new)
- `backend/src/OrderMgmt.Application/CashDebt/Funds/**` (modify — opening balance, in-use check)
- `backend/src/OrderMgmt.Application/CashDebt/CashReasons/Services/CashReasonService.cs` (modify — in-use lock)
- `backend/src/OrderMgmt.Application/CashDebt/CashVouchers/{Interfaces,Models,Services,Validators}/*` (new — incl. `CashVoucherPermissions`)
- `backend/src/OrderMgmt.Application/CashDebt/Ledger/DebtActivityLog.cs` (modify — Receipt/Payment arm)
- `backend/src/OrderMgmt.Application/Organization/Branches/Services/BranchService.cs` (modify — `HasDocumentsAsync` adds cash vouchers)
- `backend/src/OrderMgmt.Infrastructure/Persistence/Configurations/CashDebtConfiguration.cs` (modify)
- `backend/src/OrderMgmt.Infrastructure/Persistence/Migrations/<ts>_AddCashVouchers.cs` (generated)
- `backend/src/OrderMgmt.WebApi/Controllers/{CashVouchersController.cs (new), FundsController.cs (modify)}`
- `backend/tests/OrderMgmt.IntegrationTests/CashDebt/CashDebtTestBase.cs` (modify — voucher helpers, invariants)
- `backend/tests/OrderMgmt.IntegrationTests/CashDebt/CashVouchers/{CashVoucherSchemaTests,FundBalanceGuardTests,FundOpeningBalanceTests,CashVoucherCreateTests,CashVoucherValidationTests,CashVoucherAllocationTests,CashVoucherUpdateTests,CashVoucherLifecycleTests,CashVoucherQueryTests,CashVoucherConcurrencyTests}.cs` (new)

## Reference files (read-only)

- `backend/src/OrderMgmt.Application/Inventory/StockVouchers/Services/StockVoucherService.cs` — `LoadForWriteAsync`, `ChangeLifecycleAsync`, `NextCodeAsync`, `AddActivity`, list/query
- `backend/src/OrderMgmt.Application/Inventory/StockVouchers/Models/StockVoucherDtos.cs`
- `backend/tests/OrderMgmt.IntegrationTests/Inventory/StockVouchers/*` — test shapes to mirror

## Data model

```csharp
public enum DocumentStatus { Active = 1, Cancelled = 9 }
public enum DocumentStatusFilter { Active = 1, Cancelled = 9, All = 0 }
public enum CashDocumentActivityAction { Created = 1, Updated = 2, Cancelled = 3, Restored = 4, Deleted = 5, SettlementRemoved = 6 }

public class CashVoucher : BaseEntity
{
    public CashVoucherType Type { get; set; }
    public string Code { get; set; } = default!;
    public DateTimeOffset VoucherAt { get; set; }
    public Guid BranchId { get; set; }  public Branch? Branch { get; set; }
    public Guid FundId { get; set; }  public Fund? Fund { get; set; }
    public Guid ReasonId { get; set; }  public CashReason? Reason { get; set; }
    public Guid? PartnerId { get; set; }  public Customer? Partner { get; set; }
    public string? PartnerName { get; set; }  public string? PartnerAddress { get; set; }  public string? PartnerTaxCode { get; set; }
    public string? PersonName { get; set; }      // người nộp / người nhận
    public string? Description { get; set; }     // lý do nộp / lý do chi (max 500)
    public string? AttachedDocs { get; set; }    // kèm theo (max 255)
    public string? Note { get; set; }
    public decimal Total { get; set; }           // Σ lines, numeric(18,2)
    public DocumentStatus Status { get; set; } = DocumentStatus.Active;
    public DateTimeOffset? CancelledAt { get; set; }  public Guid? CancelledBy { get; set; }
    public Guid OwnerUserId { get; set; }  public User? Owner { get; set; }
    public Guid? SourceStockVoucherId { get; set; }  public StockVoucher? SourceStockVoucher { get; set; }
    public uint Version { get; set; }            // xmin
    public ICollection<CashVoucherLine> Lines { get; set; } = new List<CashVoucherLine>();
}
// table cash_vouchers; unique (type, branch_id, code) filtered is_deleted = false; index (branch_id, type, voucher_at);
// index (fund_id, voucher_at); unique (source_stock_voucher_id) filtered is_deleted = false AND source_stock_voucher_id IS NOT NULL.

public class CashVoucherLine : BaseEntity
{
    public Guid CashVoucherId { get; set; }  public CashVoucher? CashVoucher { get; set; }
    public int SortOrder { get; set; }
    public string Description { get; set; } = default!;   // max 500
    public decimal Amount { get; set; }                   // > 0
    public string? Note { get; set; }
}

public class CashDocumentActivity     // not BaseEntity, append-only
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DocumentType DocumentType { get; set; }        // Receipt, Payment, FundTransfer, DebtOffset
    public Guid DocumentId { get; set; }
    public CashDocumentActivityAction Action { get; set; }
    public Guid? ActorUserId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public string Description { get; set; } = default!;
    public string? MetadataJson { get; set; }             // jsonb
}
// table cash_document_activities; index (document_type, document_id, occurred_at).
```

## API contract

Route `api/cash-vouchers`, `[Authorize]`. Permissions are checked in the service through `CashVoucherPermissions.{View,Create,Edit,Delete,Cancel,EditAll}(CashVoucherType)`, which returns `receipts.*` or `payments.*`.

| Method | Route | Request → Response |
|---|---|---|
| GET | `/?type=&page=&pageSize=&from=&to=&fundId=&reasonId=&partnerId=&search=&status=&ownerIds=` | `CashVoucherListRequest` → `CashVoucherListResult` |
| GET | `/defaults?type=` | `CashVoucherDefaultsDto` |
| GET | `/partners?type=&keyword=&reasonId=&limit=20` | `List<CustomerSearchItemDto>` |
| GET | `/{id}` | `CashVoucherDto` |
| GET | `/{id}/activities` | `IReadOnlyList<CashDocumentActivityDto>` |
| POST | `/` | `UpsertCashVoucherRequest` → `CashVoucherDto` |
| PUT | `/{id}` | `UpsertCashVoucherRequest` → `CashVoucherDto` |
| POST | `/{id}/cancel` | `CashDocumentActionRequest` → `CashVoucherDto` |
| POST | `/{id}/restore` | `CashDocumentActionRequest` → `CashVoucherDto` |
| DELETE | `/{id}` | `[FromQuery] CashDocumentActionRequest` |

DTOs below use shorthand field lists; every member is a public `{ get; set; }` auto-property.

```csharp
UpsertCashVoucherRequest { CashVoucherType Type; DateTimeOffset VoucherAt; Guid FundId; Guid ReasonId; Guid? PartnerId;
    string? PartnerName; string? PartnerAddress; string? PartnerTaxCode;   // null → from the partner record
    string? PersonName; string? Description; string? AttachedDocs; string? Note; uint? Version;
    bool ConfirmRemoveSettlements; bool AcknowledgeNegativeFund;
    List<UpsertCashVoucherLineRequest> Lines; List<AllocationRequest>? Allocations; }
UpsertCashVoucherLineRequest { Guid? Id; string Description; decimal Amount; string? Note; }
AllocationRequest { Guid EntryId; decimal Amount; }
CashDocumentActionRequest { uint? Version; bool ConfirmRemoveSettlements; bool AcknowledgeNegativeFund; }

CashVoucherDto { Id; Type; Code; VoucherAt; BranchId; FundId; FundCode; FundName; FundKind; ReasonId; ReasonCode; ReasonName;
    DebtEffect ReasonDebtEffect; PartnerId; PartnerCode; PartnerName; PartnerAddress; PartnerTaxCode; PersonName; Description;
    AttachedDocs; Note; Total; Status; CancelledAt; OwnerUserId; OwnerName; Version; SourceStockVoucherId; SourceStockVoucherCode;
    StockDirection? SourceStockVoucherType; bool IsManaged; bool CanEdit; bool CanCancel; bool CanDelete;
    List<CashVoucherLineDto> Lines; List<CashVoucherAllocationDto> Allocations; }
CashVoucherLineDto { Id; SortOrder; Description; Amount; Note; }
CashVoucherAllocationDto { SettlementId; EntryId; DebtSourceType SourceType; Guid SourceId; DocCode; DocDate; Amount; SettlementOrigin Origin; }
CashVoucherListRequest : PageRequest { CashVoucherType Type; DateOnly? From; DateOnly? To; Guid? FundId; Guid? ReasonId; Guid? PartnerId;
    string? Search; DocumentStatusFilter Status = Active; string? OwnerIds /* comma-separated, like stock vouchers */; }
CashVoucherListResult : PagedResult<CashVoucherListItemDto> { decimal TotalAmount; }
CashVoucherListItemDto { Id; Type; Code; VoucherAt; FundCode; FundName; ReasonName; PartnerCode; PartnerName; PersonName;
    Description; Total; Status; OwnerUserId; OwnerName; SourceStockVoucherId; SourceStockVoucherCode; IsManaged; }
CashVoucherDefaultsDto { NextCode; VoucherAt; Guid? FundId /* default Cash fund */; Guid? ReasonId /* TKH or TNCC */; }
CashDocumentActivityDto { Id; Action; ActorUserId; ActorName; OccurredAt; Description; }
```

## Save rules

Business validation is collected into one `ValidationDomainException`. FluentValidation checks shape only (`UpsertCashVoucherRequestValidator`):
- `Lines` not empty, max 200;
- `Description` NotEmpty, max 500;
- `Amount` > 0 with ≤ 2 decimals;
- `Allocations` max 500.

**Business rules** (keys in parentheses):
1. `reasonId`: exists, `reason.Type == request.Type`, not `IsAuto`.
2. `fundId`: exists, in the working branch, active (an unchanged inactive fund on edit is allowed).
3. `partnerId`:
   - required when `reason.DebtEffect ≠ None`;
   - must be empty when `reason.PartnerType = None`;
   - must match `PartnerType` (Round 1 D4 rules);
   - with `DebtEffect ≠ None`, must have the side role (P19).
4. `allocations`: a **non-empty** list is allowed only when `DebtEffect ≠ None` (else "Lý do này không ảnh hưởng công nợ."). An empty list is always accepted; on update it clears the voucher's `AtVoucher` settlements. Then `AllocationValidator` (Phase 03) runs; it does not use the brainstorm's 422 `ALLOCATION_EXCEEDS_TOTAL`, because this is a plain input error.
5. `type`: on update, must equal the stored type.

**Steps per operation.** Every operation runs in `ITransactionRunner`. "Locks" means:
1. the shared branch gate;
2. `ICashDebtLock.AcquireAsync(branch, partners {old ∪ new}, funds {old ∪ new})`.

The branch row and `CashDebtSettings` are read after the locks.

| Operation | Steps |
|---|---|
| Create | permission `Create(type)` → locks → `PeriodLockGuard(voucherAt)` → rules → `fundCapture = Capture([(fund, voucherAt)])` → build voucher (`Code` via allocator, owner, snapshot, lines via `DbSet.Add`, `Total = Σ`) → save → `PostAsync(src, id, drafts, confirm)` → allocations → `CheckAsync(fundCapture, ack)` → activity Created → save |
| Update | `LoadForWrite(Edit, active)` (409 `MANAGED_BY_STOCK_VOUCHER` if `SourceStockVoucherId != null`) → locks → `PeriodLockGuard(old, new)` → rules → capture old fund from `min(old,new at)` and new fund likewise → if `Allocations != null`: `RemoveOwnSettlementsAsync(src, id, AtVoucher)` → apply header and lines (upsert by line `Id`; removed lines soft-deleted; mark header modified, D29) → save → `PostAsync` → if `Allocations != null`: capacity = `Total − SettledAmounts(own entry)` → validate → `AddSettlementsAsync(…, AtVoucher, Guid.NewGuid(), "allocations")` → fund check → activity Updated → save |
| Cancel | `LoadForWrite(Cancel, active)` (managed → 409) → locks → `PeriodLockGuard(stored)` → capture → `RemoveSourceAsync(confirm)` → `Status = Cancelled`, `CancelledAt/By` → save → fund check → activity Cancelled → save |
| Restore | `LoadForWrite(Cancel, cancelled)` (managed → 409) → locks → `PeriodLockGuard(stored)` → reason/fund/partner not deleted (400) → capture → `Status = Active` → save → `PostAsync` (no settlements come back) → fund check → activity Restored → save |
| Delete | `LoadForWrite(Delete, any status)` (managed → 409) → locks → `PeriodLockGuard(stored)` → capture → if Active: `RemoveSourceAsync(confirm)` → `IsDeleted = true` → save → fund check → activity Deleted → save |

Debt draft for a manual voucher (when `reason.DebtEffect ≠ None`):
- `Side`/`Direction` from `DebtEffectRules.Resolve`;
- `Amount = Total`, `PostedAt = VoucherAt`, `DocDate = VnTime.ToVnDate(VoucherAt)`;
- `DueDate = DebtDates.EntryDueDate(dir, docDate, null)`;
- `OwnerUserId = voucher.OwnerUserId`, `DocCode = Code`, `Description = Description ?? reason.Name`.
- The source type is `Receipt` or `Payment`.

## Tasks

### Task 5.1 — Cash voucher schema and activity table

1. **Write the failing test** `CashDebt/CashVouchers/CashVoucherSchemaTests.cs`:
   - `Code_unique_per_type_and_branch_and_xmin_concurrency`:
     - two Receipts with code `PT00001` in the main branch → `DbUpdateException`;
     - a Payment `PT00001` is allowed;
     - a stale `Version` on update → `DbUpdateConcurrencyException`.
   - `One_active_auto_voucher_per_stock_voucher` (two rows with the same `SourceStockVoucherId` → `DbUpdateException`; after soft-deleting the first, the second inserts)
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~CashVoucherSchemaTests"`. Expected: FAIL (compile).
3. **Write the minimal implementation:** enums, entities, configurations (`Version` `IsRowVersion()`, `numeric(18,2)`, query filters like stock vouchers: lines filter on the parent's `IsDeleted` too), DbSets, migration `AddCashVouchers`.
4. **Run tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git commit -m "feat(cash): cash voucher, line and document activity schema"`

### Task 5.2 — Extract `IDocumentCodeAllocator`

```csharp
public interface IDocumentCodeAllocator
{
    /// Reads DocumentNumbering (400 NUMBERING_NOT_CONFIGURED), computes the period key from the VN date, peeks or
    /// takes the counter, and skips codes already used (same logic as StockVoucherService.NextCodeAsync today).
    Task<string> NextAsync(DocumentType docType, Guid branchId, DateTimeOffset at, bool peek,
        Func<IReadOnlyCollection<string>, CancellationToken, Task<IReadOnlySet<string>>> usedAmong, CancellationToken ct = default);
}
```

This is a pure refactor: move the body of `StockVoucherService.NextCodeAsync` into `DocumentCodeAllocator`; `StockVoucherService` passes a `usedAmong` lambda that queries its own codes.

1. **Write the failing test** `CashDebt/CashVouchers/CashVoucherCreateTests.cs` → `Allocator_numbers_receipts_and_skips_used_codes`:
   - resolve `IDocumentCodeAllocator` in a scope;
   - insert a Receipt row `PT00001` via `InDbAsync`;
   - `NextAsync(Receipt, main, now, peek: false, usedAmong: codes in cash_vouchers of type Receipt)` → `PT00002`.
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~CashVoucherCreateTests"`. Expected: FAIL (compile).
3. **Write the minimal implementation:** interface, class, DI, `StockVoucherService` switched over.
4. **Run tests to verify they pass:** `--filter "FullyQualifiedName~CashVoucherCreateTests|FullyQualifiedName~StockVoucherConcurrencyTests|FullyQualifiedName~StockVoucherCreateTests|FullyQualifiedName~NumberingSettingsTests"`. Expected: PASS.
5. **Commit:** `git commit -m "refactor(inventory): extract document code allocator for reuse by cash documents"`

### Task 5.3 — Fund balance guard and fund opening balance

```csharp
public sealed record FundCapture(IReadOnlyList<FundCaptureItem> Items);
public sealed record FundCaptureItem(Guid FundId, DateTimeOffset From, RunningBalanceResult Before);
public interface IFundBalanceGuard
{
    /// Movements per P17: OpeningAmount, then Active non-deleted cash vouchers (Phase 06 adds fund transfers).
    Task<IReadOnlyList<BalanceMovement>> MovementsAsync(Guid fundId, CancellationToken ct = default);
    /// Same fund listed twice → the earlier From wins.
    Task<FundCapture> CaptureAsync(IEnumerable<(Guid FundId, DateTimeOffset From)> funds, CancellationToken ct = default);
    /// Recomputes each item; RunningBalance.IsWorse + NegativeFundPolicy + LimitPolicyDecision.
    /// Warn → ConfirmationRequiredException("NEGATIVE_FUND_WARNING", "Quỹ sẽ bị âm.", details, true);
    /// Block → ("NEGATIVE_FUND_BLOCKED", "Không đủ tiền trong quỹ.", details, false).
    /// details key = fund Code; value = ["Âm {−min:N0} tại {firstNegativeAt:dd/MM/yyyy HH:mm}"] (VN time).
    Task CheckAsync(FundCapture capture, bool acknowledge, CancellationToken ct = default);
}
```

Fund opening balance:
- Endpoint `PUT api/funds/{id}/opening-balance` `[HasPermission(Permissions.Cash.OpeningBalance)]`, body `SetFundOpeningBalanceRequest { decimal Amount; bool AcknowledgeNegativeFund; }` (Amount ≥ 0) → `FundDto`.
- Flow:
  1. gate + fund lock;
  2. branch `OpeningDate` required (400 `OPENING_DATE_NOT_SET`);
  3. `PeriodLockGuard(OpeningDate)`;
  4. capture from `DateTimeOffset.MinValue`;
  5. set and save;
  6. check.
- Extend `FundService.EnsureNotUsedAsync`: the fund is referenced by a non-deleted cash voucher.

1. **Write the failing tests:**
   - `CashDebt/CashVouchers/FundBalanceGuardTests.cs`. Use `InTransactionAsync`; insert Active payment/receipt rows directly with `InDbAsync`.
     - `Warn_and_block_on_a_payment_that_overdraws` (opening 1,000; insert a payment of 1,500 between capture and check → Warn → 422 `NEGATIVE_FUND_WARNING`, details key `QTM`; acknowledge → passes; Block → `NEGATIVE_FUND_BLOCKED`)
     - `Allow_policy_passes`
     - `Existing_deficit_reduced_passes`
   - `CashDebt/CashVouchers/FundOpeningBalanceTests.cs`:
     - `Set_opening_balance` (branch date set → 200, `openingAmount` updated; SALES → 403; no branch date → `OPENING_DATE_NOT_SET`; locked → `PERIOD_LOCKED`)
     - `Lowering_opening_below_later_payments_warns` (opening 2,000, Active payment 1,500 → set 1,000 → 422 `NEGATIVE_FUND_WARNING`)
     - `Fund_with_vouchers_cannot_be_deleted` (409)
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~FundBalanceGuardTests|FullyQualifiedName~FundOpeningBalanceTests"`. Expected: FAIL.
3. **Write the minimal implementation:** guard, DI, fund service method, controller action, `EnsureNotUsedAsync` extension.
4. **Run tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git commit -m "feat(cash): negative-fund guard and fund opening balance"`

### Task 5.4 — Create receipts and payments (happy path)

`CashDebtTestBase` helpers:
- `Task<Guid> CashReasonIdAsync(string code)`
- `Task<Guid> FundIdAsync(string code, Guid? branchId = null)`
- `static UpsertCashVoucherRequest CashRequest(CashVoucherType type, Guid reasonId, Guid fundId, string at, Guid? partnerId, params decimal[] lineAmounts)` (line descriptions "Dòng 1", "Dòng 2" …)
- `static Task<(HttpStatusCode Status, CashVoucherDto? Voucher, ApiError? Error)> PostCashAsync(HttpClient c, UpsertCashVoucherRequest r)`
- `Task<CashVoucherDto> CreateCashAsync(HttpClient c, UpsertCashVoucherRequest r)` (asserts 200)
- `PutCashAsync`, `CancelCashAsync`, `RestoreCashAsync`, `DeleteCashAsync`, each returning the same tuple
- `static UpsertCashVoucherRequest CashUpdateFrom(CashVoucherDto v)`

Extend `AssertCashDebtInvariantsAsync`: every non-deleted Active cash voucher whose reason has `DebtEffect ≠ None` has exactly one entry matching side, direction, `Amount == Total`, partner, branch and `PostedAt`; Cancelled or deleted vouchers have none; `Total == Σ` non-deleted lines.

1. **Write the failing tests**, added to `CashVoucherCreateTests.cs`:
   - `Receipt_from_customer_posts_receivable_decrease`:
     - TKH, KH1, fund QTM, lines 1,000,000 + 500,000;
     - result: `Code` `PT00001`, `Total` 1,500,000, snapshot name from the partner;
     - entry R− 1,500,000, `DocCode` `PT00001`;
     - activity Created.
   - `Payment_other_without_partner_posts_no_debt` (CK, no partner → `PC00001`, no entries)
   - `Permissions_are_per_type` (a client with only `receipts.view/create` creates a Receipt → 200 and a Payment → 403; GET of a Payment → 403)
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~CashVoucherCreateTests"`. Expected: FAIL (404).
3. **Write the minimal implementation:**
   - `CashVoucherPermissions`;
   - `ICashVoucherService.CreateAsync` and `GetAsync` (the Create row of the steps table **without** business rules 1–5 and without allocations);
   - `LoadDtoAsync`, controller POST and GET, DI.
4. **Run tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git commit -m "feat(cash): create receipts and payments with debt posting"`

### Task 5.5 — Validation rules

1. **Write the failing tests** `CashDebt/CashVouchers/CashVoucherValidationTests.cs`:
   - `Reason_rules` (a Payment reason on a Receipt → `reasonId`; `TPX` (auto) → `reasonId`)
   - `Partner_rules` (TKH without partner → `partnerId`; TKH with a supplier-only partner → `partnerId`; a custom reason with `PartnerType = None` plus a partner → `partnerId`)
   - `Fund_rules` (inactive fund → `fundId`; a fund of branch B → `fundId`)
   - `Line_rules` (no lines → `lines`; amount 0 → `lines[0].amount`)
   - `Period_lock_and_utc` (lock 10-05; `VoucherAt` 10-05 → `PERIOD_LOCKED`; an offset `+07:00` 10-06 08:00 → 200 with `VoucherAt` returned in UTC)
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~CashVoucherValidationTests"`. Expected: FAIL.
3. **Write the minimal implementation:** rules 1–3 and 5, the shape validator, `PeriodLockGuard`.
4. **Run tests to verify they pass:** `--filter "FullyQualifiedName~CashVoucherValidationTests|FullyQualifiedName~CashVoucherCreateTests"`. Expected: PASS.
5. **Commit:** `git commit -m "feat(cash): cash voucher validation rules"`

### Task 5.6 — Allocation at voucher time

1. **Write the failing tests** `CashDebt/CashVouchers/CashVoucherAllocationTests.cs`. Each test ends with invariants.
   - `Allocates_receipt_to_open_invoices`:
     - opening debts for KH1: `HD1` 1,000 (due 09-20) and `HD2` 2,000 (due 09-25);
     - receipt 2,500 with allocations `HD1` 1,000 + `HD2` 1,500;
     - two `AtVoucher` settlements in one batch;
     - GET returns `Allocations` with doc codes;
     - `HD2` is open 500 in `IDebtQueryService`.
   - `Partial_allocation_leaves_prepayment` (receipt 3,000 allocating 1,000 → the receipt entry is open 2,000)
   - `Allocation_errors` (over open → `allocations[0].amount`; Σ over total → `allocations`; a reason with `DebtEffect = None` → `allocations`; an entry of another partner → `allocations[0].entryId`)
   - `Refund_payment_allocates_against_prepayment` (CHKH = R+, allocated to a prepayment R− from an opening debt → settlement with the payment entry as Increase)
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~CashVoucherAllocationTests"`. Expected: FAIL.
3. **Write the minimal implementation:**
   - rule 4;
   - in Create, call `IDebtQueryService.OpenItemsAsync` (ForSource = this voucher) to build the `OpenEntry` map;
   - `AllocationValidator`, then `AddSettlementsAsync(pairs, AtVoucher, batchId, "allocations")` with the pair order chosen by direction;
   - `Allocations` in `LoadDtoAsync`.
4. **Run tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git commit -m "feat(cash): allocate receipts and payments to open documents when saving"`

### Task 5.7 — Update with ownership, concurrency and allocation replacement

1. **Write the failing tests** `CashDebt/CashVouchers/CashVoucherUpdateTests.cs`:
   - `Owner_updates_and_entry_is_updated_in_place` (change amount and date → same entry id, new amount and `PostedAt`)
   - `Edit_rules`:
     - a user with `receipts.view/create/edit` editing an admin voucher → 403; with `receipts.edit_all` → 200;
     - missing `Version` → 400;
     - a Cancelled voucher → 409;
     - a stale `Version` → 409 `CONCURRENCY`;
     - changing `Type` → 400 `type`.
   - `Line_only_edit_still_checks_version` (D29)
   - `Allocations_null_keeps_existing_and_list_replaces_only_at_voucher`:
     - receipt 2,000 allocated to `HD1` 1,000 (`AtVoucher`); add a Manual settlement of 500 to `HD2` via `AddSettlementsAsync`;
     - PUT with `allocations: null` → both kept;
     - PUT with `allocations: [HD2 300]` → the `HD1` `AtVoucher` settlement is gone, a new `AtVoucher` `HD2` 300 exists, Manual 500 is kept;
     - PUT with `[HD1 1,600]` → 400 `allocations` (capacity 2,000 − 500 = 1,500).
   - `Shrinking_below_settled_needs_confirmation` (receipt 2,000 with a Manual 1,500; PUT total 1,000 with `allocations: null` → 422 `DEBT_SETTLEMENTS_WILL_BE_REMOVED`; with `confirmRemoveSettlements` → 200, Manual gone, and the receipt has a `SettlementRemoved` activity. The other side is an opening debt, which logs nothing.)
   - `Changing_partner_with_settlements_is_409` (`DEBT_HAS_SETTLEMENTS` when `allocations` is null; when the request sends `allocations: []`, the `AtVoucher` settlements are removed first and the partner change succeeds)
   - `Managed_voucher_is_read_only` (insert a receipt with `SourceStockVoucherId` = an existing stock voucher id via `InDbAsync` → PUT, cancel and delete → 409 `MANAGED_BY_STOCK_VOUCHER`)
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~CashVoucherUpdateTests"`. Expected: FAIL.
3. **Write the minimal implementation:**
   - `LoadForWriteAsync` (mirrors stock vouchers, plus the managed check);
   - `UpdateAsync` per the steps table;
   - the `IDebtActivityLog` arm for Receipt/Payment: `CashDocumentActivity` with `DocumentType = Receipt/Payment`;
   - controller PUT.
4. **Run tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git commit -m "feat(cash): update cash vouchers with allocation replacement and settlement confirmations"`

### Task 5.8 — Cancel, restore, delete and the negative-fund policy on every path

1. **Write the failing tests** `CashDebt/CashVouchers/CashVoucherLifecycleTests.cs`:
   - `Cancel_restore_delete_follow_status_rules`:
     - cancel → status Cancelled, entry removed;
     - restore → entry back, no settlements;
     - delete of a Cancelled voucher → hidden from GET (404);
     - activities Created, Cancelled, Restored, Deleted.
   - `Cancel_with_settlements_needs_confirmation` (422 → confirm → settlements gone)
   - `Negative_fund_policy_on_each_operation`. Fund `QTM` opening 1,000.
     - Payment 1,500 → 422 `NEGATIVE_FUND_WARNING`; acknowledge → 200.
     - Receipt 2,000 @10-01 followed by payment 1,500 @10-02 (acknowledged): cancelling the receipt → 422 Warn, and deleting it → 422 Warn.
     - Under Block → `NEGATIVE_FUND_BLOCKED`, nothing changes.
     - Moving a payment to another fund that has enough money → passes.
     - Under Allow → passes.
   - `Restore_requires_existing_references` (soft-delete the reason → restore → 400 `reasonId`)
   - `Period_lock_on_lifecycle` (a voucher dated before the lock → cancel, restore and delete → `PERIOD_LOCKED`)
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~CashVoucherLifecycleTests"`. Expected: FAIL.
3. **Write the minimal implementation:**
   - Cancel, Restore and Delete per the steps table, sharing a `ChangeLifecycleAsync` like stock vouchers;
   - the fund checks wired into Create and Update too;
   - controller actions.
4. **Run tests to verify they pass:** `--filter "FullyQualifiedName~CashVoucher"`. Expected: PASS.
5. **Commit:** `git commit -m "feat(cash): cancel, restore and delete cash vouchers with negative-fund policy"`

### Task 5.9 — List, defaults, partner search, activities and in-use locks

Service methods:
- `ListAsync`:
  - working branch, filtered by type;
  - `From`/`To` as VN-date UTC ranges;
  - `Status` default Active;
  - search over code, partner name, person name and description (`ILike`);
  - owners;
  - order `VoucherAt desc, Code desc`;
  - `TotalAmount = Σ Total` of the filtered set.
- `GetDefaultsAsync`: `NextCode` (peek), `VoucherAt = now`, default Cash fund, reason `TKH`/`TNCC`.
- `SearchPartnersAsync`: the type's view permission; the reason's partner type, narrowed by the side role (P19); reuse `ICustomerService.SearchAsync`.
- `ListActivitiesAsync`.

In-use locks:
- `CashReasonService`: a reason used by a non-deleted cash voucher keeps `Type`, `PartnerType` and `DebtEffect` (400 with those keys) and cannot be deleted (409).
- `FundService.EnsureNotUsedAsync`: done in Task 5.3.
- `BranchService.HasDocumentsAsync` adds `_db.CashVouchers.AnyAsync(v => v.BranchId == id)`.

1. **Write the failing tests** `CashDebt/CashVouchers/CashVoucherQueryTests.cs`:
   - `List_filters_and_total` (3 receipts, 1 payment, 1 cancelled receipt → default list: 3 receipts with `TotalAmount` = Σ; `status=All` → 4; filters fund/partner/search/date; branch B list is empty)
   - `Defaults_and_partner_search` (`nextCode` `PT00001` without consuming the counter; TKH → only customers; TNCC → only suppliers)
   - `Reason_and_fund_in_use_locks` (a custom reason used → PUT `debtEffect` → 400; DELETE → 409)
   - `Branch_with_cash_voucher_has_documents` (opening-date PUT → 409 `BRANCH_HAS_DOCUMENTS`)
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~CashVoucherQueryTests"`. Expected: FAIL.
3. **Write the minimal implementation** of the methods and locks above, plus the controller GET actions.
4. **Run tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git commit -m "feat(cash): cash voucher list, defaults, partner search and in-use locks"`

### Task 5.10 — Concurrency and locks

1. **Write the failing tests** `CashDebt/CashVouchers/CashVoucherConcurrencyTests.cs`:
   - `Parallel_creates_get_distinct_sequential_codes` (5 clients via `CloneAdminClient()` → `PT00001`..`PT00005`)
   - `Parallel_payments_cannot_overdraw_under_block` (opening 1,000; Block; two parallel payments of 700 → exactly one 200 and one 422 `NEGATIVE_FUND_BLOCKED`)
   - `Rejected_create_leaves_no_voucher_entry_or_counter_change` (a 422 → no row, no entry; the next create still gets `PT00001`)
   - Both parallel tests are regression tests for the lock order. If they already pass, say so in the commit message; a deliberate RED is not required.
2. **Run the tests to verify they fail or pass as noted:** `--filter "FullyQualifiedName~CashVoucherConcurrencyTests"`.
3. **Write the minimal implementation** only if a test fails (expected cause: the fund lock taken after reading balances).
4. **Run tests to verify they pass:** `--filter "FullyQualifiedName~CashVoucher|FullyQualifiedName~CashDebt"`. Expected: PASS.
5. **Commit:** `git commit -m "test(cash): cash voucher concurrency and lock-order regression tests"`

## Verification

```bash
cd backend
dotnet build OrderMgmt.sln
dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~CashDebt|FullyQualifiedName~StockVoucher"
dotnet test OrderMgmt.sln   # compare with BASELINE.md
```

## Exit Criteria

- `/api/cash-vouchers` supports the full lifecycle, with per-type permissions, ownership, `xmin`, period lock, debt posting and allocation.
- Only `AtVoucher` settlements are replaced on edit.
- Settlement confirmations and the negative-fund policy apply on every path.
- Vouchers with `SourceStockVoucherId` are read-only (409 `MANAGED_BY_STOCK_VOUCHER`).
- The fund opening balance can be set with the negative check.
- Reasons and funds are locked when used.
- Stock voucher numbering still passes after the allocator extraction.
