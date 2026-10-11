# Phase 00 — Baseline commit, branch and failure baseline

**Status:** [x] complete
**Complexity:** S

## Objective

Commit the Round 2 brainstorm and this plan on `main`, create the feature branch, and record the current test failures so later phases can tell regressions from known failures. No production code changes, so there is no TDD cycle.

## Files

- `docs/brainstorms/261007-2252-cash-debt-round2/**` (commit, already written)
- `docs/brainstorms/261006-2139-stock-voucher-clone/SUMMARY.md` (commit, already modified)
- `docs/brainstorms/261007-2304-pwa-mobile-learn-from-crm/**` (commit, already written — `docs/SUMMARY.md` links to it)
- `docs/SUMMARY.md` (commit, already modified)
- `docs/plans/261007-2314-cash-debt-round2a/**` (commit)
- `docs/plans/261007-2314-cash-debt-round2a/BASELINE.md` (new)

## Tasks

### Task 0.1 — Commit docs on `main` and branch

`.claude/settings.local.json` and `.gitignore` are staged by the user for an unrelated change. **Do not commit them.** Use a path-limited commit, which ignores everything else that is staged.

1. Check the working tree: `git status --short`. The paths listed in **Files** above should be the only docs changes. If other files under `docs/` are modified, stop and ask the user.
2. Commit only the docs paths:
   ```bash
   git add docs/brainstorms/261007-2252-cash-debt-round2 docs/brainstorms/261007-2304-pwa-mobile-learn-from-crm docs/brainstorms/261006-2139-stock-voucher-clone/SUMMARY.md docs/SUMMARY.md docs/plans/261007-2314-cash-debt-round2a
   git commit -m "docs: round 2 cash/debt brainstorm and round 2A plan" -- docs/brainstorms/261007-2252-cash-debt-round2 docs/brainstorms/261007-2304-pwa-mobile-learn-from-crm docs/brainstorms/261006-2139-stock-voucher-clone/SUMMARY.md docs/SUMMARY.md docs/plans/261007-2314-cash-debt-round2a
   ```
3. Verify that `.claude/settings.local.json` and `.gitignore` are still staged and not committed: `git status --short` shows them with `M ` in the first column.
4. Create the branch: `git switch -c feat/cash-debt-round2a` (or let the executing skill create a worktree from this commit).

**Check:** `git log -1 --stat` lists only `docs/` paths.

### Task 0.2 — Record the failure baseline

1. Backend:
   ```bash
   export TEST_DB_CONNECTION="Host=localhost;Port=5432;Database=qldonhang_integtest;Username=postgres;Password=1"
   cd backend && dotnet build OrderMgmt.sln && dotnet test OrderMgmt.sln --logger "trx;LogFileName=baseline.trx" 2>&1 | tail -40
   ```
   List the failing tests: `grep -o 'testName="[^"]*" [^>]*outcome="Failed"' tests/OrderMgmt.IntegrationTests/TestResults/baseline.trx | sed 's/ .*//'`.
2. Frontend: `cd frontend && npm run test 2>&1 | tail -60` and `npm run lint 2>&1 | tail -30`. List the failing test names and the lint error count.
3. Write `docs/plans/261007-2314-cash-debt-round2a/BASELINE.md`:
   - date;
   - commit hash;
   - backend failing tests, one per line (the Round 1 list had 19: AdminRolesCrudTests ×6, AuthTests.Refresh_rotates_token_and_revokes_old_one, HandoverExportTests ×2, QuotationExportTests ×2, QuotationStateMachineTests ×5, RevenueLineItemsExportTests ×2, SalesRevenueReportTests.Report_FiltersByConfirmedAt_NotQuotationDate);
   - frontend failing tests;
   - lint error count.
4. Commit: `git add docs/plans/261007-2314-cash-debt-round2a/BASELINE.md && git commit -m "chore(plan): record round 2A test baseline"`.

**Check:** `BASELINE.md` lists every failing test by full name.

## Verification

- `git log --oneline -3` shows the docs commit on `main` and the baseline commit on `feat/cash-debt-round2a`.
- `git status --short` still shows `.claude/settings.local.json` and `.gitignore` staged, untouched.

## Exit Criteria

- The brainstorm and the plan are committed on `main`; work continues on `feat/cash-debt-round2a`.
- `BASELINE.md` exists. Every later "no new failures" check compares against it test by test.
