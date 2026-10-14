# Phase 00 — Worktree, branch, failure baseline and the parallel decision

**Status:** [ ] pending
**Complexity:** S

## Objective

Create the worktree and branch, commit the plan with the reviewed brainstorm, and record which tests already fail, so later phases can tell a regression from a known failure. No production code changes.

## Files

- `docs/plans/261014-1632-tt133-voucher-print-forms/` (the plan, copied into the worktree)
- `docs/brainstorms/261013-2042-tt133-voucher-print-forms/SUMMARY.md` (the reviewed version, copied into the worktree)
- `docs/plans/261014-1632-tt133-voucher-print-forms/BASELINE.md` (new)
- `docs/SUMMARY.md` (modify — add the plan row, update the brainstorm row)

## Tasks

### Task 0.1 — Worktree, branch and plan commit

1. From the main checkout `D:\Projects\QLBanHang`:
   ```bash
   git fetch qlbanhang
   git merge-base --is-ancestor 8135477 qlbanhang/main   # the plan's base must be in main
   git ls-remote --heads qlbanhang feat/tt133-voucher-print-forms   # expect no output
   git worktree add .claude/worktrees/tt133-voucher-print -b feat/tt133-voucher-print-forms qlbanhang/main
   ```
   If the remote branch already exists, check what it holds (`git log qlbanhang/feat/tt133-voucher-print-forms --oneline -5`); if it holds only commits of `main`, base the local branch on it instead, otherwise **stop and ask the user**.
2. Link the frontend packages of the main checkout into the worktree (PowerShell, because Git Bash cannot make a junction):
   ```powershell
   New-Item -ItemType Junction -Path "D:\Projects\QLBanHang\.claude\worktrees\tt133-voucher-print\frontend\node_modules" -Target "D:\Projects\QLBanHang\frontend\node_modules"
   ```
   Before ever removing the worktree, delete only this link with `(Get-Item "<path>").Delete()`, then check that `D:\Projects\QLBanHang\frontend\node_modules` still has its packages.
3. Copy the plan folder and the reviewed brainstorm from the main checkout into the worktree (both are uncommitted there):
   ```bash
   cp -r /d/Projects/QLBanHang/docs/plans/261014-1632-tt133-voucher-print-forms /d/Projects/QLBanHang/.claude/worktrees/tt133-voucher-print/docs/plans/
   cp /d/Projects/QLBanHang/docs/brainstorms/261013-2042-tt133-voucher-print-forms/SUMMARY.md /d/Projects/QLBanHang/.claude/worktrees/tt133-voucher-print/docs/brainstorms/261013-2042-tt133-voucher-print-forms/SUMMARY.md
   ```
   Tell the user in the phase report that the main checkout keeps uncommitted copies of these files (and of its own `docs/SUMMARY.md` edits); after this branch is merged they can be discarded there with `git checkout -- <path>` when identical.
4. In the worktree's `docs/SUMMARY.md` ("Other" table):
   - after the last `plans/` row, add:
     `| [plans/261014-1632-tt133-voucher-print-forms/SUMMARY.md](plans/261014-1632-tt133-voucher-print-forms/SUMMARY.md) | Implementation plan for the TT133 voucher print forms — 01-TT/02-TT (A5) and 01-VT/02-VT (A4) as Excel/PDF from the voucher and list pages, three price modes for stock vouchers, shared export support |`
   - replace the description of the row `brainstorms/261013-2042-tt133-voucher-print-forms/SUMMARY.md` with:
     `Brainstorm for TT133 voucher print forms — Excel/PDF of 01-TT/02-TT (receipt/payment, A5 landscape) and 01-VT/02-VT (stock in/out, A4 portrait), single and bulk print from form and list pages, three price modes for stock vouchers (cost read from the inventory ledger, VAT row that adds up to the total); builds on the merged TT133 report forms plan (reviewed against code) |`
