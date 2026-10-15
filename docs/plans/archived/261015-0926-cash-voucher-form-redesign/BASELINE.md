# Baseline — cash voucher form redesign

> Recorded: 2026-10-15 10:55 · Branch `feat/cash-voucher-form-redesign` · Base `qlbanhang/main` at `1a877638` (PR #20, docs only, on top of `3ae3804`)

A failure listed here is a known failure. Any other failure in a later phase is a regression.

## Overlap with the payment QR plan

- The QR plan is not merged into `qlbanhang/main` (no `feat/voucher-payment-qr*` branch on the remote yet). The base has no `PaymentQrButton`.
- It runs locally in `.claude/worktrees/voucher-qr` (`feat/voucher-payment-qr`) and `.claude/worktrees/voucher-qr-fe` (`feat/voucher-payment-qr-fe`, tip `9495d65b`). The frontend branch touches the screens of this plan:
  - `c9959a1e feat(payment-qr): QR button on the receipt and stock-out pages`
  - `68b440dd test(payment-qr): pin the props the pages pass to the QR dialog`
  - Files: `cash-voucher-form-page.tsx` (+12), `cash-voucher-form-page.test.tsx` (+57), `stock-voucher-form-page.tsx` (+12), `stock-voucher-form-page.test.tsx` (+57).
- Whichever merges second rebases. Conflicts are expected only in the button row of the two pages and their test files.

## Frontend (`frontend/`)

- `npm run typecheck`: clean.
- `npm run lint`: **fails on the base** with 3 errors and 13 warnings. The errors are known:
  - `src/components/ui/table.tsx:75` `jsx-a11y/no-static-element-interactions`
  - `src/features/customers/components/customer-catalog-list.tsx:58` `jsx-a11y/no-autofocus` (this file moves in Phase 03; the move may fix it)
  - `src/features/products/components/product-catalog-list.tsx:51` `jsx-a11y/no-autofocus`
  Later phases must add no lint error; the count may only go down.
- Tests: 634 files, 2199 tests, 2196 passed, **3 failed**:
  - `src/hooks/useNotificationHub.test.ts` :: useNotificationHub invalidates unread-count query on NewNotification
  - `src/features/bank-accounts/bank-accounts-tab.test.tsx` :: BankAccountsTab adds a new account via the form
  - `src/pages/payment-qr/payment-qr-page.test.tsx` :: PaymentQrPage generates and displays a QR after submitting valid data

## Backend (`backend/`, targeted classes)

`TEST_DB_CONNECTION` base name `qldonhang_integtest_cvf_base`. `dotnet build OrderMgmt.sln` clean.
Filter `CashVoucherQueryTests|StockVoucherHelpersTests|CashDebtSeedTests|DbSeederUpgradeTests|AdminRolesCrudTests`: 38 tests, 32 passed, **6 failed**, all in `AdminRolesCrudTests`:

- `Create_with_invalid_code_format_returns_400(badCode: "1ABC")`
- `Create_with_invalid_code_format_returns_400(badCode: "A")`
- `Create_with_invalid_code_format_returns_400(badCode: "sales")`
- `Create_with_reserved_system_code_returns_400(reservedCode: "SALES")` (got 409 Conflict)
- `Create_with_reserved_system_code_returns_400(reservedCode: "Accountant")`
- `Create_with_reserved_system_code_returns_400(reservedCode: "admin")`

## Commands

```bash
cd "$WT/frontend"
npm run typecheck
npm run lint
npm run test -- --testTimeout=30000 --maxWorkers=3 --reporter=json --outputFile="$TEMP/cvf-baseline.json"

cd "$WT/backend"
export TEST_DB_CONNECTION="Host=localhost;Port=5432;Database=qldonhang_integtest_cvf_base;Username=postgres;Password=1"
dotnet build OrderMgmt.sln
dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~CashVoucherQueryTests|FullyQualifiedName~StockVoucherHelpersTests|FullyQualifiedName~CashDebtSeedTests|FullyQualifiedName~DbSeederUpgradeTests|FullyQualifiedName~AdminRolesCrudTests" --logger "trx;LogFileName=baseline.trx" --results-directory "$TEMP/cvf-baseline"
```
