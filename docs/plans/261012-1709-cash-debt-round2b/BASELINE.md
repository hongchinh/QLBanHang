# Round 2B — Test baseline

> Recorded: 2026-10-12 · Commit: `fa2fd87` on `feat/cash-debt-round2b` (plan commit on top of `af562f0`, PR #2 merged) · No code differs from the end of Round 2A.

Later phases run the same commands and compare **test by test** with the lists below. Backend count: 790 tests, 771 passed, 19 failed (the same 19 as at the end of 2A, `6c0be04`). Frontend: 714 tests, 711 passed, 3 failed. Lint: 3 errors / 14 warnings. Typecheck: clean.

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

List command (from `backend/`, after a run with `--logger "trx;LogFileName=<name>.trx"`): a Python regex over `<UnitTestResult … testName="…" … outcome="Failed">` in `tests/OrderMgmt.IntegrationTests/TestResults/<name>.trx`, HTML-unescaped and sorted.

## Frontend failing tests (3)

- `src/features/bank-accounts/bank-accounts-tab.test.tsx` › BankAccountsTab › adds a new account via the form
- `src/hooks/useNotificationHub.test.ts` › useNotificationHub › invalidates unread-count query on NewNotification
- `src/pages/payment-qr/payment-qr-page.test.tsx` › PaymentQrPage › generates and displays a QR after submitting valid data

## Lint and typecheck

- `npm run lint`: 17 problems = 3 errors, 14 warnings.
- `npm run typecheck`: exit 0.

## Difference from the end of 2A

None: the same 19 backend failures, 3 frontend failures and lint counts. The backend suite is larger (790 vs 717 tests) because of the Phase A/B review fixes and the refactor phase merged before this branch.
