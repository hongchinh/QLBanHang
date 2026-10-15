# Baseline — voucher payment QR

Base commit: `606400c4` (plan commit on `qlbanhang/main` `1a877638`). Recorded 2026-10-15 by Phase 00.

## Backend (`dotnet test OrderMgmt.sln`, database base `qldonhang_integtest_vqr`)

Build: 0 errors, 9 warnings. Total 1632, passed 1613, **failed 19**, skipped 0 (39 m 9 s, run under load from parallel lanes).

Failing tests (compare by name in later phases):

- `OrderMgmt.IntegrationTests.Admin.AdminRolesCrudTests.Create_with_invalid_code_format_returns_400(badCode: "1ABC")`
- `OrderMgmt.IntegrationTests.Admin.AdminRolesCrudTests.Create_with_invalid_code_format_returns_400(badCode: "A")`
- `OrderMgmt.IntegrationTests.Admin.AdminRolesCrudTests.Create_with_invalid_code_format_returns_400(badCode: "sales")`
- `OrderMgmt.IntegrationTests.Admin.AdminRolesCrudTests.Create_with_reserved_system_code_returns_400(reservedCode: "Accountant")`
- `OrderMgmt.IntegrationTests.Admin.AdminRolesCrudTests.Create_with_reserved_system_code_returns_400(reservedCode: "SALES")`
- `OrderMgmt.IntegrationTests.Admin.AdminRolesCrudTests.Create_with_reserved_system_code_returns_400(reservedCode: "admin")`
- `OrderMgmt.IntegrationTests.AuthTests.Refresh_rotates_token_and_revokes_old_one`
- `OrderMgmt.IntegrationTests.Quotations.HandoverExportTests.HandoverNoPrice_Pdf_returns_pdf_via_fake_converter`
- `OrderMgmt.IntegrationTests.Quotations.HandoverExportTests.HandoverWithPrice_Pdf_returns_pdf_via_fake_converter`
- `OrderMgmt.IntegrationTests.Quotations.QuotationExportTests.Excel_AdvancePayment_WrittenToCorrectCells`
- `OrderMgmt.IntegrationTests.Quotations.QuotationExportTests.Pdf_returns_pdf_bytes_via_fake_converter`
- `OrderMgmt.IntegrationTests.Quotations.QuotationStateMachineTests.Cannot_accounting_confirm_from_draft`
- `OrderMgmt.IntegrationTests.Quotations.QuotationStateMachineTests.Cannot_accounting_confirm_from_sent`
- `OrderMgmt.IntegrationTests.Quotations.QuotationStateMachineTests.Cannot_confirm_directly_from_draft`
- `OrderMgmt.IntegrationTests.Quotations.QuotationStateMachineTests.Cannot_uncancel`
- `OrderMgmt.IntegrationTests.Quotations.QuotationStateMachineTests.Update_on_cancelled_returns_conflict`
- `OrderMgmt.IntegrationTests.Reports.RevenueLineItemsExportTests.Excel_DataRows_ReflectConfirmedQuotation`
- `OrderMgmt.IntegrationTests.Reports.RevenueLineItemsExportTests.Excel_HeaderRow_HasExpectedColumns`
- `OrderMgmt.IntegrationTests.Reports.SalesRevenueReportTests.Report_FiltersByConfirmedAt_NotQuotationDate`

## Frontend (`frontend/`)

- `npm run typecheck`: clean.
- `npm run lint`: 16 problems (3 errors, 13 warnings), all in files outside this plan.
- `npm run test -- --testTimeout=30000 --maxWorkers=3`: 226 files, 2199 tests; **3 failed**, 2196 passed. The captured output was cut off, so only one failing name was seen at first: `src/pages/payment-qr/payment-qr-page.test.tsx` › "generates and displays a QR after submitting valid data" (Radix option "Vietcombank" not found). The Phase 05 final run (234 files, 2296 tests, 3 failed) confirmed all three by name:
  - `useNotificationHub` › "invalidates unread-count query on NewNotification";
  - `bank-accounts-tab` › "adds a new account via the form";
  - `payment-qr-page` › "generates and displays a QR after submitting valid data".
- `npm run build`: succeeds (service worker generated).
