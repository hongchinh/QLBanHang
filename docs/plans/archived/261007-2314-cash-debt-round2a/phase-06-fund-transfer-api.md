# Phase 06 — Fund transfer API

**Status:** [x] complete
**Complexity:** M

## Objective

Add the `FundTransfer` document, which moves money between two funds of the same branch: depositing cash into a bank account, withdrawing cash from the bank, or moving money between bank accounts. Requirements:

- numbering `CQ`;
- ownership, `xmin`, period lock and lifecycle;
- negative-fund checks on the source fund (save) and on the destination fund (cancel, delete, edits that reduce it);
- transfers counted in fund movements and in the fund/branch in-use checks.

Fund transfers never touch debt.

## Files

- `backend/src/OrderMgmt.Domain/Entities/CashDebt/FundTransfer.cs` (new)
- `backend/src/OrderMgmt.Application/CashDebt/FundTransfers/{Interfaces,Models,Services,Validators}/*` (new — incl. `FundTransferPermissions` helper)
- `backend/src/OrderMgmt.Application/CashDebt/Guards/FundBalanceGuard.cs` (modify — transfer movements)
- `backend/src/OrderMgmt.Application/CashDebt/Funds/Services/FundService.cs` (modify — in-use)
- `backend/src/OrderMgmt.Application/Organization/Branches/Services/BranchService.cs` (modify — `HasDocumentsAsync`)
- `backend/src/OrderMgmt.Infrastructure/Persistence/Configurations/CashDebtConfiguration.cs` (modify)
- `backend/src/OrderMgmt.Infrastructure/Persistence/Migrations/<ts>_AddFundTransfers.cs` (generated)
- `backend/src/OrderMgmt.WebApi/Controllers/FundTransfersController.cs` (new)
- `backend/tests/OrderMgmt.IntegrationTests/CashDebt/CashDebtTestBase.cs` (modify — helpers)
- `backend/tests/OrderMgmt.IntegrationTests/CashDebt/FundTransfers/{FundTransferCreateTests,FundTransferLifecycleTests,FundTransferQueryTests}.cs` (new)

## Reference files (read-only)

- `backend/src/OrderMgmt.Application/CashDebt/CashVouchers/Services/CashVoucherService.cs` (Phase 05) — `LoadForWriteAsync`, lifecycle helper, activity, allocator use

## Data model

```csharp
public class FundTransfer : BaseEntity
{
    public string Code { get; set; } = default!;
    public DateTimeOffset VoucherAt { get; set; }
    public Guid BranchId { get; set; }
    public Guid FromFundId { get; set; }  public Fund? FromFund { get; set; }
    public Guid ToFundId { get; set; }    public Fund? ToFund { get; set; }
    public decimal Amount { get; set; }               // numeric(18,2), > 0
    public string? Description { get; set; }          // max 500
    public string? Note { get; set; }
    public DocumentStatus Status { get; set; } = DocumentStatus.Active;
    public DateTimeOffset? CancelledAt { get; set; }  public Guid? CancelledBy { get; set; }
    public Guid OwnerUserId { get; set; }  public User? Owner { get; set; }
    public uint Version { get; set; }                 // xmin
}
// table fund_transfers; unique (branch_id, code) filtered; index (from_fund_id, voucher_at), (to_fund_id, voucher_at);
// CHECK from_fund_id <> to_fund_id; FKs Restrict.
```

## API contract

Route `api/fund-transfers`. The guards are static `[HasPermission]`, because there is only one type: GET → `fund_transfers.view`; POST → `create`; PUT → `edit`; cancel and restore → `cancel`; DELETE → `delete`. Editing another user's transfer also needs `fund_transfers.edit_all` (service).

