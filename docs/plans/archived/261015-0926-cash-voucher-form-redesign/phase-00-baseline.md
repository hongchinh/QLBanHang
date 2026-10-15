# Phase 00 — Worktree, branch, failure baseline

**Status:** [x] done (2026-10-15; see EXECUTION-REPORT.md)
**Complexity:** S

## Objective

Create the feature worktree and branch from `qlbanhang/main`, commit the brainstorm and this plan there, and record which tests already fail, so later phases can tell a regression from a known failure.

## Files

- `docs/brainstorms/261015-0644-cash-voucher-form-redesign/` (copied from the main checkout, mockups included)
- `docs/plans/261015-0926-cash-voucher-form-redesign/` (copied from the main checkout; `BASELINE.md` new)
- `docs/bd/phan-tich-hanh-vi-4-2-ma-doi-tuong.md` (copied from the main checkout: it holds the "parts superseded" note)
- `docs/SUMMARY.md` (modify — four rows)

## Tasks

### Task 0.1 — Worktree, branch and plan commit

The main checkout `D:\Projects\QLBanHang` is shared: it holds other sessions' uncommitted files and may be on any branch. **Do not switch branches or commit there.**

1. From `/D/Projects/QLBanHang` (uppercase drive):
   ```bash
   git ls-remote qlbanhang main            # note the sha; the plan was written against 3ae3804 (1a87763 = docs-only PR #20 on top is fine)
   git fetch qlbanhang                      # if it fails with "Could not resolve host", retry with the sandbox disabled
   git cat-file -e qlbanhang/main:frontend/src/components/icons/icon-registry.ts   # the AppIcon registry (PR #18) must be in
   git cat-file -e qlbanhang/main:frontend/src/pages/cash-vouchers/components/cash-voucher-header.tsx
   git worktree add .claude/worktrees/cash-form -b feat/cash-voucher-form-redesign qlbanhang/main
   ```
   If the branch name already exists on the remote with other commits (`git ls-remote --heads qlbanhang feat/cash-voucher-form-redesign`), stop and report. From here on `$WT=/D/Projects/QLBanHang/.claude/worktrees/cash-form`.
2. Check the overlap with the payment QR plan and write the result into `BASELINE.md` (Task 0.2):
   ```bash
   git ls-remote --heads qlbanhang 'feat/voucher-payment-qr*'
   git worktree list                        # the QR plan runs locally in .claude/worktrees/voucher-qr
   git log --oneline qlbanhang/main..feat/voucher-payment-qr -- frontend/src/pages/cash-vouchers/ frontend/src/pages/stock-vouchers/
   git -C "$WT" log --oneline -3 -- frontend/src/pages/cash-vouchers/cash-voucher-form-page.tsx
   git -C "$WT" grep -n "PaymentQrButton" -- frontend/src/pages/cash-vouchers/ | head
   ```
   If the QR plan is merged, Phase 04 keeps its button and its `isDirty` disable rule untouched. If the local QR branch already touches these screens but is not merged, record its commits; whichever merges second rebases (SUMMARY "Risks").
3. Copy the design and the plan into the worktree:
   ```bash
   cp -r docs/brainstorms/261015-0644-cash-voucher-form-redesign "$WT/docs/brainstorms/"
   cp -r docs/plans/261015-0926-cash-voucher-form-redesign "$WT/docs/plans/"
   cp docs/bd/phan-tich-hanh-vi-4-2-ma-doi-tuong.md "$WT/docs/bd/"
   git -C "$WT" diff --stat -- docs/bd/phan-tich-hanh-vi-4-2-ma-doi-tuong.md     # only the note at the top may differ
   ```
   From now on edit only the worktree copies. Delete the untracked copies in the main checkout once the branch is merged and pulled.
