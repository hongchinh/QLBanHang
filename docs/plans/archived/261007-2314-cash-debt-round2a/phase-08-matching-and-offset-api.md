# Phase 08 — Matching & debt offset API

**Status:** [x] complete
**Complexity:** L

## Objective

Expose the remaining debt operations:

- the open-items endpoint used by the allocation grid, the matching screen and the offset form, plus a debt partner search;
- manual matching (`Manual` batches) and unmatching (P22);
- the debt offset voucher (P23), which offsets a partner's receivable documents against their payable documents. It creates two Decrease entries and `Offset` settlements.

## Files

- `backend/src/OrderMgmt.Domain/Entities/CashDebt/DebtOffsetVoucher.cs` (new)
- `backend/src/OrderMgmt.Application/CashDebt/Debt/{Interfaces/IDebtService.cs,Models/DebtDtos.cs,Services/DebtService.cs,Validators/DebtValidators.cs}` (modify)
- `backend/src/OrderMgmt.Application/CashDebt/DebtOffsets/{Interfaces,Models,Services,Validators}/*` (new)
- `backend/src/OrderMgmt.Application/CashDebt/Ledger/DebtActivityLog.cs` (modify — Offset arm)
- `backend/src/OrderMgmt.Application/Organization/Branches/Services/BranchService.cs` (modify — `HasDocumentsAsync`)
- `backend/src/OrderMgmt.Infrastructure/Persistence/Configurations/CashDebtConfiguration.cs` (modify)
- `backend/src/OrderMgmt.Infrastructure/Persistence/Migrations/<ts>_AddDebtOffsets.cs` (generated)
- `backend/src/OrderMgmt.WebApi/Controllers/{DebtController.cs (modify), DebtOffsetsController.cs (new)}`
- `backend/tests/OrderMgmt.IntegrationTests/CashDebt/CashDebtTestBase.cs` (modify — invariants)
- `backend/tests/OrderMgmt.IntegrationTests/CashDebt/Matching/{OpenItemsApiTests,ManualMatchingTests,DebtOffsetTests}.cs` (new)

## API contract

DTOs use shorthand field lists; every member is a public `{ get; set; }` auto-property.

**`DebtController` (route `api/debt`)**

| Method | Route | Permission | Request → Response |
|---|---|---|---|
| GET | `open-items?partnerId=&side=&direction=&forSourceType=&forSourceId=` | any of `receipts.create`, `receipts.edit`, `payments.create`, `payments.edit`, `debt.matching`, `debt_offset.create` (service) | `IReadOnlyList<OpenItemDto>` (Phase 04 DTO) |
| GET | `partners?side=&bothRoles=&keyword=&limit=20` | any of `debt.matching`, `debt.opening_balance`, `debt_offset.view`, `reports.debt`, `credit_limit.manage` (service) | `List<CustomerSearchItemDto>` — `side=Receivable` → customers, `Payable` → suppliers, `bothRoles=true` → partners with both roles, neither → any role. Accountants need neither `customers.view` nor `suppliers.view`. |
| GET | `settlements?partnerId=&side=` | `debt.matching` | `IReadOnlyList<SettlementDto>` |
| POST | `settlements` | `debt.matching` | `CreateSettlementBatchRequest` → `SettlementBatchDto` |
| DELETE | `settlements/{id}` | `debt.matching` | — |
| DELETE | `settlement-batches/{batchId}` | `debt.matching` | — |

```csharp
CreateSettlementBatchRequest { Guid PartnerId; DebtSide Side; List<SettlementItemRequest> Items; }
SettlementItemRequest { Guid IncreaseEntryId; Guid DecreaseEntryId; decimal Amount; }
SettlementBatchDto { Guid BatchId; List<SettlementDto> Settlements; }
SettlementDto { Id; BatchId; Origin; IncreaseEntryId; IncreaseDocCode; IncreaseDocDate; IncreaseSourceType; IncreaseSourceId;
    DecreaseEntryId; DecreaseDocCode; DecreaseDocDate; DecreaseSourceType; DecreaseSourceId; Amount; EffectiveAt; CreatedAt; CreatedByName; }
```

**`DebtOffsetsController` (route `api/debt-offsets`)**: GET → `debt_offset.view`; POST → `create`; cancel → `cancel`; DELETE → `delete`.

| Method | Route | Request → Response |
|---|---|---|
| GET | `/?page=&pageSize=&from=&to=&partnerId=&status=` | `DebtOffsetListResult : PagedResult<DebtOffsetListItemDto> { TotalAmount }` |
| GET | `/defaults` | `{ NextCode, VoucherAt }` |
| GET | `/{id}`, `/{id}/activities` | `DebtOffsetDto`, `IReadOnlyList<CashDocumentActivityDto>` |
| POST | `/` | `CreateDebtOffsetRequest` → `DebtOffsetDto` |
| POST | `/{id}/cancel` | `CashDocumentActionRequest` → `DebtOffsetDto` |
| DELETE | `/{id}` | `[FromQuery] CashDocumentActionRequest` |

