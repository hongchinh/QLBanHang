# Phase 01 — Permissions, cash settings, numbering & branch opening date

**Status:** [x] complete
**Complexity:** L

## Objective

Lay the foundations every later phase needs:

- the new permission codes, with their seed and role grants (P5, P6);
- the `CashDebtSettings` singleton (P7);
- numbering for the four new document types (P4);
- `Branch.OpeningDate` (P8), together with the switch of opening stock to the branch date;
- the shared test base `CashDebtTestBase`, plus two shared pieces: the `ConfirmationRequiredException` → 422 mapping (P10) and `PeriodLockGuard` (P15).

## Files

- `backend/src/OrderMgmt.Domain/Constants/Permissions.cs` (modify)
- `backend/src/OrderMgmt.Domain/Enums/CashDebtEnums.cs` (new — `LimitPolicy`; grows in later phases)
- `backend/src/OrderMgmt.Domain/Enums/InventoryEnums.cs` (modify — `DocumentType` gains `Receipt = 3, Payment = 4, FundTransfer = 5, DebtOffset = 6`)
- `backend/src/OrderMgmt.Domain/Common/DomainException.cs` (modify — `ConfirmationRequiredException`, `ConflictException(code, message)` overload)
- `backend/src/OrderMgmt.Domain/Entities/CashDebt/CashDebtSettings.cs` (new)
- `backend/src/OrderMgmt.Domain/Entities/Organization/Branch.cs` (modify — `OpeningDate`)
- `backend/src/OrderMgmt.Application/Common/Interfaces/IAppDbContext.cs` (modify)
- `backend/src/OrderMgmt.Application/CashDebt/Settings/{Interfaces,Models,Services,Validators}/*` (new)
- `backend/src/OrderMgmt.Application/Organization/Branches/PeriodLockGuard.cs` (new)
- `backend/src/OrderMgmt.Application/Organization/Branches/{Services/BranchService.cs,Interfaces/IBranchService.cs,Models/*}` (modify — opening date)
- `backend/src/OrderMgmt.Application/Inventory/Numbering/DocumentNumberingDefaults.cs` (modify)
- `backend/src/OrderMgmt.Application/Inventory/Settings/Services/InventorySettingsService.cs` (modify — numbering permission per type)
- `backend/src/OrderMgmt.Application/Inventory/OpeningStocks/**` (modify — branch date, `RepostBranchAsync`)
- `backend/src/OrderMgmt.Application/DependencyInjection.cs` (modify)
- `backend/src/OrderMgmt.Infrastructure/Persistence/AppDbContext.cs` (modify)
- `backend/src/OrderMgmt.Infrastructure/Persistence/Configurations/CashDebtConfiguration.cs` (new)
- `backend/src/OrderMgmt.Infrastructure/Persistence/Configurations/OrganizationConfiguration.cs` (modify)
- `backend/src/OrderMgmt.Infrastructure/Persistence/Seed/DbSeeder.cs` (modify)
- `backend/src/OrderMgmt.Infrastructure/Persistence/Migrations/<ts>_{AddCashDebtSettings,AddBranchOpeningDate}.cs` (generated)
- `backend/src/OrderMgmt.WebApi/Controllers/{CashSettingsController.cs (new), BranchesController.cs, InventorySettingsController.cs}` (modify)
- `backend/src/OrderMgmt.WebApi/Middleware/GlobalExceptionMiddleware.cs` (modify)
- `backend/tests/OrderMgmt.IntegrationTests/CashDebt/CashDebtTestBase.cs` (new)
- `backend/tests/OrderMgmt.IntegrationTests/CashDebt/{CashDebtSeedTests,CashSettingsTests,CashNumberingTests,BranchOpeningDateTests}.cs` (new)
- `backend/tests/OrderMgmt.IntegrationTests/CashDebt/Unit/{PeriodLockGuardTests,ConfirmationExceptionMappingTests}.cs` (new)
- `backend/tests/OrderMgmt.IntegrationTests/Inventory/{OpeningStockTests,OpeningStockLargeGridTests,InventoryTestBase}.cs` and other tests that send `OpeningDate` (modify — `grep -rln "OpeningDate" backend/tests`)

## Reference files (read-only)

