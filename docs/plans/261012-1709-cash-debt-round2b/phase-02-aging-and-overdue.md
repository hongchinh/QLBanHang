# Phase 02 — Aging & overdue reports

**Status:** [ ] pending
**Complexity:** M

## Objective

Deliver, on top of Phase 01's `DebtAging` and `DebtPositionQuery`:

- **Tuổi nợ** (`GET /api/reports/debt-aging`): per partner, the open amounts split by age (`At − DocDate`) into buckets the user sets, minus the unallocated amount; totals equal the documents report (B4, B5, B6).
- **Nợ quá hạn** (`GET /api/reports/debt-overdue`): per partner, the open documents whose due date is before the date, with days overdue and buckets; the partner's unallocated amount is shown for information (B6). This report is the overdue warning list (B2).

## Files

- `backend/src/OrderMgmt.Application/CashDebt/Reports/Interfaces/IDebtPositionReportService.cs` (modify)
- `backend/src/OrderMgmt.Application/CashDebt/Reports/Models/DebtPositionReportDtos.cs` (modify)
- `backend/src/OrderMgmt.Application/CashDebt/Reports/Services/DebtPositionReportService.cs` (modify)
- `backend/src/OrderMgmt.Application/CashDebt/Reports/Validators/DebtPositionReportValidators.cs` (modify)
- `backend/src/OrderMgmt.WebApi/Controllers/DebtPositionReportsController.cs` (modify)
- `backend/tests/OrderMgmt.IntegrationTests/CashDebt/Reports/DebtAgingReportTests.cs` (new)
- `backend/tests/OrderMgmt.IntegrationTests/CashDebt/Reports/DebtOverdueReportTests.cs` (new)

## Reference files (read-only)

- Phase 01 of this plan — dataset table and hand-computed positions, `DebtPositionQuery`, `DebtAging`, documents report
- `backend/tests/OrderMgmt.IntegrationTests/CashDebt/Reports/DebtDocumentsReportTests.cs` — GET helper, permission test

## API contract

Route `api/reports`, permission `reports.debt`, `[FromQuery]`:

| Method | Route | Response |
|---|---|---|
| GET | `debt-aging?side=&at=&partnerId=&buckets=` | `DebtAgingReportDto` |
| GET | `debt-overdue?side=&at=&partnerId=&buckets=` | `DebtOverdueReportDto` |

`buckets` is a comma-separated string (`30,60,90`); missing → `DebtAging.DefaultBoundaries`.

```csharp
AgingBucketDto { int From; int? To; }   // To null = open-ended last bucket

DebtAgingReportRequest { DebtSide Side; DateOnly At; Guid? PartnerId; string? Buckets; }
DebtAgingReportDto { DebtSide Side; DateOnly At; List<AgingBucketDto> Buckets; List<DebtAgingRowDto> Rows; DebtAgingRowDto Totals; }
DebtAgingRowDto { Guid? PartnerId; string? PartnerCode; string? PartnerName;
    List<decimal> Amounts;      // one per bucket, same order as Buckets
    decimal TotalOpen;          // Σ Amounts
    decimal Unallocated;        // Σ open Decrease
    decimal Balance; }          // TotalOpen − Unallocated

DebtOverdueReportRequest { DebtSide Side; DateOnly At; Guid? PartnerId; string? Buckets; }
DebtOverdueReportDto { DebtSide Side; DateOnly At; List<AgingBucketDto> Buckets; List<DebtOverdueGroupDto> Groups;
    List<decimal> TotalAmounts; decimal TotalOverdue; decimal TotalUnallocated; }
DebtOverdueGroupDto { Guid PartnerId; string? PartnerCode; string? PartnerName; List<DebtOverdueRowDto> Items;
    List<decimal> Amounts; decimal TotalOverdue;
    decimal Unallocated; }      // information only, not subtracted (B6)
DebtOverdueRowDto { Guid EntryId; DebtSourceType SourceType; Guid SourceId; string DocCode; DateOnly DocDate; DateOnly DueDate;
    string? Description; decimal Amount; decimal Remaining; int DaysOverdue; int BucketIndex; }
```

