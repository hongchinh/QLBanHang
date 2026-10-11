# Phase 01 — Backend shared helpers

**Status:** [ ] pending
**Complexity:** M

## Objective

Move the code the review found copied or hidden behind concrete classes into shared helpers, with no behaviour change:
- `LikePattern` (the `EscapeLike` copies of the three cash document services);
- `CashDocuments` (activity list, new activity, working-branch check, voucher date range of `CashVoucherService`, `FundTransferService`, `DebtOffsetService`);
- `SettlementRemovalConfirmation` (the 422 helpers that `StockVoucherService` reads from the concrete `DebtLedgerService`);
- `IReferenceUsage` (the hand-written "in use" lists of partners, payment methods, branches and funds).

## Files

- New: `backend/src/OrderMgmt.Application/Common/LikePattern.cs`
- New: `backend/src/OrderMgmt.Application/CashDebt/Common/CashDocuments.cs`
- New: `backend/src/OrderMgmt.Application/CashDebt/Ledger/SettlementRemovalConfirmation.cs`
- New: `backend/src/OrderMgmt.Application/Common/References/IReferenceUsage.cs`, `ReferenceUsage.cs`
- Modified: `backend/src/OrderMgmt.Application/CashDebt/CashVouchers/Services/CashVoucherService.cs`, `CashDebt/FundTransfers/Services/FundTransferService.cs`, `CashDebt/DebtOffsets/Services/DebtOffsetService.cs`, `CashDebt/Ledger/DebtLedgerService.cs`, `Inventory/StockVouchers/Services/StockVoucherService.cs`, `Catalog/Customers/Services/CustomerService.cs`, `Inventory/PaymentMethods/Services/PaymentMethodService.cs`, `Organization/Branches/Services/BranchService.cs`, `CashDebt/Funds/Services/FundService.cs`, `backend/src/OrderMgmt.Application/DependencyInjection.cs`
- New tests: `backend/tests/OrderMgmt.IntegrationTests/CashDebt/Unit/LikePatternTests.cs`, `CashDebt/Unit/CashDocumentsTests.cs`, `CashDebt/Unit/SettlementRemovalConfirmationTests.cs`, `backend/tests/OrderMgmt.IntegrationTests/CashDebt/ReferenceUsageTests.cs`

Commands (from `backend/`, after `export TEST_DB_CONNECTION="Host=localhost;Port=5432;Database=qldonhang_integtest;Username=postgres;Password=1"`):
- unit: `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~CashDebt.Unit"`
- class: `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~<ClassName>"`
- area: `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~OrderMgmt.IntegrationTests.CashDebt"`

## Tasks

### Task 1.1 — `LikePattern`

1. Write the failing test `CashDebt/Unit/LikePatternTests.cs` (no collection, no DB): `LikePattern.Escape("50%_a\\b")` returns `"50\\%\\_a\\\\b"`, and `LikePattern.EscapeCharacter` is `"\\"`.
2. Run `--filter "FullyQualifiedName~LikePatternTests"` — expected: build error CS0103 (`LikePattern` does not exist).
3. Create `Application/Common/LikePattern.cs`:
   ```csharp
   namespace OrderMgmt.Application.Common;

   /// ILike patterns from user input. Every ILike on an escaped pattern must pass EscapeCharacter: without an escape
   /// argument Npgsql sends ESCAPE '' and a search containing %, _ or \ misses the rows that contain it (F-1).
   public static class LikePattern
   {
       public const string EscapeCharacter = "\\";

       public static string Escape(string input) =>
           input.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
   }
   ```
   Replace the private `EscapeLike` and `LikeEscape` (and their comment) of `CashVoucherService`, `FundTransferService` and `DebtOffsetService` with `LikePattern.Escape` / `LikePattern.EscapeCharacter`. The three services already pass the escape argument to every `ILike`; keep the calls otherwise unchanged.
4. Run the unit test and `--filter "FullyQualifiedName~CashVoucherQueryTests|FullyQualifiedName~FundTransfer|FullyQualifiedName~DebtOffsetTests"` — expected: PASS.
5. Commit `refactor(cash): share LikePattern across the cash document services`.

