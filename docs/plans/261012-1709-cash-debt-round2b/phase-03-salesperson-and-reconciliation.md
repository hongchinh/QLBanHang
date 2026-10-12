# Phase 03 — By-salesperson & reconciliation reports

**Status:** [ ] pending
**Complexity:** M

## Objective

Deliver the last two backend reports:

- **Công nợ theo nhân viên** (`GET /api/reports/debt-by-salesperson`): per owner of the debt document (B1), opening open, new debt, settled, closing open and its overdue part over a period, plus an unallocated line; every row and the totals reconcile (B7).
- **Biên bản đối chiếu công nợ** (`GET /api/reports/debt-reconciliation`): the ledger of one partner for each side it has, with the net amount when it has both (B8). On screen only; the print template is Round 3.

- **Owner options for opening debts** (`GET /api/opening-debts/owners`): the users offered by the owner picker that Phase 05 adds to the opening-debt form (B1).

After this phase the backend contracts of Round 2B are frozen for Phases 04–05.

## Files

- `backend/src/OrderMgmt.Application/CashDebt/Reports/Interfaces/IDebtPositionReportService.cs` (modify)
- `backend/src/OrderMgmt.Application/CashDebt/Reports/Models/DebtPositionReportDtos.cs` (modify)
- `backend/src/OrderMgmt.Application/CashDebt/Reports/Services/DebtPositionReportService.cs` (modify)
- `backend/src/OrderMgmt.Application/CashDebt/Reports/Validators/DebtPositionReportValidators.cs` (modify)
- `backend/src/OrderMgmt.WebApi/Controllers/DebtPositionReportsController.cs` (modify)
- `backend/src/OrderMgmt.Application/CashDebt/Reports/Interfaces/ICashDebtReportService.cs` (modify)
- `backend/src/OrderMgmt.Application/CashDebt/Reports/Models/CashDebtReportDtos.cs` (modify)
- `backend/src/OrderMgmt.Application/CashDebt/Reports/Services/CashDebtReportService.cs` (modify)
- `backend/src/OrderMgmt.Application/CashDebt/Reports/Validators/CashDebtReportValidators.cs` (modify)
- `backend/src/OrderMgmt.WebApi/Controllers/CashDebtReportsController.cs` (modify)
- `backend/tests/OrderMgmt.IntegrationTests/CashDebt/Reports/ReportDataset.cs` (modify — expose the warehouse id)
- `backend/tests/OrderMgmt.IntegrationTests/CashDebt/Reports/DebtBySalespersonReportTests.cs` (new)
- `backend/tests/OrderMgmt.IntegrationTests/CashDebt/Reports/DebtReconciliationReportTests.cs` (new)
- `backend/src/OrderMgmt.Application/CashDebt/OpeningDebts/Interfaces/IOpeningDebtService.cs`, `Models/OpeningDebtDtos.cs`, `Services/OpeningDebtService.cs` (modify — owner options)
- `backend/src/OrderMgmt.WebApi/Controllers/OpeningDebtsController.cs` (modify)
- `backend/tests/OrderMgmt.IntegrationTests/CashDebt/OpeningDebtTests.cs` (modify)

## Reference files (read-only)

- Phase 01 of this plan — dataset, hand-computed positions, `DebtPositionQuery`
- `backend/src/OrderMgmt.Application/CashDebt/Reports/Services/CashDebtReportService.cs` — `BuildDebtLedgerAsync` (reused by the reconciliation)
- `backend/tests/OrderMgmt.IntegrationTests/Inventory/InventoryTestBase.cs` — `CreateClientWithPermissionsAsync`, `CreatePartnerAsync`, `UseBranch`

## API contract

| Method | Route | Service | Response |
|---|---|---|---|
| GET | `debt-by-salesperson?side=&from=&to=` | `IDebtPositionReportService.GetBySalespersonAsync` | `DebtBySalespersonReportDto` |
| GET | `debt-reconciliation?partnerId=&from=&to=` | `ICashDebtReportService.GetDebtReconciliationAsync` | `DebtReconciliationReportDto` |
| GET | `api/opening-debts/owners` (permission `debt.opening_balance`) | `IOpeningDebtService.ListOwnersAsync` | `List<OpeningDebtOwnerDto>` |

