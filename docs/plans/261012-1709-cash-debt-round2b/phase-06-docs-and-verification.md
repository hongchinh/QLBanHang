# Phase 06 — Documentation & final verification

**Status:** [ ] pending
**Complexity:** S

## Objective

Record Round 2B in the project docs, close the follow-up items it delivers, run the final verification and write the execution report. No production code, so there is no TDD cycle.

## Files

- `docs/project-pdr/product-goals.md` (modify — Round 2B delivered; the five reports; owner = voucher creator, or the user picked on an opening debt; overdue report as warning list)
- `docs/architecture/system-architecture.md` (modify — the `debt-*` report endpoints, `DebtPositionQuery`, the "as at" window B3, `GET /api/opening-debts/owners`)
- `docs/code-standard/conventions.md` (modify — the cash & debt patterns section: B3 window rule, B4 identity tests, `buckets` comma format and `DebtAging`)
- `docs/SUMMARY.md` (modify — link to this plan)
- `docs/brainstorms/261006-2139-stock-voucher-clone/SUMMARY.md` (modify — tick the six Round 2B items and rewrite the "Điều kiện trước khi plan" line, B11)
- `docs/plans/261012-1709-cash-debt-round2b/SUMMARY.md` (modify — tick the phases)
- `docs/plans/261012-1709-cash-debt-round2b/EXECUTION-REPORT.md` (new)

## Tasks

### Task 6.1 — Project docs

1. Read each file's current Round 2A section and add the Round 2B facts next to it in the same style (no rewrite of unrelated text):
   - `product-goals.md`: the five reports, B1 (creator = salesperson, or the owner picked on an opening debt; BA can still ask for `SalespersonId`), B2.
   - `system-architecture.md`: `DebtPositionReportsController` + `IDebtPositionReportService` (documents, aging, overdue, by-salesperson), `debt-reconciliation` on `CashDebtReportsController`, `DebtPositionQuery` and rule B3.
   - `conventions.md`: an entry "As-at debt reports" with B3, B4 and the `buckets` parameter format.
   - `docs/SUMMARY.md`: one row for `plans/261012-1709-cash-debt-round2b/SUMMARY.md` ("Implementation plan for Round 2B — debt documents, aging, overdue, by-salesperson and reconciliation reports").
   - stock-voucher-clone SUMMARY: tick the six Round 2B lines (`- [x]`) and replace the "Điều kiện trước khi plan: BA chốt thanh toán liên chi nhánh và tài khoản NH dùng chung nhiều chi nhánh" line by "Không chặn 2B: các báo cáo 2B theo chi nhánh làm việc; câu hỏi BA vẫn mở cho đợt sau" (B11; if the user or the BA answered meanwhile, write that answer instead).
2. **Check:** `git diff --stat` lists only `docs/` files (and no `*.tsbuildinfo`: run `git checkout -- frontend/tsconfig.app.tsbuildinfo frontend/tsconfig.node.tsbuildinfo` after the build of Task 6.2 and before this check if needed).
3. **Commit:** `git add <the files above> && git commit -m "docs: round 2B debt reports"`

### Task 6.2 — Final verification and execution report

1. Run the Final Verification of `SUMMARY.md` (backend build + full test suite; frontend typecheck, lint, test, build). Compare with `BASELINE.md` test by test.
2. Run the manual smoke test of `SUMMARY.md` if the dev stack is available; otherwise record that it was not run.
3. Write `EXECUTION-REPORT.md`: commits per phase, the state of the B11 assumption (still open / answered), deviations from the plan (numbered `D-1 …`, with the reason), verification numbers (backend failed / total, frontend failed / total, lint counts, build), follow-ups for Round 3 or a later fix.
4. Tick every phase in this plan's `SUMMARY.md` (`- [x]`) and set each phase file's **Status** to `[x] complete`.
5. Restore the tracked build files (`git checkout -- frontend/tsconfig.app.tsbuildinfo frontend/tsconfig.node.tsbuildinfo`).
6. **Commit:** `git add docs/plans/261012-1709-cash-debt-round2b && git commit -m "chore(plan): round 2B complete"`

## Verification

- `git status --short` shows no tracked changes.
- `EXECUTION-REPORT.md` states the verification results and lists every deviation.

## Exit Criteria

- Docs describe the five reports and rules B1–B3.
- Final verification: no new failures against `BASELINE.md`; `npm run build` passes.
- The branch is ready for `finishing-a-development-branch` (PR to remote `qlbanhang`).