### Task 1.2 — `CashDocuments`

1. Write the failing test `CashDebt/Unit/CashDocumentsTests.cs`:
   - `CashDocuments.IsValidVoucherAt(new DateTimeOffset(1999,12,31,23,0,0,TimeSpan.Zero), now)` is false; `2000-01-01T00:00Z` is true; `now.AddYears(1)` is true; `now.AddYears(1).AddSeconds(1)` is false.
   - `CashDocuments.NewActivity(DocumentType.Receipt, id, CashDocumentActivityAction.Updated, actor, at, "Cập nhật phiếu")` returns an activity with exactly those values and `MetadataJson == null`.
2. Run `--filter "FullyQualifiedName~CashDocumentsTests"` — expected: build error CS0103.
3. Create `Application/CashDebt/Common/CashDocuments.cs`, a static class with:
   - `public static readonly DateTimeOffset MinVoucherAt = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);`
   - `public static bool IsValidVoucherAt(DateTimeOffset voucherAt, DateTimeOffset utcNow) => voucherAt >= MinVoucherAt && voucherAt <= utcNow.AddYears(1);`
   - `public static CashDocumentActivity NewActivity(DocumentType type, Guid documentId, CashDocumentActivityAction action, Guid? actorUserId, DateTimeOffset occurredAt, string description)`
   - `public static Task<List<CashDocumentActivityDto>> ListActivitiesAsync(IAppDbContext db, DocumentType type, Guid documentId, CancellationToken ct)` — the query now copied in the three `ListActivitiesAsync` methods (ordered by `OccurredAt` descending, actor name "Hệ thống" / "Người dùng không xác định").
   - `public static async Task EnsureWorkingBranchAsync(ICurrentBranch currentBranch, Guid branchId, string message, CancellationToken ct)` — throws `ForbiddenException(message)` when `branchId` is not the working branch.
   In the three services: delete `MinVoucherAt`, `IsValidVoucherAt`, `AddActivity`'s body (keep a one-line private wrapper that calls `_db.CashDocumentActivities.Add(CashDocuments.NewActivity(...))`), the activity query and `EnsureWorkingBranchAsync`'s body; call the helpers. Keep each service's own `ForbiddenException` message text.
4. Run the unit test and the area command — expected: PASS (the activity, permission and date tests of the three services are the regression net).
5. Commit `refactor(cash): share activity, branch and date helpers of the cash documents`.

### Task 1.3 — `SettlementRemovalConfirmation`

1. Write the failing test `CashDebt/Unit/SettlementRemovalConfirmationTests.cs`:
   - `SettlementRemovalConfirmation.Code == "DEBT_SETTLEMENTS_WILL_BE_REMOVED"`.
   - `Create(details)` returns a `ConfirmationRequiredException` with that code, `CanConfirm == true`, message "Chứng từ đã được đối trừ. Lưu sẽ bỏ các đối trừ sau." and the given details.
   - `Merge(a, b)` merges details by key (values of the same key concatenated, `a` first).
2. Run `--filter "FullyQualifiedName~SettlementRemovalConfirmationTests"` — expected: build error CS0103.
3. Create `Application/CashDebt/Ledger/SettlementRemovalConfirmation.cs` (static class) and move into it, unchanged: `DebtLedgerService.SettlementsWillBeRemovedCode` (as `Code`), the private `SettlementsWillBeRemoved(IDictionary<string,string[]>)` (as `Create`) and `DebtLedgerService.MergeSettlementRemovals` (as `Merge`). `DebtLedgerService` keeps its private overload that builds the details from settlements and calls `Create`. Replace `DebtLedgerService.SettlementsWillBeRemovedCode` / `.MergeSettlementRemovals` in `StockVoucherService` (and any test) with the new class, so no caller references the concrete `DebtLedgerService`.
4. Run the unit test and `--filter "FullyQualifiedName~CashDebt.StockVoucherDebt|FullyQualifiedName~DebtLedgerPostTests|FullyQualifiedName~DebtSettlementTests"` — expected: PASS.
5. Commit `refactor(debt): move the settlement-removal confirmation helpers out of DebtLedgerService`.

