# Branch sample data — Test baseline

> Recorded: 2026-10-14 · Commit: `e844171` on `feat/branch-sample-data` (plan commit on top of `8135477`, `qlbanhang/main` with PR #14 merged) · No production code differs from `qlbanhang/main`.

Later phases run the same commands and compare **test by test** with the lists below. Backend: 1281 tests, 1262 passed, 19 failed (the same 19 as in the TT133 and executive dashboard baselines). Frontend: 1961 tests in 197 files, 1958 passed, 3 failed. Lint: 3 errors / 13 warnings. Typecheck: clean. Build: OK.

## Backend failing tests (19)

```
OrderMgmt.IntegrationTests.Admin.AdminRolesCrudTests.Create_with_invalid_code_format_returns_400(badCode: "1ABC")
OrderMgmt.IntegrationTests.Admin.AdminRolesCrudTests.Create_with_invalid_code_format_returns_400(badCode: "A")
OrderMgmt.IntegrationTests.Admin.AdminRolesCrudTests.Create_with_invalid_code_format_returns_400(badCode: "sales")
OrderMgmt.IntegrationTests.Admin.AdminRolesCrudTests.Create_with_reserved_system_code_returns_400(reservedCode: "Accountant")
OrderMgmt.IntegrationTests.Admin.AdminRolesCrudTests.Create_with_reserved_system_code_returns_400(reservedCode: "SALES")
OrderMgmt.IntegrationTests.Admin.AdminRolesCrudTests.Create_with_reserved_system_code_returns_400(reservedCode: "admin")
OrderMgmt.IntegrationTests.AuthTests.Refresh_rotates_token_and_revokes_old_one
OrderMgmt.IntegrationTests.Quotations.HandoverExportTests.HandoverNoPrice_Pdf_returns_pdf_via_fake_converter
OrderMgmt.IntegrationTests.Quotations.HandoverExportTests.HandoverWithPrice_Pdf_returns_pdf_via_fake_converter
OrderMgmt.IntegrationTests.Quotations.QuotationExportTests.Excel_AdvancePayment_WrittenToCorrectCells
OrderMgmt.IntegrationTests.Quotations.QuotationExportTests.Pdf_returns_pdf_bytes_via_fake_converter
OrderMgmt.IntegrationTests.Quotations.QuotationStateMachineTests.Cannot_accounting_confirm_from_draft
OrderMgmt.IntegrationTests.Quotations.QuotationStateMachineTests.Cannot_accounting_confirm_from_sent
OrderMgmt.IntegrationTests.Quotations.QuotationStateMachineTests.Cannot_confirm_directly_from_draft
OrderMgmt.IntegrationTests.Quotations.QuotationStateMachineTests.Cannot_uncancel
OrderMgmt.IntegrationTests.Quotations.QuotationStateMachineTests.Update_on_cancelled_returns_conflict
OrderMgmt.IntegrationTests.Reports.RevenueLineItemsExportTests.Excel_DataRows_ReflectConfirmedQuotation
OrderMgmt.IntegrationTests.Reports.RevenueLineItemsExportTests.Excel_HeaderRow_HasExpectedColumns
OrderMgmt.IntegrationTests.Reports.SalesRevenueReportTests.Report_FiltersByConfirmedAt_NotQuotationDate
```

List command (from `backend/`, after a run with `--logger "trx;LogFileName=<name>.trx" --results-directory "$TEMP/<dir>"`): a Python regex over `<UnitTestResult … testName="…" … outcome="Failed">` in the trx file, HTML-unescaped and sorted (Phase 00 Task 0.2).

## Frontend failing tests (3)

- `src/features/bank-accounts/bank-accounts-tab.test.tsx` › BankAccountsTab › adds a new account via the form
- `src/hooks/useNotificationHub.test.ts` › useNotificationHub › invalidates unread-count query on NewNotification
- `src/pages/payment-qr/payment-qr-page.test.tsx` › PaymentQrPage › generates and displays a QR after submitting valid data

## Lint, typecheck and build

- `npm run lint`: 16 problems = 3 errors, 13 warnings. The 3 errors: `jsx-a11y/no-static-element-interactions` (1) and `jsx-a11y/no-autofocus` (2).
- `npm run typecheck`: exit 0.
- `npm run build`: exit 0.

## Notes on the run

- Run in a separate detached worktree `.claude/worktrees/sd-baseline` on test database `qldonhang_integtest_sd_base`, while Phase 01 and the first lane tasks ran in other worktrees.
- The full backend suite took 37 minutes (19:58 → 20:36) under that load.
- Frontend tests ran with `npm run test -- --testTimeout=30000 --maxWorkers=3`.
- The trx file was written to `$TEMP/sd-baseline`, outside the repository.

## Difference from the TT133 baseline

The same 19 backend failures and 3 frontend failures. Lint has one warning less (13 instead of 14), with the same 3 errors. The suites are larger (backend 1281 vs 841 tests, frontend 1961 vs 948) because of the work merged since (PR #11–#14).