- `backend/src/OrderMgmt.Infrastructure/Persistence/Seed/DbSeeder.cs` — `SeedPermissionsAsync` (`permissionDefs`), `SeedRolesAsync` (`roleDefs`, `AssignPermissions`), `SeedInventoryReferenceDataAsync`
- `backend/src/OrderMgmt.Application/Inventory/Settings/**`, `InventorySettingsController.cs` — singleton settings pattern
- `backend/tests/OrderMgmt.IntegrationTests/Inventory/InventorySeedTests.cs` — how the seeder is re-run in a test
- `backend/src/OrderMgmt.Application/Organization/Branches/Services/BranchService.cs` — `SetLockAsync` (exclusive gate)

## Tasks

### Task 1.1 — Permission codes, seed, role grants and `CashDebtTestBase`

Add to `Permissions.cs`:

```csharp
public const string CashModule = "cash";

public static class Receipts      { View = "receipts.view"; Create = "receipts.create"; Edit = "receipts.edit"; Delete = "receipts.delete"; Cancel = "receipts.cancel"; EditAll = "receipts.edit_all"; }
public static class Payments      { View = "payments.view"; Create = "payments.create"; Edit = "payments.edit"; Delete = "payments.delete"; Cancel = "payments.cancel"; EditAll = "payments.edit_all"; }
public static class FundTransfers { View = "fund_transfers.view"; Create = "fund_transfers.create"; Edit = "fund_transfers.edit"; Delete = "fund_transfers.delete"; Cancel = "fund_transfers.cancel"; EditAll = "fund_transfers.edit_all"; }
public static class DebtOffsets   { View = "debt_offset.view"; Create = "debt_offset.create"; Cancel = "debt_offset.cancel"; Delete = "debt_offset.delete"; }
public static class Debt          { Matching = "debt.matching"; OpeningBalance = "debt.opening_balance"; PartnerSummary = "debt.partner_summary"; }
public static class Cash          { OpeningBalance = "cash.opening_balance"; ManageCatalogs = "cash.catalogs.manage"; Settings = "cash.settings"; }
public static class CreditLimits  { Manage = "credit_limit.manage"; }
// in Reports:
public const string Cash = "reports.cash";
```

(The shorthand stands for `public const string X = "…";` members.)

Seed rules:
- `permissionDefs` gets one row per code. Module `CashModule` for everything except `reports.cash`, which uses `ReportModule` like `reports.debt`.
- Vietnamese names:
  - Phiếu thu: Xem / Tạo / Sửa / Xóa / Hủy / Sửa của người khác; Phiếu chi likewise; Chuyển quỹ likewise.
  - Bù trừ công nợ: Xem / Tạo / Hủy / Xóa.
  - "Đối trừ chứng từ", "Nợ đầu kỳ", "Xem công nợ khách trên phiếu xuất", "Số dư quỹ đầu kỳ", "Danh mục lý do thu/chi và quỹ", "Cấu hình tiền & công nợ", "Hạn mức nợ", "Sổ quỹ".
- `roleDefs` (P5):
  - ADMIN and MANAGER already get everything.
  - ACCOUNTANT adds every new code.
  - SALES adds `debt.partner_summary`.
  - WAREHOUSE: unchanged.

Create `CashDebt/CashDebtTestBase.cs : InventoryTestBase` (empty for now, except a constructor taking `PostgresFixture`). Later tasks add helpers.

1. **Write the failing tests** `CashDebt/CashDebtSeedTests.cs : CashDebtTestBase`:
   - `New_permission_codes_exist_with_modules`: every code above exists; `reports.cash` has module `report` and the others have `cash`.
   - `Default_role_grants_follow_p5`:
     - ACCOUNTANT has all 30 new codes;
     - SALES has `debt.partner_summary` and no `receipts.view`;
     - WAREHOUSE has none of them;
     - MANAGER has all of them.
   - `Removed_grant_survives_a_reseed`: remove `receipts.view` from ACCOUNTANT via `InDbAsync`, re-run the seeder as `InventorySeedTests` does, and the grant is still absent.
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~CashDebtSeedTests"`. Expected: FAIL (compile: `Permissions.Receipts` missing).
3. **Write the minimal implementation:** constants, `permissionDefs`, `roleDefs`.
4. **Run tests to verify they pass:** `--filter "FullyQualifiedName~CashDebtSeedTests|FullyQualifiedName~InventorySeedTests"`. Expected: PASS.
5. **Commit:** `git commit -m "feat(cash): cash and debt permission codes with default role grants"`

### Task 1.2 — `ConfirmationRequiredException` and `PeriodLockGuard`

