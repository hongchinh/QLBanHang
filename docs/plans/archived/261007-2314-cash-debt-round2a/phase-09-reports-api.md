# Phase 09 — Reports API & invariant replay

**Status:** [x] complete
**Complexity:** L

## Objective

Deliver the three 2A reports from the ledger and voucher tables: **Số dư công nợ** (debt balance summary), **Sổ chi tiết công nợ** (debt ledger per partner) and **Sổ quỹ** (cash book, with several funds and transfers, P25). Each is verified against hand-computed numbers. Then add a replay test proving that back-dated edits give the same debt and fund state as entering the final data directly.

## Files

- `backend/src/OrderMgmt.Domain/Enums/CashDebtEnums.cs` (modify — `CashBookSourceType`, `FundKindFilter`)
- `backend/src/OrderMgmt.Application/CashDebt/Reports/{Interfaces/ICashDebtReportService.cs,Models/CashDebtReportDtos.cs,Services/CashDebtReportService.cs,Validators/CashDebtReportValidators.cs}` (new)
- `backend/src/OrderMgmt.Application/DependencyInjection.cs` (modify)
- `backend/src/OrderMgmt.WebApi/Controllers/CashDebtReportsController.cs` (new)
- `backend/tests/OrderMgmt.IntegrationTests/CashDebt/Reports/{ReportDataset.cs,DebtBalanceReportTests,DebtLedgerReportTests,CashBookReportTests}.cs` (new)
- `backend/tests/OrderMgmt.IntegrationTests/CashDebt/CashDebtInvariantReplayTests.cs` (new)

## Reference files (read-only)

- `backend/src/OrderMgmt.Application/Inventory/Reports/**`, `InventoryReportsController.cs` — report service/controller/validator shape (`ValidateAndThrowAsync`, `[FromQuery]`)
- `backend/tests/OrderMgmt.IntegrationTests/Inventory/InventoryInvariantTests.cs` — replay pattern

## API contract

Route `api/reports` (a separate controller from `InventoryReportsController`). Parameters bind `[FromQuery]`. All reports run in the working branch. Date windows are VN dates: opening = entries with `PostedAt < VnTime.StartOfDay(from)` **or** `SourceType == Opening`; period = the other entries in `[StartOfDay(from), StartOfNextDay(to))`. Opening debts are posted at 00:00 of the branch opening date, so a report starting on the opening date must still show them as opening balance, not as movements. The same rule applies to a fund's `OpeningAmount` (P17). The validator requires `from ≤ to`.

| Method | Route | Permission | Response |
|---|---|---|---|
| GET | `debt-balance?side=&from=&to=&partnerId=&search=` | `reports.debt` | `DebtBalanceReportDto` |
| GET | `debt-ledger?side=&partnerId=&from=&to=` | `reports.debt` | `DebtLedgerReportDto` |
| GET | `cash-book?kind=&fundId=&from=&to=` | `reports.cash` | `CashBookReportDto` |

DTOs use shorthand field lists; every member is a public `{ get; set; }` auto-property.