| Method | Route | Request → Response |
|---|---|---|
| GET | `/?page=&pageSize=&from=&to=&fundId=&search=&status=&ownerIds=` | `FundTransferListRequest` → `FundTransferListResult : PagedResult<FundTransferListItemDto> { TotalAmount }` |
| GET | `/defaults` | `FundTransferDefaultsDto { NextCode, VoucherAt }` |
| GET | `/{id}` | `FundTransferDto` |
| GET | `/{id}/activities` | `IReadOnlyList<CashDocumentActivityDto>` |
| POST | `/` | `UpsertFundTransferRequest` → `FundTransferDto` |
| PUT | `/{id}` | `UpsertFundTransferRequest` → `FundTransferDto` |
| POST | `/{id}/cancel`, `/{id}/restore` | `CashDocumentActionRequest` → `FundTransferDto` |
| DELETE | `/{id}` | `[FromQuery] CashDocumentActionRequest` |

DTOs use shorthand field lists; every member is a public `{ get; set; }` auto-property.

```csharp
UpsertFundTransferRequest { DateTimeOffset VoucherAt; Guid FromFundId; Guid ToFundId; decimal Amount; string? Description; string? Note;
    uint? Version; bool AcknowledgeNegativeFund; }
FundTransferDto { Id; Code; VoucherAt; BranchId; FromFundId; FromFundCode; FromFundName; FromFundKind; ToFundId; ToFundCode; ToFundName;
    ToFundKind; Amount; Description; Note; Status; CancelledAt; OwnerUserId; OwnerName; Version; CanEdit; CanCancel; CanDelete; }
FundTransferListItemDto { Id; Code; VoucherAt; FromFundCode; FromFundName; ToFundCode; ToFundName; Amount; Description; Status; OwnerUserId; OwnerName; }
FundTransferListRequest : PageRequest { DateOnly? From; DateOnly? To; Guid? FundId /* from or to */; string? Search; DocumentStatusFilter Status = Active; string? OwnerIds; }
```

## Rules

Business rules, collected into one `ValidationDomainException`:
- `fromFundId` / `toFundId`: exist, in the working branch, active (an unchanged inactive fund on edit is allowed).
- `toFundId`: must differ from `fromFundId` ("Quỹ đến phải khác quỹ đi.").
- `amount`: > 0 with ≤ 2 decimals (shape validator).

Locks: shared branch gate → `ICashDebtLock.AcquireAsync(branch, [], funds {old from, old to, new from, new to})`.

Fund capture covers every fund involved (old and new) from `min(old VoucherAt, new VoucherAt)`. Lifecycle steps follow Phase 05's steps table without the debt calls. Activities use `DocumentType.FundTransfer`.

## Tasks

### Task 6.1 — Schema and fund movements

