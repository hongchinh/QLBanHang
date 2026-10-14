# Executive dashboard — Test baseline

> Base commit: `a76ce85` (`main`, equal to `qlbanhang/main`, PR #8 and PR #10 merged).
> Source: the lists below were recorded on 2026-10-13 by the TT133 plan on commit `f441baf`, which is `a76ce85` plus plan documents only (`docs/plans/261013-1926-tt133-report-forms/BASELINE.md` on `feat/tt133-report-forms`). They were not measured again for this plan; Phase 08 runs the full suites and compares with them test by test.

Backend: 841 tests, 822 passed, 19 failed. Frontend: 948 tests in 142 files, 945 passed, 3 failed. Lint: 3 errors / 14 warnings. Typecheck: clean. Build: OK.

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

To list the failures of a later run, read the trx file written by `--logger "trx;LogFileName=dash.trx" --results-directory "$TEMP/qlbh-dash-results"`: every `<UnitTestResult … testName="…" … outcome="Failed">` element, HTML-unescaped and sorted.

## Frontend failing tests (3)

- `src/features/bank-accounts/bank-accounts-tab.test.tsx` › BankAccountsTab › adds a new account via the form
- `src/hooks/useNotificationHub.test.ts` › useNotificationHub › invalidates unread-count query on NewNotification
- `src/pages/payment-qr/payment-qr-page.test.tsx` › PaymentQrPage › generates and displays a QR after submitting valid data

The second one matters to Phase 05: the hook invalidates `['notifications']` while the test expects `['notifications', 'unread-count']`. Phase 05 changes that test file for the new handlers and leaves this assertion and the hook's behaviour as they are, so the failure stays in the baseline.

## Lint, typecheck and build

- `npm run lint`: 17 problems = 3 errors, 14 warnings.
- `npm run typecheck`: exit 0.
- `npm run build`: exit 0.

## Notes

- The full backend suite takes about 21 minutes on this machine.
- The repository tracks five old `TestResults/*.trx` files. Write new trx files outside the repository (`--results-directory "$TEMP/qlbh-dash-results"`).