Both `reports.debt`, `[FromQuery]`, in `RunSnapshotAsync`.

```csharp
// DebtPositionReportDtos.cs
DebtBySalespersonReportRequest { DebtSide Side = DebtSide.Receivable; DateOnly From; DateOnly To; Guid? PartnerId; }
DebtBySalespersonReportDto { DebtSide Side; DateOnly From; DateOnly To;
    List<DebtBySalespersonRowDto> Rows; DebtBySalespersonRowDto Totals;   // Totals: OwnerUserId/OwnerName null
    DebtUnallocatedLineDto Unallocated;
    decimal OpeningBalance;    // Totals.OpeningOpen − Unallocated.Opening
    decimal ClosingBalance; }  // Totals.ClosingOpen − Unallocated.Closing
DebtBySalespersonRowDto { Guid? OwnerUserId; string? OwnerName;
    decimal OpeningOpen; decimal Increase; decimal Settled; decimal ClosingOpen; decimal ClosingOverdue; }
DebtUnallocatedLineDto { decimal Opening; decimal Closing; }

// OpeningDebtDtos.cs
OpeningDebtOwnerDto { Guid Id; string FullName; }   // active users, ordered by FullName (vi-VN comparer), then Id

// CashDebtReportDtos.cs
DebtReconciliationReportRequest { Guid PartnerId; DateOnly From; DateOnly To; }
DebtReconciliationReportDto { Guid PartnerId; string PartnerCode; string PartnerName; string? TaxCode; string? Address;
    string BranchName; DateOnly From; DateOnly To;
    List<DebtLedgerReportDto> Sides;   // Receivable first, then Payable
    decimal? NetBalance; }             // Receivable.ClosingBalance − Payable.ClosingBalance when both sides are listed
```

By-salesperson rules (B7):
- `startItems = DebtPositionQuery.LoadAsync(..., side, request.PartnerId, VnTime.ToUtc(VnTime.StartOfDay(From)))` (counted "at From − 1"); `endItems = LoadAsync(..., request.PartnerId, VnTime.ToUtc(VnTime.StartOfNextDay(To)))`. Every start item is also an end item.
- For each Increase item in `endItems`, owner = `OwnerUserId` (null allowed), `s` = the start item with the same `EntryId` or null:
  - `OpeningOpen += s?.Open ?? 0`
  - `Increase += s is null ? Amount : 0`
  - `Settled += Settled − (s?.Settled ?? 0)`
  - `ClosingOpen += Open`
  - `ClosingOverdue += (DueDate ?? DocDate) < To ? Open : 0`
- Rows: owners with any non-zero value; `OwnerName` from `Users.IgnoreQueryFilters()` `FullName`; ordered by `OwnerName` with the vi-VN comparer used by `StockVoucherService.ListOwnersAsync`, then `OwnerUserId`, the null-owner row last. `Totals` is an all-zero row when there are no rows (B12).
- `Unallocated.Opening` = Σ `Open` of Decrease items in `startItems`; `Unallocated.Closing` = Σ `Open` of Decrease items in `endItems`.
- Validator: `Side` in enum, `From`/`To` in `ReportDates`, `To ≥ From` (same messages as `DebtBalanceReportRequestValidator`).

Reconciliation rules (B8):
- Partner from `Customers.IgnoreQueryFilters()` (`Code`, `Name`, `TaxCode`, `CompanyAddress`, `IsCustomer`, `IsSupplier`), else `NotFoundException(nameof(Customer), id)`; `BranchName` from `Branches` of the working branch.
- Side listed when `(side == Receivable ? IsCustomer : IsSupplier)` or any `DebtEntries` row exists for (branch, partner, side). Each listed side = `await BuildDebtLedgerAsync(new DebtLedgerReportRequest { Side, PartnerId, From, To }, ct)` inside the one snapshot.
- Validator: `PartnerId` not empty ("Vui lòng chọn đối tượng."), dates as above.