### Task 1.4 — `IReferenceUsage`

1. Write the failing test `CashDebt/ReferenceUsageTests.cs` (`[Collection(nameof(PostgresCollection))]`, `: CashDebtTestBase`, fresh branch via `CreateBranchAsync` + `SetBranchOpeningDateAsync`, resolve `IReferenceUsage` through `InTransactionAsync((sp, ct) => sp.GetRequiredService<IReferenceUsage>()...)`):
   - `PartnerHasCashOrDebtDataAsync` true for a partner with only an opening debt, false for a fresh partner;
   - `PartnerHasDebtOnSideAsync(partner, Receivable)` true / `(partner, Payable)` false for that partner;
   - `PaymentMethodUsedByPaidStockVouchersAsync` true after a paid sale with the method, false for a fresh method;
   - `BranchHasDocumentsAsync` / `BranchHasOpeningDataAsync` / `EarliestDocumentDateAsync` for a branch with one receipt at `2026-10-05 09:00` (VN date 10-05);
   - `FundUsageAsync(fund)` returns `FundUsage.CashVouchers` for a fund used by a receipt and `FundUsage.None` for a fresh fund.
2. Run `--filter "FullyQualifiedName~ReferenceUsageTests"` — expected: build error CS0246 (`IReferenceUsage` not found).
3. Create `Application/Common/References/IReferenceUsage.cs` and `ReferenceUsage.cs` (scoped, `IAppDbContext`), moving the queries unchanged from:
   - `CustomerService.HasCashOrDebtDataAsync` → `PartnerHasCashOrDebtDataAsync(Guid partnerId, ct)`; `CustomerService.HasDebtAsync` → `PartnerHasDebtOnSideAsync(Guid partnerId, DebtSide side, ct)`; add `PartnerHasStockVouchersAsync(Guid partnerId, ct)` (the stock voucher check of `DeleteAsync`);
   - `PaymentMethodService` → `PaymentMethodUsedByStockVouchersAsync(Guid methodId, ct)` (delete) and `PaymentMethodUsedByPaidStockVouchersAsync(Guid methodId, ct)` (fund kind change);
   - `BranchService.HasDocumentsAsync`, `HasOpeningDataAsync`, `EarliestDocumentDateAsync` → `BranchHasDocumentsAsync`, `BranchHasOpeningDataAsync`, `EarliestDocumentDateAsync`;
   - `FundService.EnsureNotUsedAsync`'s three queries → `FundUsageAsync(Guid fundId, ct)` returning `enum FundUsage { None, CashVouchers, FundTransfers, StockVouchers }` (first match, same order as today); `FundService` keeps the `OpeningAmount` check and maps each value to today's message.
   Register `services.AddScoped<IReferenceUsage, ReferenceUsage>()` in `Application/DependencyInjection.cs`, inject it into the four services, delete the moved private methods.
4. Run the new test and `--filter "FullyQualifiedName~PartnerDebtGuardTests|FullyQualifiedName~PaymentMethodFundKindTests|FullyQualifiedName~BranchDeleteGuardTests|FullyQualifiedName~BranchOpeningDateTests|FullyQualifiedName~FundCrudTests|FullyQualifiedName~CatalogConstraintTests|FullyQualifiedName~PartnerRoleTests"` — expected: PASS.
5. Commit `refactor(cash): one reference usage checker for partners, payment methods, branches and funds`.

## Verification

- `dotnet build OrderMgmt.sln` — 0 errors.
- Area command (`OrderMgmt.IntegrationTests.CashDebt`) — all pass.
- `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~Inventory|FullyQualifiedName~Catalog|FullyQualifiedName~Organization"` — no failure outside BASELINE.md.

## Exit Criteria

- No `EscapeLike`, `MinVoucherAt`, activity query or `EnsureWorkingBranchAsync` body is duplicated across the three cash document services.
- No code outside `DebtLedgerService` references `DebtLedgerService` members.
- Every "is used" query of partners, payment methods, branches and funds lives in `ReferenceUsage`.
