# Phase 01 — Aging calculator, position query & debt documents report

**Status:** [ ] pending
**Complexity:** M

## Objective

Lay the shared ground for the four "position" reports and deliver the first one:

- `DebtAging`: a pure calculator for bucket boundaries (B5);
- `DebtPositionQuery`: the one loader that returns the entries counted at an instant and their settled amounts (B3);
- **Công nợ theo chứng từ** (`GET /api/reports/debt-documents`): open documents and unallocated amounts per partner at a VN date, reconciling with Số dư công nợ (B4, B6).

## Files

- `backend/src/OrderMgmt.Application/CashDebt/Common/DebtAging.cs` (new)
- `backend/src/OrderMgmt.Application/CashDebt/Reports/Services/DebtPositionQuery.cs` (new)
- `backend/src/OrderMgmt.Application/CashDebt/Reports/Interfaces/IDebtPositionReportService.cs` (new)
- `backend/src/OrderMgmt.Application/CashDebt/Reports/Models/DebtPositionReportDtos.cs` (new)
- `backend/src/OrderMgmt.Application/CashDebt/Reports/Services/DebtPositionReportService.cs` (new)
- `backend/src/OrderMgmt.Application/CashDebt/Reports/Validators/DebtPositionReportValidators.cs` (new)
- `backend/src/OrderMgmt.Application/DependencyInjection.cs` (modify — register the service next to `ICashDebtReportService`)
- `backend/src/OrderMgmt.WebApi/Controllers/DebtPositionReportsController.cs` (new)
- `backend/tests/OrderMgmt.IntegrationTests/CashDebt/Unit/DebtAgingTests.cs` (new)
- `backend/tests/OrderMgmt.IntegrationTests/CashDebt/Reports/DebtDocumentsReportTests.cs` (new)

## Reference files (read-only)

- `backend/src/OrderMgmt.Application/CashDebt/Reports/Services/CashDebtReportService.cs` — snapshot pattern (`_transaction.RunSnapshotAsync`), branch via `ICurrentBranch`, partners via `Customers.IgnoreQueryFilters()`, P29 window
- `backend/src/OrderMgmt.Application/CashDebt/Reports/Validators/CashDebtReportValidators.cs` — `ReportDates.Min/Max`, messages
- `backend/src/OrderMgmt.WebApi/Controllers/CashDebtReportsController.cs` — `[FromQuery]`, `ValidateAndThrowAsync`, `[HasPermission(Permissions.Reports.Debt)]`, `Success(...)`
- `backend/src/OrderMgmt.Application/CashDebt/Common/DebtDates.cs` + `tests/.../CashDebt/Unit/DebtDatesTests.cs` — pure helper + unit test style
- `backend/tests/OrderMgmt.IntegrationTests/CashDebt/Reports/{ReportDataset.cs,DebtBalanceReportTests.cs}` — dataset, `MainBranchClient(ids)`, `GetBalanceAsync` helpers

## Shared dataset (from 2A Phase 09, Task 9.1 — unchanged)

`ReportDataset.SeedAsync(this)` creates a fresh main branch (opening date 2026-10-01) and branch B:

| # | Date | Document | Amount | Notes |
|---|---|---|---|---|
| 1 | opening | KH1 receivable Debt `HD-OLD` (doc date 09-05) | 5,000 | due 09-20 |
| 2 | opening | KH1 receivable Prepayment `TT-OLD` (doc date 09-15) | 1,000 | |
| 3 | opening | QTM 10,000; NH01 20,000 | | funds, not used here |
| 4 | 10-03 09:00 | XBH `PX00001` to KH1 | 3,000 | paid 1,000 → auto receipt `PT00001` (Auto settlement 1,000) |
| 5 | 10-05 09:00 | Receipt `PT00002` TKH from KH1 | 4,000 | allocated to `HD-OLD` 4,000 (AtVoucher) |
| 6 | 10-08 09:00 | NTL `PN00001` from KH1 | 500 | paid 0, not matched |
| 7 | 10-10 09:00 | NMH `PN00002` from NCC1 | 6,000 | paid 2,000 → auto payment `PC00001` (Auto settlement 2,000) |
| 8 | 10-12 09:00 | Payment `PC00002` TNCC to NCC1 | 3,000 | not matched |
| 9–10 | 10-15 / 10-16 | transfer, other payment | | no debt entry |
| 11 | 10-18 | Receipt `PT00003` from KH1 | 300 | **cancelled** → no entry |
| 12 | 10-03 | Branch B: XBH to KH1 | 9,999 | must never appear |

