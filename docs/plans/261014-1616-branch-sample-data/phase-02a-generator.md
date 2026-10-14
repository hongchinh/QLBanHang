# Phase 02a — Lane A: deterministic sample data generator

**Status:** [ ] pending
**Complexity:** L
**Runs in parallel with:** 02b, 02c, 02d · **Worktree:** `.claude/worktrees/sd-gen` on `feat/branch-sample-data-gen` from `$P01` · **Test DB:** `qldonhang_integtest_sd_gen` (only pure tests run here)

## Objective

Implement `SampleDataGenerator.Generate(SampleDataGeneratorInput)`. It is a pure function from (branch code, today, owner count) to a `SampleDataPlan` that follows [section-01 of the brainstorm](../../brainstorms/261014-1601-branch-sample-data/section-01-dataset-spec.md):
- catalog, warehouses, funds and credit limits;
- opening balances;
- about 3½ months of documents in strictly increasing time.

The plan must be safe under every policy (`Block` included): it never makes stock negative, keeps the estimated fund balances at or above 5,000,000 ₫, and keeps the estimated credit balances at or below 80% of the limit (SUMMARY S10–S12). No database, no clock, no `Guid.NewGuid`.

## Files (this lane only — touch nothing else)

- `backend/src/OrderMgmt.Application/Organization/SampleData/Generation/SampleDataGenerator.cs` (replace the skeleton)
- `backend/src/OrderMgmt.Application/Organization/SampleData/Generation/SampleRandom.cs` (new)
- `backend/src/OrderMgmt.Application/Organization/SampleData/Generation/SampleCalendar.cs` (new)
- `backend/src/OrderMgmt.Application/Organization/SampleData/Generation/SampleCatalog.cs` (new)
- `backend/src/OrderMgmt.Application/Organization/SampleData/Generation/SampleOpeningBalances.cs` (new)
- `backend/src/OrderMgmt.Application/Organization/SampleData/Generation/SampleDocumentSimulator.cs` (new; split into more files under `Generation/` if it grows past ~400 lines)
- `backend/src/OrderMgmt.Application/Organization/SampleData/Generation/SampleDataVolumes.cs` (new — every volume knob in one place, R1)
- `backend/tests/OrderMgmt.IntegrationTests/SampleData/Unit/SampleRandomTests.cs`, `SampleCalendarTests.cs`, `SampleCatalogTests.cs`, `SampleOpeningBalancesTests.cs`, `SampleDataGeneratorTests.cs`, `SampleDataGeneratorEndStateTests.cs` (new)
- `backend/tests/OrderMgmt.IntegrationTests/SampleData/Unit/PlanReplay.cs` (new — test helper, see Task A.4)

## Reference files (read-only)

- `backend/src/OrderMgmt.Application/Inventory/StockVouchers/Services/StockVoucherCalculator.cs`:
  - `StockVoucherCalculator.Compute(StockHeaderInput, IReadOnlyList<StockLineInput>)`;
  - estimate a voucher total with `new StockHeaderInput(type, 0, 0, false)`, and per line `new StockLineInput(product.TrackInventory, PricingMode.PerUnit, false, null, null, null, null, line.Quantity, line.UnitPrice, line.DiscountRate, null, false, line.VatRate)`, then `.Totals.Total`.
- `backend/src/OrderMgmt.Application/Inventory/Common/VnTime.cs` — `VnTime.StartOfDay(DateOnly)` returns the UTC instant of 00:00 VN.
- Seeded codes:
  - unit codes `TAM, M2, M3, THUNG, CHUYEN`;
  - stock reasons `NMH, NKH, XBH, XKH, NTL, XTL`;
  - cash reasons `TKH, THNCC, TK, TNCC, CHKH, CK`;
  - payment methods `TM` (cash), `CK` (bank).
  - `Domain/Constants/StockReasonCodes.cs` has `Sale = "XBH"` and `SaleReturn = "NTL"`.

## Rules

**Dates**
- `OpeningDate` = first day of the month of `Today.AddMonths(-3)`. `LastDocumentDate` = `Today.AddDays(-1)`.
- Documents fall on working days (Monday–Saturday) in `[OpeningDate, LastDocumentDate]`.
- Times are 08:00–17:30 VN. `At = VnTime.StartOfDay(date).AddMinutes(m)` (UTC), strictly increasing across the whole plan.

**Determinism**
- `SampleRandom` wraps `new Random(seed)` with `seed = unchecked((int)Fnv1a32(UTF-8 of BranchCode))`. FNV-1a: offset `2166136261`, prime `16777619`.
- Nothing iterates a `Dictionary`/`HashSet` to decide output order. Use lists or sorted collections.