```csharp
// Domain/Common/DomainException.cs
public sealed class ConfirmationRequiredException : DomainException
{
    public ConfirmationRequiredException(string code, string message, IDictionary<string, string[]> details, bool canConfirm)
        : base(code, message) { Details = details; CanConfirm = canConfirm; }
    public IDictionary<string, string[]> Details { get; }
    public bool CanConfirm { get; }
}

// Application/Organization/Branches/PeriodLockGuard.cs
public static class PeriodLockGuard
{
    public static void EnsureNotLocked(DateOnly? lockedUntil, IEnumerable<DateTimeOffset> instants); // VnTime.ToVnDate(at) <= lockedUntil → throw
    public static void EnsureNotLocked(DateOnly? lockedUntil, IEnumerable<DateOnly> dates);
    // throws new DomainException("PERIOD_LOCKED", $"Ngày chứng từ đã khóa sổ (đến {lockedUntil:dd/MM/yyyy}).")
}
```

In `GlobalExceptionMiddleware`, add an arm **before** the `DomainException` arm:
`ConfirmationRequiredException cre => (422, new ApiError { Code = cre.Code, Message = cre.Message, Details = cre.Details })`.

1. **Write the failing tests:**
   - `CashDebt/Unit/PeriodLockGuardTests.cs`:
     - `Null_lock_never_throws`
     - `Instant_on_lock_date_in_vn_time_throws` (lock 10-05; `2026-10-05T16:59:00Z` = 23:59 VN → throws; `2026-10-05T17:00:00Z` = 10-06 00:00 VN → passes)
     - `Date_overload_throws_on_or_before_lock`
   - `CashDebt/Unit/ConfirmationExceptionMappingTests.cs`: copy the setup of `Inventory/Unit/UniqueViolationMappingTests.cs` (it invokes the middleware with a throwing `RequestDelegate`). Assert status 422, `error.code`, and `error.details` round-trip.
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~PeriodLockGuardTests|FullyQualifiedName~ConfirmationExceptionMappingTests"`. Expected: FAIL (compile).
3. **Write the minimal implementation:** the exception, the guard and the middleware arm.
4. **Run tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git commit -m "feat(cash): confirmation-required 422 exception and shared period-lock guard"`

### Task 1.3 — `CashDebtSettings` singleton

- `CashDebtEnums.cs`: `public enum LimitPolicy { Allow = 0, Warn = 1, Block = 2 }`.
- Entity `CashDebtSettings` (not `BaseEntity`): `int Id`, `LimitPolicy CreditLimitPolicy`, `LimitPolicy NegativeFundPolicy`, `DateTimeOffset UpdatedAt`, `Guid? UpdatedBy`.
  - Table `cash_debt_settings`; `Id` is never generated.
  - `HasData(new { Id = 1, CreditLimitPolicy = LimitPolicy.Warn, NegativeFundPolicy = LimitPolicy.Warn, UpdatedAt = DateTimeOffset.UnixEpoch })`.
- API `CashSettingsController` (route `api/cash`):
  - `GET settings` `[Authorize]` → `CashDebtSettingsDto { CreditLimitPolicy, NegativeFundPolicy, UpdatedAt }`.
  - `PUT settings` `[HasPermission(Permissions.Cash.Settings)]` with `UpdateCashDebtSettingsRequest { CreditLimitPolicy, NegativeFundPolicy }` (validator: `IsInEnum`) → the updated DTO.
- Service `ICashDebtSettingsService { GetAsync; UpdateAsync }`.
- Helper for later phases: `CashDebtTestBase.UpdateCashDebtSettingsAsync(Action<CashDebtSettings> mutate)` (direct DB update).

1. **Write the failing tests** `CashDebt/CashSettingsTests.cs` (smoke):
   - `Defaults_are_warn_and_update_persists` (SALES GET → 200 Warn/Warn; admin PUT Block/Allow → GET returns them)
   - `Update_requires_cash_settings_permission` (a client with only `inventory.settings` → 403)
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~CashSettingsTests"`. Expected: FAIL (404).
3. **Write the minimal implementation:** enum, entity, `CashDebtSettingsConfiguration` in `CashDebtConfiguration.cs`, `DbSet<CashDebtSettings> CashDebtSettings` on `IAppDbContext`/`AppDbContext`, migration `AddCashDebtSettings`, service, validator, controller, DI.
4. **Run tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git commit -m "feat(cash): cash and debt settings with credit-limit and negative-fund policies"`

### Task 1.4 — Numbering for Receipt / Payment / FundTransfer / DebtOffset

Changes:
- `DocumentType` gains `Receipt = 3, Payment = 4, FundTransfer = 5, DebtOffset = 6`.
- `DocumentNumberingDefaults`:
  - `AllTypes` lists all six types.
  - `Prefix` comes from a switch: `StockIn → "PN"`, `StockOut → "PX"`, `Receipt → "PT"`, `Payment → "PC"`, `FundTransfer → "CQ"`, `DebtOffset → "BT"`.
- `DbSeeder.SeedInventoryReferenceDataAsync` already adds missing (branch, type) rows for `AllTypes`, so existing databases get the four new rows. `BranchService.CreateAsync` already loops over `AllTypes`.
- Permissions:
  - `InventorySettingsController` `GET numbering` and `PUT numbering/{docType}` change from `[HasPermission(Inventory.Settings)]` to `[Authorize]`.
  - `InventorySettingsService.ListNumberingAsync` requires `inventory.settings` **or** `cash.settings` (else 403).
  - `UpdateNumberingAsync` requires `inventory.settings` for `StockIn`/`StockOut` and `cash.settings` for the other four (403).

1. **Write the failing tests** `CashDebt/CashNumberingTests.cs`:
   - `Seed_and_new_branch_have_numbering_for_all_six_types` (main branch rows for `Receipt` = `PT`, `Payment` = `PC`, `FundTransfer` = `CQ`, `DebtOffset` = `BT`; a branch created via the API has six rows)
   - `Numbering_permission_depends_on_type`:
     - a client with only `cash.settings` can GET the list and PUT `Receipt` (200), but PUT `StockIn` → 403;
     - a client with only `inventory.settings` PUT `Payment` → 403.
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~CashNumberingTests"`. Expected: FAIL.
3. **Write the minimal implementation:** enum values, defaults, permission changes. No migration is needed (ints in existing columns), and seed rows come from the seeder.
4. **Run tests to verify they pass:** `--filter "FullyQualifiedName~CashNumberingTests|FullyQualifiedName~NumberingSettingsTests|FullyQualifiedName~DocumentNumberFormatterTests"`. Expected: PASS.
5. **Commit:** `git commit -m "feat(cash): numbering for receipts, payments, fund transfers and debt offsets"`

