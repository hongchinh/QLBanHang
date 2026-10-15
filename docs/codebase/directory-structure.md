# Directory Structure

## Repository Layout

```
QLBanHang/
├── backend/
│   ├── OrderMgmt.sln
│   ├── Directory.Build.props
│   ├── .editorconfig
│   ├── src/
│   │   ├── OrderMgmt.Domain/
│   │   ├── OrderMgmt.Application/
│   │   ├── OrderMgmt.Infrastructure/
│   │   └── OrderMgmt.WebApi/
│   ├── tests/OrderMgmt.IntegrationTests/
│   └── Dockerfile
├── frontend/
│   ├── src/
│   ├── package.json
│   ├── vite.config.ts
│   └── Dockerfile
├── docs/
├── docker-compose.yml
├── AGENTS.md
└── README.md
```

## Backend Projects

| Project | Responsibility |
| ------- | -------------- |
| `OrderMgmt.Domain` | Entities, enums, permissions, role codes and domain exceptions |
| `OrderMgmt.Application` | Use-case services, DTOs, validators, Mapster registrations and application ports |
| `OrderMgmt.Infrastructure` | EF Core persistence, migrations, seed, JWT/BCrypt/refresh-token implementations, Excel/PDF export and system services |
| `OrderMgmt.WebApi` | Controllers, middleware, authorization policies, current-user adapter, background jobs (`BackgroundJobs/`) and startup configuration |
| `OrderMgmt.IntegrationTests` | xUnit integration tests using `WebApplicationFactory` and Testcontainers PostgreSQL |

## Backend Modules

