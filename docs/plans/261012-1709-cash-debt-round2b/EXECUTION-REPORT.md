# Execution Report — Cash & Debt Round 2B

> Plan: [SUMMARY.md](SUMMARY.md) · Branch: `feat/cash-debt-round2b` · Executed 2026-10-12 → 2026-10-13 · Baseline: [BASELINE.md](BASELINE.md)

## Outcome

All 7 phases (00–06) are complete. Round 2B ships the five debt reports (documents, aging, overdue, by salesperson, on-screen reconciliation), the opening-debt owner picker and list column (B1), and the "Xem nợ quá hạn" link on the stock-out customer debt panel (B2). The final verification shows **no failure beyond the Phase 00 baseline**.

| Check | Baseline (Phase 00) | Final | Result |
|---|---|---|---|
| `dotnet build OrderMgmt.sln` | 0 errors | 0 errors (9 warnings) | ✅ |
| `dotnet test OrderMgmt.sln` | 19 failed / 730 total | 19 failed / 840 total (821 passed) | ✅ the same 19 tests (run took 21m39s) |
| `npm run typecheck` | 0 errors | 0 errors | ✅ |
| `npm run lint` | 3 errors, 14 warnings | 3 errors, 14 warnings (17 problems) | ✅ same as baseline |
| `npm run test` | 3 failed | 4 failed / 785 total; effective 3 failed | ✅ see note |
| `npm run build` | passes | passes (PWA `sw.js` generated) | ✅ |

The 19 backend failures are the baseline list: AdminRolesCrudTests x6, AuthTests.Refresh_rotates_token_and_revokes_old_one, HandoverExportTests x2 (Pdf), QuotationExportTests x2, QuotationStateMachineTests x5, RevenueLineItemsExportTests x2, SalesRevenueReportTests.Report_FiltersByConfirmedAt_NotQuotationDate.

Frontend note: the 3 baseline failures are bank-accounts-tab "adds a new account", useNotificationHub "NewNotification" and payment-qr-page "generates QR". A fourth failure, stock-reason-list-page "renders reasons and creates one", timed out at 5000 ms because the backend suite was running at the same time. It passes 3/3 when re-run alone, so it is a load-induced flake.

The suites grew by 110 backend tests (730 → 840) and 142 frontend tests (643 → 785).

## Commits per phase

| Phase | Commits | Notes |
|---|---|---|
| 00 Baseline | `fa2fd87` (plan docs), `89c4ca2` → `bc0039b` | `89c4ca2` records BASELINE.md |
| 01 Aging calculator, position query, documents report | `2a508a4` → `37ba6ac` (marker `4f82c0b`) | |
| 02 Aging and overdue reports | `e4014ff` → `b2c97fe` (marker `110991a`) | includes the 36500 cap (D-1) |
| 03 By salesperson, reconciliation, owner options | `17fe96f` → `646e50f` (marker `228ac84`) | executed by a concurrent session (D-5) |
| 04 Frontend position reports and panel link | `9b4c6ba` → `eb94376` (marker `80c0ce8`) | |
| 05 Frontend by salesperson, reconciliation, owner picker | `16c9f2e` → `8e8012d` (marker `4847699`) | |
| 06 Docs and final verification | `b71b196` docs commit, then `chore(plan): round 2B complete` | |

## Review outcomes

- Every task passed spec-compliance review, code-quality review and adversarial-correctness review (with fix loops where findings were raised).
- Phase 03 was run by another Claude session. This session verified it afterwards, independently: 840 tests with only the 19 baseline failures, plus three audits (spec, adversarial, contract/frontend-readiness).
- Phase 06 docs were checked against the code: routes, the owners endpoint, `DebtAging` (36500 cap), the validator, the nav items and the customer-debt-panel link.

## Deviations from the plan

- **D-1.** Task 2.3 added a 36500-day cap per bucket boundary (`DebtAging.MaxBoundaryDays`). A Phase 01 review found an int overflow in `Ranges`. The cap is mirrored in the frontend `normalizeBuckets` and in the Phase 04 plan text.
- **D-2.** The days formulas (`DaysOutstanding` clamp, `DaysOverdue`) were consolidated in `DebtDates` and are shared by the three position reports.
- **D-3.** Task 5.2 shows the by-salesperson table when the rows are empty but the unallocated amount or the balances are non-zero, so the page reconciles with Số dư công nợ. The plan said summary rows only when there are rows.
- **D-4.** Task 5.4 (user decision 2026-10-12): an opening-debt owner missing from the options (deactivated or soft-deleted) stays visible as an extra option, labelled by name or "Nhân viên đã bị xóa — vui lòng chọn lại". The list column shows "Nhân viên đã ngừng hoạt động" for an owner without a name, and an inline `ownerUserId` server error appears under the select. The labels live in `frontend/src/features/cash-common/labels.ts`, outside the plan's Files list.
- **D-5.** Phase 03 was executed by a concurrent Claude session and verified afterwards by this one.
- **D-6.** The Phase 04 Task 4.1 plan text for `normalizeBuckets` still lists `1…2147483647` in a JSDoc; the code uses 36500.

## Smoke test (manual)

**Not run.** No dev stack was started in this session. The backend integration tests cover each report's rules end to end with hand-computed numbers, and the frontend tests cover each screen with mocked APIs. The manual checklist in [SUMMARY.md](SUMMARY.md#final-verification) remains for the user or QA.

## B11 (BA gate)

Treated as non-blocking. The user told the controller to proceed on 2026-10-12; no BA answer is on record. Two questions remain open: cross-branch payment with bank accounts shared by branches, and the salesperson semantics of B1. Every 2B report is branch-scoped.

## Follow-ups

**Backend**

- F-1. Saving an unchanged soft-deleted opening-debt owner is rejected (400). The opening-debt list reads `OwnerName` through the soft-delete-filtered `Users` (`OpeningDebtService` ~lines 255/305), while the report uses `IgnoreQueryFilters`.
- F-2. The owners endpoint lists every active user of all branches. This matches B1 but widens enumeration beyond `users.view`.
- F-3. By-salesperson `partnerId` is not existence-checked (200 with zeros); reconciliation returns 404.
- F-4. B3 means past-period figures can change after a later match.
- F-5. The three `Build*` methods in `DebtPositionReportService` duplicate the load/partner-name code, and the group ordering has no `PartnerId` tie-break.
- F-6. Weak tests: the reconciliation header TaxCode/Address is not asserted, the offset test owner check is weak, and the opening-debt test does not check the entry owner after create.

**Frontend**

- F-7. `updateParams` is copied in 4 pages.
- F-8. The `ReportPartnerPicker` clear button has `tabIndex -1` (keyboard access).
- F-9. Owners query errors are ignored in the form, and the owners query refetches after any cash mutation.
- F-10. A refetched options list that drops the user-picked owner shows "Không chọn" while still sending the old id (rare).

**Docs**

- F-11. The phase-03 route table lacks `partnerId`.
- F-12. The `buckets` convention says invalid text is a 400 `buckets` validation error; the status code and error key were not checked.

## Housekeeping

- Next: finishing-a-development-branch (PR to remote `qlbanhang`). The branch is not pushed.
- The plan folder is not archived yet because the sidebar-navigation plan links to it. Archive it when merging.