```csharp
public class DebtOffsetVoucher : BaseEntity
{
    public string Code { get; set; } = default!;  public DateTimeOffset VoucherAt { get; set; }  public Guid BranchId { get; set; }
    public Guid PartnerId { get; set; }  public Customer? Partner { get; set; }  public string PartnerName { get; set; } = default!;
    public decimal Amount { get; set; }  public string? Note { get; set; }
    public DocumentStatus Status { get; set; } = DocumentStatus.Active;
    public DateTimeOffset? CancelledAt { get; set; }  public Guid? CancelledBy { get; set; }
    public Guid OwnerUserId { get; set; }  public User? Owner { get; set; }  public uint Version { get; set; }
}
// table debt_offset_vouchers; unique (branch_id, code) filtered; index (branch_id, voucher_at).

CreateDebtOffsetRequest { DateTimeOffset VoucherAt; Guid PartnerId; string? Note; List<AllocationRequest> Receivables; List<AllocationRequest> Payables; }
DebtOffsetDto { Id; Code; VoucherAt; PartnerId; PartnerCode; PartnerName; Amount; Note; Status; CancelledAt; OwnerUserId; OwnerName;
    Version; CanCancel; CanDelete; List<DebtOffsetItemDto> Items; }
DebtOffsetItemDto { DebtSide Side; Guid EntryId; DebtSourceType SourceType; Guid SourceId; string DocCode; DateOnly DocDate; decimal Amount; }
DebtOffsetListItemDto { Id; Code; VoucherAt; PartnerCode; PartnerName; Amount; Note; Status; OwnerName; }
```

## Tasks

### Task 8.1 — Open items and partner search endpoints