| Area | Key Paths |
| ---- | --------- |
| Catalog customers | `Application/Catalog/Customers`, `Domain/Entities/Catalog/Customer.cs`, `CustomersController.cs` |
| Catalog products | `Application/Catalog/Products`, `Domain/Entities/Catalog/Product.cs`, `ProductsController.cs` |
| Lookups | `Application/Catalog/Lookups`, `LookupsController.cs` |
| Auth | `Application/Identity`, `Infrastructure/Identity`, `AuthController.cs` |
| Admin users | `Application/Identity/Admin`, `AdminUsersController.cs` |
| Roles/permissions | `Application/Identity/Admin`, `AdminRolesController.cs`, `Domain/Constants/Permissions.cs` |
| User quotation settings | `Application/Identity/UserSettings`, `AdminUserSettingsController.cs`, `MeQuotationSettingsController.cs` |
| Quotations | `Application/Sales/Quotations`, `Domain/Entities/Sales`, `QuotationsController.cs` |
| Overview dashboards | `Application/Sales/Quotations/Services` (`DashboardService`), `Application/Dashboards/Common` (`SalesRevenueSource`), `DashboardController.cs` |
| Reports | `Application/Reports/SalesRevenue`, `Application/Reports/QuotationRevenue` (`QuotationRevenueReportService`, `/api/reports/revenue/*`), `ReportsController.cs` |
| Search | `Application/Search`, `SearchController.cs` |
| Branding | `Domain/Branding`, `Application/Branding`, `SettingsController.cs` |
| Notifications | `Domain/Notifications`, `Application/Notifications`, `NotificationsController.cs` |
| Payments / VietQR | `Domain/Entities/Payments`, `Application/Payments`, `BanksController.cs`, `PaymentQrController.cs`, `MeBankAccountsController.cs` |
| Branches, period lock and opening date | `Domain/Entities/Organization` (`Branch.OpeningDate`, `LockedUntil`), `Application/Organization/Branches` (`PeriodLockGuard` shared by Round 2A code), `ICurrentBranch` (`WebApi/Services/CurrentBranch.cs`), `BranchesController.cs`, `MeBranchesController.cs` |
| Suppliers (partner roles) | `Application/Catalog/Customers` (`PartnerRole`, `PartnerPermissionGuard`), `SuppliersController.cs` |
| Inventory catalogs | `Domain/Entities/Inventory`, `Application/Inventory/{Warehouses,StockReasons,PaymentMethods}` |
| Inventory settings and numbering | `Application/Inventory/{Settings,Numbering}` (`IDocumentCodeAllocator` issues the codes of stock and cash documents), `InventorySettingsController.cs` |
| Stock vouchers | `Application/Inventory/StockVouchers` (`Services/StockVoucherCashDebt.cs` with `Interfaces/IStockVoucherCashDebt.cs`: the cash and debt steps of a save), `StockVouchersController.cs` |
| Ledger, costing and posting engine | `Application/Inventory/{Common,Ledger,Costing,Posting,Interfaces}`, `Infrastructure/Inventory` (advisory locks, document counter), `Infrastructure/Persistence/EfTransactionRunner.cs` |
| Opening stock | `Application/Inventory/OpeningStocks`, `OpeningStocksController.cs` |
| Cost recalculation | `Application/Inventory/Costing` (`InventoryRecalcService`), `InventoryCostController.cs` |
| Inventory reports | `Application/Inventory/Reports` (stock-on-hand, stock-card and the in–out–balance summary `stock-summary`), `InventoryReportsController.cs` |
| Cash & debt domain (Round 2A) | `Domain/Entities/CashDebt` (`CashReason`, `Fund`, `CashVoucher`/`CashVoucherLine`, `FundTransfer`, `DebtEntry`, `DebtSettlement`, `DebtOffsetVoucher`, `OpeningDebt`, `CreditLimit`, `CashDocumentActivity`, `CashDebtSettings`), `Domain/Enums/CashDebtEnums.cs`, `Infrastructure/Persistence/Configurations/CashDebtConfiguration.cs` |
| Cash & debt shared engine | `Application/CashDebt/Common` (`CashDocuments` shared by the three cash document services, `DebtEffectRules`, `DebtSettlementMath`, `AllocationValidator`, `RunningBalance`, `DebtBalanceSign`, `LimitPolicyDecision`), `Application/CashDebt/Ledger` (`IDebtLedgerService`, `IDebtQueryService`, `IDebtActivityLog`, `SettlementRemovalConfirmation` for the 422 token, `SettlementChangeCollector` for the removed settlements of a request), `Application/CashDebt/Guards` (`CreditLimitGuard`, `FundBalanceGuard`, `FundMovementQuery`), `Application/CashDebt/Interfaces/ICashDebtLock.cs`, `Infrastructure/CashDebt/PostgresCashDebtLock.cs` |
| Cash settings and catalogs | `Application/CashDebt/{Settings,CashReasons,Funds,CreditLimits}`, `CashSettingsController.cs`, `CashReasonsController.cs`, `FundsController.cs`, `CreditLimitsController.cs` |
| Receipts/payments and fund transfers | `Application/CashDebt/{CashVouchers,FundTransfers}`, `CashVouchersController.cs`, `FundTransfersController.cs` |
| Automatic receipt/payment of stock vouchers | `Application/CashDebt/AutoVouchers` (`IAutoCashVoucherService`; `AutoRecomputeHook` recomputes the `Auto` settlements before the commit of every request that removed settlements) |
| Payment QR (VietQR transfer QR on receipts and stock-outs) | `Application/CashDebt/PaymentQr` (`VoucherPaymentQrRules` with `PaymentQrKind`, `PaymentQrBlock` and `QrFundInfo`; `VoucherPaymentQrDto`; `IVoucherPaymentQrService` / `VoucherPaymentQrService`, the one resolver for the screen routes and the print), `Infrastructure/Qr/QrPngRenderer.cs` (QRCoder PNG, error correction M), the picture and the payment block in `Infrastructure/Excel/Tt133/Vouchers/VoucherFormBuilder.cs`, the `payment-qr` routes of `CashVouchersController.cs` and `StockVouchersController.cs`, `Fund.BankId` / `AccountHolderName` (migration `AddFundBankAccount`); the payload comes from `Application/Payments/Services/VietQrPayloadBuilder`; tests in `tests/OrderMgmt.IntegrationTests/CashDebt` (`VoucherPaymentQrTests`, `Unit/VoucherPaymentQrRulesTests`) and `Tt133/Unit/Vouchers` |
| Opening debts, matching and offsets | `Application/CashDebt/{OpeningDebts,Debt,DebtOffsets}`, `OpeningDebtsController.cs`, `DebtController.cs`, `DebtOffsetsController.cs` |
| Cross-feature application helpers | `Application/Common/LikePattern.cs` (ILike escaping), `Application/Common/References` (`IReferenceUsage`: every "is used" check of partners, payment methods, branches and funds), `Application/Common/Interfaces/IBeforeCommitHook.cs` (run by `EfTransactionRunner` before the outermost commit), `Application/Common/Interfaces/ISpreadsheetPdfConverter.cs` (xlsx → PDF, implemented by `Infrastructure/Excel/LibreOfficeSpreadsheetPdfConverter.cs`; shared by the quotation and TT133 exports), `Application/Common/VndAmountInWords.cs` (a VND amount in Vietnamese words), `Application/Common/ReportScope.cs` (`ReportScope` Branch / Company and `ReportScopeGuard`) |
| Cash & debt reports | `Application/CashDebt/Reports`, `CashDebtReportsController.cs` |
| Company profile | `Domain/Entities/Organization/CompanyProfile.cs` (one row, `Id = 1`), `Application/Organization/Company`, `Infrastructure/Persistence/Configurations/CompanyProfileConfiguration.cs`, `CompanyProfileController.cs` |
| TT133 report forms | `Application/Tt133` (`ITt133ExportService` / `Tt133ExportService`, `ITt133FormRenderer`, `Models/Tt133ExportModels.cs`, `Validators`, `Tt133ExportOptions`), `Infrastructure/Excel/Tt133` (`Tt133FormBuilder`, `Tt133Column`, `Tt133FormRenderer` and one class per form: `CashBookForm`, `DebtLedgerForm`, `DebtSummaryForm`, `DebtReconciliationForm`, `StockSummaryForm`, `StockDetailForm`, `StockCardForm`), `Tt133ExportController.cs`. Voucher prints (01-TT, 02-TT, 01-VT, 02-VT): `Application/Tt133/Vouchers` (`IVoucherPrintService` / `VoucherPrintService`, `IVoucherFormRenderer`, `VoucherPrintModels.cs`, `VoucherPrintRules`), `Application/Tt133/Services/Tt133ExportSupport.cs` (context, sheet count, file conversion shared with the report exports), `Infrastructure/Excel/Tt133/Vouchers` (`VoucherFormBuilder`, `CashVoucherForm`, `StockVoucherForm`, `VoucherFormRenderer`) |
| Executive dashboard | `Application/Dashboards/Executive`: `Models` (`ExecutiveModels.cs` with the queries, then `SalesModels`, `CashModels`, `DebtModels`, `InventoryModels`, `OverviewModels`), `Common` (`ExecutiveScope`, `ExecutivePeriod`, `ExecutiveDay`, `ExecutiveMath`, `ExecutiveSeries`, `DebtPositionMath`, `OpenDebtItem`), `Interfaces` (`IExecutiveScopeResolver` and one interface per service), `Services` (`SalesMetricsService`, `CashMetricsService`, `DebtMetricsService`, `InventoryMetricsService`, `ExecutiveOverviewService`, `ExecutiveScopeResolver`, `ExecutiveLookups`), `Validators/ExecutiveQueryValidators.cs`, `Realtime/DashboardDomains.cs`; helpers shared with the inventory reports `Application/Inventory/Common/CostProvisional.cs` and `StockValueSplit.cs`; `Domain/Constants/StockReasonCodes.cs` (`XBH`, `NTL`); `ExecutiveDashboardController.cs`; tests in `tests/OrderMgmt.IntegrationTests/ExecutiveDashboard` (pure tests in `ExecutiveDashboard/Unit`, base class `ExecutiveDashboardTestBase`) |
| Demo branches and sample data | `Application/Organization/SampleData`: `Models` (`SampleDataJob`, `SampleDataRunResult`, `SampleDataPlan` with its specs and step records), `Interfaces` (`ISampleDataQueue`, `ISampleDataGenerator`, `ISampleDataRunner`, `IDemoBranchService`), `Services` (`SampleDataQueue`, `SampleDataRunner`, `SampleStepExecutor`, `SampleCatalogWriter`, `SampleRunState`, `SampleDataPlanException`, `DemoBranchService`), `Generation` (the pure `SampleDataGenerator` and its helpers: `SampleRandom`, `SampleCalendar`, `SampleCatalog` with `SampleCatalogModels` (payment habits, sales tiers, product plans, customer profiles), `SampleOpeningBalances`, `SampleDocumentSimulator` (the day loop and schedule, split into the partial files `SampleDocumentSimulator.Sales.cs`, `.Stock.cs` and `.Cash.cs`), `SampleLedger`, and the volume knobs `SampleDataVolumes`); `WebApi/BackgroundJobs/SampleDataHostedService.cs` (the worker); the acting identity `Application/Common/Interfaces/IActingIdentity.cs` with `Application/Common/ActingIdentityHolder.cs`, read by `WebApi/Services/CurrentUser.cs` and `CurrentBranch.cs`; `Application/Inventory/StockVouchers/Services/ProductCostPrices.cs` (cost price recompute shared with the stock voucher saves); the flags on `Branch`, `Product` and `Customer`; endpoints on `BranchesController.cs`; tests in `tests/OrderMgmt.IntegrationTests/SampleData` (pure tests in `SampleData/Unit`) |
| Real-time signals | `WebApi/Hubs` (`NotificationHub` at `/hubs/notifications` with the `user-{id}` and dashboard groups, `SignalRNotifier` implementing `Application/Notifications/Interfaces/IRealtimeNotifier`, `DashboardGroups.cs` with the event and group names), `WebApi/Filters/DashboardSignalAttribute.cs` (`[DashboardSignal]`, `[SkipDashboardSignal]`, `DashboardSignalFilter`) |

