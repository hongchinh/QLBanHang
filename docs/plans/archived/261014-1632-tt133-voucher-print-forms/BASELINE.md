# TT133 voucher print forms — Test baseline

> Recorded: 2026-10-14 · Commit: `3a4cc83` on `feat/tt133-voucher-print-forms` (plan commit on top of `8135477`, `qlbanhang/main` with PR #14 merged) · No production code differs from `qlbanhang/main`.

Later phases run the same commands and compare **test by test** with the lists below. Backend: 1281 tests, 1262 passed, 19 failed (the same 19 as in the TT133 report forms baseline); the run took 1 h 50 min because other sessions ran backend tests at the same time. Frontend: 1961 tests in 197 files, 1958 passed, 3 failed. Lint: 3 errors / 13 warnings. Typecheck: clean. Build: OK.

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

List command (from `backend/`, after a run with `--logger "trx;LogFileName=<name>.trx" --results-directory "$TEMP/vprint-trx"`): the Python regex of Phase 00 Task 0.2 over `<UnitTestResult … testName="…" … outcome="Failed">`, HTML-unescaped and sorted.

## Frontend failing tests (3)

- `src/features/bank-accounts/bank-accounts-tab.test.tsx` › BankAccountsTab › adds a new account via the form
- `src/hooks/useNotificationHub.test.ts` › useNotificationHub › invalidates unread-count query on NewNotification
- `src/pages/payment-qr/payment-qr-page.test.tsx` › PaymentQrPage › generates and displays a QR after submitting valid data

Run with `npm run test -- --testTimeout=30000 --maxWorkers=3`.

## Lint, typecheck and build

- `npm run lint`: 16 problems (3 errors, 13 warnings).
- `npm run typecheck`: clean.
- `npm run build`: OK (`tsconfig.*.tsbuildinfo` restored afterwards).
