# Phase 00 — Baseline commit and failure baseline

**Status:** [x] complete
**Complexity:** S

## Objective

Commit this plan (and the follow-up list already edited in the stock-voucher-clone brainstorm) on `feat/cash-debt-round2b`, then record the current test failures so later phases can tell regressions from known failures. No production code changes, so there is no TDD cycle.

## Files

- `docs/plans/261012-1709-cash-debt-round2b/**` (commit)
- `docs/brainstorms/261006-2139-stock-voucher-clone/SUMMARY.md` (commit, already modified: "Follow-up — chưa làm" section)
- `docs/plans/261012-1709-cash-debt-round2b/BASELINE.md` (new)

## Tasks

### Task 0.1 — Commit the plan

1. Check the branch and working tree: `git branch --show-current` prints `feat/cash-debt-round2b`; `git status --short` lists only the two paths above (plus untracked `source/`). If anything else under `docs/` or code is modified, stop and ask the user.
2. Commit only those paths:
   ```bash
   git add docs/plans/261012-1709-cash-debt-round2b docs/brainstorms/261006-2139-stock-voucher-clone/SUMMARY.md
   git commit -m "docs: round 2B debt reports plan and follow-up list"
   ```

**Check:** `git log -1 --stat` lists only `docs/` paths.

### Task 0.2 — Record the failure baseline

1. Backend:
   ```bash
   export TEST_DB_CONNECTION="Host=localhost;Port=5432;Database=qldonhang_integtest;Username=postgres;Password=1"
   cd backend && dotnet build OrderMgmt.sln && dotnet test OrderMgmt.sln --logger "trx;LogFileName=baseline.trx" 2>&1 | tail -40
   ```
   List the failing tests (full names, Theory arguments included), from `backend/`: `grep -o 'testName="[^"]*" [^>]*outcome="Failed"' tests/OrderMgmt.IntegrationTests/TestResults/baseline.trx | sed -E 's/^testName="([^"]*)".*//; s/&quot;/"/g' | sort`. Later phases run the same command on `current.trx` and `diff` the two lists.
2. Frontend (from `frontend/`): `npm run test 2>&1 | tail -60`, `npm run lint 2>&1 | tail -30`, `npm run typecheck`. List failing test names, the lint error/warning counts and the typecheck result.
3. Write `docs/plans/261012-1709-cash-debt-round2b/BASELINE.md`: date, commit hash, backend failing tests (one full name per line), frontend failing tests, lint counts, typecheck result. The end of 2A (`6c0be04`) had 19 backend failures (AdminRolesCrudTests ×6, AuthTests.Refresh_rotates_token_and_revokes_old_one, HandoverExportTests ×2, QuotationExportTests ×2, QuotationStateMachineTests ×5, RevenueLineItemsExportTests ×2, SalesRevenueReportTests.Report_FiltersByConfirmedAt_NotQuotationDate), 3 frontend failures, lint 3 errors / 14 warnings. Note any difference.
4. Do not stage test results or build files (`TestResults/`, `*.tsbuildinfo`): `git status --short` must show only `BASELINE.md`. Commit: `git add docs/plans/261012-1709-cash-debt-round2b/BASELINE.md && git commit -m "chore(plan): record round 2B test baseline"`.

**Check:** `BASELINE.md` lists every failing test by full name.

## Verification

- `git log --oneline -3` shows the plan commit and the baseline commit on `feat/cash-debt-round2b`.
- `git status --short` shows no tracked changes.

## Exit Criteria

- The plan is committed on `feat/cash-debt-round2b`.
- `BASELINE.md` exists; every later "no new failures" check compares against it test by test.