**Catalog** — section-01 "Quy ước mã" and "Hàng hóa (30)", SUMMARY S13:
- Products: all `PerUnit`; `CostPrice = round(DefaultPrice × 0.78 / 100) × 100`; `DM-SP30` has `TrackInventory = false` and unit `CHUYEN`.
- Partners: names end with `" (mẫu)"`.
  - Customers `DM-KH01..20`: 6 `Company` (30 days), 5 `Agent` (15, 30, 30, 45, 45 days), 5 `Retail` (0), 4 `Project` (45).
  - Suppliers `DM-NCC01..08`: 30 days, except one with 15 and one with 45.
  - `DM-DT01..02`: customer and supplier, 30 days.
  - Each customer and `DM-DT*` gets `OwnerIndex = i % OwnerCount` (i = its 0-based position among them); suppliers get `null`.
- Warehouses: `K1` "Kho chính", `K2` "Kho phụ".
- Funds:
  - `QTM` "Quỹ tiền mặt" (Cash, opening 30,000,000);
  - `VCB` "TK Vietcombank (mẫu)" (Bank, `BankName = "Vietcombank"`, `AccountNumber = "9999000001"`, 450,000,000);
  - `TCB` "TK Techcombank (mẫu)" (Bank, `"Techcombank"`, `"9999000002"`, 150,000,000).
- Credit limits: two `Agent` customers, 300,000,000 each.

**Opening balances** — section-01 "Đầu kỳ"; keys `OD-01…` in list order:
- **Opening stock:** `K1` has 26 tracked products and `K2` 10. Quantity is 3–6 weeks of the product's planned sales, rounded to a whole number. `Amount = Quantity × CostPrice`.
- **Receivables:** 10 `Debt` items for 8 customers, no `Retail`. `DocDate` in `[OpeningDate − 120, OpeningDate − 10]`, `DueDate = DocDate + CreditDays`. One `Project` customer is the "nợ khó đòi": its opening item is never collected.
- **Payables:** 4 `Debt` items for 3 suppliers.
- **Prepayments:** one 20,000,000 receivable `Prepayment` for a `Retail` customer, and one 30,000,000 payable `Prepayment` for a supplier. Neither has a `DueDate`.
- `OwnerIndex` of a receivable item = its customer's owner; payables get `null`.

**Documents** (monthly figures for a full month; section-01 "Phát sinh")
- **Sales `XBH`, 35–45.**
  - 1–6 lines; about 20% of sales add one `DM-SP30` line with quantity 1.
  - Unit price `DefaultPrice`; `DiscountRate` by group: Agent 5, Project 3, Company 2, Retail 0. `VatRate = 10`.
  - Quantities never exceed the simulated stock of the warehouse; about 80% of sales come from `K1`.
  - Payment:
    - Retail customers pay at once with `TM` (fund `QTM`);
    - other customers: about 30% pay at once, with `TM` → `QTM` or `CK` → `VCB`/`TCB`;
    - the rest are credit sales (`PaymentMethodCode = null`, `FundCode = null`).
  - A credit sale to a customer with a limit is only planned while the customer's estimated open receivable plus the sale stays ≤ 80% of the limit; otherwise that sale is paid at once with `CK`.
- **Purchases `NMH`, about 8–14.**
  - Triggered when a product's simulated stock in a warehouse falls under its reorder threshold (about one week of sales).
  - 3–8 lines from the supplier of that product group; unit price `round(CostPrice × (0.95..1.05) / 100) × 100`.
  - About half are paid at once with `CK` from the bank fund that keeps ≥ 5,000,000 ₫ after the estimated total (otherwise credit). The rest are credit.
- **Returns:**
  - `NTL`, 1–2 per month: part of a sale of the last 30 days, same customer and warehouse, credit (no payment).
  - `XTL`, 1 per month: part of the latest purchase, same supplier, credit, quantity ≤ simulated stock.
- **`XKH`, 1 per month:** 1–2 small lines ("Hàng hỏng"), no partner, no payment method.
- **Customer receipts `TKH`.**
  - Per payment habit:
    - on time: on the due date minus 0–5 days;
    - late: 30–60 days after the due date;
    - "nợ khó đòi": never for its opening item, late for new sales.
  - One receipt per customer per day gathers every target due that day, so several receipts allocate several documents.
  - 60% `VCB`/`TCB`, 40% `QTM`. `MaxAmount = null`, except for one or two deliberate partial payments.