Rules:
- Both load `DebtPositionQuery.LoadAsync(db, branchId, side, partnerId, VnTime.ToUtc(VnTime.StartOfNextDay(At)), ct)` inside `RunSnapshotAsync`.
- **Aging:** boundaries = `DebtAging.ParseBoundaries(Buckets)!` (the validator has rejected null); `Buckets = Ranges(boundaries, firstFrom: 0)`; each open Increase adds its `Open` to `Amounts[BucketIndex(Math.Max(0, At.DayNumber − DocDate.DayNumber), boundaries)]` (B5). `Amounts` and `Totals.Amounts` always have `Buckets.Count` entries, zero-filled when there are no rows (B12). Rows: partners with any open Increase or Decrease, ordered by `PartnerCode` (ordinal). `Totals` sums each column per index (`PartnerId` etc. null).
- **Overdue:** an open Increase is overdue when `DueDate < At` (`DueDate ?? DocDate`); `DaysOverdue = At.DayNumber − DueDate.DayNumber` (≥ 1); `BucketIndex = DebtAging.BucketIndex(DaysOverdue, boundaries)`; `Buckets = Ranges(boundaries, firstFrom: 1)`. Groups: partners with ≥ 1 overdue item, ordered by `PartnerCode`; items ordered by `DueDate`, `DocDate`, `DocCode`, `EntryId`. `Unallocated` = the partner's open Decrease total at `At`. `TotalAmounts` / `TotalOverdue` / `TotalUnallocated` sum the groups; `TotalAmounts` has `Buckets.Count` entries, zero-filled when there are no groups (B12).
- **Validators** (both): `Side` in enum, `At` in `ReportDates`, `Buckets` → `Must(b => DebtAging.ParseBoundaries(b) is not null)` with message "Mốc ngày không hợp lệ: nhập các số nguyên dương tăng dần, cách nhau bởi dấu phẩy, tối đa 6 mốc." (details key `buckets`).

## Hand-computed expectations (dataset of Phase 01)

| Report | Query | Expected |
|---|---|---|
| Aging | `side=Receivable&at=2026-10-31` | Buckets `(0,30),(31,60),(61,90),(91,null)`; KH1 Amounts `[2000, 1000, 0, 0]` (`PX00001` age 28, `HD-OLD` age 56), TotalOpen 3,000, Unallocated 1,500, Balance 1,500; Totals equal |
| Aging | `…&at=2026-10-31&buckets=10,20` | Buckets `(0,10),(11,20),(21,null)`; KH1 `[0, 0, 3000]` |
| Aging | `side=Payable&at=2026-10-31` | NCC1 `[4000, 0, 0, 0]` (age 21), Unallocated 3,000, Balance 1,000 |
| Aging | `side=Receivable&at=2026-09-01` | P29 + B5 clamp: `HD-OLD` is counted before its own opening date and its age is clamped to 0 → KH1 `[5000, 0, 0, 0]`, TotalOpen 5,000, Unallocated 1,000 (`TT-OLD`), Balance 4,000 |
| Aging | `side=Payable&at=2026-10-04` | no rows; `Totals.Amounts == [0, 0, 0, 0]` (B12) |
| Overdue | `side=Receivable&at=2026-10-31` | Buckets `(1,30),(31,60),(61,90),(91,null)`; KH1 items `HD-OLD` (due 09-20, remaining 1,000, 41 days, bucket 1) then `PX00001` (due 10-03, 2,000, 28 days, bucket 0); Amounts `[2000, 1000, 0, 0]`, TotalOverdue 3,000, Unallocated 1,500 |
| Overdue | `…&at=2026-10-03` | KH1 one item `HD-OLD` remaining 5,000, 13 days; the sale is due 10-03 and **not** overdue |
| Overdue | `…&at=2026-09-25` | P29: opening entries count before the opening date → `HD-OLD` 5,000, 5 days |
| Overdue | `side=Payable&at=2026-10-10` | no group (`PN00002` is due 10-10); `TotalAmounts == [0, 0, 0, 0]`, `TotalOverdue` 0 |
| Overdue | `side=Payable&at=2026-10-31` | NCC1 `PN00002` 4,000, 21 days, Unallocated 3,000 |

