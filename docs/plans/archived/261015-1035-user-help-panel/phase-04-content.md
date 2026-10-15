# Phase 04 — Content for the other 11 screens

**Status:** [x] done (2026-10-15)
**Complexity:** M

## Objective

Bring the branch up to date with `qlbanhang/main`, then write the help files of the remaining round-1 screens with the `help-content` skill, remove their routes from `HELP_PENDING`, and check every bold label against the code. Phase 04 needs Phases 01–03 (the skill of Task 3.4, `help:stale` of Task 3.2). It runs alone on the branch: Task 4.0 merges main, which a parallel track would have to absorb.

## Files

- `frontend/src/features/help/content/*.md` (11 new; `stock-out.form.md` modified in Task 4.4, and in Task 4.0 if the merge changed the stock-out form)
- `frontend/src/features/help/help-pending.ts` (modify)
- `docs/plans/261015-1035-user-help-panel/CONTENT-CHECK.md` (new)

## How every task runs (TDD-shaped for content)

For each file of the task:

1. **Red**: remove the file's routes from `HELP_PENDING`; run `npm run test -- --testTimeout=30000 --maxWorkers=3 src/features/help/help-guard.test.ts` → FAIL, "cover every app route…" lists exactly those routes. Keep the line for the commit body.
2. **Write** the file with the `help-content` skill (`.claude/skills/help-content/SKILL.md`), reading the sources listed below first, with `related: []` for now. The text describes the screen as it is on the base commit.
3. **Green**: run the guard test → PASS.
4. **Label check**: for every `**label**` run `git grep -nF "<label>" -- <the file's sources>`; record each label with its `file:line`, or `dynamic: <why>` when the text is assembled in code, in `CONTENT-CHECK.md` (one table per key). A label found nowhere and not dynamic is a mistake: fix the text.

When every file of the task exists, fill in `related` from the task's table (only keys that have a file by then, P11), rerun the guard test, and commit the task's files together: `docs(help): <area> screens` with the RED lines in the body. The front-matter `updated` is the day the file is written.

## Tasks

### Task 4.0 — Write on the newest UI (P12)

The cash voucher form redesign and the payment QR plans were already running when this plan was reviewed and change the receipt, payment and stock forms. Content written against an old base would be stale on arrival.

1. From `$WT`: `git fetch qlbanhang main` (retry with the sandbox disabled if it fails) and `git log --oneline HEAD..qlbanhang/main`.
2. Empty → note "base unchanged" in `CONTENT-CHECK.md` and go on with Task 4.1.
3. Otherwise `git merge qlbanhang/main`. A conflict in `App.tsx` means upstream edited the route tree: keep this branch's `App.tsx` (shell only) and apply the upstream route change inside `app-routes.tsx`; any other conflict: stop and report. Then, from `$WT/frontend`: `npm ci` if `package-lock.json` changed; `npm run test -- --testTimeout=30000 --maxWorkers=3 src/features/help src/App.routes.test.tsx src/App.shell.test.tsx` → PASS (a new upstream route goes into `HELP_PENDING`); `npm run help:stale`. If it lists `stock-out.form` with `Code đổi ngày …`, recheck that file's labels against the merged code (the commands of Phase 01 Task 1.6 step 2), fix the text, set `updated` to today and commit `docs(help): follow the stock-out form changes from main`.
4. Record the merged sha and the `help:stale` output in `CONTENT-CHECK.md`. The tasks below describe the screens as they are after this merge.

### Task 4.1 — Sales: `quotations.list`, `quotations.form`, `customers.list`

| Key | Routes | Sources | Related |
|---|---|---|---|
| `quotations.list` | `/quotations` | `frontend/src/pages/quotations/quotation-list-page.tsx`, `frontend/src/pages/quotations/components/**`, `frontend/src/features/quotations/**`, `backend/src/OrderMgmt.Application/Sales/Quotations/**` | `quotations.form`, `customers.list` |
| `quotations.form` | `/quotations/new`, `/quotations/:id` | `frontend/src/pages/quotations/quotation-form-page.tsx`, `frontend/src/pages/quotations/components/**`, `frontend/src/pages/quotations/utils/**`, `frontend/src/features/quotations/**`, `backend/src/OrderMgmt.Application/Sales/Quotations/**` | `customers.list` |
| `customers.list` | `/customers` | `frontend/src/pages/customers/customer-list-page.tsx`, `frontend/src/features/customers/**`, `backend/src/OrderMgmt.Application/Catalog/Customers/**` | `quotations.form` |

Order: `customers.list`, `quotations.form`, `quotations.list`. Business rules to reflect where they touch the screen (`docs/project-pdr/product-goals.md` "Quotations"): own quotations only without `quotations.view_all`, lock-at status, cancel permission for confirmed quotations, export template per owner.

### Task 4.2 — Inventory: `stock-in.form`, `inventory-opening-stock`

| Key | Routes | Sources | Related |
|---|---|---|---|
| `stock-in.form` | `/stock-in/new`, `/stock-in/:id` | `frontend/src/pages/stock-vouchers/**`, `frontend/src/features/stock-vouchers/**`, `backend/src/OrderMgmt.Application/Inventory/StockVouchers/**` | `stock-out.form` |
| `inventory-opening-stock` | `/inventory/opening-stock` | `frontend/src/pages/inventory/opening-stock-page.tsx`, `frontend/src/features/opening-stock/**`, `backend/src/OrderMgmt.Application/Inventory/OpeningStocks/**` | `stock-in.form`, `stock-out.form` |