- **Supplier payments `TNCC`:** near the due date (0–3 days before), from a bank fund that keeps the margin; otherwise postponed day by day.
- **Expenses `CK` (FixedAmount):**
  - rent 15,000,000 on the first working day (`VCB`);
  - electricity 3–5 million (`QTM`);
  - fuel 2–4 times (`QTM`, 300,000–1,500,000);
  - salary 60,000,000 on the last working day (`VCB`).
- **Other income `TK`, 1 per month:** scrap 1–3 million, `QTM`.
- **Fund transfers.**
  - Every Saturday (or the last working day of the week): `QTM` → `VCB` of the estimated cash above 30,000,000, only when that is ≥ 10,000,000.
  - Once a month: `VCB` → `TCB` 50,000,000 when `VCB` keeps the margin.
- **Once in the whole period:**
  - `CHKH`: refund 5,000,000 of the retail prepayment (target = that `OD-` key, `MaxAmount = 5,000,000`);
  - one `SampleDebtOffsetStep` for `DM-DT01` (a sale to it and a purchase from it must exist before);
  - one `TK` receipt followed later by a `SampleCancelStep` on it.
- **Owners:** sales, returns `NTL`, receipts `TKH` and `CHKH` use the customer's `OwnerIndex`; every other step uses `null`.
- **Keys:** `{ReasonCode}-{nnnn}` per reason (counter per reason, 4 digits), `CQ-nnnn`, `BT-nnnn`, `HUY-nnnn`.

**Estimates:** totals come from `StockVoucherCalculator.Compute`. A receipt's estimated amount is the simulated open amount of its targets, capped by `MaxAmount`. Margins (S12) are checked against these estimates.

## Tasks

### Task A.1 — Seeded random and calendar

1. **Write the failing tests:**
   - `SampleRandomTests`:
     - `Fnv1a_of_branch_code`: `SampleRandom.Fnv1a32("DEMO01") == 1082858617u` and `SampleRandom.Fnv1a32("CN-DEMO") == 3232239808u`.
     - `Same_code_gives_same_sequence`: two `SampleRandom.ForBranch("DEMO01")` give the same first 20 `Next(0, 1000)` values; `"DEMO02"` differs in at least one.
   - `SampleCalendarTests`:
     - `Opening_date_is_first_of_month_three_months_back`: 2026-10-14 → 2026-07-01; 2026-01-05 → 2025-10-01; 2026-11-01 → 2026-08-01.
     - `Last_document_date_is_yesterday`: 2026-10-14 → 2026-10-13.
     - `Working_days_skip_sundays`: `WorkingDays(2026-07-01, 2026-07-07)` = 1, 2, 3, 4, 6, 7 July (5 July 2026 is a Sunday).
     - `At_is_utc_vn_time`: `SampleCalendar.At(new DateOnly(2026, 7, 1), 8 * 60)` equals `2026-07-01T01:00:00Z`.
