# Baseline (frontend) before the stock voucher form redesign

- Date: 2026-10-13 13:13:46
- Base: `qlbanhang/main` at 524a7f92464c42d9f0623d84ebd81b61c7ce6ca7
- Branch: `feat/stock-voucher-form-tabs`

## `npm run test` (FAIL lines)

- `src/features/bank-accounts/bank-accounts-tab.test.tsx > BankAccountsTab > adds a new account via the form`
- `src/hooks/useNotificationHub.test.ts > useNotificationHub > invalidates unread-count query on NewNotification`
- `src/pages/payment-qr/payment-qr-page.test.tsx > PaymentQrPage > generates and displays a QR after submitting valid data`

Totals: `Tests  3 failed | 866 passed (869)`

## `npm run lint`

`17 problems (3 errors, 14 warnings)` (3 errors, 14 warnings)

## `npm run typecheck`

pass (exit code 0)