## Hand-computed expectations

**By-salesperson** — dataset plus one extra sale made in the test by a second user `sp2`: XBH `PX00002` 2,000 to KH1 at 10-20 09:00, paid 0 (due 10-20). `admin` owns every dataset voucher; the opening debts have no owner.

| Query | Row | OpeningOpen | Increase | Settled | ClosingOpen | ClosingOverdue |
|---|---|---|---|---|---|---|
| Receivable 10-01..10-31 | admin | 0 | 3,000 | 1,000 | 2,000 | 2,000 |
| | sp2 | 0 | 2,000 | 0 | 2,000 | 2,000 |
| | (no owner) | 5,000 | 0 | 4,000 | 1,000 | 1,000 |
| | Totals | 5,000 | 5,000 | 5,000 | 5,000 | 5,000 |
| | Unallocated | opening 1,000 (`TT-OLD`) | | | closing 1,500 (`TT-OLD` + `PN00001`) | |
| | Balances | OpeningBalance **4,000** | | | ClosingBalance **3,500** | |
| Receivable 10-06..10-31 | admin | 2,000 | 0 | 0 | 2,000 | 2,000 |
| | sp2 | 0 | 2,000 | 0 | 2,000 | 2,000 |
| | (no owner) | 1,000 | 0 | 0 | 1,000 | 1,000 |
| | Unallocated / balances | opening 1,000, closing 1,500 | | | OpeningBalance **2,000**, ClosingBalance **3,500** | |
| Payable 10-01..10-31 | admin | 0 | 6,000 | 2,000 | 4,000 | 4,000 |
| | Unallocated / balances | opening 0, closing 3,000 | | | OpeningBalance 0, ClosingBalance **1,000** | |

