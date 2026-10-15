# Phase 00 — Worktree, branch and failure baseline

**Status:** [ ] pending
**Complexity:** S

## Objective

Create the worktree and branch from the current `qlbanhang/main`, commit the plan with the reviewed brainstorm and its mockups, and record which tests already fail, so later phases can tell a regression from a known failure. No production code changes.

## Files

- `docs/plans/261015-0919-voucher-payment-qr/` (the plan, copied into the worktree)
- `docs/brainstorms/261015-0656-voucher-payment-qr/` (`SUMMARY.md`, `mockup-01-print-placement.html`, `mockup-02-screen-dialog.html`; copied into the worktree)
- `docs/plans/261015-0919-voucher-payment-qr/BASELINE.md` (new)
- `docs/SUMMARY.md` (modify — two rows)

## Tasks

### Task 0.1 — Worktree, branch and plan commit

1. From the main checkout `D:\Projects\QLBanHang` (uppercase `D:`):
   ```bash
   git fetch qlbanhang
   git merge-base --is-ancestor 3ae3804 qlbanhang/main && echo ok     # the plan's base must be in main
   git ls-remote --heads qlbanhang feat/voucher-payment-qr             # expect no output
   git worktree add .claude/worktrees/voucher-qr -b feat/voucher-payment-qr qlbanhang/main
   ```
   If `git fetch` cannot resolve the host, retry with the sandbox disabled; if it still fails, continue from the local ref `qlbanhang/main` and say so in the report. If the remote branch already exists, check what it holds (`git log qlbanhang/feat/voucher-payment-qr --oneline -5`); if it holds only commits of `main`, base the local branch on it, otherwise **stop and ask the user**.
2. Link the frontend packages of the main checkout into the worktree (PowerShell, a junction):
   ```powershell
   New-Item -ItemType Junction -Path "D:\Projects\QLBanHang\.claude\worktrees\voucher-qr\frontend\node_modules" -Target "D:\Projects\QLBanHang\frontend\node_modules"
   ```
   Before ever removing the worktree, delete only this link with `(Get-Item "<path>").Delete()`, then check that `D:\Projects\QLBanHang\frontend\node_modules` still has its packages.
3. Copy the plan and the brainstorm folder (both are untracked in the main checkout) into the worktree:
   ```bash
   cp -r /d/Projects/QLBanHang/docs/plans/261015-0919-voucher-payment-qr /d/Projects/QLBanHang/.claude/worktrees/voucher-qr/docs/plans/
   cp -r /d/Projects/QLBanHang/docs/brainstorms/261015-0656-voucher-payment-qr /d/Projects/QLBanHang/.claude/worktrees/voucher-qr/docs/brainstorms/
   ```
4. In the worktree's `docs/SUMMARY.md` ("Other" table) add, after the brainstorm row `brainstorms/261015-0644-cash-voucher-form-redesign/…` if present, otherwise after the last `brainstorms/` row:
   `| [brainstorms/261015-0656-voucher-payment-qr/SUMMARY.md](brainstorms/261015-0656-voucher-payment-qr/SUMMARY.md) | Brainstorm for a VietQR transfer QR on receipts (01-TT) and stock-out vouchers (02-VT) — bank and account holder on bank funds, voucher's bank fund else the branch default bank fund, stock-out amount = total minus cash already paid, QR beside the lines on A5 and a payment block on A4, "Kèm QR" print toggle, QR dialog/bottom sheet on the voucher screens, backend-owned rules with `payment-qr` endpoints; reviewed against code (auto receipts carry the stock-out code, QR on the right of the A4 block, 5 phases); print and screen mockups alongside |`
   and after the last `plans/` row:
   `| [plans/261015-0919-voucher-payment-qr/SUMMARY.md](plans/261015-0919-voucher-payment-qr/SUMMARY.md) | Plan for the VietQR transfer QR on receipts and stock-out vouchers — bank and account holder on funds, `VoucherPaymentQrRules` and `payment-qr` routes, QR on the printed 01-TT/02-VT with the "Kèm QR" switch, QR dialog on the voucher screens; 5 phases |`
