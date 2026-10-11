# Phase 05 — Docs & final verification

**Status:** [ ] pending
**Complexity:** S

## Objective

Describe the new rules and modules in the project docs, run the final verification against the baselines, and record the outcome in the Round 2A EXECUTION-REPORT.

## Files

- `docs/code-standard/conventions.md`
- `docs/architecture/system-architecture.md`
- `docs/codebase/directory-structure.md`
- `docs/plans/archived/261007-2314-cash-debt-round2a/EXECUTION-REPORT.md`
- `docs/plans/261012-0433-round2a-review-refactor/SUMMARY.md` (phase checkboxes)

## Tasks

### Task 5.1 — Conventions and architecture

1. Check what the docs say today: `grep -n "RecomputeAutoAsync\|CounterpartEntryIdsAsync\|EscapeLike\|DEBT_SETTLEMENTS_WILL_BE_REMOVED\|withConfirmed\|StockVoucherActivityHistory" docs/code-standard/conventions.md docs/architecture/system-architecture.md docs/codebase/directory-structure.md` — every hit that describes the old mechanism is a line to change.
2. Update:
   - `conventions.md` "Cash & Debt Patterns (Round 2A)":
     - replace "A write that may remove another document's settlements reads `CounterpartEntryIdsAsync` first and calls `IAutoCashVoucherService.RecomputeAutoAsync` after its own new settlements" with: the ledger records every non-`Auto` removal in `SettlementChangeCollector`, and `AutoRecomputeHook` (an `IBeforeCommitHook` run by `EfTransactionRunner` before the outermost commit) recomputes their Auto settlements; a new write path needs no call of its own;
     - the 422 `DEBT_SETTLEMENTS_WILL_BE_REMOVED` carries `token`; clients resend it as `confirmedSettlements` with `confirmRemoveSettlements`; the ledger re-asks when a pending settlement is not in the token;
     - shared helpers: `LikePattern`, `CashDocuments`, `SettlementRemovalConfirmation`, `IReferenceUsage` (add new "is used" checks there), `IStockVoucherCashDebt`;
     - frontend: `useDocumentActions` for document forms, `SearchCombobox` for search pickers, `ActivityHistory`, `lib/date-only.ts`, `lib/to-number.ts`, `lib/money-input.ts`, `components/money-cell`.
   - `system-architecture.md`: the confirmation section (P10) gets the token rule; the settlement-origins section gets the before-commit recompute; the stock voucher save steps mention `StockVoucherCashDebt`.
   - `directory-structure.md`: the new files and folders (`Application/Common/References`, `Application/Common/LikePattern.cs`, `CashDebt/Common/CashDocuments.cs`, `CashDebt/Ledger/SettlementRemovalConfirmation.cs`, `CashDebt/Ledger/SettlementChangeCollector.cs`, `CashDebt/AutoVouchers/AutoRecomputeHook.cs`, `Inventory/StockVouchers/Services/StockVoucherCashDebt.cs`; frontend `components/search-combobox`, `components/activity-history`, `components/money-cell`, `hooks/useDocumentActions.ts`, `lib/date-only.ts`, `lib/to-number.ts`, `lib/money-input.ts`).
3. Re-run the grep of step 1 — expected: no line describes the removed mechanisms.
4. Commit `docs: describe the confirmation token, the before-commit recompute and the shared helpers`.

### Task 5.2 — Final verification and report

1. Run the Final Verification of `SUMMARY.md` (backend build + full suite, frontend typecheck, lint, test, build; restore the tsbuildinfo files).
2. Compare failures test by test with the baselines: 19 backend tests listed in `docs/plans/archived/261007-2314-cash-debt-round2a/BASELINE.md`, 3 frontend tests (`useNotificationHub`, `bank-accounts-tab`, `payment-qr-page`), 3 lint errors (`table.tsx:75`, `customer-catalog-list.tsx:58`, `product-catalog-list.tsx:51`). Any other failure is fixed before continuing.
3. In `EXECUTION-REPORT.md` §"Team-lead review and fixes": add the commits of this plan, mark F-13's "confirmation not tied to the listed settlements" and F-15 as done, and add the verification numbers. Tick the five phases in this plan's `SUMMARY.md`.
4. Commit `chore(plan): round 2A review refactor complete`.

## Verification

- The Final Verification commands of `SUMMARY.md`.

## Exit Criteria

- Docs describe only the current mechanisms.
- Failures match the baselines exactly.