### Task 1.5 — `Branch.OpeningDate` and its endpoint

Changes:
- `Branch.OpeningDate` (`DateOnly?`, column `opening_date`). Migration `AddBranchOpeningDate`, then append:
  ```csharp
  migrationBuilder.Sql("UPDATE branches b SET opening_date = s.d FROM (SELECT branch_id, MIN(opening_date) AS d FROM opening_stocks WHERE is_deleted = false GROUP BY branch_id) s WHERE s.branch_id = b.id;");
  ```
- `BranchDto` gains `OpeningDate`.
- Endpoint `PUT api/branches/{id}/opening-date`, `[HasPermission(Permissions.Branches.Manage)]`, body `SetOpeningDateRequest { DateOnly? OpeningDate }` → `BranchDto`. The validator allows null or 2000-01-01..2100-12-31.

`BranchService.SetOpeningDateAsync(Guid id, SetOpeningDateRequest request, ct)` runs in `ITransactionRunner`:
1. Take the **exclusive** branch gate for `id` (as `SetLockAsync` does).
2. Load the branch, else 404.
3. Run `PeriodLockGuard.EnsureNotLocked(branch.LockedUntil, [old, new] dates that are not null)`.
4. If `await HasDocumentsAsync(id, ct)` → `throw new ConflictException("BRANCH_HAS_DOCUMENTS", "Chi nhánh đã có chứng từ, không đổi được ngày đầu kỳ.")`.
   - Add the overload `public ConflictException(string code, string message) : base(code, message) { }` to `Domain/Common/DomainException.cs`. The middleware already maps `ConflictException` to 409 and copies `ce.Code`, so `error.code` is `BRANCH_HAS_DOCUMENTS` (the existing one-argument constructor keeps `CONFLICT`).
   - `HasDocumentsAsync` is a private method. For now it checks only `_db.StockVouchers.AnyAsync(v => v.BranchId == id)`. Phases 05, 06 and 08 each add their table.
5. Set the date and save.
6. If the date changed and the branch has opening stock rows, call `IOpeningStockService.RepostBranchAsync(id, newDate, ct)`.
   - `RepostBranchAsync`: for each warehouse with rows, set `OpeningDate`, re-post the ledger rows at `VnTime.StartOfDay(newDate)` with `acknowledgeNegativeStock: true`, then save.
   - The exclusive gate already covers this and no documents exist, so there is no shortage to report.
   - Clearing the date while opening stock rows exist → 409 `OPENING_DATA_EXISTS`.