4. In `$WT/docs/SUMMARY.md`, "Other" table:
   - add the three brainstorm rows (`brainstorms/261015-0644-cash-voucher-form-redesign/SUMMARY.md`, `…/section-01-behaviour-spec.md`, `…/section-02-review.md`) after the last `brainstorms/` row, with exactly the text of the main checkout's `docs/SUMMARY.md`;
   - replace the row text of `bd/phan-tich-hanh-vi-4-2-ma-doi-tuong.md` with the main checkout's version (it mentions the superseded parts);
   - the main checkout's `docs/SUMMARY.md` has a stale row for this plan ("…; 4 phases"): do not copy it. Add the row below after the last `plans/` row that is not `plans/archived/`; if none is left (`qlbanhang/main` 1a87763 archived the only one), put it right before the first `plans/archived/` row:
     `| [plans/261015-0926-cash-voucher-form-redesign/SUMMARY.md](plans/261015-0926-cash-voucher-form-redesign/SUMMARY.md) | Implementation plan for the receipt/payment form redesign and the shared partner field — `activeOnly` on the partner search endpoints and seed grants for quick add, pure logic (reason match, auto text, Enter focus), extended `PartnerAutocomplete` with `PartnerCodeNameField`, quick add (+ and F4) and catalog dialogs, the cash voucher form, stock voucher and quotation forms moved to the shared field, cleanup of the reason-based search mode; 7 phases |`
5. `cd "$WT/frontend" && npm ci` (do not junction `node_modules`).
6. Commit in the worktree:
   ```bash
   cd "$WT"
   git add docs/brainstorms/261015-0644-cash-voucher-form-redesign docs/plans/261015-0926-cash-voucher-form-redesign docs/bd/phan-tich-hanh-vi-4-2-ma-doi-tuong.md docs/SUMMARY.md
   git commit -m "docs(plan): cash voucher form redesign brainstorm and plan"     # plus the Co-Authored-By trailer
   ```

### Task 0.2 — Record the baseline

Runs in the worktree before any code change (nothing is edited yet, so no second worktree is needed).

1. Frontend:
   ```bash
   cd "$WT/frontend"
   npm run typecheck
   npm run lint
   npm run test -- --testTimeout=30000 --maxWorkers=3 --reporter=json --outputFile="$TEMP/cvf-baseline.json"
   node -e "const r=require(process.argv[1]);const f=[];for(const s of r.testResults)for(const t of s.assertionResults)if(t.status==='failed')f.push(s.name.split('frontend')[1]+' :: '+t.fullName);console.log(f.length);console.log(f.join('\n'))" "$TEMP/cvf-baseline.json"
   ```
2. Backend, the classes this plan touches (not the 20-minute full suite):
   ```bash
   cd "$WT/backend"
   export TEST_DB_CONNECTION="Host=localhost;Port=5432;Database=qldonhang_integtest_cvf_base;Username=postgres;Password=1"
   dotnet build OrderMgmt.sln
   dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~CashVoucherQueryTests|FullyQualifiedName~StockVoucherHelpersTests|FullyQualifiedName~CashDebtSeedTests|FullyQualifiedName~DbSeederUpgradeTests|FullyQualifiedName~AdminRolesCrudTests" --logger "trx;LogFileName=baseline.trx" --results-directory "$TEMP/cvf-baseline"
   ```
   (`TestResults/*.trx` in the repo are tracked: always write to `$TEMP`.)
3. Write `docs/plans/261015-0926-cash-voucher-form-redesign/BASELINE.md` in the worktree: the base sha, the QR overlap result of Task 0.1 step 2, the frontend counts (tests passed/failed) with the list of failing test names, the backend counts and failing names (or "none"), and the commands. A failing test listed here is a known failure; any other failure in a later phase is a regression.
4. Commit: `git add docs/plans/261015-0926-cash-voucher-form-redesign/BASELINE.md && git commit -m "docs(plan): record the cash voucher form baseline"`.

## Verification

- `git -C "$WT" status --short` is empty and `git -C "$WT" log --oneline -3` shows the two commits on top of the `qlbanhang/main` sha.
- `BASELINE.md` exists and names the base sha.

## Exit Criteria

- The worktree and branch exist, `npm ci` is done, the docs are committed, and the baseline is recorded.
- The QR plan overlap is known and written down.
