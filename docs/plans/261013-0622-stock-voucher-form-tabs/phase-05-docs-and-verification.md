# Phase 05 — Docs, manual check, final verification

**Status:** [ ] pending
**Complexity:** S

## Objective

Bring the project docs in line with the new form and the `PayableBalance` field, check the real app at both screen sizes, run the final verification and hand off the PR.

## Files

- `docs/architecture/system-architecture.md` (modify)
- `docs/codebase/directory-structure.md` (modify)
- `docs/code-standard/conventions.md` (modify)
- `docs/SUMMARY.md` (modify)
- `docs/plans/261013-0622-stock-voucher-form-tabs/SUMMARY.md` (modify: phase checkboxes)
- `docs/plans/261013-0622-stock-voucher-form-tabs/EXECUTION-REPORT.md` (new)

## Tasks

### Task 5.1 — Docs

These are documentation-only edits, so there is no test cycle.

1. **`system-architecture.md`:**
   - In the cash permissions paragraph (the `debt.partner_summary` mention), append: "(the name is kept; the debt line now shows on both stock-in and stock-out forms)".
   - In the read-endpoints paragraph, change the `partner-summary` description to: "receivable and payable balance, credit limit, available amount and overdue amount, shown in the debt line under the totals bar of the stock in/out form".
2. **`directory-structure.md`:** change the `pages/stock-vouchers/` comment (line ~190) to `# /stock-in and /stock-out list + form (main fields, tabs, line grid, totals bar, partner debt strip)`.
3. **`conventions.md`:** add these bullets under the Inventory "Frontend" list:
   - The stock voucher form keeps every tab panel mounted (`forceMount`, hidden with `data-[state=inactive]:hidden`), so form fields keep their refs and ids. A field's tab comes from `tabOfField` (`pages/stock-vouchers/utils/voucher-tabs.ts`). Map any new header field there.
   - Grid keyboard rules live in pure functions in `pages/stock-vouchers/utils/line-navigation.ts`: the Enter path per pricing mode, previous/next stop, ↑/↓ target. Per-user grid settings live in `use-stock-grid-prefs.ts` (localStorage `qldh.stock-grid-prefs.{userId}.{In|Out}`, versioned, validated on read). A new grid input field must be added to `LINE_FOCUS_FIELDS` and, if hideable, to `OPTIONAL_COLUMNS`.
   - Enter never submits the stock voucher form. Save is Ctrl+S or the buttons.
4. **`docs/SUMMARY.md`:** add a row for the plan under "Other", after the brainstorm row:
   `| [plans/261013-0622-stock-voucher-form-tabs/SUMMARY.md](plans/261013-0622-stock-voucher-form-tabs/SUMMARY.md) | Implementation plan for the stock voucher form redesign — backend PayableBalance, pure navigation/prefs/debt logic, T2 layout with tabs and debt strip, grid columns and keyboard |`
5. **Commit:**
   ```bash
   git add docs/architecture/system-architecture.md docs/codebase/directory-structure.md docs/code-standard/conventions.md docs/SUMMARY.md
   git commit -m "docs: stock voucher form tabs, grid keyboard and payable balance"
   ```

### Task 5.2 — Automated final verification

Run the commands in SUMMARY "Final Verification". Expected:

- `dotnet test` filter → PASS
- `npx vitest run src/pages/stock-vouchers` → PASS
- `npm run test` → FAIL lines == `BASELINE.md`
- typecheck → PASS
- lint counts ≤ `BASELINE.md`
- build → PASS (then restore the `tsbuildinfo` files)

If anything differs from the baseline, stop and report it. Do not fix unrelated failures.

### Task 5.3 — Manual checklist (real app)

Start the backend and `npm run dev`, and sign in as admin. For each viewport (1366×768 and 1920×1080, browser zoom 100%), check:

1. **Phiếu xuất mới.**
   - The page fills the window with no page scrollbar.
   - Enter walks Ngày → Lý do → Kho → HTTT → Mã ĐT → Tên → Địa chỉ → ô Mã hàng dòng 1, and never saves.
2. **Enter 15+ lines**, mixing Cái, md, m² and m³ products.
   - Enter stops follow the defaults table.
   - Picking a product focuses its second stop.
   - The grid scrolls inside the tab, and the header and #/Mã/Tên/Số còn lại stay put.
   - The focused cell is never hidden behind the sticky header or the left columns.
   - About 19 rows are visible on 1080p and about 11 on 768p.
3. **Arrow keys and delete.** ↑/↓ between rows; Ctrl+Delete; Insert; Shift+Enter across rows.
4. **⚙ menu.**
   - Show Ghi chú and add %VAT as a stop for Cái; the change applies at once.
   - Reload: the setting is kept.
   - Open a stock-in: it has its own defaults.
   - Khôi phục mặc định restores the defaults.
5. **Kho per line.** Set one line to another warehouse (show the Kho column first): the Kho column stays visible after hiding it in ⚙.
6. **Tabs.**
   - Alt+1…4 switch tabs.
   - Leaving Quỹ empty, or forcing a server error, puts a red dot on Thanh toán and switches to it on save.
   - Clicking Phí VC on the bar focuses the input.
7. **Debt strip.**
   - Pick a customer with debt and a credit limit: Phải thu → sau phiếu grows with unpaid lines, and Còn được nợ sau phiếu turns red when negative.
   - A supplier on a stock-in shows Phải trả → sau phiếu.
   - Sign in as WAREHOUSE (no permission): no strip.
8. **Edit and cancel.**
   - Edit a saved voucher: it opens on Hàng hóa, Lịch sử is a tab, and the after-voucher value does not double-count the saved voucher.
   - On a cancelled voucher, everything is read-only and the strip shows no "sau phiếu".
9. **Dark mode.** Sticky cells, the bar and the strip keep readable, opaque backgrounds.

Record every result, pass or fail, in `EXECUTION-REPORT.md`. Fix regressions inside this phase only when they come from this plan.

### Task 5.4 — Report and handoff

1. Write `EXECUTION-REPORT.md` with:
   - the phase commits;
   - the test, lint and build results against the baseline;
   - the manual checklist results;
   - deviations from the plan;
   - known limits (the ↑/↓ inside an open product suggestion list; D1, the permission name kept).
2. Tick all phases in `SUMMARY.md`.
3. Commit both files:
   ```bash
   git add docs/plans/261013-0622-stock-voucher-form-tabs/SUMMARY.md docs/plans/261013-0622-stock-voucher-form-tabs/EXECUTION-REPORT.md
   git commit -m "chore(plan): stock voucher form redesign complete"
   ```
4. Push the branch:
   ```bash
   git push -u qlbanhang feat/stock-voucher-form-tabs
   ```
   If the push looks hung, check `git ls-remote --heads qlbanhang feat/stock-voucher-form-tabs` before reporting a failure.
5. Give the user the compare URL `https://github.com/hongchinh/QLBanHang/compare/main...feat/stock-voucher-form-tabs` and a PR body. There is no `gh` CLI. End the PR body with the attribution line from the session's system reminder.
6. Post the final summary to the team chat.

## Verification

- All commands in SUMMARY "Final Verification" match the expectations above.
- The manual checklist is recorded in `EXECUTION-REPORT.md`.

## Exit Criteria

- The docs describe the new form and the `payableBalance` field.
- The branch is pushed, and the compare URL and PR body are given to the user.
- All phases are ticked, and the execution report is committed.