1. **Write the failing tests** `CashDebt/BranchOpeningDateTests.cs`:
   - `Set_and_read_opening_date` (PUT 10-01 → GET `/api/branches` shows it; SALES PUT → 403)
   - `Rejected_when_branch_has_documents` (create a stock voucher in the main branch → PUT → 409 `BRANCH_HAS_DOCUMENTS`; a new branch B without vouchers → 200)
   - `Rejected_when_locked` (lock 10-05; PUT 10-01 → `PERIOD_LOCKED`; PUT 10-10 while the stored date is 10-01 → `PERIOD_LOCKED`)
   - `Changing_date_reposts_opening_stock` (branch B with opening date 10-01 and opening stock 5 units → PUT 09-01 → the opening ledger rows have `PostedAt == VnTime.StartOfDay(09-01)` and `OpeningStock.OpeningDate == 09-01`)
   - `Migration_backfill_sql_takes_min_date` — runs the backfill SQL from the migration on a clone:
     1. clear `opening_date`;
     2. insert two opening rows on 09-15 and 09-01;
     3. run the SQL;
     4. assert 09-01.
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~BranchOpeningDateTests"`. Expected: FAIL.
3. **Write the minimal implementation:** entity field, configuration, migration with the backfill SQL, DTO, request, validator, service method, controller action, `RepostBranchAsync` in `OpeningStockService` (+ interface).
4. **Run tests to verify they pass:** `--filter "FullyQualifiedName~BranchOpeningDateTests|FullyQualifiedName~BranchCrudTests"`. Expected: PASS.
5. **Commit:** `git commit -m "feat(branches): per-branch opening date, locked once the branch has documents"`

### Task 1.6 — Opening stock uses the branch opening date

Changes:
- `SaveOpeningStockRequest` loses `OpeningDate`.
- `OpeningStockService.SaveAsync` reads `Branch.OpeningDate` **after** the locks:
  - null → `DomainException("OPENING_DATE_NOT_SET", "Chưa đặt ngày đầu kỳ của chi nhánh.")` (400);
  - otherwise rows get `OpeningDate = branch.OpeningDate`, and the period-lock check uses that date.
- `GET` returns `OpeningDate = branch.OpeningDate` (null if unset).
- The validator rule for `OpeningDate` is removed.
- Test helpers:
  - Add `InventoryTestBase.SetBranchOpeningDateAsync(Guid branchId, DateOnly date)`, a direct DB update.
  - Update every test that saved opening stock with a date (`grep -rln "OpeningDate" backend/tests`): call the helper first and drop the field from the request.

1. **Write the failing tests** — add to `Inventory/OpeningStockTests.cs`:
   - `Save_without_branch_opening_date_returns_opening_date_not_set` (clear the main branch date → 400 `OPENING_DATE_NOT_SET`)
   - `Save_posts_at_branch_opening_date` (branch date 10-01 → ledger `PostedAt == VnTime.StartOfDay(10-01)`; GET returns `openingDate = 2026-10-01`)
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~OpeningStockTests"`. Expected: FAIL (new tests fail; existing ones still compile because the property exists).
3. **Write the minimal implementation:** the service/DTO/validator change, the helper, and the existing test updates.
4. **Run tests to verify they pass:** `--filter "FullyQualifiedName~OpeningStock|FullyQualifiedName~InventoryInvariantTests|FullyQualifiedName~StockCardReportTests|FullyQualifiedName~StockOnHandReportTests"`. Expected: PASS.
5. **Commit:** `git commit -m "refactor(inventory): opening stock posts at the branch opening date"`

## Verification

```bash
cd backend
dotnet build OrderMgmt.sln
dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~CashDebt|FullyQualifiedName~Inventory|FullyQualifiedName~Branch"
dotnet test OrderMgmt.sln   # compare failures with BASELINE.md
```

## Exit Criteria

- 30 new permission codes are seeded and granted per P5; a revoked grant stays revoked after a reseed.
- `GET/PUT /api/cash/settings` work, with default Warn/Warn.
- Six numbering rows exist per branch, with numbering permission per type.
- `Branch.OpeningDate` is settable only before any document; opening stock uses it.
- A 422 `ConfirmationRequiredException` is mapped; `PeriodLockGuard` exists.
- No new failures against `BASELINE.md`.