KH1 and NCC1 have no `CreditDays`, so the sale is due 10-03 and the purchase 10-10 (`DueDate ?? DocDate`, C9). Opening debts have no owner. Entry `DocCode` of an opening debt is its `DocNo` (`HD-OLD`, `TT-OLD`).

**Hand-computed positions** (asserted below):

| Side, partner | At 10-31 | At 10-04 |
|---|---|---|
| Receivable KH1 — open Increase | `HD-OLD` 5,000 − 4,000 = **1,000**; `PX00001` 3,000 − 1,000 = **2,000** | `HD-OLD` **5,000** (PT00002 not counted yet); `PX00001` **2,000** |
| Receivable KH1 — unallocated Decrease | `TT-OLD` 1,000; `PN00001` 500 → **1,500** | `TT-OLD` **1,000** |
| Receivable KH1 — balance | 3,000 − 1,500 = **1,500** | 7,000 − 1,000 = **6,000** |
| Payable NCC1 | `PN00002` 6,000 − 2,000 = **4,000**; unallocated `PC00002` **3,000**; balance **1,000** | nothing |

Days at 10-31: `HD-OLD` outstanding 56 (09-05 → 10-31), overdue 41 (09-20 → 10-31); `PX00001` outstanding 28, overdue 28; `PN00002` outstanding 21, overdue 21. At 10-04: `HD-OLD` outstanding 29, overdue 14; `PX00001` outstanding 1, overdue 1.

## API contract

```csharp
// Application/CashDebt/Common/DebtAging.cs — public (the unit tests reference it)
public static class DebtAging
{
    public const int MaxBoundaries = 6;
    public static readonly IReadOnlyList<int> DefaultBoundaries = new[] { 30, 60, 90 };

    /// null/blank → DefaultBoundaries. "30,60,90" (spaces allowed) → [30, 60, 90].
    /// null when any part is not an int, a value is ≤ 0, values are not strictly ascending, or there are more than MaxBoundaries.
    public static IReadOnlyList<int>? ParseBoundaries(string? text);

    /// 0 when days ≤ b[0]; i when b[i-1] < days ≤ b[i]; b.Count when days > b[^1].
    public static int BucketIndex(int days, IReadOnlyList<int> boundaries);

    /// One (From, To) per bucket: (firstFrom, b[0]), (b[0]+1, b[1]), …, (b[^1]+1, null).
    public static IReadOnlyList<(int From, int? To)> Ranges(IReadOnlyList<int> boundaries, int firstFrom);
}

// Application/CashDebt/Reports/Services/DebtPositionQuery.cs — internal
internal sealed class DebtPositionItem
{
    public Guid EntryId { get; init; }
    public Guid PartnerId { get; init; }
    public DebtDirection Direction { get; init; }
    public DebtSourceType SourceType { get; init; }
    public Guid SourceId { get; init; }
    public string DocCode { get; init; } = default!;
    public DateOnly DocDate { get; init; }
    public DateOnly? DueDate { get; init; }
    public Guid? OwnerUserId { get; init; }
    public string? Description { get; init; }
    public decimal Amount { get; init; }
    public decimal Settled { get; init; }
    public decimal Open => Amount - Settled;
}

internal static class DebtPositionQuery
{
    /// B3: entries of the branch and side (one partner when set) with PostedAt < endExclusive or SourceType == Opening,
    /// each with Σ of its settlements whose two entries both count. All counted entries are returned, open or not.
    public static Task<List<DebtPositionItem>> LoadAsync(IAppDbContext db, Guid branchId, DebtSide side,
        Guid? partnerId, DateTimeOffset endExclusive, CancellationToken ct);
}
```