```csharp
public enum CashBookSourceType { Receipt = 1, Payment = 2, TransferIn = 3, TransferOut = 4 }
public enum FundKindFilter { All = 0, Cash = 1, Bank = 2 }

DebtBalanceReportRequest { DebtSide Side; DateOnly From; DateOnly To; Guid? PartnerId; string? Search; }
DebtBalanceReportDto { Side; From; To; List<DebtBalanceRowDto> Rows; DebtBalanceRowDto Totals; }
DebtBalanceRowDto { Guid? PartnerId; string? PartnerCode; string? PartnerName; decimal OpeningDebit; decimal OpeningCredit;
    decimal Increase; decimal Decrease; decimal ClosingDebit; decimal ClosingCredit; }
// Rows: one per partner with any non-zero value, ordered by PartnerCode. Opening/closing are split with DebtBalanceSign.Split (P24).
// Totals sums each column (Totals.PartnerId = null).

DebtLedgerReportRequest { DebtSide Side; Guid PartnerId; DateOnly From; DateOnly To; }
DebtLedgerReportDto { Side; PartnerId; PartnerCode; PartnerName; From; To; decimal OpeningBalance; decimal OpeningDebit; decimal OpeningCredit;
    List<DebtLedgerRowDto> Rows; decimal TotalIncrease; decimal TotalDecrease; decimal ClosingBalance; decimal ClosingDebit; decimal ClosingCredit; }
DebtLedgerRowDto { Guid EntryId; DateTimeOffset PostedAt; DateOnly DocDate; string DocCode; DebtSourceType SourceType; Guid SourceId;
    string? Description; decimal Increase; decimal Decrease; decimal RunningBalance; }
// Rows ordered by PostedAt, SourceType, DocCode.

CashBookReportRequest { FundKindFilter Kind = All; Guid? FundId; DateOnly From; DateOnly To; }
CashBookReportDto { Kind; From; To; List<CashBookGroupDto> Groups; decimal GrandOpening; decimal GrandIn; decimal GrandOut; decimal GrandClosing; }
CashBookGroupDto { FundId; FundCode; FundName; FundKind; BankName; AccountNumber; decimal Opening; List<CashBookRowDto> Rows;
    decimal TotalIn; decimal TotalOut; decimal Closing; }
CashBookRowDto { DateTimeOffset At; CashBookSourceType SourceType; Guid SourceId; string? ReceiptCode; string? PaymentCode;
    string? TransferCode; string? PartnerOrPerson; string? Description; decimal In; decimal Out; decimal Running; }
```

Cash book rules:
- **Fund selection:**
  - `FundId` set → that fund only. It must be in the working branch (else 404), and it must match `Kind` when `Kind ≠ All` (else 400 `fundId`).
  - Otherwise every fund of the branch matching `Kind`, active or not, ordered by Kind and Code.
- **Group opening:** `OpeningAmount` + Σ movements before `StartOfDay(from)` (P17 movements, the same source as `IFundBalanceGuard.MovementsAsync`).
- **Rows** (sorted by At, inflow before outflow, then code):
  - receipts → `In`, `ReceiptCode`;
  - payments → `Out`, `PaymentCode`;
  - transfers → `TransferIn` / `TransferOut`, `TransferCode`.
- **Text columns:**
  - `PartnerOrPerson` = partner name snapshot, else `PersonName`;
  - receipt/payment `Description` = `Description ?? reason name`;
  - transfer `Description` = `Description ?? "Chuyển quỹ {from.Code} → {to.Code}"`.
- **Grand totals (P25):**
  - `GrandOpening = Σ Opening` and `GrandClosing = Σ Closing`;
  - `GrandIn` / `GrandOut` leave out a transfer when both of its funds are in the selection;
  - therefore `GrandOpening + GrandIn − GrandOut == GrandClosing`.

## Tasks

### Task 9.1 — Shared report dataset

Create `CashDebt/Reports/ReportDataset.cs`: a static builder `Task<ReportIds> SeedAsync(CashDebtTestBase t)` that creates, through the API, in the main branch (opening date 2026-10-01; branch B is used for isolation):

| # | Date | Document | Amount | Notes |
|---|---|---|---|---|
| 1 | opening | KH1 receivable Debt `HD-OLD` | 5,000 | due 09-20 |
| 2 | opening | KH1 receivable Prepayment `TT-OLD` | 1,000 | |
| 3 | opening | QTM opening balance | 10,000 | NH01 bank fund opening 20,000 |
| 4 | 10-03 | XBH to KH1 | 3,000 | paid 1,000 TM → auto receipt `PT00001` |
| 5 | 10-05 | Receipt TKH from KH1, QTM | 4,000 | allocated `HD-OLD` 4,000 |
| 6 | 10-08 | NTL from KH1 (customer return) | 500 | paid 0 |
| 7 | 10-10 | NMH from NCC1 | 6,000 | paid 2,000 CK → auto payment `PC00001` from NH01 |
| 8 | 10-12 | Payment TNCC to NCC1, NH01 | 3,000 | |
| 9 | 10-15 | Fund transfer NH01 → QTM | 5,000 | |
| 10 | 10-16 | Payment CK (other), QTM | 700 | no partner, PersonName "Nguyễn A" |
| 11 | 10-18 | Receipt TKH from KH1 | 300 | then **cancelled** |
| 12 | 10-03 | Branch B: XBH to KH1 | 9,999 | must never appear |

