# TT133 report forms — Test baseline

> Recorded: 2026-10-13 · Commit: `f441baf` on `feat/tt133-report-forms` (plan commit on top of `a76ce85`, `qlbanhang/main` with PR #8 and PR #10 merged) · No production code differs from `qlbanhang/main`.

Later phases run the same commands and compare **test by test** with the lists below. Backend count: 841 tests, 822 passed, 19 failed (the same 19 as at the end of Round 2B). Frontend: 948 tests in 142 files, 945 passed, 3 failed. Lint: 3 errors / 14 warnings. Typecheck: clean. Build: OK.

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

List command (from `backend/`, after a run with `--logger "trx;LogFileName=<name>.trx"`): a Python regex over `<UnitTestResult … testName="…" … outcome="Failed">` in the trx file, HTML-unescaped and sorted (see Phase 00 Task 0.2).

## Frontend failing tests (3)

- `src/features/bank-accounts/bank-accounts-tab.test.tsx` › BankAccountsTab › adds a new account via the form
- `src/hooks/useNotificationHub.test.ts` › useNotificationHub › invalidates unread-count query on NewNotification
- `src/pages/payment-qr/payment-qr-page.test.tsx` › PaymentQrPage › generates and displays a QR after submitting valid data

## Lint, typecheck and build

- `npm run lint`: 17 problems = 3 errors, 14 warnings.
- `npm run typecheck`: exit 0.
- `npm run build`: exit 0.

## Notes on the run

- The full backend suite took 21 minutes on this machine.
- A plain `dotnet build OrderMgmt.sln` failed with MSB3021 (another process held `OrderMgmt.WebApi/bin`), so the build and the test run used `-o "$TEMP/qlbh-build"`.
- The repository tracks five old `TestResults/*.trx` files; a run that reuses one of those names modifies a tracked file. Write new trx files with `--results-directory` outside the repository, or restore the file with `git checkout --`.

## Difference from the end of Round 2B

The same 19 backend failures, 3 frontend failures and lint counts. The suites are larger (backend 841 vs 840 tests, frontend 948 vs 714) because of the sidebar redesign and the stock voucher form merged before this branch.
