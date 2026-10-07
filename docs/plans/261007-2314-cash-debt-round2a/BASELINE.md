# Round 2A test baseline

- Recorded: 2026-10-08 04:33:42
- Commit: 9faac7a (branch `feat/cash-debt-round2a`)
- Test DB: `qldonhang_integtest`

## Backend — 19 failed / 413 passed / 432 total

Same 19 as Round 1.

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

## Frontend tests — 3 failed / 457 passed / 460 total

- `src/hooks/useNotificationHub.test.ts > useNotificationHub > invalidates unread-count query on NewNotification`
- `src/features/bank-accounts/bank-accounts-tab.test.tsx > BankAccountsTab > adds a new account via the form`
- `src/pages/payment-qr/payment-qr-page.test.tsx > PaymentQrPage > generates and displays a QR after submitting valid data`

## Frontend lint — 3 errors, 14 warnings

- `src/components/ui/table.tsx:75` jsx-a11y/no-static-element-interactions
- `src/features/customers/components/customer-catalog-list.tsx:58` jsx-a11y/no-autofocus
- `src/features/products/components/product-catalog-list.tsx:51` jsx-a11y/no-autofocus
