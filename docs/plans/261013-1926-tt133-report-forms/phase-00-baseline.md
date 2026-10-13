# Phase 00 — Branch and failure baseline

**Status:** [ ] pending
**Complexity:** S

## Objective

Create the working branch, commit the plan, and record which tests already fail, so later phases can tell a regression from a known failure.

## Files

- `docs/plans/261013-1926-tt133-report-forms/BASELINE.md` (new)
- `docs/SUMMARY.md` (modify — add the plan row)

## Tasks

### Task 0.1 — Branch and plan commit

1. Fetch and check the merge the plan waits for (T1): after `git fetch qlbanhang`, `git merge-base --is-ancestor c7ade63 qlbanhang/main` must succeed (`c7ade63` is a commit of `feat/stock-voucher-form-tabs`; if that branch was squash-merged, check instead that `git log qlbanhang/main --oneline` shows its merge), and the stock voucher form plan (`docs/plans/261013-0622-stock-voucher-form-tabs/SUMMARY.md` on `qlbanhang/main`, or its archived copy) must show every phase `[x]`. If a check fails, **stop and tell the user**: the stock voucher form branch is not merged yet. Then branch: `git switch -c feat/tt133-report-forms qlbanhang/main`. If the plan folder exists only on another branch, bring it over with `git checkout <that branch> -- docs/plans/261013-1926-tt133-report-forms`.
2. Check the preconditions; stop and report if one fails:
   - `git cat-file -e HEAD:frontend/src/pages/cash-reports/debt-reconciliation-page.tsx` (Round 2B is in);
   - `git cat-file -e HEAD:backend/src/OrderMgmt.Infrastructure/Excel/LibreOfficeSpreadsheetPdfConverter.cs`;
   - `psql -h localhost -U postgres -c "select 1"` answers (password `1`).
3. Add this row to the "Other" table of `docs/SUMMARY.md`, after the last `plans/` row:
   `| [plans/261013-1926-tt133-report-forms/SUMMARY.md](plans/261013-1926-tt133-report-forms/SUMMARY.md) | Implementation plan for the TT133 report forms — company profile, shared ClosedXML form builder, Excel/PDF exports of S04a/S05/S06/S07/S08/S12, debt summary and reconciliation record, branch/company scope, stock summary report |`
4. Commit: `git add docs/plans/261013-1926-tt133-report-forms docs/SUMMARY.md && git commit -m "docs(plan): TT133 report forms plan"`

### Task 0.2 — Record the baseline

1. Backend, from `backend/`:
   ```bash
   export TEST_DB_CONNECTION="Host=localhost;Port=5432;Database=qldonhang_integtest;Username=postgres;Password=1"
   dotnet build OrderMgmt.sln
   dotnet test OrderMgmt.sln --logger "trx;LogFileName=baseline.trx"
   ```
2. List the failing tests (from `backend/`):
   ```bash
   python - <<'EOF'
   import re, html
   text = open('tests/OrderMgmt.IntegrationTests/TestResults/baseline.trx', encoding='utf-8').read()
   names = sorted({html.unescape(m.group(1)) for m in re.finditer(r'<UnitTestResult[^>]*testName="([^"]+)"[^>]*outcome="Failed"', text)})
   print(len(names)); print('\n'.join(names))
   EOF
   ```
3. Frontend, from `frontend/`: `npm run typecheck`, `npm run lint`, `npm run test`, `npm run build`, then `git checkout -- frontend/tsconfig.app.tsbuildinfo frontend/tsconfig.node.tsbuildinfo`.
4. Write `BASELINE.md` in the plan folder with: the commit hash, the backend totals and the full list of failing test names, the frontend failing tests (file › suite › test), the lint error and warning counts, and the typecheck and build results. Use `docs/plans/archived/261012-1709-cash-debt-round2b/BASELINE.md` as the format. For reference, the end of Round 2B had 19 backend failures, 3 frontend failures and lint 3 errors / 14 warnings.
5. Commit: `git add docs/plans/261013-1926-tt133-report-forms/BASELINE.md && git commit -m "chore(plan): TT133 forms test baseline"`

## Verification

- `git branch --show-current` prints `feat/tt133-report-forms`.
- `BASELINE.md` lists every failing test by name.

## Exit Criteria

- The branch exists from a `qlbanhang/main` that contains the merged stock voucher form work, and holds the plan.
- The baseline is committed; no production code has changed.