Hand-computed expectations (assert them in the tests below):
- Codes: auto receipt `PT00001`, #5 `PT00002`, #11 `PT00003`; #6 `PN00001`, #7 `PN00002`; auto payment `PC00001`, #8 `PC00002`, #10 `PC00003`; #9 `CQ00001`.
- KH1 receivable movements:
  - opening (entries at 10-01 00:00): +5,000 −1,000 = **4,000**;
  - 10-03: +3,000, then the auto receipt −1,000;
  - 10-05: −4,000;
  - 10-08: −500;
  - closing **1,500**.
- NCC1 payable: +6,000 (10-10), −2,000 (auto, 10-10), −3,000 (10-12) → closing **1,000**.
- QTM: 10,000 +1,000 (10-03) +4,000 (10-05) +5,000 (10-15) −700 (10-16) = **19,300**.
- NH01: 20,000 −2,000 (10-10) −3,000 (10-12) −5,000 (10-15) = **10,000**.

1. **Write** the dataset builder, plus a smoke test `DebtBalanceReportTests.Dataset_builds_with_invariants` (seed → `AssertCashDebtInvariantsAsync()`).
2. **Run:** `--filter "FullyQualifiedName~Dataset_builds_with_invariants"`. Expected: PASS (it uses only existing APIs; no RED expected — say so in the commit).
3. —
4. —
5. **Commit:** `git commit -m "test(debt): shared report dataset with hand-computed expectations"`

### Task 9.2 — Debt balance report