Route `api/reports`, permission `reports.debt`, parameters `[FromQuery]`:

| Method | Route | Response |
|---|---|---|
| GET | `debt-documents?side=&at=&partnerId=` | `DebtDocumentsReportDto` |

```csharp
// Models/DebtPositionReportDtos.cs (public auto-properties, lists default to new())
DebtDocumentsReportRequest { DebtSide Side; DateOnly At; Guid? PartnerId; }
DebtDocumentsReportDto { DebtSide Side; DateOnly At; List<DebtDocumentsGroupDto> Groups;
    decimal TotalRemaining; decimal TotalUnallocated; decimal TotalBalance; }
DebtDocumentsGroupDto { Guid PartnerId; string? PartnerCode; string? PartnerName;
    List<DebtDocumentRowDto> Items; List<DebtUnallocatedRowDto> Unallocated;
    decimal TotalRemaining; decimal TotalUnallocated; decimal Balance; }   // Balance = TotalRemaining − TotalUnallocated
DebtDocumentRowDto { Guid EntryId; DebtSourceType SourceType; Guid SourceId; string DocCode; DateOnly DocDate; DateOnly DueDate;
    string? Description; decimal Amount; decimal Settled; decimal Remaining; int DaysOutstanding; int DaysOverdue; }
DebtUnallocatedRowDto { Guid EntryId; DebtSourceType SourceType; Guid SourceId; string DocCode; DateOnly DocDate;
    string? Description; decimal Amount; decimal Settled; decimal Remaining; }
```

Rules:
- Window: `end = VnTime.ToUtc(VnTime.StartOfNextDay(At))` → `DebtPositionQuery.LoadAsync(..., end, ct)`; keep items with `Open > 0`.
- `Items` = open Increase entries, ordered by `DueDate`, `DocDate`, `DocCode` (ordinal), `EntryId`. `DueDate` is always set on Increase rows (`DueDate ?? DocDate` as a safety net). `DaysOutstanding = Math.Max(0, At.DayNumber − DocDate.DayNumber)` (B5); `DaysOverdue = DueDate < At ? At.DayNumber − DueDate.DayNumber : 0`.
- `Unallocated` = open Decrease entries, ordered by `DocDate`, `DocCode`, `EntryId`.
- `Groups`: partners with at least one item or unallocated row, ordered by `PartnerCode` (ordinal); names via `Customers.IgnoreQueryFilters()`.
- Totals: `TotalRemaining = Σ group.TotalRemaining`, `TotalUnallocated = Σ group.TotalUnallocated`, `TotalBalance = TotalRemaining − TotalUnallocated`.
- Validator: `Side` in enum ("Vui lòng chọn loại công nợ."), `At` in `ReportDates` ("Ngày báo cáo không hợp lệ.").

## Tasks

### Task 1.1 — `DebtAging` calculator

1. **Write the failing test** `CashDebt/Unit/DebtAgingTests.cs`:
   - `Parse_defaults_and_valid_input`: `null`, `""`, `"  "` → `[30, 60, 90]`; `"15, 45"` → `[15, 45]`; `"7"` → `[7]`.
   - `Parse_rejects_invalid_input` (`[Theory]`): `"abc"`, `"30,,60"`, `"0,30"`, `"-5"`, `"60,30"`, `"30,30"`, `"1,2,3,4,5,6,7"` → `null`.
   - `Bucket_index_edges`: boundaries `[30,60,90]` → days −4 → 0, 0 → 0, 30 → 0, 31 → 1, 60 → 1, 61 → 2, 90 → 2, 91 → 3, 400 → 3; boundaries `[10]` → 10 → 0, 11 → 1.
   - `Ranges_for_aging_and_overdue`: `[30,60,90]`, firstFrom 0 → `(0,30),(31,60),(61,90),(91,null)`; firstFrom 1 → `(1,30),…`.