2. **Run the tests to verify they fail:** `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~SampleRandomTests|FullyQualifiedName~SampleCalendarTests"`. Expected: FAIL — build error, `SampleRandom` does not exist.
3. **Write the minimal implementation:** `SampleRandom` (`Fnv1a32`, `ForBranch`, thin `Next`/`NextDecimal`/`Pick` helpers) and `SampleCalendar` (`OpeningDate`, `LastDocumentDate`, `WorkingDays`, `At`).
4. **Run the tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git add <files> && git commit -m "feat(sample-data): seeded random and calendar for the generator"`

### Task A.2 — Catalog, warehouses, funds, credit limits

1. **Write the failing tests** `SampleCatalogTests` (generate with `new SampleDataGeneratorInput("DEMO01", new DateOnly(2026, 10, 14), 3)`):
   - **Products:** 30, codes `DM-SP01..30` unique and in order; names and prices as in section-01's table; unit codes ⊂ {TAM, M2, M3, THUNG, CHUYEN}; only `DM-SP30` has `TrackInventory == false`; `CostPrice` per the rule.
   - **Partners:** 30 in total.
     - 20 `IsCustomer && !IsSupplier`, with the group and credit-day split of the rules;
     - 8 `IsSupplier && !IsCustomer`;
     - `DM-DT01`, `DM-DT02` both roles.
     - Every name ends with `" (mẫu)"`; codes unique.
   - **Owners:** customer `OwnerIndex` values cover 0, 1 and 2 with 3 owners. With `OwnerCount = 1`, every non-null `OwnerIndex` is 0. `OwnerCount = 0` throws `ArgumentOutOfRangeException`.
   - **Warehouses and funds:** keys `K1`, `K2`; funds `QTM`, `VCB`, `TCB` with the kinds, bank names, account numbers and opening amounts of the rules.
   - **Credit limits:** two, both `Agent` customers, 300,000,000.
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~SampleCatalogTests"`. Expected: FAIL — `NotImplementedException("Phase 02a")` from the skeleton.
3. **Write the minimal implementation:** `SampleCatalog` (static data) and the part of `Generate` that fills `Products`, `Partners`, `Warehouses`, `Funds`, `CreditLimits`, `OpeningDate`, `LastDocumentDate`. `Steps`, `OpeningStocks` and `OpeningDebts` stay empty.
4. **Run the tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git commit -m "feat(sample-data): sample catalog, warehouses, funds and credit limits"` (after `git add`).

### Task A.3 — Opening balances

1. **Write the failing tests** `SampleOpeningBalancesTests` (same input):
   - **Opening stock:** `K1` has 26 lines and `K2` 10; products are distinct per warehouse and tracked; `Quantity > 0` and whole; `Amount == Quantity × CostPrice`.
   - **Opening debts:** 16 in total, keys `OD-01..OD-16`, unique.
     - 10 receivable `Debt` for 8 distinct non-retail customers; 4 payable `Debt` for 3 distinct suppliers; 1 receivable `Prepayment` (retail, 20,000,000); 1 payable `Prepayment` (30,000,000).
     - Every `DocDate ≤ OpeningDate`; every `Debt` has `DueDate == DocDate + CreditDays` (so `≥ DocDate`); prepayments have `DueDate == null`; amounts > 0.
     - Receivable `Debt` sum in [300,000,000; 400,000,000]; payable `Debt` sum in [150,000,000; 250,000,000].
     - A receivable item's `OwnerIndex` equals its customer's `OwnerIndex`; payables are `null`.
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~SampleOpeningBalancesTests"`. Expected: FAIL — the lists are empty.
3. **Write the minimal implementation:** `SampleOpeningBalances` and its call from `Generate`.
4. **Run the tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git commit -m "feat(sample-data): opening stock and opening debts"` (after `git add`).

### Task A.4 — Document simulation and its invariants

1. **Write the test helper and the failing tests.**

   `PlanReplay` (test helper, pure) replays a plan in step order with estimated amounts and exposes:
   - **stock:** per warehouse × product, opening + `In` − `Out`, tracked products only;
   - **funds:** opening + paid sales − paid purchases + receipts − payments ± transfers ± fixed amounts;
   - **debt items:** opening `Debt` and credit/paid documents with an estimated open amount, reduced by allocations (`min(open, MaxAmount ?? open)`, in target order), by automatic settlement of paid documents, and by offsets;
   - **credit balances** of the limited customers;
   - a callback after every step.

   It uses the same `StockVoucherCalculator` estimate as the rules.

   `SampleDataGeneratorTests` (input `("DEMO01", 2026-10-14, 3)` unless stated):
   - `Steps_are_strictly_increasing_inside_the_window`: every `At` is greater than the previous; its VN date is in [2026-07-01, 2026-10-13] and not a Sunday; its VN time is in [08:00, 17:30].
   - `Keys_are_unique_and_targets_point_backwards`:
     - all keys (opening debts and steps) are unique;
     - every `Targets[*].TargetKey`, `ReceivableTargetKey`, `PayableTargetKey` and `SampleCancelStep.TargetKey` names an **earlier** key;
     - targets belong to the step's partner;
     - `TKH` targets are `XBH` steps or receivable `Debt` opening items;
     - `TNCC` targets are `NMH` steps or payable `Debt` opening items;
     - the `CHKH` target is the receivable `Prepayment`;
     - the offset's receivable target is an `XBH` and its payable target an `NMH` of `DM-DT01`;
     - the cancel target is a `TK` receipt.
   - `Reasons_partners_and_funds_are_consistent`:
     - `NMH`/`XTL` have a supplier; `XBH`/`NTL` a customer; `XKH` no partner and no payment method;
     - `TKH`/`CHKH` have a customer and `TNCC` a supplier;
     - `TK`/`CK` have `FixedAmount > 0`, no partner and no targets; `TKH`/`TNCC`/`CHKH` have targets and no `FixedAmount`;
     - `PaymentMethodCode` is in {`TM`, `CK`, null}; `TM` goes with `QTM`, `CK` with `VCB`/`TCB`, null with a null fund;
     - every product code exists in the plan's products and every warehouse key is `K1`/`K2`.
   - `Stock_never_goes_negative`: with `PlanReplay`, no warehouse × product quantity is ever < 0.
   - `Fund_estimates_keep_the_margin`: no estimated fund balance is ever < 5,000,000.
   - `Credit_estimates_stay_under_80_percent`: after every `XBH`, each limited customer's estimated open receivable is ≤ 240,000,000.
   - `Monthly_volumes_match_the_spec`: for July, August and September 2026 separately:
     - `XBH` 35–45, `NMH` 8–14, `NTL` 1–2, `XTL` 1, `XKH` 1, `CK` 5–7, `TK` 1 (plus the cancelled one in its month);
     - QTM → VCB transfers 4–6; VCB → TCB 1.
   - `Once_per_period_items`: exactly one `SampleDebtOffsetStep`, one `CHKH`, one `SampleCancelStep`.
   - `Is_deterministic`: two runs serialize equal. Serialize each step with `JsonSerializer.Serialize(step, step.GetType())` and compare the lists.
   - `Depends_on_branch_code`: `"DEMO02"` gives a different step list.
   - `Works_with_one_owner`: with `OwnerCount = 1`, every `OwnerIndex` is null or 0 and the invariants above still hold (run the replay checks).
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~SampleDataGeneratorTests"`. Expected: FAIL — `Steps` is empty (counts are 0).
3. **Write the minimal implementation:** `SampleDocumentSimulator` with the rules above. Every volume parameter (sales per working day, line counts, the 30% / 20% / 80% shares, thresholds, margins, transfer targets) is a named constant in `SampleDataVolumes`.
4. **Run the tests to verify they pass:** same filter. Expected: PASS. Tune `SampleDataVolumes` to fit the ranges, not the tests.
5. **Commit:** `git commit -m "feat(sample-data): document simulation for three months"` (after `git add`).

