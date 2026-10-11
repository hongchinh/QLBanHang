# Execution Report — Cash & Debt Round 2A

> Plan: [SUMMARY.md](SUMMARY.md) · Branch: `feat/cash-debt-round2a` · Executed 2026-10-08 → 2026-10-11 · Baseline: [BASELINE.md](BASELINE.md)

## Outcome

All 14 phases are complete. Backend (Phases 01–09) and frontend (Phases 10–12) ship every in-scope item of the plan, and Phase 13 brings the docs up to date. The final verification on `feat/cash-debt-round2a` shows **no failure beyond the Phase 00 baseline**, compared test by test.

| Check | Baseline (Phase 00) | Final | Result |
|---|---|---|---|
| `dotnet build OrderMgmt.sln` | 0 errors | 0 errors (8 old warnings) | ✅ |
| `dotnet test OrderMgmt.sln` | 19 failed / 450 total | 19 failed / 730 total (711 passed) | ✅ the same 19 tests |
| `npm run typecheck` | 0 errors | 0 errors | ✅ |
| `npm run lint` | 3 errors, 14 warnings | 3 errors, 14 warnings | ✅ the same 3 errors (`table.tsx:75`, `customer-catalog-list.tsx:58`, `product-catalog-list.tsx:51`) |
| `npm run test` | 3 failed / 460 total | 3 failed / 643 total (640 passed) | ✅ the same 3 tests |
| `npm run build` | passes | passes | ✅ |

The suites grew by 280 backend tests and 183 frontend tests.

## How it was executed

- Phases 00–02 ran one subagent per task, with a controller review after each task.
- From Phase 03 on, each phase ran through the Workflow script `.claude/workflows/round2a-phase.js`. That script is local only, kept out of git through `.git/info/exclude`.
  - **Per task:** an implementer in an isolated worktree on an `r2a/pNN-tN.M` branch, then a spec-compliance review, then a code-quality review and an adversarial-correctness review in parallel, with up to 3 fix loops.
  - **Per phase:** an integrator fast-forwarded or cherry-picked the approved branches onto `feat/cash-debt-round2a`, ran the phase verification and the full suite against BASELINE.md, and committed `chore(plan): phase NN complete`.
- **Modes:** sequential for Phases 04–09, parallel for Phase 03, and DAG for Phases 10–13. In DAG mode, independent lines ran in parallel and were then cherry-picked in order.
- **Tooling additions made during execution:**
  - a separate test database per agent (`qldonhang_integtest_<tag>`), because the fixture drops and recreates `<name>_tpl` on every run;
  - `resume`, `preFix` and `branchBase` for continuing runs that died at session limits;
  - `noIntegrate` and `startRef` for running a phase task by task;
  - `approved`, which reuses a task approved in an earlier run.

## Commits per phase

| Phase | Range (exclusive start → phase marker) | Commits | Notes |
|---|---|---|---|
| 00 Baseline | `289cbb2` (docs on main) → `13ee9de` | 2 | `2645321` records BASELINE.md |
| 01 Permissions, settings, numbering, opening date | `13ee9de` → `7d6ff2e` | 10 | |
| 02 Catalogs | `7d6ff2e` → `6dc6ef4` | 9 | |
| 03 Pure calculators | `6dc6ef4` → `82fd872` | 6 | 5 tasks in parallel; 88 unit tests |
| 04 Debt ledger, locks, guards, opening debts | `82fd872` → `976eca6` | 9 | |
| 05 Cash voucher API | `976eca6` → `296024e` | 19 | resumed twice after session limits |
| 06 Fund transfer API | `296024e` → `23b83ce` | 5 | all tasks approved in the first round |
| 07 Stock voucher integration & automatic vouchers | `23b83ce` → `ed77e4a` | 15 | run task by task at the user's request |
| 08 Matching & debt offset API | `ed77e4a` → `718c2dc` | 5 | |
| 09 Reports API & invariant replay | `718c2dc` → `af433a6` | 8 | backend API contracts frozen here |
| 10 Frontend foundations, catalogs & settings | `af433a6` → `18b44b8` | 10 | DAG: 10.4–10.8 in parallel |
| 11 Frontend vouchers & stock voucher form | `18b44b8` → `c423cd5` | 8 | DAG: 3 lines |
| 12 Frontend matching, offset, opening debts, reports | `c423cd5` → `7a052a8` | 7 | DAG: 3 lines |
| 13 Docs & final verification | `7a052a8` → (this commit) | 6 | 13.1–13.3 written in parallel and reviewed; `63a0b5a` applies review wording fixes; the final commit adds this report |