**Reconciliation** — KH1 (customer only), 10-01..10-31: `Sides` = [Receivable], equal to `GET debt-ledger?side=Receivable&partnerId=KH1&from=2026-10-01&to=2026-10-31` (closing 1,500); `NetBalance` null; `BranchName` = `"Chi nhánh RD{suffix}"` (the main branch's `Name`). A partner `BOTH` (customer and supplier) created in the test with XBH 1,000 at 10-20 and NMH 400 at 10-21, both paid 0: 10-01..10-31 → two sides, closings 1,000 and 400, `NetBalance` 600; 10-01..10-20 → the payable side is still listed (role) with no rows and closing 0, `NetBalance` 1,000.

## Tasks

### Task 3.1 — By-salesperson report

1. **Write the failing tests** `CashDebt/Reports/DebtBySalespersonReportTests.cs`:
   - First add `public Guid MainWarehouseId { get; init; }` to `ReportIds` and set it in `ReportDataset.SeedAsync` (the `mainWarehouseId` it already creates), so tests can post more stock vouchers with `ReportDataset.PostStockAsync`.
   - A private helper `SeedWithSecondSalespersonAsync()`: seed the dataset; create `sp2` with `CreateClientWithPermissionsAsync("sp2_" + suffix, ids.MainBranchId, Permissions.StockOut.View, Permissions.StockOut.Create)` (add any permission the stock-out save names in a 403); `UseBranch(sp2, ids.MainBranchId)`; post the extra sale with `ReportDataset.PostStockAsync(this, sp2, StockDirection.Out, "XBH", "2026-10-20 09:00", ids.MainWarehouseId, ids.Kh1Id, 2_000m, paymentMethodId: null, paid: 0m)`; return the ids, the admin user id/name and the sp2 user id/name (from `Users`).
   - `Receivable_by_owner_for_october`, `Receivable_by_owner_from_mid_month`, `Payable_by_owner` — the three blocks of the table (rows compared with `BeEquivalentTo(expected, o => o.WithStrictOrdering())`, because the order is part of the contract: by `OwnerName` (vi-VN), the null-owner row last).
   - `Opening_match_straddling_the_period`: match `TT-OLD` 1,000 (opening, before `From`) to sp2's `PX00002` through `POST /api/debt/settlements`; the settlement is effective 10-20, the posting day of `PX00002`. For 10-01..10-31: sp2 row Increase 2,000, Settled 1,000, ClosingOpen 1,000, ClosingOverdue 1,000 (this proves that `Settled` counts a Decrease that predates `From`, B7); admin and no-owner rows as in the first block; `Unallocated.Opening` 1,000 (the match is not counted at `From − 1`) and `Unallocated.Closing` 500 (`PN00001` only); Σ ClosingOpen 4,000, `OpeningBalance` 4,000, `ClosingBalance` 3,500 (a match never changes a balance).
   - `Opening_debt_with_an_owner_is_grouped_under_that_owner`: create an opening debt for KH1 (`POST /api/opening-debts`, 700, doc date before the opening date, `ownerUserId` = sp2) → for 10-01..10-31 sp2 has `OpeningOpen` 700 and the no-owner row is unchanged; `OpeningBalance` is 700 higher than in the first block.
   - `Partner_filter`: `partnerId=KH1` equals the unfiltered result (only KH1 has receivable debt); `partnerId=NCC1` on the Receivable side → no rows, all-zero `Totals`, both balances 0.
   - `Empty_result_is_zero_filled`: `side=Payable&from=2026-10-01&to=2026-10-04` → no rows, all-zero `Totals`, both balances 0.
   - `Rows_reconcile_with_the_balance_report`: for each row `OpeningOpen + Increase − Settled == ClosingOpen`; for both sides and both windows, `OpeningBalance` / `ClosingBalance` equal the signed opening / closing of `GET debt-balance` `Totals` for the same window.
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~DebtBySalespersonReportTests"`. Expected: FAIL — build error, `DebtBySalespersonReportDto` does not exist (404 once the empty DTO classes exist).
3. **Write the minimal implementation:** `GetBySalespersonAsync`, DTOs, controller action `GET debt-by-salesperson`, an empty validator class (rules in Task 3.3).
4. **Run the tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git commit -m "feat(debt): debt by salesperson report"`

### Task 3.2 — Reconciliation report

1. **Write the failing tests** `CashDebt/Reports/DebtReconciliationReportTests.cs`:
   - `Customer_only_partner` (KH1 expectations above; header `PartnerCode`, `PartnerName`, `BranchName`).
   - `Partner_on_both_sides` (`CreatePartnerAsync("BOTH" + suffix, isCustomer: true, isSupplier: true)`, the two vouchers via `ReportDataset.PostStockAsync` with `ids.MainWarehouseId`, both windows above).
   - `Partner_on_both_sides_after_an_offset` (the debt offset, bù trừ, that the approved report dataset includes): on `BOTH`, `POST /api/debt-offsets` with `VoucherAt` 10-22 09:00 VN, `PartnerId`, one receivable allocation (the 1,000 sale entry, 400) and one payable allocation (the 400 purchase entry, 400) — build the request the way `CashDebt/Matching/DebtOffsetTests.cs` does. For 10-01..10-31: the receivable ledger closes at 600 and the payable at 0, `NetBalance` 600 (an offset never changes the net). In the same test: `debt-documents?side=Receivable&at=2026-10-31&partnerId={BOTH}` shows the sale Settled 400, Remaining 600, and `debt-by-salesperson` (Receivable, 10-01..10-31, `partnerId={BOTH}`) has `Settled` 400 on the creator's row — an offset is a settlement origin other than a receipt, and B7 counts every origin.
   - `Side_with_entries_is_listed_without_the_role`: set `IsCustomer = false` on KH1 through `InDbAsync` → the receivable side is still listed with closing 1,500; `NetBalance` null (no payable role, no payable entries).
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~DebtReconciliationReportTests"`. Expected: FAIL — build error, `DebtReconciliationReportDto` does not exist (404 once the empty DTO classes exist).
3. **Write the minimal implementation:** `GetDebtReconciliationAsync` in `ICashDebtReportService` / `CashDebtReportService` (one `RunSnapshotAsync`, reusing `BuildDebtLedgerAsync`), DTOs, an empty validator class, action `GET debt-reconciliation` in `CashDebtReportsController`.
4. **Run the tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git commit -m "feat(debt): on-screen debt reconciliation report"`

### Task 3.3 — Validation and permission

1. **Write the failing tests** `Validation_and_permission` in both classes:
   - by-salesperson: `from=2026-10-31&to=2026-10-01` → 400 key `to`; `reports.cash`-only client → 403 (`Side` defaults to Receivable, so an omitted `side` is valid and there is no `side` test here);
   - reconciliation: missing `partnerId` → 400 key `partnerId`; `from` > `to` → 400; an unknown `partnerId` → 404; `reports.cash`-only client → 403.
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~Validation_and_permission"`. Expected: FAIL — the invalid requests return 200 or 500 (empty validators).
3. **Write the minimal implementation:** the validator rules from the contract.
4. **Run tests to verify they pass:** `--filter "FullyQualifiedName~CashDebt.Reports"`. Expected: PASS (all 2A and 2B report tests).
5. **Commit:** `git commit -m "feat(debt): validate salesperson and reconciliation report requests"`

### Task 3.4 — Opening-debt owner options (B1)

1. **Write the failing tests** in `CashDebt/OpeningDebtTests.cs`:
   - `Owner_options_list_active_users`: `GET /api/opening-debts/owners` as the admin client → 200; it contains the admin and a second active user created in the test, each as `{ id, fullName }`; a deactivated user and a soft-deleted user are absent; the list is ordered by `FullName` (vi-VN comparer, then id);
   - `Owner_options_need_the_opening_balance_permission`: a client with only `reports.debt` → 403; a client with only `debt.opening_balance` → 200 (so an accountant needs no `users.view`);
   - `Opening_debt_keeps_the_picked_owner`: create an opening debt with `ownerUserId` = the second user → the list item returns `ownerUserId` and `ownerName`; update it with `ownerUserId = null` → both are null again and the debt entry's `OwnerUserId` follows (read `DebtEntries` through `InDbAsync`).
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~OpeningDebtTests"`. Expected: FAIL — build error, `OpeningDebtOwnerDto` does not exist (404 for the endpoint once it does).
3. **Write the minimal implementation:** `OpeningDebtOwnerDto`; `IOpeningDebtService.ListOwnersAsync(CancellationToken)` — `_db.Users` (the soft-delete filter applies; keep the active ones with the property the `User` entity really has), sorted in memory with the vi-VN comparer of `StockVoucherService.ListOwnersAsync`, select `Id`, `FullName`; controller action `[HttpGet("owners")]` with `[HasPermission(Permissions.Debt.OpeningBalance)]`.
4. **Run tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git add <the files above> && git commit -m "feat(debt): opening debt owner options"`

### Task 3.5 — Backend checkpoint

1. Run the full backend suite: `dotnet test OrderMgmt.sln` and compare failures with `BASELINE.md` test by test.
2. If a new failure appears, fix it with a focused failing test first (in the service where it lives), then re-run.
3. Commit only if a fix was needed: `git commit -m "fix(debt): <what>"`.

## Verification

```bash
export TEST_DB_CONNECTION="Host=localhost;Port=5432;Database=qldonhang_integtest;Username=postgres;Password=1"
cd backend
dotnet build OrderMgmt.sln
dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~CashDebt"
dotnet test OrderMgmt.sln   # compare with BASELINE.md
```

## Exit Criteria

- `debt-by-salesperson` matches the table (strict row order), accepts `partnerId`, and puts an opening debt on the owner picked on it; each row satisfies opening + increase − settled = closing; the balances equal the balance report for both sides and both windows.
- `debt-reconciliation` lists the right sides, reuses the ledger numbers and computes the net amount, also after a debt offset.
- `GET /api/opening-debts/owners` returns the active users to holders of `debt.opening_balance`.
- Full backend suite: no new failures against `BASELINE.md`.
- **The five Round 2B API contracts and the owner-options endpoint are frozen for Phases 04–05.**
