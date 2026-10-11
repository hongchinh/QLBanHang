# Phase 13 — Documentation & final verification

**Status:** [x] complete
**Complexity:** S

## Objective

Bring the project docs in line with Round 2A, run the final verification against the baseline, record the manual smoke test, and close the plan. This phase is docs only, so there is no TDD cycle. Each task ends with a check command and its own commit.

## Files

- `docs/project-pdr/product-goals.md` (modify)
- `docs/architecture/system-architecture.md` (modify)
- `docs/codebase/directory-structure.md` (modify)
- `docs/code-standard/conventions.md` (modify)
- `docs/SUMMARY.md` (modify)
- `docs/plans/261007-2314-cash-debt-round2a/{SUMMARY.md,phase-*.md,EXECUTION-REPORT.md}` (modify/new)

## Tasks

### Task 13.1 — `product-goals.md`

- Move the Round 2A items from "Planned Scope" (Round 2) to the current scope. Edit the existing bullets in place; do not duplicate them. Name what stays planned for **Round 2B**: reports Công nợ theo chứng từ, Tuổi nợ, Nợ quá hạn, Công nợ theo nhân viên, Biên bản đối chiếu.
- Add a section "Tiền & Công nợ" with the user-visible rules:
  - cash reasons with debt effect;
  - funds per branch with one default per kind;
  - receipts/payments with allocation;
  - automatic vouchers managed by stock vouchers (P3 number gap);
  - paid amount requires a fund-backed method (P9);
  - credit limit and negative fund policies (Allow / Warn / Block, default Warn);
  - matching, offset, opening debts;
  - branch opening date locked once documents exist (P8).

  Mark P3, P4, P7's defaults and P5/P6 SALES access as "chờ kế toán/BA xác nhận".
- In the user types, the accountant line moves from planned to current for cash/debt.

**Check:** `grep -n "Tiền & Công nợ\|Round 2B\|Đợt 2B" docs/project-pdr/product-goals.md`
**Commit:** `git commit -m "docs: product goals for round 2A cash and debt"`

### Task 13.2 — `system-architecture.md`

Add `### Cash & Debt (Round 2A)` under Core Business Flows. It covers:
- the data model (`DebtEntry` + `DebtSettlement`, updated in place, one entry per source × side; derived data without `BaseEntity`);
- the posting sources table (stock vouchers by `DebtEffect`, manual vouchers, automatic vouchers, opening debts, offsets);
- the settlement origins and who manages each (P21, P22);
- the confirmation codes and their order (P10), and the blocked edits (P11);
- the lock order (P14) and "read settings after locks";
- fund running balance (P17) and the cash book grand total rule (P25);
- the report window rule (P29).

Also:
- update the Overview sentence ("debt is Round 2" → current);
- add the 422 codes and 409 codes (`DEBT_HAS_SETTLEMENTS`, `MANAGED_BY_STOCK_VOUCHER`, `SETTLEMENT_MANAGED`, `BRANCH_HAS_DOCUMENTS`, `OPENING_DATA_EXISTS`) to the API contract table;
- add the `cash` permission module, the P5 grants and the deploy note to Role And Permission Management;
- add the `['cash']` query root and the confirmation helper to Frontend Layering.

**Check:** `grep -n "Cash & Debt (Round 2A)\|DEBT_SETTLEMENTS_WILL_BE_REMOVED\|debt-partner:" docs/architecture/system-architecture.md`
**Commit:** `git commit -m "docs: architecture for cash and debt round 2A"`

### Task 13.3 — `directory-structure.md`, `conventions.md`, `docs/SUMMARY.md`

- `directory-structure.md`:
  - backend `Domain/Entities/CashDebt`, `Application/CashDebt/*`, `Infrastructure/CashDebt`;
  - every new controller (`CashSettings`, `CashReasons`, `Funds`, `CreditLimits`, `OpeningDebts`, `CashVouchers`, `FundTransfers`, `Debt`, `DebtOffsets`, `CashDebtReports`);
  - the frontend feature and page folders added in Phases 10–12.
- `conventions.md`, a new subsection "Cash & Debt Patterns (Round 2A)" with only the new patterns:
  - `ConfirmationRequiredException` + `confirm*`/`acknowledge*` flags and the client flag accumulation;
  - `PeriodLockGuard` for new code;
  - `IDocumentCodeAllocator` for every numbered document;
  - `ICashDebtLock` after the branch gate;
  - `IDebtLedgerService.PostAsync` vs `RemoveSourceAsync` and "remove your own origin first";
  - `CashDebtTestBase.AssertCashDebtInvariantsAsync` at the end of every scenario;
  - the `['cash']` root key;
  - no cash paths in `sw-routes.ts`.
- `docs/SUMMARY.md`: add a row for `plans/261007-2314-cash-debt-round2a/SUMMARY.md` ("Implementation plan for Round 2A — receipts/payments, funds, fund transfers, receivables/payables with matching and offset, credit limits, debt balance/ledger and cash book reports").

**Check:** `grep -n "Cash & Debt Patterns" docs/code-standard/conventions.md && grep -n "261007-2314-cash-debt-round2a" docs/SUMMARY.md && grep -n "CashVouchersController" docs/codebase/directory-structure.md`
**Commit:** `git commit -m "docs: directory structure, conventions and summary for round 2A"`

### Task 13.4 — Final verification and execution report

1. Run the **Final Verification** commands of `SUMMARY.md`. Compare the failures **test by test** with `BASELINE.md`; any new failure blocks completion.
2. Run the 12-step manual smoke test of `SUMMARY.md` on a reset dev database, and record pass/fail per step.
3. Write `EXECUTION-REPORT.md`:
   - commits per phase;
   - deviations from the plan, each with a reason;
   - test results compared with the baseline;
   - smoke test results;
   - the open go-live gates (copied from `SUMMARY.md`);
   - follow-ups for Round 2B.
4. Tick every phase in `SUMMARY.md`, and set each phase file's `Status` to `[x] complete`.

**Check:** `grep -c "\[x\]" docs/plans/261007-2314-cash-debt-round2a/SUMMARY.md` returns 14.
**Commit:** `git commit -m "chore(plan): round 2A cash and debt complete"`

## Verification

- Every **Check** command above succeeds.
- Final Verification shows no failures beyond `BASELINE.md`.

## Exit Criteria

- The docs describe Round 2A as current and Round 2B as planned.
- The execution report exists with results and deviations; every phase is marked complete.