5. Check the preconditions in the worktree; stop and report if one fails:
   - `git cat-file -e HEAD:backend/src/OrderMgmt.Application/Tt133/Services/Tt133ExportService.cs`
   - `git cat-file -e HEAD:backend/src/OrderMgmt.WebApi/Filters/DashboardSignalAttribute.cs`
   - `"/c/Program Files/PostgreSQL/18/bin/psql" -h localhost -U postgres -c "select 1"` answers (`PGPASSWORD=1`).
6. Commit: `git add docs/plans/261014-1632-tt133-voucher-print-forms docs/brainstorms/261013-2042-tt133-voucher-print-forms/SUMMARY.md docs/SUMMARY.md && git commit -m "docs(plan): TT133 voucher print forms plan"`

### Task 0.2 — Record the baseline

1. Backend, from the worktree's `backend/`:
   ```bash
   export TEST_DB_CONNECTION="Host=localhost;Port=5432;Database=qldonhang_integtest_vprint;Username=postgres;Password=1"
   dotnet build OrderMgmt.sln
   dotnet test OrderMgmt.sln --logger "trx;LogFileName=baseline.trx" --results-directory "$TEMP/vprint-trx"
   ```
2. List the failing tests:
   ```bash
   python - "$TEMP/vprint-trx/baseline.trx" <<'EOF'
   import re, html, sys
   text = open(sys.argv[1], encoding='utf-8').read()
   names = sorted({html.unescape(m.group(1)) for m in re.finditer(r'<UnitTestResult[^>]*testName="([^"]+)"[^>]*outcome="Failed"', text)})
   print(len(names)); print('\n'.join(names))
   EOF
   ```
   (If the trx file lands in a sub-folder, find it with `ls -R "$TEMP/vprint-trx"`.)
3. Frontend, from the worktree's `frontend/`: `npm run typecheck`, `npm run lint`, `npm run test -- --testTimeout=30000`, `npm run build`, then `git checkout -- tsconfig.app.tsbuildinfo tsconfig.node.tsbuildinfo` (skip a path that is not tracked). A test that fails only on timeout is re-run alone before it counts.
4. Write `BASELINE.md` in the plan folder: the commit hash, the backend totals and the full list of failing test names, the frontend failing tests (file › suite › test), the lint error and warning counts, the typecheck and build results. Format: `docs/plans/archived/261013-1926-tt133-report-forms/BASELINE.md`.
5. Commit: `git add docs/plans/261014-1632-tt133-voucher-print-forms/BASELINE.md && git commit -m "chore(plan): voucher print forms test baseline"`

### Task 0.3 — Parallel or one after the other

1. Decide whether the two tracks run in parallel (SUMMARY, Phases and Risks "Machine load"): run `git worktree list`; if the branch-sample-data lanes (`branch-sample-data`, `sd-*`) are running tests, or when unsure, ask the user.
2. For a parallel run, create the frontend worktree from the baseline commit (so it holds `BASELINE.md`) and link its packages as in Task 0.1 step 2:
   ```bash
   git worktree add .claude/worktrees/tt133-voucher-print-fe -b feat/tt133-voucher-print-forms-fe feat/tt133-voucher-print-forms
   ```
   ```powershell
   New-Item -ItemType Junction -Path "D:\Projects\QLBanHang\.claude\worktrees\tt133-voucher-print-fe\frontend\node_modules" -Target "D:\Projects\QLBanHang\frontend\node_modules"
   ```
3. Record the choice (parallel or one after the other) in the phase report. The baseline holds for both worktrees (same code). Phases 02 and 04 mark their status in the frontend worktree's copy of the plan; Task 5.0 merges it.

## Verification

- `git -C /d/Projects/QLBanHang/.claude/worktrees/tt133-voucher-print branch --show-current` prints `feat/tt133-voucher-print-forms`; for a parallel run, the same command on `tt133-voucher-print-fe` prints `feat/tt133-voucher-print-forms-fe`.
- `BASELINE.md` lists every failing test by name.
- `git status --short` in the worktree is clean; no file under `backend/tests/OrderMgmt.IntegrationTests/TestResults/` changed.

## Exit Criteria

- The worktree and branch exist from `qlbanhang/main` (at or after `8135477`) and hold the plan and the reviewed brainstorm.
- The baseline is committed; no production code has changed.
