# Baseline - Sidebar navigation redesign

- Date: 2026-10-13
- Commit: 5898861 (branch `feat/sidebar-nav-redesign`, from `qlbanhang/main`, Round 2B merged)
- Frontend only, run from `frontend/`.

## Tests (`npm run test`)

Totals: `Test Files  3 failed | 125 passed (128)` / `Tests  3 failed | 782 passed (785)`

Failing (pre-existing, same as Round 2B baseline):

- `src/features/bank-accounts/bank-accounts-tab.test.tsx` > BankAccountsTab > adds a new account via the form
- `src/hooks/useNotificationHub.test.ts` > useNotificationHub > invalidates unread-count query on NewNotification
- `src/pages/payment-qr/payment-qr-page.test.tsx` > PaymentQrPage > generates and displays a QR after submitting valid data

## Lint (`npm run lint`)

`17 problems (3 errors, 14 warnings)`

## Typecheck (`npm run typecheck`)

Passes (`tsc -p tsconfig.app.json`, exit 0, no output).