## API Controllers

| Controller | Main Routes |
| ---------- | ----------- |
| `AuthController` | `/api/auth/login`, `/refresh`, `/logout`, `/me` |
| `CustomersController` | `/api/customers`, `/api/customers/search` |
| `ProductsController` | `/api/products`, `/api/products/search` |
| `QuotationsController` | `/api/quotations`, status transition, Excel/PDF export, clone, owner transfer |
| `DashboardController` | `/api/dashboard/summary`, revenue series, top lists, recent activity, sales leaderboard |
| `ExecutiveDashboardController` | GET `/api/dashboard/executive/` `sales/summary`, `sales/series`, `sales/top`, `cash/summary`, `cash/series`, `cash/breakdown`, `debt/summary`, `debt/breakdown`, `inventory/summary`, `inventory/breakdown`, `branches`, `attention`, `recent` |
| `ReportsController` | `/api/reports/sales-revenue` |
| `AdminUsersController` | `/api/admin/users` CRUD, reset password and status updates |
| `AdminRolesController` | `/api/admin/roles`, `/api/admin/permissions` |
| `AdminUserSettingsController` | `/api/admin/user-settings/{userId}`, bulk quotation transfer |
| `MeQuotationSettingsController` | `/api/me/quotation-settings` and personal template upload/download/delete |
| `SettingsController` | `/api/settings/branding`, logo read/update |
| `NotificationsController` | `/api/notifications`, unread count, read actions |
| `SearchController` | `/api/search/global` |
| `LookupsController` | `/api/lookups/product-groups`, `/api/lookups/units` |
| `BanksController` | `/api/banks` — seeded Vietnamese bank list |
| `PaymentQrController` | `/api/payment-qr/generate` — builds a VietQR payload string |
| `MeBankAccountsController` | `/api/me/bank-accounts` CRUD + `/default`, scoped to the current user |
| `BranchesController` | `/api/branches` CRUD (POST takes `withSampleData`), `/api/branches/{id}/lock` (period lock), `/api/branches/{id}/opening-date`, GET `/api/branches/{id}/demo-deletion` (preview), DELETE `/api/branches/{id}/demo` (demo branch hard delete) |
| `MeBranchesController` | `/api/me/branches` — default/working branch and switchable branches |
| `WarehousesController` | `/api/warehouses` CRUD, scoped to the working branch |
| `StockReasonsController` | `/api/stock-reasons` CRUD |
| `PaymentMethodsController` | `/api/payment-methods` CRUD |
| `SuppliersController` | `/api/suppliers` CRUD and `/search` (partners with the supplier role) |
| `InventorySettingsController` | `/api/inventory/settings`, `/api/inventory/numbering`, `/api/inventory/numbering/{docType}` (stock types need `inventory.settings`, the cash types `Receipt`/`Payment`/`FundTransfer`/`DebtOffset` need `cash.settings`; checked in the service) |
| `StockVouchersController` | `/api/stock-vouchers` list/CRUD, `/{id}/cancel`, `/{id}/restore`, `/{id}/activities`, `/owners`, `/defaults`, `/stock-at`, `/partners`, GET `/{id}/print` and POST `/print` (Excel/PDF of 01-VT/02-VT, `?format=xlsx\|pdf&priceMode=Voucher\|Cost\|None&includeQr=false\|true`; `includeQr` is a field of the POST body), GET `/{id}/payment-qr` (transfer QR data of a stock-out; 422 `PAYMENT_QR_UNAVAILABLE`) |
| `OpeningStocksController` | `/api/inventory/opening-stock` (GET grid, PUT save) |
| `InventoryCostController` | `/api/inventory/recalc-cost` |
| `InventoryReportsController` | `/api/reports/stock-on-hand`, `/api/reports/stock-card`, `/api/reports/stock-summary` |
| `CashSettingsController` | `/api/cash/settings` (credit-limit and negative-fund policies) |
| `CashReasonsController` | `/api/cash-reasons` CRUD (`includeAuto` shows the automatic reasons) |
| `FundsController` | `/api/funds` CRUD, `/{id}/opening-balance`, scoped to the working branch; a bank fund also takes `bankId` (an active `Banks` row) and `accountHolderName` for the transfer QR, and `FundDto` returns `bankShortName` |
| `CreditLimitsController` | `/api/credit-limits` list, `PUT`/`DELETE /{customerId}` |
| `OpeningDebtsController` | `/api/opening-debts` list/CRUD |
| `CashVouchersController` | `/api/cash-vouchers` list/CRUD, `/{id}/cancel`, `/{id}/restore`, `/{id}/activities`, `/defaults`, `/partners`, GET `/{id}/print` and POST `/print` (Excel/PDF of 01-TT/02-TT, `?format=xlsx\|pdf&includeQr=false\|true`; `includeQr` is a field of the POST body), GET `/{id}/payment-qr` (transfer QR data of a receipt; 422 `PAYMENT_QR_UNAVAILABLE`) |
| `FundTransfersController` | `/api/fund-transfers` list/CRUD, `/{id}/cancel`, `/{id}/restore`, `/{id}/activities`, `/defaults` |
| `DebtController` | `/api/debt/partner-summary`, `/open-items`, `/partners`, `/settlements` (GET, POST), `DELETE /settlements/{id}`, `DELETE /settlement-batches/{batchId}` |
| `DebtOffsetsController` | `/api/debt-offsets` list/create/view, `/{id}/cancel`, `DELETE /{id}`, `/{id}/activities`, `/defaults` |
| `CashDebtReportsController` | `/api/reports/debt-balance`, `/api/reports/debt-ledger`, `/api/reports/debt-reconciliation`, `/api/reports/cash-book` |
| `CompanyProfileController` | `/api/settings/company` — GET for any signed-in user, PUT with `settings.company` |
| `Tt133ExportController` | TT133 forms as Excel/PDF (`?format=xlsx\|pdf`): GET `/api/reports/cash-book/export`, `/debt-balance/export`, `/debt-reconciliation/export`, `/stock-summary/export`; POST `/api/reports/debt-ledger/export`, `/stock-card/export`, `/stock-detail/export` |