1. **Write the failing tests** `CashDebt/Reports/DebtBalanceReportTests.cs`:
   - `Receivable_balance_for_october` (from 10-01 to 10-31 → KH1: `OpeningDebit` 4,000, `Increase` 3,000, `Decrease` 5,500, `ClosingDebit` 1,500; Totals equal; branch B's sale is absent)
   - `Window_and_sign` (from 10-06 to 10-31 → opening 2,000 (4,000 + 3,000 − 1,000 − 4,000), increase 0, decrease 500, closing 1,500; payable side NCC1 from 10-01 → `ClosingCredit` 1,000; a partner whose payable balance is negative shows `ClosingDebit`)
   - `Filters_and_permission` (`partnerId`; `search` on code/name; from > to → 400; WAREHOUSE → 403)
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~DebtBalanceReportTests"`. Expected: FAIL (404).
3. **Write the minimal implementation:** `CashDebtReportService.GetDebtBalanceAsync`, which runs one grouped query per window (opening and period sums by partner), validator, controller, DI.
4. **Run tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git commit -m "feat(debt): debt balance summary report"`

### Task 9.3 — Debt ledger report

1. **Write the failing tests** `CashDebt/Reports/DebtLedgerReportTests.cs`:
   - `Ledger_rows_and_running_balance` (KH1 receivable 10-01..10-31 → opening 4,000; rows `PX00001` +3,000 → 7,000, `PT00001` −1,000 → 6,000 (same instant: StockOut sorts before Receipt), `PT00002` −4,000 → 2,000, `PN00001` (NTL) −500 → 1,500; closing 1,500; the opening rows are not listed; the cancelled receipt `PT00003` is absent)
   - `Closing_matches_balance_report_and_query_service` (for every partner/side in the dataset, the ledger closing == the balance-report closing == `IDebtQueryService.BalanceAsync(at: StartOfNextDay(to))`)
   - `Validation_and_permission` (missing `partnerId` → 400; WAREHOUSE → 403)
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~DebtLedgerReportTests"`. Expected: FAIL.
3. **Write the minimal implementation:** `GetDebtLedgerAsync`.
4. **Run tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git commit -m "feat(debt): debt ledger report per partner"`

### Task 9.4 — Cash book

1. **Write the failing tests** `CashDebt/Reports/CashBookReportTests.cs`:
   - `Cash_fund_book` (`kind=Cash`, 10-01..10-31 → one group QTM: opening 10,000; rows `PT00001` +1,000, `PT00002` +4,000, the transfer in +5,000, `PC00003` −700 with `PartnerOrPerson` "Nguyễn A"; closing 19,300; the cancelled receipt is absent)
   - `Bank_book_all_accounts` (`kind=Bank` → NH01 group closing 10,000; the transfer shows as `TransferOut`; it counts in the grand total because QTM is not selected; add NH02 with no movements → a second group with opening = closing = its opening amount)
   - `All_funds_exclude_internal_transfers_from_grand_totals` (`kind=All` → groups for QTM and NH01 both show the transfer; `GrandIn` / `GrandOut` leave it out; `GrandOpening + GrandIn − GrandOut == GrandClosing == 29,300`)
   - `Opening_window_and_fund_filter` (from 10-11 → QTM opening 15,000; `fundId` of NH01 with `kind=Cash` → 400; a fund of branch B → 404; a user without `reports.cash` → 403)
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~CashBookReportTests"`. Expected: FAIL.
3. **Write the minimal implementation:** `GetCashBookAsync`, reusing the movement query of `FundBalanceGuard`. Extract a shared internal `FundMovementQuery` in `Application/CashDebt/Guards` that returns rows with code, partner/person and description, so the guard and the report read the same source.
4. **Run tests to verify they pass:** `--filter "FullyQualifiedName~CashBookReportTests|FullyQualifiedName~FundBalanceGuardTests"`. Expected: PASS.
5. **Commit:** `git commit -m "feat(cash): cash book report across cash and bank funds"`

### Task 9.5 — Invariant replay

`CashDebt/CashDebtInvariantReplayTests.cs` → `Back_dated_edits_equal_entering_final_data`:
1. **Scenario A** (main branch): run the dataset, then:
   - edit #4 to total 3,500, paid 1,500, date 10-02;
   - cancel #5 with `confirmRemoveSettlements`, restore it (the allocation is not restored, C7), then re-allocate 4,000 to `HD-OLD` through the matching API;
   - move #9 to 10-04;
   - delete #10;
   - edit #8's fund to QTM.
2. **Snapshot A** = for each (partner, side): balance; for each `DocCode`: the entry `Amount` and Σ settled; for each fund: closing (cash book `kind=All` to 10-31).
3. **Scenario B** (fresh branch C with the same opening data): enter the final state directly, in date order.
4. **Snapshot B** — the same snapshot, with codes mapped by document role (the dataset row number), because branch C numbers independently.
5. Assert A == B and `AssertCashDebtInvariantsAsync()`.

1. **Write the test.**
2. **Run:** `--filter "FullyQualifiedName~CashDebtInvariantReplayTests"`. A pass on the first run is expected and acceptable (regression test). If it fails, the failure is an engine bug: fix it in the service where it lives, with a focused test first.
3. —
4. **Run the whole cash/debt suite:** `--filter "FullyQualifiedName~CashDebt"`. Expected: PASS.
5. **Commit:** `git commit -m "test(debt): back-dated edit replay matches direct entry"`

## Verification

```bash
cd backend
dotnet build OrderMgmt.sln
dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~CashDebt"
dotnet test OrderMgmt.sln   # compare with BASELINE.md
```

## Exit Criteria

- `debt-balance`, `debt-ledger` and `cash-book` match the hand-computed dataset.
- The ledger closing equals the balance-report closing and the query-service balance.
- Cash book grand totals follow P25.
- The replay test passes; there are no new failures against `BASELINE.md`.
- **Backend API contracts are frozen for Phases 10–12.**