5. Check the preconditions in the worktree; stop and report if one fails:
   - `git cat-file -e HEAD:backend/src/OrderMgmt.Application/Tt133/Vouchers/VoucherPrintService.cs`
   - `git cat-file -e HEAD:backend/src/OrderMgmt.Application/Payments/Services/VietQrPayloadBuilder.cs`
   - `git cat-file -e HEAD:frontend/src/components/icons/icon-registry.ts`
   - `"/c/Program Files/PostgreSQL/18/bin/psql" -h localhost -U postgres -c "select 1"` answers (`PGPASSWORD=1`).
6. Commit: `git add docs/plans/261015-0919-voucher-payment-qr docs/brainstorms/261015-0656-voucher-payment-qr docs/SUMMARY.md && git commit -m "docs(plan): voucher payment QR plan"`

### Task 0.2 — Record the baseline

1. Backend, from the worktree's `backend/`:
   ```bash
   export TEST_DB_CONNECTION="Host=localhost;Port=5432;Database=qldonhang_integtest_vqr;Username=postgres;Password=1"
   dotnet build OrderMgmt.sln
   dotnet test OrderMgmt.sln --logger "trx;LogFileName=baseline.trx" --results-directory "$TEMP/vqr-trx"
   ```
2. List the failing tests:
   ```bash
   python - "$TEMP/vqr-trx/baseline.trx" <<'EOF'
   import re, html, sys
   text = open(sys.argv[1], encoding='utf-8').read()
   names = sorted({html.unescape(m.group(1)) for m in re.finditer(r'<UnitTestResult[^>]*testName="([^"]+)"[^>]*outcome="Failed"', text)})
   print(len(names)); print('\n'.join(names))
   EOF
   ```
   (If the trx file lands in a sub-folder, find it with `ls -R "$TEMP/vqr-trx"`.)
3. Frontend, from the worktree's `frontend/`: `npm run typecheck`, `npm run lint`, `npm run test -- --testTimeout=30000`, `npm run build`, then `git checkout -- tsconfig.app.tsbuildinfo tsconfig.node.tsbuildinfo` (skip a path that is not tracked). A test that fails only on timeout is re-run alone before it counts. Known flaky tests of earlier runs: bank-accounts-tab "adds a new account", useNotificationHub "invalidates unread-count", payment-qr-page "generates and displays a QR".
4. Write `BASELINE.md` in the plan folder: the commit hash, the backend totals and the full list of failing test names, the frontend failing tests (file › suite › test), the lint error and warning counts, the typecheck and build results. Format: `docs/plans/archived/261013-1926-tt133-report-forms/BASELINE.md`.
5. Commit: `git add docs/plans/261015-0919-voucher-payment-qr/BASELINE.md && git commit -m "chore(plan): voucher payment QR test baseline"`

### Task 0.3 — Parallel or one after the other (for Phases 03 and 04)

1. Run `git worktree list`; if other plans' lanes are running tests, or when unsure, ask the user (Machine load risk). Phase 04 is vitest only, so it is the light one.
2. Record the choice in the phase report. Nothing is created now: Phase 04 Task 4.0 creates the second worktree from the Phase 02 commit when it runs in parallel.

## Verification

- `git -C /d/Projects/QLBanHang/.claude/worktrees/voucher-qr branch --show-current` prints `feat/voucher-payment-qr`.
- `BASELINE.md` lists every failing test by name.
- `git status --short` in the worktree is clean; no file under `backend/tests/OrderMgmt.IntegrationTests/TestResults/` changed.

## Exit Criteria

- The worktree and branch exist from `qlbanhang/main` (at or after `3ae3804`) and hold the plan, the brainstorm and its mockups.
- The baseline is committed; no production code has changed.