## Deviations from the plan

All deviations below were reviewed; the ones marked **(user)** were explicitly approved by the user during execution.

### Backend

| # | Area | Deviation | Reason |
|---|---|---|---|
| D-1 | Funds (2.3) | Extra migration `AddFundDefaultUniqueIndex`: a partial unique index allows one default fund per branch × kind. A default fund cannot change `Kind` while siblings of its kind exist (409). | The quality review found a race in "exactly one default". Added to the rollback migration list in SUMMARY.md. |
| D-2 | Ledger (4.x) | Added `OpeningDebtKind`. Ledger methods save as they go. `RemoveSettlementsAsync` returns 404 for an unknown id, and `SettledAmountsAsync` returns 0 for unsettled ids. OpenItems ForSource also leaves the source's own settlements out of Settled. Extra FK index `ix_debt_entries_partner_id`. | Implementation details found necessary in review. |
| D-3 | Opening date (5.9 pre-fix) | A fund with `OpeningAmount ≠ 0` also blocks clearing the branch opening date (409 `OPENING_DATA_EXISTS`). | Gap in P8: the fund opening balance would otherwise have no date. |
| D-4 | Locks, P14 (5.9) | New reason key `cash-reason:{id:N}`: shared on cash voucher create/update, exclusive on cash reason update/delete. | Race in the P19 in-use check. |
| D-5 | Locks, P14 (7.3) **(user)** | New key `stock-reason:{id:N}` (`ICashDebtLock.AcquireStockReasonAsync`): shared on stock voucher create/update/restore, exclusive in `StockReasonService` update/delete, which now runs in a transaction. Final lock order: branch gate → products → partners → funds → reason keys → counter row. | The same race once stock vouchers post debt. |
| D-6 | Cash voucher list (5.9) | The search applies `ILike` with the escape argument, so `%`, `_` and `\` match literally. | Bug found in review. Other services still have it (follow-up F-1). |
| D-7 | Due date, P27 (7.3) | The default is applied in `ApplyRequestAsync`, so it uses the reason and partner read after the locks. | Read-after-lock rule (P14). Behaviour is unchanged. |
| D-8 | Confirmations, P10 (7.5) **(user)** | The first 422 `DEBT_SETTLEMENTS_WILL_BE_REMOVED` lists the foreign settlements of both the stock entry and the automatic voucher's entry. | Before this change, the confirmed resend removed settlements the user never saw. |
| D-9 | Auto settlement, P20 (7.5, 8.2, 8.3) **(user)** | `IAutoCashVoucherService.RecomputeAutoAsync` recomputes the Auto settlement whenever foreign settlements are removed: cash voucher edit/cancel/delete, opening debt edit/delete, other stock vouchers' saves, manual unmatch, and offset cancel/delete. | Otherwise invariant 9 breaks, and open items and ageing go wrong. The plan recomputed Auto only on the stock voucher's own save. |
| D-10 | Period lock (7.5) **(user)** | `RecomputeAutoAsync` leaves Auto settlements dated on or before `LockedUntil` unchanged. Invariant 9 is relaxed to Auto ≤ AutoAmount for those pairs only. | The brainstorm rule (§5): an open-period write is never refused because of a locked settlement. |
| D-11 | Period lock (7.5) **(user)** | `BranchService.SetLockAsync` calls `RecomputeReopenedAsync` when `LockedUntil` moves earlier or is cleared. It recomputes the skipped Auto settlements in the reopened days and writes no activity. | Keeps invariant 9 strict in every open period. |
| D-12 | Period lock (7.6) **(user)** | `lockedUntil` in year 9999 is rejected with 400 `lockedUntil`. `RecomputeReopenedAsync` sets no upper bound at `DateOnly.MaxValue`. | A lock at the maximum date made unlocking fail with 500. |
| D-13 | Credit limit, C12 (7.6) **(user)** | Kept as planned: the check runs only on `ReceivableIncrease` saves and restores. | Cancelling, deleting or lowering an NTL (customer return), and raising its refund, raise the receivable without a check. This is recorded as a BA question. |
| D-14 | Debt offset, P23 (8.3) | Invariant 10 is relaxed. Another document's confirmed cancel/edit may remove an offset's Offset settlements (brainstorm §5). The offset stays Active, and its `Amount` may exceed the sum of its Items. Offset cancel/delete pass the request's `confirmRemoveSettlements` instead of always confirming, so a 422 appears when an offset entry was reused by another document. | Found by the 8.3 quality and adversarial reviews. |
| D-15 | Reports (9.1) | `ReportDataset` seeds a fresh branch on every call instead of the main branch. | Test classes share one cloned DB, and the plan's document codes (`PT00001` …) need a fresh counter. |
| D-16 | Reports (9.2) | Opening entries count as opening even when the report window ends before the branch opening date. | P29, and the closing of one period must equal the opening of the next. |

### Frontend

| # | Area | Deviation | Reason |
|---|---|---|---|
| D-17 | Confirmations (10.3) | `ConfirmationRequiredException.CanConfirm` is not serialized. The client infers "blocked" from the `_BLOCKED` suffix. | Backend 422 body shape. |
| D-18 | Settings (10.8) | The settings hub nav entry is shown to `cash.settings` holders. | The seeded Accountant role could not reach the new settings page. |
| D-19 | Shared components (10.5/10.8) | `ProtectedRoute` gains an `anyPermission` prop. | Routes the plan gives "any of" permissions, such as `funds`. |
| D-20 | Offsets (12.3) | The offset form cannot be saved while any amount is negative. | Found by the adversarial review. |

## Smoke test (manual, SUMMARY.md §Final Verification)

**Not performed** (user decision, 2026-10-11). The 12-step checklist in [SUMMARY.md](SUMMARY.md#final-verification) remains open. It must be run by the user or QA on a dev/test environment after the database reset that deploying 2A requires (go-live gate below). Until then, the automated suites are the evidence:
- the backend integration tests cover every step's business rule end to end through the API, including the hand-computed report dataset and the back-dated replay;
- the frontend tests cover each screen with mocked APIs.

| Step | Result |
|---|---|
| 1–12 | Not run |

## Open go-live gates (from SUMMARY.md)

| Gate | Owner | When |
|---|---|---|
| Accountant approves the seeded cash reasons and every `DebtEffect` (cash and stock reasons, incl. `NTL`/`XTL`) — section-02 §1 | PO + accountant | Before go-live (seed/data change only) |
| Accountant accepts the number gap of deleted automatic vouchers (P3) and one series per type (P4) | PO + accountant | Before go-live |
| BA confirms the defaults `CreditLimitPolicy` / `NegativeFundPolicy` = Warn (P7) and SALES access to the debt panel (P5/P6) | BA | Before go-live |
| BA: cross-branch payment, bank accounts shared by several branches (brainstorm open questions) | BA | Before Round 2B planning |
| Fetch the legacy `GetSoDuConNo`, `SoTongHopCongNo`, `SoQuyTienMat` definitions from production | Dev | Before reconciliation |
| Manual reconciliation of one month of debt balances and the cash book against the legacy software | Dev + accountant | Before go-live |
| Decide manual vs Excel import of opening debt (Round 3) | PO | Before Round 3 planning |
| Reset the dev/test database after deploying 2A (no backfill) | Dev | At deploy |

New BA questions raised during execution:
- **Credit limit on returns (D-13):** should cancelling, deleting or lowering a customer return (NTL), or raising its refund, be checked against the credit limit?
- **Partner summary for suppliers:** `GET /api/debt/partner-summary` returns zeros for a supplier-only partner and 404 for a soft-deleted customer. Is that the intended behaviour?
- **Unmatch across branches:** unmatching another branch's settlement returns 403 "Đối trừ thuộc chi nhánh khác.", which reveals that the id exists. Is that acceptable?

## Team-lead review and fixes (2026-10-11)

After Phase 13, a team-lead review covered the whole branch in four areas: the ledger, automatic vouchers and stock voucher integration; the cash voucher, transfer, offset, opening debt and matching services; the catalogs, migrations and reports; and the frontend. Every High and Medium finding was traced again in the code before being accepted. The blocking items were then fixed test-first, one commit each:

| Commit | Finding | Fix |
|---|---|---|
| `ad404f1` | **High.** Editing a cash voucher removed the `AtVoucher` settlements that another cash voucher had made against it (`TKH` ↔ `CHKH`, `THNCC` ↔ `TNCC`), with no P10 confirmation and no activity, and the P11 409 no longer fired. | `AtVoucher` settlements use the voucher id as `BatchId`, as `Auto` and `Offset` already did. `RemoveOwnSettlementsAsync` removes only `BatchId == source`. `CashVoucherAllocationDto.IsOwn` tells the form which rows it edits. Unmatching re-reads the batch's partners after the locks (409 `CONCURRENCY`). |
| `8a8df8d` | **Medium.** The matching, offset and debt-ledger partner pickers never found inactive partners (`activeOnly` defaults to true). | `useDebtPartnerSearch(..., { includeInactive })`, used by those three screens. |
| `f276516` | **Medium.** A partner with cash/debt data but no stock voucher could be deleted, or lose the role of a side it had debt on. | 409 on delete when debt entries, cash vouchers, opening debts or offsets exist. 400 `isCustomer` / `isSupplier` on a role removal while that side has entries. |
| `585a1a2` | **Medium.** `PaymentMethod.FundKind` could change while paid stock vouchers used the method, which left them invalid (P9). | 400 `fundKind` while a non-deleted stock voucher with `PaidAmount > 0` uses the method. |
| `2c7e314` | **Low.** The opening-date validation error had the key `value`. | Key `openingDate`, which the branch form already reads. |

| # | Area | Deviation | Reason |
|---|---|---|---|
| D-21 | Open items, P26 (`ad404f1`) | With `forSourceType` / `forSourceId`, only the source's own `AtVoucher` settlements are added back, not every settlement of its entries. | An edit replaces only those. The plan's wording made the edit grid show more room than the save accepts once `Manual`, `Offset` or another voucher's settlements exist. |

`AtVoucher` settlements saved before `ad404f1` have a random `BatchId`, so they now count as another document's settlement: an edit keeps them and asks for confirmation before removing them. The dev/test database reset at deploy (go-live gate) clears them.

Verification after the fixes (on `2c7e314`): `dotnet test` 19 failed / 735 total (716 passed), the same 19 baseline tests; `npm run test` 3 failed / 646 total (643 passed), the same 3 baseline tests; typecheck clean; lint 3 errors and 14 warnings (baseline); `npm run build` passes.

## Follow-ups (for Round 2B or a separate fix)

| # | Item |
|---|---|
| F-1 | `EF.Functions.ILike` is still called without the escape argument in Round 1/2A services (`StockVoucherService`, `CustomerService.SearchAsync`, `ProductService`, `ProductGroupService`, `WarehouseService`, `AdminUserService`, `InventoryReportService`, `QuotationService`, `SearchService`, `CreditLimitService`, `OpeningDebtService`), so a search containing `%`, `_` or `\` matches nothing. About 12 private `EscapeLike` copies exist; consolidate them into a shared helper. |
| F-2 | `InventoryRecalcService` (lines ~103/121) still calls `AddDays(1)` on `LockedUntil`. It overflows for a branch already stored at `DateOnly.MaxValue`; new 9999 locks are now rejected. |
| F-3 | `IDebtQueryService.BalanceAsync` counts `PostedAt <= at`, while the reports use `< StartOfNextDay(to)`. They differ only for an entry posted exactly at 00:00 of the next day. |
| F-4 | Pre-2A test stock vouchers with `PaidAmount > 0` and no `FundId` return 500 on restore or save. This is accepted per the plan: reset the test data at deploy. |
| F-5 | `StockVoucherService` is about 1,300 lines, `cash-voucher-form-page.tsx` about 720 lines and `credit-limit-list-page.tsx` about 470 lines. Extract the credit/fund capture and payment-rule helpers. |
| F-6 | Frontend polish: no client-side 2-decimal check on cash voucher amounts. The opening-debt 2-decimal check rejects some amounts above 1e9 that have cents. The cash-reason and stock-reason forms do not filter "Đối tượng" by debt effect. The confirmation dialog shows raw item keys. Deleting the last row on the last page leaves the page empty. |
| F-7 | Test gaps the reviewers noted:
  - offset cancel/delete under a period lock;
  - unmatch under a period lock;
  - another branch's settlement or offset (403);
  - the stock-reason key on update and restore;
  - several update paths of fund transfers. |
| F-8 | Performance: the cash book loads every movement of each fund and filters in memory. Partner-summary overdue reads every entry of the partner. Both are fine at current volumes. |
| F-9 | No ownership or `edit_all` check on offset cancel/delete, because the plan defines no such permission. |
| F-10 | Round 2B (separate plan): Công nợ theo chứng từ, Tuổi nợ, Nợ quá hạn, Công nợ theo nhân viên, Biên bản đối chiếu, and the overdue warning list. |
| F-11 | **Before go-live (review, Medium).** Nothing requires `Branch.OpeningDate` before the first document or `VoucherAt ≥ OpeningDate`. A branch created without a date that gets a voucher can never take opening stock, opening debt or a fund opening balance (`BRANCH_HAS_DOCUMENTS`). Reject stock, cash, transfer and offset saves without a date or dated before it, or require the date when a branch is created. |
| F-12 | **Before go-live (review, Medium; extends F-8).** The negative-fund guard loads a fund's whole history two to four times per write while holding the `cash-fund` key, on every receipt, payment, transfer and paid stock voucher. Sum the balance before `from` in SQL and load only the later movements. |
| F-13 | Backend items from the review (Low):
  - stock voucher `Total → 0` with settlements returns 409 instead of the confirmable 422;
  - `DueDate` may be earlier than the document date;
  - a confirmed resend removes whatever settlements exist then, not only the ones the 422 listed;
  - restore does not re-check the stored fund or the method's `FundKind`;
  - soft-deleted numbers can be issued again after a numbering reset-policy change;
  - an unmatch is undone by saving a cash voucher form opened before it;
  - `OpeningDebt` has no `xmin`;
  - branch delete ignores 2A data;
  - `GET /api/funds` returns `OpeningAmount` to every role (BA);
  - every stock voucher save logs `Updated` on its automatic voucher;
  - the rollback recipe does not remove `document_numberings` of types 3–6 or `NTL`/`XTL`;
  - the report tests have no VN-day boundary case. |
| F-14 | Frontend items from the review (Low):
  - the cash voucher form submits allocations its own grid marks invalid;
  - the confirmation dialog's dismiss button reads "Hủy", which is ambiguous on a cancel action;
  - the paid amount stays editable for a method without a fund, and server errors on `fundId` / `dueDate` show only as a toast;
  - links to automatic vouchers and opening debts ignore permissions;
  - the Sổ chi tiết running balance does not split Nợ/Có;
  - the list state may carry over between /receipts and /payments (no `key`). |
| F-15 | Refactoring from the review:
  - `CashVoucherService`, `FundTransferService` and `DebtOffsetService` duplicate their lifecycle, activity, lock and helper code;
  - `RecomputeAutoAsync` is a manual hook at six call sites;
  - the "in use" guards list their tables by hand (the cause of `f276516`);
  - the frontend has five partner pickers and copies the form-action code across four forms. |

## Housekeeping for the user

- **Branches:** remove the worktrees under `.claude/worktrees/wf_*`, including the locked `wf_14ef4db1-69a-*` left by an abandoned bridge run, and their `worktree-wf_*` branches. All `r2a/p*` branches are merged and can be deleted.
- **Databases:** stale test databases `qldonhang_integtest_*` may remain on local Postgres.
- **Rollback list:** `AddFundDefaultUniqueIndex` has been added to the rollback migration list in SUMMARY.md (D-1).