## Tasks

### Task 2.1 — Aging report

1. **Write the failing tests** `CashDebt/Reports/DebtAgingReportTests.cs` (same base, collection and GET-helper style as `DebtDocumentsReportTests`):
   - `Receivable_aging_default_buckets` (row 1 of the table; branch B's 9,999 absent)
   - `Custom_buckets` (row 2)
   - `Payable_aging` (row 3)
   - `Opening_debt_before_the_opening_date_and_empty_result` (rows 4 and 5 of the aging block: `at=2026-09-01` and `side=Payable&at=2026-10-04`)
   - `Totals_equal_documents_report`: for both sides at 10-03, 10-04 and 10-31, `Totals.TotalOpen == documents.TotalRemaining`, `Totals.Unallocated == documents.TotalUnallocated`, `Totals.Balance == documents.TotalBalance`, and per partner `Balance == group.Balance`.
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~DebtAgingReportTests"`. Expected: FAIL — build error, `DebtAgingReportDto` does not exist (404 once the empty DTO classes exist).
3. **Write the minimal implementation:** `GetAgingAsync` in the interface and service, DTOs, controller action `GET debt-aging`, a validator class with only the `Side` and `At` rules (the `Buckets` rule is Task 2.3). Parse with `DebtAging.ParseBoundaries(request.Buckets) ?? DebtAging.DefaultBoundaries` here; Task 2.3 makes the validator reject invalid input first.
4. **Run the tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git commit -m "feat(debt): debt aging report with user-set buckets"`

### Task 2.2 — Overdue report

1. **Write the failing tests** `CashDebt/Reports/DebtOverdueReportTests.cs`:
   - `Receivable_overdue_at_month_end` (row 4)
   - `Due_today_is_not_overdue` (row 5)
   - `Opening_debts_count_before_the_opening_date` (row 6)
   - `Payable_overdue` (rows 7 and 8; row 7 also asserts the zero-filled `TotalAmounts`)
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~DebtOverdueReportTests"`. Expected: FAIL — build error, `DebtOverdueReportDto` does not exist (404 once the empty DTO classes exist).
3. **Write the minimal implementation:** `GetOverdueAsync`, DTOs, controller action `GET debt-overdue`, validator with `Side` and `At` rules (same parse fallback as Task 2.1).
4. **Run the tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git commit -m "feat(debt): overdue debt report"`

### Task 2.3 — Bucket validation and permission

1. **Write the failing tests:**
   - in `DebtAgingReportTests`: `Validation_and_permission` — `buckets=60,30` → 400 key `buckets`; `buckets=abc` → 400 key `buckets`; missing `at` → 400 key `at`; a `reports.cash`-only client → 403;
   - in `DebtOverdueReportTests`: `Validation_and_permission` — `buckets=0,30` → 400 key `buckets`; `buckets=1,2,3,4,5,6,7` → 400; `reports.cash`-only client → 403.
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~Validation_and_permission"`. Expected: FAIL — the invalid `buckets` return 200 (fallback to defaults).
3. **Write the minimal implementation:** the `Buckets` rule in both validators; drop the `?? DefaultBoundaries` fallback in the service for invalid input (`ParseBoundaries` returns the defaults for blank input, so `!` is now safe).
4. **Run tests to verify they pass:** `--filter "FullyQualifiedName~DebtAgingReportTests|FullyQualifiedName~DebtOverdueReportTests|FullyQualifiedName~DebtDocumentsReportTests"`. Expected: PASS.
5. **Commit:** `git commit -m "feat(debt): validate aging and overdue bucket boundaries"`

## Verification

```bash
export TEST_DB_CONNECTION="Host=localhost;Port=5432;Database=qldonhang_integtest;Username=postgres;Password=1"
cd backend
dotnet build OrderMgmt.sln
dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~CashDebt"
```

## Exit Criteria

- `debt-aging` and `debt-overdue` match every row of the table above.
- Aging totals equal the documents report per partner and in total at two dates.
- Invalid boundaries return 400 with key `buckets`; users without `reports.debt` get 403.
- No new failures in the `CashDebt` suite.