### Task A.5 — End state at T

1. **Write the failing tests** `SampleDataGeneratorEndStateTests`. Replay to the end; "open at T" means after the last step.
   - `Monthly_sales_revenue_is_in_range`: estimated `XBH` total per full month in [800,000,000; 1,200,000,000].
   - `Open_receivables_at_T_are_in_range`: the sum of open receivable `Increase` items is in [400,000,000; 600,000,000].
   - `Overdue_buckets_are_all_filled`: open receivable items with `DueDate < T` have overdue days (`T − DueDate`) in each of 1–30, 31–60, 61–90 and > 90. At least one item per bucket.
   - `A_prepayment_stays_open`: the receivable prepayment has 15,000,000 open (20,000,000 − 5,000,000 refund).
   - `Every_owner_has_open_debt`: for 3 owners, each index 0, 1, 2 owns at least one open receivable item.
   - `Some_products_end_low`: at least 3 tracked products end with total stock (K1 + K2) below one week of their average planned sales, and none below 0.
   - `Funds_move_every_week`: every Monday–Saturday week of the period has a step touching `QTM` and a step touching `VCB` or `TCB` (paid documents count through their fund).
   - `Step_count_is_reasonable`: 200 ≤ `Steps.Count` ≤ 320.
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~SampleDataGeneratorEndStateTests"`. Expected: at least one assertion fails (typically the buckets or the prepayment). If all pass at once, record that in the report: A.4 already met them and no RED is possible; then add the missing behaviour test that fails.
3. **Write the minimal implementation:** adjust payment habits, opening-debt dates and `SampleDataVolumes` until the end state holds. Keep A.4 green.
4. **Run the tests to verify they pass:** `--filter "FullyQualifiedName~SampleData.Unit"`. Expected: PASS (all generator tests).
5. **Commit:** `git commit -m "feat(sample-data): end state targets of the sample dataset"` (after `git add`).

### Task A.6 — Lane checkpoint

1. `dotnet build OrderMgmt.sln` (no warnings added in the new files); `--filter "FullyQualifiedName~SampleData.Unit"` passes 3 times in a row (determinism).
2. Generate for 2026-01-05, 2026-03-01 (February in the window) and 2026-12-31 in a throwaway test run (not committed), and check that `Works_with_one_owner`-style invariant checks pass for those dates. If one fails, add that date to the `[Theory]` data of the failing test (RED), fix, commit `fix(sample-data): <what>`.
3. Report: test counts, the measured plan size (steps per type) for `("DEMO01", 2026-10-14, 3)`, and the final `SampleDataVolumes` values.

## Verification

```bash
cd /D/Projects/QLBanHang/.claude/worktrees/sd-gen/backend
dotnet build OrderMgmt.sln
dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~SampleData.Unit"
```

## Exit Criteria

- `Generate` is pure and deterministic and returns a plan that passes every invariant and end-state test for several dates and owner counts.
- Only the files listed above changed (`git diff --stat $P01..HEAD`).