Use `STOCK_VOUCHER_LABELS.In` for the stock-in labels (`Ngày nhập`, `Lý do nhập`, `Người giao hàng`). Rules: `product-goals.md` "Inventory" (one stock unit per product, metre-based lines, a paid amount creates an automatic payment voucher, order discount allocation).

### Task 4.3 — Cash: `receipts.form`, `payments.form`, `fund-transfers.form`

| Key | Routes | Sources | Related |
|---|---|---|---|
| `receipts.form` | `/receipts/new`, `/receipts/:id` | `frontend/src/pages/cash-vouchers/**`, `frontend/src/features/cash-vouchers/**`, `frontend/src/features/cash-common/**`, `backend/src/OrderMgmt.Application/CashDebt/CashVouchers/**` | `payments.form` |
| `payments.form` | `/payments/new`, `/payments/:id` | same as `receipts.form` | `receipts.form` |
| `fund-transfers.form` | `/fund-transfers/new`, `/fund-transfers/:id` | `frontend/src/pages/fund-transfers/fund-transfer-form-page.tsx`, `frontend/src/features/fund-transfers/**`, `backend/src/OrderMgmt.Application/CashDebt/FundTransfers/**` | `receipts.form`, `payments.form` |

Order: `payments.form`, `receipts.form`, `fund-transfers.form`. Both voucher keys share one page component (`cash-voucher-form-page.tsx`, labels in `pages/cash-vouchers/labels.ts`): write each with its own labels (Phiếu thu / Phiếu chi, Lý do nộp / Lý do chi…). Rules: `product-goals.md` section "Tiền & Công nợ" under "Core Business Rules" (allocation to open documents on save, automatic vouchers of stock vouchers are read-only here, negative fund policy). Describe the form as it is after Task 4.0; if the cash voucher form redesign has not merged yet, it will change this screen later and Phase 05 Task 5.2 brings the help in line (P12).

### Task 4.4 — Debt and the executive dashboard: `debt-matching`, `debt-opening`, `dashboard-executive`; links of `stock-out.form`

| Key | Routes | Sources | Related |
|---|---|---|---|
| `debt-matching` | `/debt/matching` | `frontend/src/pages/debt/debt-matching-page.tsx`, `frontend/src/pages/debt/components/**`, `frontend/src/features/debt/**`, `backend/src/OrderMgmt.Application/CashDebt/Debt/**` | `receipts.form`, `payments.form` |
| `debt-opening` | `/debt/opening` | `frontend/src/pages/debt/opening-debt-page.tsx`, `frontend/src/pages/debt/opening-debt-form-dialog.tsx`, `frontend/src/features/opening-debts/**`, `backend/src/OrderMgmt.Application/CashDebt/OpeningDebts/**` | `debt-matching`, `customers.list` |
| `dashboard-executive` | `/dashboard/executive` | `frontend/src/pages/executive-dashboard/**`, `frontend/src/features/executive-dashboard/**`, `backend/src/OrderMgmt.Application/Dashboards/Executive/**` | none in round 1 (its reports have no help yet) |

Rules: `product-goals.md` section "Tiền & Công nợ" under "Core Business Rules" (matching never exceeds open amounts) and the bullets of "Executive dashboard — Bảng điều hành" under "Current Scope" (who sees it, revenue from stock-out vouchers, choosing a branch also makes it the working branch). Then set `related: [receipts.form, debt-matching]` in `stock-out.form.md` (Phase 01 Task 1.6 left it empty) and update its `updated`.

### Task 4.5 — Review pass

1. Read the 12 files side by side: same tone, same heading words, steps start with verbs, no step longer than about 25 words, no internal names (entity, endpoint, DTO).
2. `npm run test -- --testTimeout=30000 --maxWorkers=3 src/features/help` → PASS; `npm run help:stale` → no `cũ hơn lần commit cuối` line (`updated-older-than-commit`) and no `không khớp file nào` line (`sources-no-match`).
3. `HELP_PENDING` no longer holds any route of the 12 keys: `git grep -nE "'/(quotations|customers|stock-in|stock-out|receipts|payments|fund-transfers)(/new|/:id)?'|'/debt/(matching|opening)'|'/inventory/opening-stock'|'/dashboard/executive'" -- src/features/help/help-pending.ts` prints only `/customers/new`, `/customers/:id`, `/stock-in`, `/stock-out`, `/receipts`, `/payments` and `/fund-transfers` (the lists and forms that are not in round 1).
4. Commit any fixes: `docs(help): round 1 review fixes`; commit `CONTENT-CHECK.md`: `docs(plan): help label check`.

## Verification

```bash
cd "$WT/frontend"
npm run test -- --testTimeout=30000 --maxWorkers=3 src/features/help
npm run help:stale
ls src/features/help/content | wc -l      # 12
```

## Exit Criteria

- 12 valid help files; their routes are out of `HELP_PENDING`; every bold label is accounted for in `CONTENT-CHECK.md`.
- `stock-out.form` links to `receipts.form` and `debt-matching`.