## Frontend Structure

```
frontend/src/
├── app-routes.tsx               # the route tree (appRoutes), rendered by App.tsx
├── components/
│   ├── auth/                    # permission-aware UI helpers
│   ├── confirm-required-dialog/ # dialog for confirmable/blocked 422 responses
│   ├── activity-history/        # ActivityHistory: activity list of a document
│   ├── help/                    # HelpPanel (+ help-doc-view.tsx), HelpButton, FirstVisitHint
│   ├── icons/                   # icon-registry.ts (business name → lucide drawing + tone, the only lucide-react importer), AppIcon, and the tests that block direct lucide imports and glyph-character icons
│   ├── layout/                  # app shell, header (header/header-branch-switcher.tsx marks a demo branch "(demo)"),
│   │                            # sidebar, search, notifications
│   │   ├── nav-config.ts        # sidebar groups/items, permissions, icons, sections
│   │   └── sidebar/             # sidebar (wide), sidebar-rail + sidebar-flyout (collapsed), sidebar-search,
│   │                            # sidebar-pin-chips, sidebar-group, sidebar-item, nav-search.ts (pure filter/highlight/active item)
│   ├── money-cell/              # MoneyCell, CellError: editable money cell of the line and allocation grids
│   ├── partner-autocomplete/    # the shared partner picker: partner-autocomplete.tsx (code combobox: rows, Enter rules, F4),
│   │                            # partner-code-name-field.tsx (label + code + name; owns the dialogs; vouchers and quotations),
│   │                            # partner-quick-add-dialog.tsx, partner-catalog-dialog.tsx + partner-catalog-list.tsx,
│   │                            # stop-form-events.tsx, use-unpicked-partner-guard.ts, search-summary.ts
│   ├── payment-qr/              # voucher transfer QR: PaymentQrButton, PaymentQrDialog (dialog from 640 px, bottom sheet below),
│   │                            # qr-image.ts (the PNG to download), qr-actions.ts (copyText, shareOrDownload), use-min-width.ts
│   ├── period-preset-select/    # PeriodPresetSelect: month / quarter / year presets of a report date range
│   ├── report-scope-select/     # ReportScopeSelect and useReportScope: Branch / Company scope of a report, kept in the URL
│   ├── search-combobox/         # generic search combobox behind DebtPartnerPicker and the ledger PartnerPicker
│   ├── tt133-export/            # Tt133ExportButton ("Xuất theo mẫu TT133"), ExportTargetsDialog (one / several / all with activity),
│   │                            # company-profile-hint.tsx ("Chưa có thông tin đơn vị", also used by the voucher print button)
│   ├── ui/                      # shadcn-style primitives (includes selection-checkbox.tsx: select-all with an indeterminate state)
│   └── voucher-print/           # VoucherPrintButton (menu "In (PDF)" / "Excel", plus "Kèm QR chuyển khoản" on receipts), StockPriceModeDialog (Giá trên phiếu / Giá vốn / Không in tiền, plus "Kèm QR chuyển khoản" for stock-outs)
├── features/
│   ├── admin-roles/
│   ├── admin-user-settings/
│   ├── admin-users/
│   ├── auth/
│   ├── bank-accounts/           # saved receiving bank accounts (CRUD, current user)
│   ├── banks/                   # read-only seeded bank list
│   ├── branches/                # branches, /me/branches, useBranchContext, useSwitchWorkingBranch; demo branches:
│   │                            # sample-data.ts (branchesRefetchInterval: poll while a branch is Running),
│   │                            # useDemoBranchDeletionPreview / useDeleteDemoBranch, keys.ts branchKeys.demoDeletion
│   ├── branding/
│   ├── cash-common/             # ['cash'] root key, shared cash/debt types and labels; reason-match.ts (required role per reason,
│   │                            # reason auto-match), reason-notice.ts + reason-notice-line.tsx (the line under the reason select),
│   │                            # use-reason-notice.ts (useReasonSwitchNotice: text and 1.6 s flash)
│   ├── cash-reasons/
│   ├── cash-reports/            # debt balance, debt ledger and cash book queries
│   ├── cash-settings/           # credit-limit and negative-fund policies
│   ├── cash-vouchers/           # receipts/payments, allocation helpers, schema, payload; auto-text.ts + use-cash-auto-text.ts
│   │                            # ("reason - partner" text of Lý do nộp/chi and the line descriptions)
│   ├── company-profile/         # Thông tin đơn vị: GET/PUT /settings/company
│   ├── credit-limits/
│   ├── customers/               # also partner-search-item.ts (customer record to search item), use-partner-permissions.ts
│   │                            # (create/view of customers and suppliers), use-quotation-customer-search.ts
│   ├── dashboard/               # overview dashboards (`/`, `/admin/dashboard`), `use-overview-live`, `revenue-subtitle`; format.ts (also compact money and percent) and the shared tiles
│   │                            # in components/: TileCard (+ TileError), StatTile, LiveStatus, RangePicker (optional
│   │                            # `presets`); stat-tile-state.ts (statTileState: the StatTile props that follow a query)
│   ├── debt/                    # partner summary, open items, matching helpers, settlements
│   ├── debt-offsets/
│   ├── executive-dashboard/     # types, api, keys (['executive']), hooks (executiveQueryOptions), presets, links, period,
│   │                            # series.ts (usePeriodGranularity, isSalesSeriesEmpty / isCashSeriesEmpty), labels.ts
│   │                            # (table texts: codeName, withBranch, formatAmount, formatCompactAmount),
│   │                            # use-executive-params / -scope / -access / -live
│   ├── fund-transfers/
│   ├── funds/
│   ├── help/                    # in-app user help: parse-help.ts (Markdown parser, stepHash), registry.ts (matchRoutes over the help files),
│   │                            # load-help.ts + content-index.ts (one lazy chunk), content/<key>.md (one file per route),
│   │                            # videos.json (written by the video tool), help-pending.ts (HELP_PENDING), stale.ts, guard tests
│   ├── inventory-reports/       # stock-on-hand, stock-card and stock-summary queries
│   ├── inventory-settings/      # costing settings, numbering, cost recalculation
│   ├── me-settings/
│   ├── notifications/
│   ├── opening-debts/
│   ├── opening-stock/
│   ├── payment-methods/
│   ├── payment-qr/              # VietQR payload generation
│   ├── products/
│   ├── quotations/
│   ├── reports/sales-revenue/
│   ├── search/
│   ├── stock-reasons/
│   ├── stock-vouchers/          # voucher module, schema, payload, draft store
│   ├── suppliers/
│   ├── tt133-export/            # downloadTt133, useTt133Download, Tt133ExportForm, tt133FileBase
│   ├── voucher-payment-qr/      # api, types, keys (under cashKeys.all), useVoucherPaymentQr (staleTime 0, gcTime 0)
│   ├── voucher-print/           # types, api, useVoucherPrint, price-mode-storage (last price mode per user),
│   │                            # qr-pref-storage (the "Kèm QR chuyển khoản" switch per user)
│   └── warehouses/              # warehouse queries, selectableWarehouses
├── pages/
│   ├── admin/
│   ├── cash-reasons/
│   ├── cash-reports/            # /reports/debt-balance, /reports/debt-ledger, /reports/cash-book
│   ├── cash-vouchers/           # /receipts and /payments list + form, allocation grid; utils/cash-navigation.ts (Enter order of the
│   │                            # header fields and the line grid, cashLineCellId)
│   ├── credit-limits/
│   ├── customers/
│   ├── debt/                    # /debt/opening, /debt/matching, /debt/offsets list + form
│   ├── executive-dashboard/     # /dashboard/executive: page shell; components/ (KPI row, charts, GranularityToggle and
│   │                            # SegmentedToggle, DataTable, fund table, debt term bar, branch table, BranchDocumentLink,
│   │                            # ScopeSelect, attention list, recent feed); tabs/ (overview, sales, cash, debt, inventory)
│   ├── fund-transfers/          # /fund-transfers list + form
│   ├── funds/                   # funds and fund opening balance
│   ├── inventory/               # opening stock, stock-on-hand, stock card, stock summary (/inventory/stock-summary, Tổng hợp nhập xuất tồn)
│   ├── payment-methods/
│   ├── payment-qr/              # /qr-thanh-toan standalone page
│   ├── products/
│   ├── quotations/
│   ├── reports/                 # reports/revenue: API and hooks of the Doanh thu report (`/api/reports/revenue/*`)
│   ├── settings/                # also branches (period lock, opening date), inventory and cash settings, numbering, recalc,
│   │                            # company profile (/settings/company, Thông tin đơn vị); branches-page.tsx ("Loại" column
│   │                            # with the Demo / progress / error badges), branch-form-dialog.tsx (the sample data
│   │                            # checkbox), demo-branch-delete-dialog.tsx (preview counts, type the code to confirm)
│   ├── stock-reasons/
│   ├── stock-vouchers/          # /stock-in and /stock-out list + form (main fields, tabs, line grid, totals bar, partner debt strip)
│   ├── suppliers/
│   └── warehouses/
├── hooks/                       # useDocumentActions (shared save flow of the four document forms), route access hooks
│                                # (useCanAccessRoute, useCanOpenLink for links with a query string), useNotificationHub
│                                # (the one SignalR connection: notifications, dashboard signal, connection status), use-dashboard-signal
│                                # (listens to the dashboard and quotation signals for the live overview and executive pages)
├── routes/
├── stores/
├── lib/                         # includes date-only.ts, to-number.ts, money-input.ts, vn-datetime.ts,
│                                # report-scope.ts, period-presets.ts, blob-error.ts, date-range-presets.ts (dashboard
│                                # and quotation range presets), realtime-bus.ts (hub status and dashboard signal),
│                                # blob-download.ts (requestBlob, saveBlob), pdf-tab.ts (openPdfTab), use-row-selection.ts,
│                                # enter-focus.ts (focusNextInOrder, focusById)
├── styles/
└── test/

frontend/scripts/
└── help-stale.ts                # npm run help:stale: lists help that may be out of date
```

Feature folders generally contain `api.ts`, `hooks.ts`, `types.ts`, optional `schema.ts` and `keys.ts` (every feature keeps its query keys there, `credit-limits`, `opening-debts` and `cash-settings` included). Pages compose those hooks with reusable UI components.

## Route Entry Points

| Concern | File |
| ------- | ---- |
| Backend startup | [../../backend/src/OrderMgmt.WebApi/Program.cs](../../backend/src/OrderMgmt.WebApi/Program.cs) |
| Backend configuration | [../../backend/src/OrderMgmt.WebApi/appsettings.json](../../backend/src/OrderMgmt.WebApi/appsettings.json) |
| Development backend config | [../../backend/src/OrderMgmt.WebApi/appsettings.Development.json](../../backend/src/OrderMgmt.WebApi/appsettings.Development.json) |
| EF DbContext | [../../backend/src/OrderMgmt.Infrastructure/Persistence/AppDbContext.cs](../../backend/src/OrderMgmt.Infrastructure/Persistence/AppDbContext.cs) |
| Seed | [../../backend/src/OrderMgmt.Infrastructure/Persistence/Seed/DbSeeder.cs](../../backend/src/OrderMgmt.Infrastructure/Persistence/Seed/DbSeeder.cs) |
| Frontend startup | [../../frontend/src/main.tsx](../../frontend/src/main.tsx) |
| Frontend routes | [../../frontend/src/app-routes.tsx](../../frontend/src/app-routes.tsx) (rendered by `App.tsx`) |
| API client | [../../frontend/src/lib/api-client.ts](../../frontend/src/lib/api-client.ts) |
| Auth store | [../../frontend/src/stores/auth-store.ts](../../frontend/src/stores/auth-store.ts) |

## Runtime Artifacts

| Path | Notes |
| ---- | ----- |
| `backend/src/OrderMgmt.WebApi/logs/qldh-{date}.log` | Serilog rolling file logs |
| `backend/src/OrderMgmt.WebApi/templates/template_baogia.xlsx` | Default quotation Excel template |
| `backend/src/OrderMgmt.WebApi/templates/templete_bbbg.xlsx` | Default handover Excel template with price |
| `backend/src/OrderMgmt.WebApi/templates/templete_bbbg_sl.xlsx` | Default handover Excel template without price |
| `backend/src/OrderMgmt.WebApi/bin/Debug/net9.0/templates/users/{userId}.xlsx` | Local Debug runtime location for per-user quotation templates when `QuotationExport:UserTemplatesPath` is the relative default `templates/users` |
| `backend/src/OrderMgmt.WebApi/bin/Debug/net9.0/templates/users/{userId}_handover_with_price.xlsx` | Local Debug runtime location for per-user handover templates with price |
| `backend/src/OrderMgmt.WebApi/bin/Debug/net9.0/templates/users/{userId}_handover_no_price.xlsx` | Local Debug runtime location for per-user handover templates without price |
| `backend/**/bin`, `backend/**/obj` | .NET build outputs |
| `frontend/dist` | Vite production build output |

Template paths configured as relative values are resolved from `AppContext.BaseDirectory`, not the repository root. In local Debug runs that base directory is normally `backend/src/OrderMgmt.WebApi/bin/Debug/net9.0/`. Use absolute paths in deployment if templates must live outside the application output directory.

## Required Production Configuration

| Key | Purpose |
| --- | ------- |
| `ConnectionStrings__Default` | PostgreSQL/Npgsql connection string |
| `Jwt__Secret` | Symmetric signing key, at least 32 characters |
| `Jwt__Issuer`, `Jwt__Audience` | JWT metadata |
| `Seed__AdminPassword` | Initial admin password when production seed should create admin |
| `Database__AutoMigrateAndSeed` | Enables automatic migration/seed; usually `false` outside controlled deployments |
| `Cors__Origins__0`, `Cors__Origins__1` | Allowed frontend origins |
| `AuthCookie__SameSite`, `AuthCookie__Secure` | Refresh cookie behavior |
| `QuotationExport__LibreOfficePath` | Optional explicit LibreOffice executable path for PDF conversion; the TT133 PDF exports use the same converter. The backend image sets it to `/usr/local/bin/soffice-vi`, a wrapper that runs `soffice` in the `vi_VN` locale so PDFs print `1.250.000` |
| `QuotationExport__ConversionTimeoutSeconds` | Optional; seconds one PDF conversion may take before its LibreOffice process is killed (130 in `appsettings.json`, set from the 300-sheet S06 measurement). Serves the quotation and the TT133 PDF exports |
| `Tt133Export__MaxSheets` | Optional; the most sheets one TT133 export may hold (default 300) |
| `VITE_API_BASE_URL` | Frontend build-time backend API URL |