2. **Run the test to verify it fails:** `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~DebtAgingTests"`. Expected: FAIL — build error, `DebtAging` does not exist.
3. **Write the minimal implementation:** `DebtAging` per the contract (`string.Split(',')`, `int.TryParse` with `NumberStyles.None` after `Trim()`, `CultureInfo.InvariantCulture`).
4. **Run the test to verify it passes:** same filter. Expected: PASS.
5. **Commit:** `git add backend/src/OrderMgmt.Application/CashDebt/Common/DebtAging.cs backend/tests/OrderMgmt.IntegrationTests/CashDebt/Unit/DebtAgingTests.cs && git commit -m "feat(debt): aging bucket calculator"`

### Task 1.2 — Debt documents report (happy path, shared loader)

1. **Write the failing tests** `CashDebt/Reports/DebtDocumentsReportTests.cs` (`[Collection(nameof(PostgresCollection))]`, inherits `CashDebtTestBase`; a private helper `GetDocumentsAsync(HttpClient, string query)` that GETs `/api/reports/debt-documents?{query}`, asserts 200 and reads `DebtDocumentsReportDto` the way `DebtBalanceReportTests.GetBalanceAsync` does):
   - `Receivable_documents_at_month_end` (`side=Receivable&at=2026-10-31`): exactly one group, KH1 (branch B's 9,999 is absent). `Items` equal, in order:
     - `HD-OLD`: SourceType Opening, DocDate 09-05, DueDate 09-20, Amount 5,000, Settled 4,000, Remaining 1,000, DaysOutstanding 56, DaysOverdue 41;
     - `PX00001`: SourceType StockOut, SourceId `ids.SaleId`, DocDate 10-03, DueDate 10-03, Amount 3,000, Settled 1,000, Remaining 2,000, DaysOutstanding 28, DaysOverdue 28.
     `Unallocated` equal, in order: `TT-OLD` (Opening, 09-15, 1,000/0/1,000), `PN00001` (StockIn, `ids.Return6Id`, 10-08, 500/0/500). Fully settled `PT00001`/`PT00002` and the cancelled `PT00003` are absent. Group totals 3,000 / 1,500 / balance 1,500; report totals equal.
   - `Receivable_documents_on_the_posting_day` (`at=2026-10-03`, the day `PX00001` is posted at 09:00): `PX00001` is counted — Remaining 2,000, DaysOutstanding 0, DaysOverdue 0 (due that day, not overdue); `HD-OLD` Remaining 5,000; `Unallocated` = `TT-OLD` 1,000; balance 6,000. A window off by one day (`<= At` start of day, or `< At`) fails this test.
   - `Receivable_documents_before_the_opening_date` (`at=2026-09-01`, P29): `HD-OLD` and `TT-OLD` are counted although the opening date is 10-01; `HD-OLD` DaysOutstanding is 0 (DocDate 09-05 is after `At`, clamped by B5), DaysOverdue 0; balance 4,000.
   - `Receivable_documents_at_an_earlier_date` (`at=2026-10-04`): `HD-OLD` Settled 0, Remaining 5,000, DaysOutstanding 29, DaysOverdue 14; `PX00001` Remaining 2,000, DaysOutstanding 1, DaysOverdue 1; `Unallocated` = `TT-OLD` 1,000 only; balance 6,000.
   - `Payable_documents` (`side=Payable&at=2026-10-31`): one group NCC1; item `PN00002` Amount 6,000, Settled 2,000, Remaining 4,000, DueDate 10-10, DaysOverdue 21; unallocated `PC00002` 3,000; balance 1,000.
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~DebtDocumentsReportTests"`. Expected: FAIL — build error, `DebtDocumentsReportDto` does not exist (the test file references the new DTO; after the empty DTO and request classes exist, the same tests fail with 404).
3. **Write the minimal implementation:**
   - `DebtPositionQuery.LoadAsync`: build the counted-entries query (`Where(e => e.BranchId == branchId && e.Side == side && (partnerId == null || e.PartnerId == partnerId) && (e.PostedAt < end || e.SourceType == DebtSourceType.Opening))`); load the entries; load the settlements with `IncreaseEntryId` **and** `DecreaseEntryId` both in that query (`Where(s => counted.Any(e => e.Id == s.IncreaseEntryId) && counted.Any(e => e.Id == s.DecreaseEntryId))`, select the two ids and `Amount`); sum per entry id in memory (each settlement adds to both of its entries).
   - `IDebtPositionReportService.GetDocumentsAsync(DebtDocumentsReportRequest, CancellationToken)`; `DebtPositionReportService` (deps `IAppDbContext`, `ICurrentBranch`, `ITransactionRunner`), body inside `_transaction.RunSnapshotAsync`.
   - DTOs, an empty `DebtDocumentsReportRequestValidator` class (rules come in Task 1.4), DI registration, `DebtPositionReportsController` (`[Route("api/reports")]`, `GET debt-documents`, `[HasPermission(Permissions.Reports.Debt)]`, `ValidateAndThrowAsync` then `Success(...)`).
4. **Run the tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git commit -m "feat(debt): debt documents report with the shared position query"` (stage the new files and `DependencyInjection.cs` explicitly).

### Task 1.3 — Identity with the balance report (B4)

1. **Write the failing test** `Documents_balance_equals_balance_report` in `DebtDocumentsReportTests`: for `side` in {Receivable, Payable} and `at` in {2026-10-02, 2026-10-03, 2026-10-04, 2026-10-31} (10-03 is the posting day of the sale, so an off-by-one window shows up): GET `debt-documents` and `debt-balance?side=&from=2026-10-01&to={at}`. For every balance row, the signed closing (`Receivable: ClosingDebit − ClosingCredit`, `Payable: ClosingCredit − ClosingDebit`) equals the matching group's `Balance` (a partner with closing 0 and no open rows has no group); `TotalBalance` equals the signed closing of `Totals`. Before the asserts, add one manual matching batch in the main branch so a `Manual` settlement is in play, the way `CashDebtInvariantReplayTests` does it: `POST /api/debt/settlements` with `CreateSettlementBatchRequest { PartnerId = ids.Kh1Id, Side = Receivable, Items = [{ IncreaseEntryId = <PX00001 entry>, DecreaseEntryId = ids.OpeningTtOldEntryId, Amount = 1_000 }] }` (the sale's entry from `EntriesOfAsync(DebtSourceType.StockOut, ids.SaleId)`); its `EffectiveAt` is 10-03 09:00. Then also assert the matched state: at 10-31, 10-04 and 10-03 `PX00001` remaining is 1,000, `TT-OLD` is no longer in `Unallocated`, and the KH1 balance is unchanged (1,500 / 6,000 / 6,000). **At 10-02 the manual settlement is not counted** (its `PX00001` entry is posted on 10-03, so only one of its two entries counts, B3): `Items` = `HD-OLD` 5,000 only, `Unallocated` = `TT-OLD` 1,000, balance 4,000 — this is the "both entries must count" half of B3 for a Decrease that is already counted.
2. **Run the test:** `--filter "FullyQualifiedName~Documents_balance_equals_balance_report"`. A pass on the first run is expected and acceptable (regression test over Task 1.2's code; say so in the commit body). If it fails, the loader is wrong: fix the settlement counting in `DebtPositionQuery`, with this test as the RED step.
3. **Write the minimal implementation:** none expected.
4. **Run tests to verify they pass:** `--filter "FullyQualifiedName~DebtDocumentsReportTests"`. Expected: PASS.
5. **Commit:** `git commit -m "test(debt): debt documents report reconciles with the balance report"`

### Task 1.3b — Settlement between two Opening entries counts before the opening date (B3)

1. **Write the failing test** `Opening_to_opening_settlement_counts_before_the_opening_date` in `DebtDocumentsReportTests`: seed the dataset, then match `HD-OLD` with `TT-OLD` through `POST /api/debt/settlements` (`PartnerId = ids.Kh1Id`, `Side = Receivable`, one item `IncreaseEntryId = ids.OpeningHdOldEntryId`, `DecreaseEntryId = ids.OpeningTtOldEntryId`, `Amount = 1_000`). This is the case where B3 ("both entries count") differs from the simpler `EffectiveAt < end` rule, because the settlement's `EffectiveAt` is not before 09-25. Expected:
   - `at=2026-09-25` (before the opening date, P29): `HD-OLD` Amount 5,000, Settled 1,000, Remaining 4,000, DaysOutstanding 20, DaysOverdue 5; `TT-OLD` is absent from `Unallocated` (fully matched); balance 4,000;
   - `at=2026-10-31`: `HD-OLD` is fully settled (4,000 receipt + 1,000 match) and absent, `PX00001` Remaining 2,000, `Unallocated` = `PN00001` 500 only, balance 1,500 (unchanged by the match).
2. **Run the test:** `--filter "FullyQualifiedName~Opening_to_opening_settlement"`. If the loader implements `EffectiveAt < end`, it fails at 09-25 (Settled 0): that is the RED. With the Task 1.2 loader (both entries count) a pass on the first run is expected; say so in the commit body.
3. **Write the minimal implementation:** none expected; if the test fails, fix the settlement counting in `DebtPositionQuery`.
4. **Run tests to verify they pass:** `--filter "FullyQualifiedName~DebtDocumentsReportTests"`. Expected: PASS.
5. **Commit:** `git commit -m "test(debt): opening-to-opening settlement counts at the opening window"`

### Task 1.4 — Validation, filter and permission

1. **Write the failing test** `Filters_validation_and_permission` in `DebtDocumentsReportTests`:
   - `partnerId=NCC1` with `side=Receivable` → 200, `Groups` empty, totals 0;
   - `partnerId=KH1` with `side=Receivable` → one group;
   - missing `at` → 400 with details key `at`; `side` omitted → 400 with key `side` (an omitted enum binds to `default(DebtSide)` = 0, which `IsInEnum` rejects; `side=9` would be rejected by model binding before the validator runs and does not return the validator's `details` shape);
   - a client created with `CreateClientWithPermissionsAsync("dd_cash_" + suffix, ids.MainBranchId, Permissions.Reports.Cash)` → 403.
2. **Run the test to verify it fails:** `--filter "FullyQualifiedName~Filters_validation_and_permission"`. Expected: FAIL — missing `at` returns 200 (empty validator).
3. **Write the minimal implementation:** the validator rules from the contract.
4. **Run tests to verify they pass:** `--filter "FullyQualifiedName~DebtDocumentsReportTests|FullyQualifiedName~DebtAgingTests"`. Expected: PASS.
5. **Commit:** `git commit -m "feat(debt): validate the debt documents report request"`

## Verification

```bash
export TEST_DB_CONNECTION="Host=localhost;Port=5432;Database=qldonhang_integtest;Username=postgres;Password=1"
cd backend
dotnet build OrderMgmt.sln
dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~CashDebt"
```

## Exit Criteria

- `DebtAging` and `DebtPositionQuery` exist with the exact signatures above (Phases 02–03 use them).
- `debt-documents` matches the hand-computed numbers at 10-04 and 10-31 on both sides and equals the balance report per partner (B4), including with a manual settlement.
- No new failures in the `CashDebt` suite.
