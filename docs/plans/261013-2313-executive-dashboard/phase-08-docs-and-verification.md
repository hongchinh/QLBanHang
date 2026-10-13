# Phase 08 — Docs, final verification, execution report

**Status:** [ ] pending
**Complexity:** M

## Objective

Bring the project documents in line with the code, run the final verification against the baseline, list the manual checks that automation cannot do, and write the execution report.

## Files

- `docs/SUMMARY.md` (modify — move the plan entry to `plans/archived/…` when the folder is archived)
- `docs/architecture/system-architecture.md`, `docs/codebase/directory-structure.md`, `docs/code-standard/conventions.md`, `docs/project-pdr/product-goals.md` (modify)
- `docs/plans/261013-2313-executive-dashboard/EXECUTION-REPORT.md` (new)
- `docs/plans/261013-2313-executive-dashboard/SUMMARY.md` and the phase files (modify — status boxes)

## Tasks

### Task 8.1 — Project documents

Write for a developer who has not seen this plan. Keep each addition in the style and language of its file.

1. `docs/architecture/system-architecture.md`:
   - the executive dashboard flow: `ExecutiveDashboardController` → the four metrics services and the overview service → `DebtPositionQuery` / `FundMovementQuery` / ledger; one snapshot per request; scope resolver (company-wide or one branch);
   - the permission matrix rows for `dashboard.executive` and the per-domain permissions (P9 of the plan summary);
   - the real-time signal: `[DashboardSignal]` on the document controllers, the event `DashboardChanged` with `{ branchId, domains }`, the groups `dashboard-all` and `dashboard-branch-{id}`, the client-side one-second batch and the 60-second fallback.
2. `docs/codebase/directory-structure.md`: `Application/Dashboards/Executive/` (Models, Common, Interfaces, Services, Validators, Realtime), `WebApi/Filters/`, `WebApi/Hubs/DashboardGroups.cs`, `features/executive-dashboard/`, `pages/executive-dashboard/`, the new files of `features/dashboard/`, `lib/realtime-bus.ts`, the test folder `ExecutiveDashboard/`.
3. `docs/code-standard/conventions.md`:
   - backend: a new write controller whose documents change dashboard figures gets `[DashboardSignal(<domains>)]`, and a non-GET action that only reads gets `[SkipDashboardSignal]`; dashboard figures are read through the same query helpers as the reports and each has a test that compares it with the report; balance figures take `at` so tests do not depend on the clock; the sale and return reasons are `StockReasonCodes`;
   - frontend: executive queries live under `executiveKeys` (root `['executive']`), every hook spreads `executiveQueryOptions`, a new tile uses `TileCard` / `StatTile`, the dashboard branch scope changes the working branch only through `useExecutiveScope` → `useSwitchWorkingBranch` (never awaited inside the page), a link with a query string is checked with `useCanOpenLink`, a document code on a multi-branch screen is a `BranchDocumentLink`, range presets live only in `lib/date-range-presets.ts`;
   - the sidebar rule that lists the report sections (Doanh thu / Kho / Quỹ / Công nợ) gains "Tổng hợp";
   - tests: a worktree that runs backend tests next to another session uses its own `TEST_DB_CONNECTION` database name.
4. `docs/project-pdr/product-goals.md`: under Current Scope, the executive dashboard (what it shows, who sees it, that revenue here comes from stock-out vouchers while the quotation dashboards keep quotation revenue); under Planned Scope, the three remaining role dashboards.
5. Check every relative link in the four files (`grep -n "](" <file>` and confirm each target exists).
6. Commit: `git add docs/architecture docs/codebase docs/code-standard docs/project-pdr` and `git commit -m "docs: executive dashboard"`.

### Task 8.2 — Final verification

1. Backend. If any file under `backend/` changed after the full run of Phase 04 (`git log --oneline <last Phase 04 commit>..HEAD -- backend` is not empty), run the full suite again as in Phase 04 Task 4.5 and compare with `BASELINE.md`. Otherwise record the Phase 04 result.
2. Frontend, from `frontend/`:

   ```bash
   npm run typecheck
   npm run lint
   npm run test
   npm run build
   ```

   Expected: typecheck exit 0; lint 17 problems (3 errors, 14 warnings) and none in files of this plan; tests fail only the 3 baseline cases; build exit 0. Restore the tracked `tsconfig.*.tsbuildinfo` files afterwards.
3. `git status --short` is empty.

### Task 8.3 — Manual checks

These need a browser and real data; an executor without them lists each as "not done" in the report. None is a reason to change code silently: report what is seen.

1. Sign in as a user with every permission and open `/dashboard/executive`: the seven KPI tiles, the tiles of the Tổng quan tab and the four other tabs render at 1366×768 without horizontal scrolling; at 375 px the page is one column.
2. Live refresh: with the dashboard open in one browser, save a receipt in another; the Tiền and Công nợ figures and "Vừa phát sinh" change within about two seconds, and the status reads "Trực tiếp".
3. Stop the API for a few seconds: the status reads "Mất kết nối"; after it returns the figures refresh.
4. A user without `inventory.view_cost`: Lãi gộp and Giá trị tồn kho read "—".
5. A user without `branches.access_all`: no branch select, figures of the own branch only.
6. Choose a branch in the select: the header branch switcher shows the same branch and a report link opens that branch's report.
7. Response times: with real-like data, read the 13 `dashboard/executive` requests in the browser's network panel and write the slowest three into the report. A request above one second is a follow-up, not a fix in this plan.
8. Compare three figures with the reports on the same branch and date: Tồn quỹ with Sổ quỹ, Phải thu with Số dư công nợ, Giá trị tồn kho with Tồn kho.

### Task 8.4 — Execution report and archive

1. Mark the phase boxes in `SUMMARY.md` and the status lines of the phase files.
2. Write `EXECUTION-REPORT.md`: plan reference; tasks completed per phase with commit ids; files changed per task; review outcomes; deviations from the plan with the reason; the verification results against `BASELINE.md`; the manual checks with "done" or "not done"; residual risks and follow-ups (the open questions of `SUMMARY.md`, any slow route, the merge with `feat/tt133-report-forms`).
3. Move the plan folder: `git mv docs/plans/261013-2313-executive-dashboard docs/plans/archived/261013-2313-executive-dashboard`, and change its entry in `docs/SUMMARY.md` to the archived path with "Archived plan (executed) …".
4. Commit: `git commit -m "docs(plan): executive dashboard execution report"`.
5. Announce: `Execution complete. Report archived at docs/plans/archived/261013-2313-executive-dashboard/EXECUTION-REPORT.md.` Then use the `finishing-a-development-branch` skill; pushing the branch and opening a pull request need the user's go-ahead (remote `qlbanhang`, no `gh` CLI on this machine).

## Verification

- Every link in the edited documents resolves.
- The backend and frontend results equal `BASELINE.md` plus the new passing tests.
- `git status --short` is empty.

## Exit Criteria

- The five documents describe the dashboard, its permissions and the signal.
- `EXECUTION-REPORT.md` exists in the archived plan folder and states which manual checks were done.
- The branch `feat/executive-dashboard` is ready for review.