1. **Write the failing tests** `CashDebt/Matching/OpenItemsApiTests.cs`:
   - `Partner_search_by_side_and_roles` (`side=Receivable` returns customers only; `Payable` returns suppliers only; `bothRoles=true` returns dual-role partners only; an ACCOUNTANT client without `suppliers.view` → 200; WAREHOUSE → 403)
   - `Returns_open_items_in_working_branch` (an opening debt and a sale for KH1 in main; a sale for KH1 in branch B → main returns 2 rows ordered by due date; branch B returns its own)
   - `For_source_adds_back_own_allocations` (a receipt allocated 1,000 to `HD1` → `forSourceType=Receipt&forSourceId=…` shows `HD1` with the full open amount)
   - `Permission_rules` (WAREHOUSE → 403; a client with only `debt.matching` → 200)
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~OpenItemsApiTests"`. Expected: FAIL (404).
3. **Write the minimal implementation:** `IDebtService.GetOpenItemsAsync(OpenItemsRequest, ct)` delegating to `IDebtQueryService` with the working branch; `IDebtService.SearchPartnersAsync(DebtPartnerSearchRequest, ct)`; validator (`PartnerId` NotEmpty, enums `IsInEnum`); controller actions. `SearchPartnersAsync` queries `_db.Customers` directly: role filter (`IsCustomer`, `IsSupplier`, both, or none), active status as `ICustomerService.SearchAsync` does, keyword `ILike` on code/name/tax code, order by `Code`, `Take(limit)`. It maps to `CustomerSearchItemDto` with the same field mapping as `CustomerService`.
4. **Run tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git commit -m "feat(debt): open items and debt partner search endpoints"`

### Task 8.2 — Manual matching and unmatching

Write flow, inside `ITransactionRunner`:
1. Shared branch gate, then partner lock.
2. Every item's two entries must belong to the request's partner and side and to the working branch (400 `items[i].increaseEntryId` / `items[i].decreaseEntryId`).
3. `AddSettlementsAsync(drafts, Manual, Guid.NewGuid(), "items")`.

Unmatching:
- load the settlement(s) → partner lock → origin must be `Manual` or `AtVoucher` (else `ConflictException("SETTLEMENT_MANAGED", "Đối trừ này do chứng từ quản lý, không bỏ được ở đây.")`);
- `PeriodLockGuard(EffectiveAt)`;
- `RemoveSettlementsAsync`, which logs activities on both documents.
- Deleting a batch removes only its `Manual`/`AtVoucher` rows. If any row of the batch is managed → 409, and nothing is removed.

1. **Write the failing tests** `CashDebt/Matching/ManualMatchingTests.cs`. Each test ends with invariants.
   - `Match_prepayment_to_invoices_in_one_batch` (receipt 3,000 without allocation; `HD1` 1,000, `HD2` 2,500 → batch `[HD1 1,000, HD2 2,000]` → open items: receipt 0, `HD2` 500; GET settlements lists both with the same `BatchId`)
   - `Validation` (amount above open → 400 `items[0].amount`; an entry of another partner → `items[0].increaseEntryId`; a locked effective date → `PERIOD_LOCKED`; SALES → 403)
   - `Unmatch_single_and_batch_and_log_activity` (delete one → open restored, the receipt has activity `SettlementRemoved`; delete the batch → all gone)
   - `Managed_origins_cannot_be_unmatched` (an `Auto` settlement from a paid sale → 409 `SETTLEMENT_MANAGED`; `AtVoucher` → 200)
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~ManualMatchingTests"`. Expected: FAIL.
3. **Write the minimal implementation:** `IDebtService.ListSettlementsAsync`, `CreateSettlementBatchAsync`, `DeleteSettlementAsync`, `DeleteSettlementBatchAsync`; validators; controller actions.
4. **Run tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git commit -m "feat(debt): manual document matching and unmatching"`

### Task 8.3 — Debt offset voucher

Create flow, inside `ITransactionRunner`:
1. Permission `debt_offset.create`.
2. Gate + partner lock.
3. `PeriodLockGuard(voucherAt)`.
4. Validate, collecting errors:
   - `partnerId`: exists and has **both** roles ("Đối tượng phải vừa là khách hàng vừa là nhà cung cấp.");
   - `receivables` / `payables`: each list is non-empty;
   - each side runs `AllocationValidator` with that side, `ownDirection = Decrease`, `capacity = Σ that side`, and open items of the partner (key prefixes `receivables` / `payables`). Only Increase entries are allowed, because the own direction is Decrease;
   - `payables`: Σ receivables must equal Σ payables and be > 0 ("Số tiền hai bên phải bằng nhau.").
5. Create the voucher: `Code` via the allocator (`DebtOffset`), `Amount = Σ`, partner name snapshot, owner. Save.
6. `PostAsync(DebtSourceType.Offset, id, [R− Amount, P− Amount], false)`, with `DocCode = Code` and `Description = "Bù trừ công nợ"`.
7. `AddSettlementsAsync` for each receivable item (Increase = item entry, Decrease = the offset's R− entry), then each payable item, with origin `Offset` and batch = voucher id.
8. Activity Created, then save.

Cancel:
1. `LoadForWrite(Cancel, active)` with version.
2. Gate + partner lock.
3. `PeriodLockGuard(stored)`.
4. `RemoveOwnSettlementsAsync(Offset, id, Offset)`, then `RemoveSourceAsync(Offset, id, confirmRemoveSettlements: true)`.
5. Status Cancelled; activity.

Delete: the same when Active, then `IsDeleted`. There is no edit and no restore (P23).

Other changes:
- The `IDebtActivityLog` Offset arm writes `CashDocumentActivity` with `DocumentType.DebtOffset`.
- `BranchService.HasDocumentsAsync` adds offsets.
- `AssertCashDebtInvariantsAsync`: an Active, non-deleted offset has exactly an R− and a P− entry of `Amount`, each fully settled by `Offset` settlements; cancelled or deleted offsets have none.

1. **Write the failing tests** `CashDebt/Matching/DebtOffsetTests.cs`:
   - `Offset_receivable_against_payable`:
     - dual-role partner DT1 with XBH 5,000 and NMH 3,000;
     - offset `[XBH 3,000]` vs `[NMH 3,000]` → `BT00001`, `Amount` 3,000;
     - receivable balance 2,000 and payable balance 0;
     - open items: XBH 2,000, NMH 0.
   - `Validation` (unequal sides → `payables`; a customer-only partner → `partnerId`; an amount over open → `receivables[0].amount`; a Decrease entry in `payables` → `payables[0].entryId`; an empty side → `receivables`)
   - `Cancel_and_delete_restore_open_amounts` (cancel → entries and settlements gone, balances back; delete a Cancelled offset → 404 on GET; activities)
   - `Offset_settlements_cannot_be_unmatched_manually` (DELETE settlement → 409 `SETTLEMENT_MANAGED`)
   - `List_defaults_and_branch_documents` (list `TotalAmount`; defaults `BT00001`; opening-date PUT → 409 `BRANCH_HAS_DOCUMENTS`; SALES → 403)
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~DebtOffsetTests"`. Expected: FAIL.
3. **Write the minimal implementation:** entity, config, DbSet, migration `AddDebtOffsets`, service, validators, controller, DI, activity arm, `HasDocumentsAsync`, invariants.
4. **Run tests to verify they pass:** `--filter "FullyQualifiedName~Matching|FullyQualifiedName~CashDebt"`. Expected: PASS.
5. **Commit:** `git commit -m "feat(debt): debt offset vouchers between receivables and payables"`

## Verification

```bash
cd backend
dotnet build OrderMgmt.sln
dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~CashDebt"
dotnet test OrderMgmt.sln   # compare with BASELINE.md
```

## Exit Criteria

- Open items, manual matching/unmatching and debt offsets work with permissions, period lock and activity logs.
- Managed settlements (`Auto`, `Offset`) cannot be removed manually.
- Invariants hold after every scenario.
