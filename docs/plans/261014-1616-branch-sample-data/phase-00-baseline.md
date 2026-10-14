# Phase 00 — Worktree, plan commit and failure baseline

**Status:** [ ] pending
**Complexity:** S

## Objective

Create the feature worktree and branch, commit the brainstorm and the plan there, and record which tests already fail, so later phases can tell a regression from a known failure.

## Files

- `docs/brainstorms/261014-1601-branch-sample-data/` (copied from the main checkout)
- `docs/plans/261014-1616-branch-sample-data/` (copied from the main checkout; `BASELINE.md` new)
- `docs/SUMMARY.md` (modify — three rows)

## Tasks

### Task 0.1 — Worktree, branch and plan commit

The main checkout `D:\Projects\QLBanHang` is shared with other sessions: it is on `main`, holds their uncommitted files (`docs/SUMMARY.md` is already modified, other brainstorm folders are untracked) and other worktrees exist under `.claude/worktrees/`. **Do not switch branches or commit in the main checkout.**

1. From `/D/Projects/QLBanHang` (uppercase drive, see SUMMARY "Executor conventions"):
   ```bash
   git fetch qlbanhang
   git worktree add .claude/worktrees/branch-sample-data -b feat/branch-sample-data qlbanhang/main
   ```
   From here on, `$WT` is `/D/Projects/QLBanHang/.claude/worktrees/branch-sample-data` and every path of this plan is relative to it.
2. Copy the design and the plan into the worktree:
   ```bash
   cp -r docs/brainstorms/261014-1601-branch-sample-data "$WT/docs/brainstorms/"
   cp -r docs/plans/261014-1616-branch-sample-data "$WT/docs/plans/"
   ```
   From now on, edit only the worktree copies (status marks, `BASELINE.md`, the report). The untracked copies in the main checkout go stale. Delete them once the branch is merged into `main` and pulled.
3. Check the preconditions; stop and report if one fails:
   - `git -C "$WT" cat-file -e HEAD:backend/src/OrderMgmt.Application/Common/ReportScope.cs` (TT133 work is in);
   - `git -C "$WT" cat-file -e HEAD:backend/tests/OrderMgmt.IntegrationTests/CashDebt/Reports/ReportDataset.cs`;
   - `git -C "$WT" cat-file -e HEAD:backend/src/OrderMgmt.Application/Dashboards/Executive/Services/ExecutiveScopeResolver.cs` (PR #14 executive dashboard is in; the plan was written against `8135477`);
   - `PGPASSWORD=1 "/c/Program Files/PostgreSQL/18/bin/psql" -h localhost -U postgres -c "select 1"` answers.
4. In `$WT/docs/SUMMARY.md`, "Other" table:
   - after the row of `brainstorms/261014-0618-mobile-manager-hub/section-04-bank-feed.md` (or, if that row is not on this branch, after the last `brainstorms/` row), add the two brainstorm rows exactly as they are in the main checkout's `docs/SUMMARY.md` (the rows that link `brainstorms/261014-1601-branch-sample-data/SUMMARY.md` and `.../section-01-dataset-spec.md`);
   - after the last `plans/` row that is not `plans/archived/`, add:
     `| [plans/261014-1616-branch-sample-data/SUMMARY.md](plans/261014-1616-branch-sample-data/SUMMARY.md) | Implementation plan for demo branches with sample data — Branch/Product/Customer flags, acting identity for background work, deterministic dataset generator, background runner calling the business services, demo-branch hard delete with catalog cleanup, company-scope reports excluding demo branches, branches page UI; four parallel lanes |`
5. Frontend dependencies of the worktree: `cd "$WT/frontend" && npm ci` (do **not** junction `node_modules` to the main checkout; see the memory note on junction removal).
6. Commit in the worktree:
   ```bash
   cd "$WT"
   git add docs/brainstorms/261014-1601-branch-sample-data docs/plans/261014-1616-branch-sample-data docs/SUMMARY.md
   git commit -m "docs(plan): branch sample data brainstorm and plan"
   ```
   (with the attribution line, see "Executor conventions").

### Task 0.2 — Record the baseline

This task can run in the background while Phase 01 starts: it only reads the commit of Task 0.1. Run it in a separate worktree so Phase 01's edits do not change what is measured.

1. ```bash
   cd /D/Projects/QLBanHang
   git worktree add --detach .claude/worktrees/sd-baseline feat/branch-sample-data
   cd .claude/worktrees/sd-baseline/backend
   export TEST_DB_CONNECTION="Host=localhost;Port=5432;Database=qldonhang_integtest_sd_base;Username=postgres;Password=1"
   dotnet build OrderMgmt.sln
   dotnet test OrderMgmt.sln --logger "trx;LogFileName=baseline.trx" --results-directory "$TEMP/sd-baseline"
   ```
2. List the failing tests:
   ```bash
   python - "$TEMP/sd-baseline/baseline.trx" <<'EOF'
   import re, html, sys
   text = open(sys.argv[1], encoding='utf-8').read()
   names = sorted({html.unescape(m.group(1)) for m in re.finditer(r'<UnitTestResult[^>]*testName="([^"]+)"[^>]*outcome="Failed"', text)})
   print(len(names)); print('\n'.join(names))
   EOF
   ```
   (If `python` prints a `UnicodeEncodeError`, prefix the command with `PYTHONIOENCODING=utf-8`.)
3. Frontend, in `.claude/worktrees/sd-baseline/frontend`: `npm ci`, `npm run typecheck`, `npm run lint`, `npm run test -- --testTimeout=30000`, `npm run build`.
4. Write `$WT/docs/plans/261014-1616-branch-sample-data/BASELINE.md` in the format of `docs/plans/archived/261013-1926-tt133-report-forms/BASELINE.md`: the commit hash, the backend totals and the full list of failing test names, the frontend failing tests (file › suite › test), the lint error and warning counts, typecheck and build results. For reference, the end of the TT133 plan had 19 backend failures (listed in that file), 3 frontend failures and lint 3 errors / 14 warnings; later merges (PR #13) may have changed this.
5. Commit in `$WT`: `git add docs/plans/261014-1616-branch-sample-data/BASELINE.md && git commit -m "chore(plan): branch sample data test baseline"`.
6. Remove the baseline worktree: `cd /D/Projects/QLBanHang && git worktree remove .claude/worktrees/sd-baseline` (it has a real `node_modules`, not a junction, so this is safe).

## Verification

- `git -C "$WT" branch --show-current` prints `feat/branch-sample-data`.
- `git -C /D/Projects/QLBanHang status --short` shows the same entries as before the phase (the main checkout is untouched apart from `.claude/worktrees/`).
- `BASELINE.md` lists every failing test by name.

## Exit Criteria

- The worktree and branch exist from the current `qlbanhang/main` and hold the brainstorm and the plan.
- The baseline is committed; no production code has changed.