1. **Write the failing tests** `CashDebt/FundTransfers/FundTransferCreateTests.cs`:
   - `Schema_rejects_same_fund_and_duplicate_code` (insert with `FromFundId == ToFundId` → `DbUpdateException`; duplicate code in a branch → `DbUpdateException`)
   - `Transfers_are_fund_movements` (insert an Active transfer QTM → NH01 of 300 via `InDbAsync` → `IFundBalanceGuard.MovementsAsync(QTM)` contains −300 with Order 1, and `NH01` contains +300 with Order 0; a Cancelled transfer is not included)
   - `Fund_with_transfer_cannot_be_deleted` (409)
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~FundTransferCreateTests"`. Expected: FAIL (compile).
3. **Write the minimal implementation:** entity, config (`HasCheckConstraint("ck_fund_transfers_distinct_funds", "from_fund_id <> to_fund_id")`), DbSet, migration `AddFundTransfers`, guard movements, `FundService.EnsureNotUsedAsync` extension.
4. **Run tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git commit -m "feat(cash): fund transfer schema counted in fund movements"`

### Task 6.2 — Create and get, with validation and negative-fund check

Test helpers in `CashDebtTestBase`: `static UpsertFundTransferRequest TransferRequest(Guid from, Guid to, decimal amount, string at)` and `PostTransferAsync` / `PutTransferAsync` / `CancelTransferAsync` / `RestoreTransferAsync` / `DeleteTransferAsync`, each returning `(HttpStatusCode, FundTransferDto?, ApiError?)`.

1. **Write the failing tests**, added to `FundTransferCreateTests.cs`:
   - `Create_transfer_numbers_and_logs` (QTM opening 1,000,000 → NH01 400,000 → `CQ00001`, activity Created, both funds' movements updated)
   - `Validation_rules` (same fund → `toFundId`; a fund of branch B → `fromFundId`; amount 0 → `amount`; locked date → `PERIOD_LOCKED`)
   - `Overdrawing_the_source_fund_follows_policy` (QTM opening 100 → transfer 500 → 422 `NEGATIVE_FUND_WARNING` with details key `QTM`; acknowledge → 200; Block → `NEGATIVE_FUND_BLOCKED`)
   - `Requires_permission` (SALES → 403)
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~FundTransferCreateTests"`. Expected: FAIL.
3. **Write the minimal implementation:** `IFundTransferService.CreateAsync` / `GetAsync`, shape validator, rules, allocator call with `DocumentType.FundTransfer`, fund check, activity, controller POST and GET, DI.
4. **Run tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git commit -m "feat(cash): create fund transfers with negative-fund policy"`

### Task 6.3 — Update, cancel, restore, delete

1. **Write the failing tests** `CashDebt/FundTransfers/FundTransferLifecycleTests.cs`:
   - `Update_with_ownership_and_concurrency` (another user without `edit_all` → 403; stale version → 409 `CONCURRENCY`; owner changes amount → 200)
   - `Cancelling_can_overdraw_the_destination_fund`:
     - NH01 has only the transfer of 400 and a later payment of 300 from NH01;
     - cancel the transfer → 422 `NEGATIVE_FUND_WARNING` (key `NH01`); acknowledge → Cancelled;
     - restore → checks QTM again.
   - `Delete_and_status_rules` (delete an Active transfer → 404 afterwards; restore an Active one → 409; activities Created/Cancelled/Restored/Deleted)
   - `Changing_destination_checks_old_destination` (move the destination from NH01 to NH02 while NH01 has a later payment that depends on it → 422 key `NH01`)
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~FundTransferLifecycleTests"`. Expected: FAIL.
3. **Write the minimal implementation:** `UpdateAsync`, `CancelAsync`, `RestoreAsync`, `DeleteAsync`, controller actions.
4. **Run tests to verify they pass:** `--filter "FullyQualifiedName~FundTransfer"`. Expected: PASS.
5. **Commit:** `git commit -m "feat(cash): update, cancel, restore and delete fund transfers"`

### Task 6.4 — List, defaults, activities and branch documents

1. **Write the failing tests** `CashDebt/FundTransfers/FundTransferQueryTests.cs`:
   - `List_filters_by_fund_status_and_date` (a `fundId` filter matches either side; `TotalAmount`; branch B is empty)
   - `Defaults_and_activities` (`nextCode` peek `CQ00001`; activities ordered by time)
   - `Branch_with_transfer_has_documents` (409 `BRANCH_HAS_DOCUMENTS` on the opening-date PUT)
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~FundTransferQueryTests"`. Expected: FAIL.
3. **Write the minimal implementation:** `ListAsync`, `GetDefaultsAsync`, `ListActivitiesAsync`, controller GETs, `BranchService.HasDocumentsAsync` extension.
4. **Run tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git commit -m "feat(cash): fund transfer list, defaults and activities"`

## Verification

```bash
cd backend
dotnet build OrderMgmt.sln
dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~CashDebt"
dotnet test OrderMgmt.sln   # compare with BASELINE.md
```

## Exit Criteria

- `/api/fund-transfers` supports the full lifecycle.
- Negative-fund checks hit the right fund on every path.
- Transfers are fund movements, and they block deleting a fund or changing the branch opening date.
