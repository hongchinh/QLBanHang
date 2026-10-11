# Directory Structure

## Repository Layout

```
QLDonHang/
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
| `OrderMgmt.WebApi` | Controllers, middleware, authorization policies, current-user adapter and startup configuration |
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
| Dashboard | `Application/Sales/Quotations/Services`, `DashboardController.cs` |
| Reports | `Application/Reports/SalesRevenue`, `ReportsController.cs` |
| Search | `Application/Search`, `SearchController.cs` |
| Branding | `Domain/Branding`, `Application/Branding`, `SettingsController.cs` |
| Notifications | `Domain/Notifications`, `Application/Notifications`, `NotificationsController.cs` |
| Payments / VietQR | `Domain/Entities/Payments`, `Application/Payments`, `BanksController.cs`, `PaymentQrController.cs`, `MeBankAccountsController.cs` |
| Branches, period lock and opening date | `Domain/Entities/Organization` (`Branch.OpeningDate`, `LockedUntil`), `Application/Organization/Branches` (`PeriodLockGuard` shared by Round 2A code), `ICurrentBranch` (`WebApi/Services/CurrentBranch.cs`), `BranchesController.cs`, `MeBranchesController.cs` |
| Suppliers (partner roles) | `Application/Catalog/Customers` (`PartnerRole`, `PartnerPermissionGuard`), `SuppliersController.cs` |
| Inventory catalogs | `Domain/Entities/Inventory`, `Application/Inventory/{Warehouses,StockReasons,PaymentMethods}` |
| Inventory settings and numbering | `Application/Inventory/{Settings,Numbering}` (`IDocumentCodeAllocator` issues the codes of stock and cash documents), `InventorySettingsController.cs` |
| Stock vouchers | `Application/Inventory/StockVouchers`, `StockVouchersController.cs` |
| Ledger, costing and posting engine | `Application/Inventory/{Common,Ledger,Costing,Posting,Interfaces}`, `Infrastructure/Inventory` (advisory locks, document counter), `Infrastructure/Persistence/EfTransactionRunner.cs` |
| Opening stock | `Application/Inventory/OpeningStocks`, `OpeningStocksController.cs` |
| Cost recalculation | `Application/Inventory/Costing` (`InventoryRecalcService`), `InventoryCostController.cs` |
| Inventory reports | `Application/Inventory/Reports`, `InventoryReportsController.cs` |
| Cash & debt domain (Round 2A) | `Domain/Entities/CashDebt` (`CashReason`, `Fund`, `CashVoucher`/`CashVoucherLine`, `FundTransfer`, `DebtEntry`, `DebtSettlement`, `DebtOffsetVoucher`, `OpeningDebt`, `CreditLimit`, `CashDocumentActivity`, `CashDebtSettings`), `Domain/Enums/CashDebtEnums.cs`, `Infrastructure/Persistence/Configurations/CashDebtConfiguration.cs` |
| Cash & debt shared engine | `Application/CashDebt/Common` (`DebtEffectRules`, `DebtSettlementMath`, `AllocationValidator`, `RunningBalance`, `DebtBalanceSign`, `LimitPolicyDecision`), `Application/CashDebt/Ledger` (`IDebtLedgerService`, `IDebtQueryService`, `IDebtActivityLog`), `Application/CashDebt/Guards` (`CreditLimitGuard`, `FundBalanceGuard`, `FundMovementQuery`), `Application/CashDebt/Interfaces/ICashDebtLock.cs`, `Infrastructure/CashDebt/PostgresCashDebtLock.cs` |
| Cash settings and catalogs | `Application/CashDebt/{Settings,CashReasons,Funds,CreditLimits}`, `CashSettingsController.cs`, `CashReasonsController.cs`, `FundsController.cs`, `CreditLimitsController.cs` |
| Receipts/payments and fund transfers | `Application/CashDebt/{CashVouchers,FundTransfers}`, `CashVouchersController.cs`, `FundTransfersController.cs` |
| Automatic receipt/payment of stock vouchers | `Application/CashDebt/AutoVouchers` (`IAutoCashVoucherService`), called from `StockVoucherService` and from every path that removes settlements |
| Opening debts, matching and offsets | `Application/CashDebt/{OpeningDebts,Debt,DebtOffsets}`, `OpeningDebtsController.cs`, `DebtController.cs`, `DebtOffsetsController.cs` |
| Cash & debt reports | `Application/CashDebt/Reports`, `CashDebtReportsController.cs` |

## API Controllers

| Controller | Main Routes |
| ---------- | ----------- |
| `AuthController` | `/api/auth/login`, `/refresh`, `/logout`, `/me` |
| `CustomersController` | `/api/customers`, `/api/customers/search` |
| `ProductsController` | `/api/products`, `/api/products/search` |
| `QuotationsController` | `/api/quotations`, status transition, Excel/PDF export, clone, owner transfer |
| `DashboardController` | `/api/dashboard/summary`, revenue series, top lists, recent activity, sales leaderboard |
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
| `BranchesController` | `/api/branches` CRUD, `/api/branches/{id}/lock` (period lock), `/api/branches/{id}/opening-date` |
| `MeBranchesController` | `/api/me/branches` — default/working branch and switchable branches |
| `WarehousesController` | `/api/warehouses` CRUD, scoped to the working branch |
| `StockReasonsController` | `/api/stock-reasons` CRUD |
| `PaymentMethodsController` | `/api/payment-methods` CRUD |
| `SuppliersController` | `/api/suppliers` CRUD and `/search` (partners with the supplier role) |
| `InventorySettingsController` | `/api/inventory/settings`, `/api/inventory/numbering`, `/api/inventory/numbering/{docType}` (stock types need `inventory.settings`, the cash types `Receipt`/`Payment`/`FundTransfer`/`DebtOffset` need `cash.settings`; checked in the service) |
| `StockVouchersController` | `/api/stock-vouchers` list/CRUD, `/{id}/cancel`, `/{id}/restore`, `/{id}/activities`, `/owners`, `/defaults`, `/stock-at`, `/partners` |
| `OpeningStocksController` | `/api/inventory/opening-stock` (GET grid, PUT save) |
| `InventoryCostController` | `/api/inventory/recalc-cost` |
| `InventoryReportsController` | `/api/reports/stock-on-hand`, `/api/reports/stock-card` |
| `CashSettingsController` | `/api/cash/settings` (credit-limit and negative-fund policies) |
| `CashReasonsController` | `/api/cash-reasons` CRUD (`includeAuto` shows the automatic reasons) |
| `FundsController` | `/api/funds` CRUD, `/{id}/opening-balance`, scoped to the working branch |
| `CreditLimitsController` | `/api/credit-limits` list, `PUT`/`DELETE /{customerId}` |
| `OpeningDebtsController` | `/api/opening-debts` list/CRUD |
| `CashVouchersController` | `/api/cash-vouchers` list/CRUD, `/{id}/cancel`, `/{id}/restore`, `/{id}/activities`, `/defaults`, `/partners` |
| `FundTransfersController` | `/api/fund-transfers` list/CRUD, `/{id}/cancel`, `/{id}/restore`, `/{id}/activities`, `/defaults` |
| `DebtController` | `/api/debt/partner-summary`, `/open-items`, `/partners`, `/settlements` (GET, POST), `DELETE /settlements/{id}`, `DELETE /settlement-batches/{batchId}` |
| `DebtOffsetsController` | `/api/debt-offsets` list/create/view, `/{id}/cancel`, `DELETE /{id}`, `/{id}/activities`, `/defaults` |
| `CashDebtReportsController` | `/api/reports/debt-balance`, `/api/reports/debt-ledger`, `/api/reports/cash-book` |

## Frontend Structure

```
frontend/src/
├── components/
│   ├── auth/                    # permission-aware UI helpers
│   ├── confirm-required-dialog/ # dialog for confirmable/blocked 422 responses
│   ├── customer-autocomplete/   # quotation customer picker + quick add
│   ├── layout/                  # app shell, header, sidebar, search, notifications
│   ├── partner-autocomplete/    # customer/supplier picker for cash and debt screens
│   └── ui/                      # shadcn-style primitives
├── features/
│   ├── admin-roles/
│   ├── admin-user-settings/
│   ├── admin-users/
│   ├── auth/
│   ├── bank-accounts/           # saved receiving bank accounts (CRUD, current user)
│   ├── banks/                   # read-only seeded bank list
│   ├── branches/                # branches, /me/branches, useBranchContext, useSwitchWorkingBranch
│   ├── branding/
│   ├── cash-common/             # ['cash'] root key, shared cash/debt types and labels
│   ├── cash-reasons/
│   ├── cash-reports/            # debt balance, debt ledger and cash book queries
│   ├── cash-settings/           # credit-limit and negative-fund policies
│   ├── cash-vouchers/           # receipts/payments, allocation helpers, schema, payload
│   ├── credit-limits/
│   ├── customers/
│   ├── dashboard/
│   ├── debt/                    # partner summary, open items, matching helpers, settlements
│   ├── debt-offsets/
│   ├── fund-transfers/
│   ├── funds/
│   ├── inventory-reports/       # stock-on-hand and stock-card queries
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
│   └── warehouses/              # warehouse queries, selectableWarehouses
├── pages/
│   ├── admin/
│   ├── cash-reasons/
│   ├── cash-reports/            # /reports/debt-balance, /reports/debt-ledger, /reports/cash-book
│   ├── cash-vouchers/           # /receipts and /payments list + form, allocation grid
│   ├── credit-limits/
│   ├── customers/
│   ├── debt/                    # /debt/opening, /debt/matching, /debt/offsets list + form
│   ├── fund-transfers/          # /fund-transfers list + form
│   ├── funds/                   # funds and fund opening balance
│   ├── inventory/               # opening stock, stock-on-hand, stock card
│   ├── payment-methods/
│   ├── payment-qr/              # /qr-thanh-toan standalone page
│   ├── products/
│   ├── quotations/
│   ├── reports/
│   ├── settings/                # also branches (period lock, opening date), inventory and cash settings, numbering, recalc
│   ├── stock-reasons/
│   ├── stock-vouchers/          # /stock-in and /stock-out list + form, customer debt panel
│   ├── suppliers/
│   └── warehouses/
├── routes/
├── stores/
├── lib/
├── styles/
└── test/
```

Feature folders generally contain `api.ts`, `hooks.ts`, `types.ts`, optional `schema.ts` and `keys.ts`. Pages compose those hooks with reusable UI components.

## Route Entry Points

| Concern | File |
| ------- | ---- |
| Backend startup | [../../backend/src/OrderMgmt.WebApi/Program.cs](../../backend/src/OrderMgmt.WebApi/Program.cs) |
| Backend configuration | [../../backend/src/OrderMgmt.WebApi/appsettings.json](../../backend/src/OrderMgmt.WebApi/appsettings.json) |
| Development backend config | [../../backend/src/OrderMgmt.WebApi/appsettings.Development.json](../../backend/src/OrderMgmt.WebApi/appsettings.Development.json) |
| EF DbContext | [../../backend/src/OrderMgmt.Infrastructure/Persistence/AppDbContext.cs](../../backend/src/OrderMgmt.Infrastructure/Persistence/AppDbContext.cs) |
| Seed | [../../backend/src/OrderMgmt.Infrastructure/Persistence/Seed/DbSeeder.cs](../../backend/src/OrderMgmt.Infrastructure/Persistence/Seed/DbSeeder.cs) |
| Frontend startup | [../../frontend/src/main.tsx](../../frontend/src/main.tsx) |
| Frontend routes | [../../frontend/src/App.tsx](../../frontend/src/App.tsx) |
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
| `QuotationExport__LibreOfficePath` | Optional explicit LibreOffice executable path for PDF conversion |
| `VITE_API_BASE_URL` | Frontend build-time backend API URL |
