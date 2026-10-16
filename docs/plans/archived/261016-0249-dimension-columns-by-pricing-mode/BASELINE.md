# Baseline — dimension columns

> Recorded 2026-10-16 in `.claude/worktrees/dimension-columns` (branch `feat/dimension-columns`), before any code change. A failure or warning listed here is known; any other one in a later phase is a regression.

## Base

- `qlbanhang/main` = `438b0db4` (Merge PR #31 `fix/typeahead-no-open-on-focus`). The plan was written against `e61e4f1b`.
- `git diff --stat e61e4f1b qlbanhang/main` over the directories the phases edit lists only: `features/help/content/inventory-opening-stock.md`, `inventory-stock-card.md`, `settings-recalc-cost.md`, `pages/inventory/opening-stock-page(.test).tsx`, `pages/inventory/stock-card-page(.test).tsx`, `pages/settings/recalc-cost-page(.test).tsx`. No file of Phases 01–03 is among them, so the `e61e4f1b` line references stay valid. `frontend/package.json` and `package-lock.json` did not change, so `node_modules` is a junction to the main checkout.
- `feat/dimension-columns` did not exist on the remote.

## Typecheck

`npm run typecheck`: exit 0.

## Lint

`npm run lint`: 17 problems, 2 errors and 15 warnings (as expected).

Errors:
- `src/components/ui/table.tsx:75` jsx-a11y/no-static-element-interactions
- `src/features/products/components/product-catalog-list.tsx:51` jsx-a11y/no-autofocus

Warnings:
- `src/components/layout/back-link.tsx:17`, `:23` react-refresh/only-export-components
- `src/hooks/useNotificationHub.test.ts:55` @typescript-eslint/consistent-type-imports
- `src/pages/admin/components/reset-password-dialog.tsx:76`, `:83` jsx-a11y/label-has-associated-control
- `src/pages/product-groups/product-group-list-page.tsx:108` (no rule id)
- `src/pages/quotations/components/line-items-grid.tsx:61` react-refresh/only-export-components
- `src/pages/quotations/quotation-form-page.draft.test.tsx:21`, `:76` @typescript-eslint/consistent-type-imports
- `src/pages/reports/revenue-page.test.tsx:10` @typescript-eslint/consistent-type-imports
- `src/pages/reports/sales-performance-page.test.tsx:9` @typescript-eslint/consistent-type-imports
- `src/pages/reports/sales-revenue-detail-page.tsx:238` react-refresh/only-export-components (twice)
- `src/pages/reports/sales-revenue-page.tsx:88` jsx-a11y/label-has-associated-control
- `src/pages/settings/my-quotation-settings-page.tsx:8` @typescript-eslint/consistent-type-imports

## Tests

`npm run test -- --testTimeout=30000 --maxWorkers=3`: 3043 tests, 3040 passed, 3 failed (the known three):
- `src/hooks/useNotificationHub.test.ts` :: useNotificationHub invalidates unread-count query on NewNotification
- `src/features/bank-accounts/bank-accounts-tab.test.tsx` :: BankAccountsTab adds a new account via the form
- `src/pages/payment-qr/payment-qr-page.test.tsx` :: PaymentQrPage generates and displays a QR after submitting valid data

## help:stale

`npm run help:stale` prints 3 warnings (flagged by PR #30's merge commit `bb2af827` and fix `242a4baf`):
- admin-dashboard: Code đổi ngày 2026-10-16, hướng dẫn sửa lần cuối ngày 2026-10-16
- home: Code đổi ngày 2026-10-16, hướng dẫn sửa lần cuối ngày 2026-10-16
- reports-revenue: Code đổi ngày 2026-10-16, hướng dẫn sửa lần cuối ngày 2026-10-15

`inventory-stock-card` is not flagged. Phase 04 Task 4.14 deals with these.

## Commands

Run from `frontend/` of the worktree:

```bash
npm run typecheck
npm run lint
npm run help:stale
npm run test -- --testTimeout=30000 --maxWorkers=3 --reporter=json --outputFile="$TEMP/dimcols-baseline.json"
```
