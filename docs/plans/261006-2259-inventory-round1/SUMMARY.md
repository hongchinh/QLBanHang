# Inventory Round 1 — Stock-in / Stock-out Vouchers (Đợt 1 — Kho)

> Created: 2026-10-06 22:59:36 · Source design: [brainstorm SUMMARY](../../brainstorms/261006-2139-stock-voucher-clone/SUMMARY.md), [section-03 Round 1 design](../../brainstorms/261006-2139-stock-voucher-clone/section-03-round1-design.md), [section-05 review decisions](../../brainstorms/261006-2139-stock-voucher-clone/section-05-review-decisions.md), [section-02 legacy bug decisions](../../brainstorms/261006-2139-stock-voucher-clone/section-02-legacy-bug-decisions.md)

## Goal

Deliver Round 1 of the legacy `PhieuNhapXuat` replacement: branches with a per-user working branch, warehouses, a shared partner catalog (customer/supplier roles), stock reasons, payment methods and inventory flags on products; opening stock; stock-in and stock-out vouchers (quotation-style list and form) that post to an inventory ledger with running quantities, a negative-stock policy, a per-branch period lock, transactional document numbering and periodic weighted-average costing that recalculates automatically after back-dated changes; and stock-on-hand and stock-card reports. All of this ships behind new permissions and is invisible to users who don't have them.

## Scope

- **In scope**
  - Backend: new permissions and a seeder upgrade path, `Branch` and `User.DefaultBranchId`, working branch via the `X-Branch-Id` header (`ICurrentBranch`), CRUD for branches / warehouses / stock reasons / payment methods, partner roles on `Customer` with a `/api/suppliers` API, inventory fields on `Product`, `InventorySettings`, `DocumentNumbering`, stock vouchers (create/update/cancel/restore/delete, defaults, stock-at, partner search), inventory ledger, `StockBalance`, `InventoryCostPeriod`, costing engine, opening stock, manual cost recalculation, settings-change recalculation, stock-on-hand and stock-card reports.
  - Frontend: permissions and route rules, a working-branch switcher, a "Kho" sidebar group, catalog screens (warehouses, stock reasons, payment methods, suppliers), customer/product form fields, settings screens (branches, period lock, numbering, inventory settings, cost recalculation), stock voucher list and form for both directions, the opening stock screen, and the stock-on-hand and stock-card reports.
  - Docs: `product-goals.md`, `system-architecture.md`, `directory-structure.md`, `conventions.md`.
- **Out of scope** (later rounds or backlog — see **Backlog**)
  - Round 2: cash vouchers, receivables/payables, credit limits, debt offset, automatic cash vouchers. `PaymentMethod.IsCash` is stored now and used then.
  - Round 3: print/Excel templates (01-VT/02-VT), Excel import of lines, voucher copy, `stock_in.export` / `stock_out.export` permissions, inventory report export; proposed: Excel import of opening stock and opening debt (go-live gate).
  - Backlog: FIFO, warehouse transfer, stocktake, returns, quotation → sales voucher.
  - Removed for good (section-02): group price lists, unit conversions, lots/expiry, barcodes, FTP transfer, legacy data migration.

## Decisions made while planning (in addition to the brainstorm)

| # | Topic | Decision |
|---|---|---|
| D1 | Supplier permissions | New `suppliers.view/create/update/delete`, separate from `customers.*` (user decision 2026-10-06). |
| D2 | Partner with both roles | Updating or deleting a partner requires the `update` / `delete` permission of **every** role it has, before and after the change. Turning a role flag on requires that role's permission. Without `suppliers.*`, a sales user cannot edit a partner that is also a supplier (user decision 2026-10-06). |
| D3 | Partner search on vouchers | A dedicated `GET /api/stock-vouchers/partners` endpoint gated by the voucher type's `view` permission. It filters by the reason's `PartnerType` (form), or returns any role when no reason is given (list filter). Warehouse staff do not need `customers.view` / `suppliers.view` to pick a partner. |
| D4 | Rules per `PartnerType` | `Customer` → partner required and must be a customer. `Supplier` → partner required and must be a supplier. `Any` → partner optional, any role. `None` → partner must be empty. |
| D5 | `export` permissions | `stock_in.export` / `stock_out.export` are deferred to Round 3, when print/Excel ships. Round 1 has nothing to export. |
| D6 | Inbound value storage | `StockVoucherLine.InboundValue` stores **Net′ + allocated freight (without VAT)**. The ledger `InValue` = `InboundValue + (PurchaseCostIncludesVat ? VatAmount : 0)`. This matches the section-03 formula at ledger level, and lets a `PurchaseCostIncludesVat` change re-derive the ledger without touching voucher rows. |
| D7 | Opening value of a costing run | Taken from the ledger itself: `Σ(QtyIn − QtyOut)` and `Σ(InValue − CostAmount)` before the period start, in scope. One code path. Works across scope/period changes. `InventoryCostPeriod` is the output snapshot and the source of the "previous AvgCost" fallback. |
| D8 | Zero closing quantity | When `ClosingQty = 0` **and** the period has at least one outbound row, the residual value is added to the last outbound row and `ClosingValue = 0`. With no outbound row in the period, `ClosingValue = Opening + In` is kept (only reachable with negative stock). |
| D9 | AvgCost precision | `AvgCost = round((OpeningValue + InValue) / (OpeningQty + InQty), 4)`. `CostAmount = round(QtyOut × AvgCost, 0)`. Money uses `MidpointRounding.AwayFromZero` at 0 decimals. Quantities are stored as `numeric(18,6)`. |
| D10 | Order-discount / freight remainder | The rounding remainder goes to the **last line (by SortOrder) whose base is > 0** — Net for order discount, Net′ of tracked lines for freight on stock-in. If that would leave any allocation negative (or, for order discount, above its line's Net), the split falls back to largest remainder: each line gets the floor of its share, and the units left go one by one to the lines with the largest fraction (ties: the later line first). Frontend preview uses the same algorithm. |
| D11 | Config change after data exists | Changing `CostingPeriod`, `CostingScope` or `PurchaseCostIncludesVat` recalculates every branch from the first period that is not fully locked. A `CostingPeriod` change is rejected (400) unless every branch's `LockedUntil` is empty or falls on a period end under the **new** period type. A `PurchaseCostIncludesVat` change re-derives `InValue` only for inbound rows dated after `LockedUntil`, and is accepted only when every such `LockedUntil` is empty or a period end (D33). |
| D12 | Default negative-stock policy | `Warn`. |
| D13 | Numbering tokens | `{KH}` = prefix, `{STT}` = counter zero-padded to `Length`, `{THANG}` = `MM`, `{NAM}` = `yyyy`. The pattern must contain `{STT}`. Defaults: `{KH}{STT}`, `PN`/`PX`, length 5, reset `None`. The counter period comes from the voucher's VN date. |
| D14 | Time zone | `VoucherAt` is stored as `timestamptz`. Every date rule (period lock, costing period, numbering period, opening-stock time = 00:00) uses the fixed Vietnam offset UTC+07:00 (`VnTime`). The frontend assumes the browser runs in VN time. |
| D15 | Main branch | Seeded by migration with a fixed id `BranchDefaults.MainBranchId`. `User.DefaultBranchId` defaults to it, so existing users and existing test helpers keep working. |
| D16 | Concurrency token | `StockVoucher.Version` is mapped to PostgreSQL `xmin`. Update/cancel/restore/delete must send `version`. A mismatch returns 409 (`CONCURRENCY`). |
| D17 | Negative-stock response | HTTP 422 with code `NEGATIVE_STOCK_WARNING` (Warn, not acknowledged) or `NEGATIVE_STOCK_BLOCKED` (Block). `details` is keyed `"{productCode}@{warehouseCode}"`, and each value lists the shortage message. The client resends with `acknowledgeNegativeStock: true` after the user confirms a warning. |
| D18 | Period-lock response | HTTP 400 with code `PERIOD_LOCKED`. |
| D19 | `Product.CostPrice` | **Superseded by D35** (recompute from the latest Active stock-in line after every stock-in change). Kept rule: `CostPrice` is the line's `UnitPrice` (before discount/VAT); if a product appears on several lines of the winning voucher, the last line by SortOrder wins. |
| D20 | Opening stock ledger source | `SourceType = Opening`, `SourceId = WarehouseId`, `SourceLineId = OpeningStock.Id`, `PostedAt` = 00:00 VN on the opening date. The grid has one opening date per warehouse. |
| D21 | `StockBalance` use | Maintained on every ledger change. Read by the stock-on-hand report when no `at` is given; the `at` variant sums the ledger. |
| D22 | System seeds | Reasons: `NMH` Nhập mua hàng (In, Supplier), `NKH` Nhập khác (In, Any), `XBH` Xuất bán hàng (Out, Customer), `XKH` Xuất khác (Out, Any) — all `IsSystem`. Payment methods: `TM` Tiền mặt (`IsCash`), `CK` Chuyển khoản. Warehouse `KHO01` "Kho chính" in the main branch. |
| D23 | Default role grants | ADMIN and MANAGER get every new permission. WAREHOUSE gets `stock_in.view/create/edit/delete/cancel`, `stock_out.view/create/edit/delete/cancel`, `inventory.opening_stock`, `reports.inventory` and `suppliers.view`. A new seeder step grants newly introduced codes to existing system roles whose defaults include them. That step runs once per code, so later admin removals survive restarts. Inserting the permission rows and granting them run in one transaction, so the "new codes" signal cannot be lost (Phase 01 Task 1.1). Production exposure: D39. |
| D24 | Stock unit name snapshot | `PerUnit` → `Product.Unit.Name`; `PerLinearMeter` → `m`; `PerSquareMeter` → `m²`; `PerCubicMeter` → `m³`. |
| D25 | Test database (user decision 2026-10-06) | Integration tests run against local PostgreSQL via `TEST_DB_CONNECTION=Host=localhost;Port=5432;Database=qldonhang_integtest;Username=postgres;Password=1`. Phase 00 replaces the per-test drop/migrate/seed with a template database migrated once per run and cloned per test. The fixture refuses the dev databases `qldonhang_test` / `qldonhang`. |
| D26 | Test depth (user decision 2026-10-06) | Full coverage for the engine: calculators, ledger/running quantity, costing, negative-stock policy, period lock, numbering, concurrency and invariants (backend), and the preview calculator, line grid and voucher form (frontend). Catalog CRUD, settings and simple screens get merged **smoke-level** tests: one happy path, plus permission or business-rule guards where a rule exists. |

### Decisions added by the tech-lead review (2026-10-07, see `PLAN-REVIEW.md`)

| # | Topic | Decision |
|---|---|---|
| D27 | UTC instants (review B1) | Every `DateTimeOffset` written to the database or used as a query parameter has offset 0 (Npgsql ≥ 6 rejects other offsets for `timestamptz`; the project does not enable legacy timestamp behaviour). `VnTime.StartOfDay/StartOfNextDay` return UTC instants. Services normalize request values (`VoucherAt`, `StockAtRequest.At`, report `at`, defaults `voucherAt`) with `.ToUniversalTime()`, and `IInventoryLedgerService` normalizes `PostedAt`. VN-date rules inside EF queries are written as UTC ranges `[VnTime.StartOfDay(d), VnTime.StartOfNextDay(d))`, because EF cannot translate `VnTime.ToVnDate`. Defaults use `IDateTime.UtcNow`, never `Now`. |
| D28 | Bool columns with defaults (review B2) | Never configure `HasDefaultValue(true)` on a non-nullable `bool` without `.HasSentinel(true)`: EF treats `false` (the CLR default) as "not set" and the database default would win, so `false` could never be inserted. `Customer.IsCustomer`, `Product.TrackInventory` and `Warehouse.IsActive` use `.HasDefaultValue(true).HasSentinel(true)`: existing rows get `true` from the migration and inserts always send the value. |
| D29 | Optimistic concurrency end to end (review M1, M2) | Update, cancel, restore and delete always mark the voucher header modified (set `UpdatedAt` / `UpdatedBy`) before the first `SaveChangesAsync`, so `UPDATE … WHERE xmin = @version` runs on every write even when only lines change. The frontend keeps `version` inside the form values. On 409 it refetches and resets the form to the server data, telling the user that local edits were discarded. A background refetch resets the form only when it is not dirty. |
| D30 | Branch costing gate (review M6) | `IInventoryLock` adds a branch gate `pg_advisory_xact_lock[_shared](hashtextextended('inv-branch:{branchId:N}', 0))`. Voucher posting and opening stock take the gate **shared**, then the product keys. Settings-change recalculation and manual recalculation take the gate **exclusive** per branch (ascending branch id) and take no product keys, so they never hold thousands of advisory locks (`max_locks_per_transaction` × `max_connections` is about 6,400 slots). Lock order everywhere: branch gate → product keys (ascending) → document counter row. `InventorySettings` is read only after the locks are held. |
| D31 | Negative-stock check compares before and after (review M7) | A pair counts as a shortage only when its new minimum `RunningQty` over rows with `PostedAt >= From` is < 0 **and** either it is lower than the minimum over the same window before the change, or the first negative point moved earlier. The old minimum is read before the old rows are deleted. Operations that only reduce an existing deficit (a back-dated receipt, cancelling a stock-out, a larger opening stock) pass under every policy. To be confirmed by the BA (wording of section-03 §5 rule 8). |
| D32 | Report valuation per costing scope (review M5) | Warehouse scope: values are ledger sums per (product, warehouse). Branch scope: the scope value `Σ(InValue − CostAmount)` and quantity are computed per (product, branch); each warehouse row gets `R0(scopeValue × rowQty / scopeQty)`, with the rounding residual on the last row by warehouse code; when the scope quantity is 0, rows get value 0 and `TotalValue` still includes the scope value. Stock card with a `warehouseId` under Branch scope: per-row `UnitCost` / `InValue` / `CostAmount` are shown, but `OpeningValue`, `RunningValue` and `ClosingValue` are null and `ValuesAtScopeOnly = true`. |
| D33 | `PurchaseCostIncludesVat` change guard (review M8) | Like a `CostingPeriod` change, a `PurchaseCostIncludesVat` change is rejected (400, key `purchaseCostIncludesVat`) unless every branch with ledger rows has `LockedUntil` empty or on a period end under the current `CostingPeriod`. When accepted, `InValue` is re-derived for every `StockIn` row dated after `LockedUntil` (whole unlocked periods), so one period never mixes the two policies. |
| D34 | Structural refinements of the brainstorm | Accepted implementation details that differ from the section-03 wording: `DocumentCounter` keyed by `PeriodKey`; composite ledger ordering `PostedAt, SourceType, SourceCode, LineSortOrder, Id` instead of a `SortKey` column; entity name `InventoryLedgerEntry`; the "Kho" sidebar group is visible when any of its items is visible; report values per D32. For implementation detail the task text wins; for business rules the brainstorm wins unless a D-entry says otherwise. |
| D35 | `Product.CostPrice` recompute (replaces D19's "never reverted") | After any stock-in create, update, cancel, restore or delete, the `CostPrice` and `CostPriceUpdatedOn` of each affected product are recomputed from its latest Active, non-deleted stock-in line (by `VoucherAt`, then the voucher's `CreatedAt`, then its `Code`, then the line's `SortOrder`, all descending). If no such line remains, `CostPrice` is kept and `CostPriceUpdatedOn` is cleared. A mistyped future date can therefore be corrected. To be confirmed by the BA. |
| D36 | Opening-stock values | `inventory.opening_stock` includes seeing and entering opening values (`Amount` and the derived unit value) for the warehouses of the working branch, even without `inventory.view_cost`. Reports and voucher cost data still require `inventory.view_cost`. |
| D37 | Negative average guard | In `PeriodicAverageCalculator`, when `OpeningQty + InQty > 0` but `OpeningValue + InValue < 0` (possible after negative stock), `AvgCost` uses the fallback (previous `AvgCost`, then `Product.CostPrice`), never a negative average. |
| D38 | Partner roles everywhere (review M9) | Every query that lists or accepts customers filters `IsCustomer` (global search, quotation create/update, `/api/customers/*`); supplier screens and search filter `IsSupplier`. Global search adds a supplier group gated by `suppliers.view` that links to `/suppliers/{id}`. |
| D39 | Deploy and go-live exposure (review M4) | Round 1 needs one `DbSeeder` run per deploy (`Database__AutoMigrateAndSeed=true`, as on Railway) to create the permissions, the D23 grants, `KHO01`, the system reasons, the payment methods and the numbering rows. If Round 1 reaches production before go-live, an admin revokes the inventory permissions from WAREHOUSE and MANAGER in Settings → Phân quyền right after the deploy (the seeder never re-grants), and grants them again at go-live. ACCOUNTANT gets no inventory defaults (open question). |

## Assumptions

- The brainstorm (including section-05) is the approved spec. If the plan and the brainstorm disagree on a business rule, the brainstorm wins, except for D1–D39 above. For implementation detail (names, folders, table shapes) the task text wins (D34).
- `PurchaseCostIncludesVat` defaults to `true`, as the brainstorm states. The accountant still needs to confirm it before go-live (section-05 §6). It is a setting, so no code change follows from the answer.
- Products in group `VC` (Vận chuyển) are seeded with `TrackInventory = false`; every other product gets `true`. The real catalog still has to be reviewed (section-05 §6). That review is a data task, not code.
- A local PostgreSQL (13+, for `DROP DATABASE … WITH (FORCE)`) runs on `localhost:5432`, and user `postgres` / `1` may create and drop databases. Earlier executions used this setup (`qldonhang_integtest`, `qldonhang_int_test`). Testcontainers stays as a fallback when `TEST_DB_CONNECTION` is unset.
- `dotnet-ef` 9.0.15 is installed globally. Migrations go to `Persistence/Migrations` (per `README.md`).
- After Phase 00, every `WebAppFactory` gets its own clone of a migrated, seeded template database, so scenario tests are isolated.
- The business is small (thousands of products, tens of thousands of ledger rows a year). Recalculating a (product, warehouse) pair in memory from the earliest affected time, and a (product, scope) cost run per period, is fast enough.

## Risks

- **Legacy number parity**: the legacy SQL functions (`GetSoLuongTonTenVatTu`, `GetDonGiaTonVatTu`) are not in the repo. Costs can differ from the legacy software. Mitigation: hand-computed tests (Phase 03/04) and a manual reconciliation of one month for a few items before go-live (outside this plan).
- **Engine correctness under back-dated edits**: mitigated by the invariant suite in Phase 06 (back-dated edit/delete/cancel gives the same result as re-entering from scratch; `StockBalance = Σ ledger`; `Closing(k) = Opening(k+1)`).
- **Deadlocks**: locks are always taken in the order branch gate (shared or exclusive) → product keys in ascending `(ProductId, BranchId)` order → document counter row (D30). Phase 05 has a parallel-save test, Phase 06 a gate test.
- **Large recalculation** after a settings change: it runs synchronously in one request and holds the exclusive branch gate, so voucher saves in that branch wait until it finishes. Acceptable at this data size. If it becomes slow, a background job is follow-up work.
- **`Product.CostPrice` side effect** on the quotation default cost (accepted in the brainstorm).
- **Partner-permission tightening (D2)** could block sales users who today edit customers that are later flagged as suppliers. This is intended; it is called out in the Phase 11 docs.
- **Phase sizes**: Phases 04, 05 and 09 are XL. Each task inside them is still small and committed separately.
- **Leftover test databases**: if a test run is killed, clones `qldonhang_integtest_<hex>` may remain. The next run drops and recreates the template. Clean up clones with `SELECT 'DROP DATABASE "' || datname || '" WITH (FORCE);' FROM pg_database WHERE datname LIKE 'qldonhang_integtest_%';` in `psql`.
- **Lighter screen tests (D26)**: CRUD and settings screens rely on smoke tests, so UI regressions there are more likely to be caught in the manual smoke test than by Vitest.

## Phases

- [x] Phase 00 — Baseline commit and fast integration-test database (S) — `phase-00-fast-test-database.md`
- [x] Phase 01 — Permissions & branch foundation (L) — `phase-01-permissions-and-branches.md`
- [x] Phase 02 — Inventory catalogs, partner roles & settings (L) — `phase-02-catalogs-partners-settings.md`
- [x] Phase 03 — Pure calculators (M) — `phase-03-pure-calculators.md`
- [x] Phase 04 — Voucher & ledger schema, posting engine (XL) — `phase-04-ledger-and-posting-engine.md`
- [x] Phase 05 — Stock voucher API (XL) — `phase-05-stock-voucher-api.md`
- [x] Phase 06 — Opening stock, cost recalculation & report APIs (L) — `phase-06-opening-recalc-reports-api.md`
- [x] Phase 07 — Frontend foundations: permissions, working branch, navigation (M) — `phase-07-frontend-foundations.md`
- [x] Phase 08 — Frontend catalog & settings screens (L) — `phase-08-frontend-catalogs-settings.md`
- [x] Phase 09 — Frontend stock voucher list & form (XL) — `phase-09-frontend-stock-vouchers.md`
- [x] Phase 10 — Frontend opening stock & inventory reports (M) — `phase-10-frontend-opening-and-reports.md`
- [-] Phase 11 — Documentation & final verification (S) — `phase-11-docs-and-verification.md`

Phases run in order. Phase 00 runs first because every later backend test relies on the fast test database. Phases 07–10 depend on the API contracts from 01–06 and must not start before Phase 06 is complete.

## Executor conventions

- Work on branch `feat/inventory-round1` (or the worktree the executing skill creates), after Phase 00 Task 0.0 has committed the baseline docs on `main`. Commit after every task, using conventional commits (`feat(inventory): …`, `test(inventory): …`, `refactor(quotations): …`).
- **Staging**: stage files explicitly (`git add <paths>`). Never use `git add -A` / `git add .`: `source/` (the legacy VB.NET code used by the brainstorm) is untracked on purpose and must never be committed.
- **Shell**: the commands below are written for Git Bash (the Bash tool). Windows PowerShell 5.1 has no `&&`; adapt the syntax if you use it.
- **Test database** (D25) — set before any backend test command:
  - PowerShell: `$env:TEST_DB_CONNECTION = "Host=localhost;Port=5432;Database=qldonhang_integtest;Username=postgres;Password=1"`
  - bash: `export TEST_DB_CONNECTION="Host=localhost;Port=5432;Database=qldonhang_integtest;Username=postgres;Password=1"`
- **Test depth** (D26): when a phase marks a test as "smoke level", keep it to the listed cases; do not add exhaustive per-field tests.
- **Backend commands** (run from `backend/`):
  - Build: `dotnet build OrderMgmt.sln`
  - Targeted tests: `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~<ClassName>"`
  - Pure unit tests only: `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~Inventory.Unit"`
  - Full suite: `dotnet test OrderMgmt.sln`
  - Migration: `dotnet ef migrations add <Name> --project src/OrderMgmt.Infrastructure --startup-project src/OrderMgmt.WebApi -o Persistence/Migrations`
- **Frontend commands** (run from `frontend/`): `npx vitest run <path>`, `npm run typecheck`, `npm run lint`, `npm run test`, `npm run build`. Until Phase 07 Task 7.1 fixes the script, `npm run typecheck` (`tsc --noEmit` on a solution-style `tsconfig.json` with `"files": []`) checks nothing; after it, the script is `tsc -p tsconfig.app.json --noEmit`. Every frontend phase ends with `npm run build` as well, because only `tsc -b` type-checks everything.
- **Known baseline failures** (2026-10-07, before any change; do not count them as regressions): backend 19 failing tests (AdminRolesCrudTests ×6, AuthTests.Refresh_rotates_token_and_revokes_old_one, HandoverExportTests ×2, QuotationExportTests ×2, QuotationStateMachineTests ×5, RevenueLineItemsExportTests ×2, SalesRevenueReportTests.Report_FiltersByConfirmedAt_NotQuotationDate); they are unrelated to inventory (quotation payload/state-machine/export changes) and stay out of scope. Frontend 3 failing tests (bank-accounts-tab "adds a new account via the form", useNotificationHub "invalidates unread-count query on NewNotification", payment-qr-page "generates and displays a QR after submitting valid data").
- **Test locations**
  - Integration tests: `backend/tests/OrderMgmt.IntegrationTests/{Organization,Inventory,Catalog,Admin}/`. Annotate with `[Collection(nameof(PostgresCollection))]`.
  - Pure unit tests: `backend/tests/OrderMgmt.IntegrationTests/Inventory/Unit/` (no collection attribute, no DB).
  - Frontend tests sit next to the file under test (`*.test.ts(x)`).
- **Shared test base**: `InventoryTestBase : QuotationTestBase` (created in Phase 02 Task 2.1, extended later). Engine tests use `InventoryEngineTestBase : InventoryTestBase` (Phase 04 Task 4.5).
- **Logins in tests**: `POST /api/auth/login` is rate-limited (5 per minute, one partition per test host). Extra admin clients reuse the admin token (`CloneAdminClient()`, Phase 02 Task 2.1); `WebAppFactory` raises the login limit for tests (Phase 00). Never log in more than a few users per test.
- **Test notation**: in engine-level scenarios `In q @ v` means quantity q with **line value** v (e.g. `In 10 @ 1,000,000` = 10 units worth 1,000,000 in total); in API scenarios `In q × p` means quantity q at **unit price** p. Dates are written `10-05` (= 2026-10-05) unless a year is given.
- **UTC** (D27): never pass a `DateTimeOffset` with a non-zero offset to EF. Test helper `Vn("2026-10-02 08:00")` returns the UTC instant of that VN time.
- **Bool defaults** (D28): `HasDefaultValue(true)` on a non-nullable bool always comes with `.HasSentinel(true)`.
- **Child rows**: `BaseEntity` pre-assigns `Id`, so a new line, activity or opening-stock row added only through a navigation collection is sent as an UPDATE (0 rows → `DbUpdateConcurrencyException`). Add new child entities with their `DbSet.Add(...)` (or set `EntityState.Added`), as `QuotationService` does.
- **Validation keys**: `details` keys are camelCase paths in both layers (`orderDiscount`, `lines[0].vatRate`): Phase 05 sets a camelCase FluentValidation property-name resolver, and services build `ValidationDomainException` keys the same way. `ValidationDomainException` carries a Vietnamese summary message (first detail) so toasts are meaningful.
- **Backend layout**: entities in `Domain/Entities/{Organization,Inventory}`, enums in `Domain/Enums/InventoryEnums.cs`, use cases in `Application/{Organization,Inventory}/<Feature>/{Interfaces,Models,Services,Validators}`, EF configs in `Infrastructure/Persistence/Configurations/{OrganizationConfiguration,InventoryConfiguration}.cs`, Postgres-specific ports in `Infrastructure/Inventory/`. Controllers stay thin. Every service method that a static `[HasPermission]` can't cover checks permissions itself (type-dependent permissions, partner roles).
- **Never** trust totals, quantities or costs from the client. The backend recomputes everything with `StockVoucherCalculator`.
- **Delete style**: business entities (`Branch`, `Warehouse`, `StockReason`, `PaymentMethod`, `StockVoucher`, `StockVoucherLine`, `StockVoucherActivity`, `OpeningStock`) inherit `BaseEntity` and soft-delete. Derived data (`InventoryLedgerEntry`, `InventoryCostPeriod`, `StockBalance`, `DocumentCounter`) does not inherit `BaseEntity`; it is hard-deleted and rewritten.
- **Folder layout detail**: engine components use flat feature folders (`Application/Inventory/{Common,Numbering,Ledger,Costing,Posting}`), shared inventory ports live in `Application/Inventory/Interfaces`, fixture tests may live in `tests/.../Fixtures/`, and frontend tests follow an existing `__tests__` folder where one exists.

## Go-live gates (tracked here, not implemented by this plan)

| Gate | Owner | When |
|---|---|---|
| Accountant confirms `PurchaseCostIncludesVat` (section-05 §6) and signs off D8 (value kept on zero quantity when a period has no outbound row) | PO + accountant | Before go-live |
| Review which products are `TrackInventory = false` (beyond group `VC`) | PO | **Before any opening stock is entered** — Phase 04 Task 4.8 locks the flag once activity exists |
| Fetch the legacy `GetSoLuongTonTenVatTu` / `GetDonGiaTonVatTu` definitions from the production database | Dev | Before reconciliation |
| Manual reconciliation of one month for a few items against the legacy software (section-03 §7) | Dev + accountant | Before go-live |
| Cut-over plan; decision on Excel import of opening stock and opening debt (proposed for Round 3) | PO | Before Round 3 planning |
| BA confirms D31 (negative-stock check) and D35 (cost price recompute); decide ACCOUNTANT inventory defaults (D39) | BA / PO | Before go-live |

## Backlog (from section-05 §5 — not implemented here; constraints to keep)

| Item | Constraint to respect when it is built |
|---|---|
| FIFO | Needs a `FifoLayer` table. `InventorySettings.CostingMethod` already has `Fifo`; the PUT validator rejects it until then. |
| Warehouse transfer | With scope = Branch, a transfer inside the branch doesn't change cost. With scope = Warehouse, the receiving warehouse's cost depends on the issuing warehouse, so costing must chain across warehouses (pairs are no longer independent). Cross-branch transfers = an issue plus a receipt that carries the cost. |
| Stocktake | Adjustment voucher for the difference, valued at the period's average cost. |
| Returns (customer return / return to supplier) | A customer return takes the cost of the original outbound voucher, not a typed-in price. |
| Quotation → sales voucher | The revenue source moves from quotations to sales vouchers; dashboards and revenue reports must follow. |
| Debt offset | Round 2 (uses the shared partner catalog delivered here). |

## Final Verification

Run after all phases:

```bash
export TEST_DB_CONNECTION="Host=localhost;Port=5432;Database=qldonhang_integtest;Username=postgres;Password=1"
cd backend
dotnet build OrderMgmt.sln
dotnet test OrderMgmt.sln

cd ../frontend
npm run typecheck
npm run lint
npm run test
npm run build
```

Manual smoke test (dev stack: `docker compose up -d`, backend `dotnet run --project src/OrderMgmt.WebApi`, frontend `npm run dev`, login `admin` / `Admin@123`):

1. Settings → Chi nhánh: create branch `CN02`; Cấu hình kho shows policy `Warn`; Đánh số shows `PN`/`PX`.
2. Kho → Tồn đầu kỳ: KHO01, date = first day of the current month, a tracked m² product = 100 m² valued at 5,000,000 → save.
3. Kho → Phiếu nhập kho → new: reason "Nhập mua hàng", a supplier, two lines (one PerUnit, one PerSquareMeter with sheets + dimensions), order discount and freight → Lưu tạm. Code `PN00001`; totals match the form preview.
4. Kho → Phiếu xuất kho → new: issue more than the stock on hand → a warning dialog lists the shortage → confirm → saved.
5. Edit the stock-in voucher, moving its date before the outbound voucher → Báo cáo Thẻ kho shows the new order and the recalculated outbound cost (as a user with `inventory.view_cost`).
6. Header branch switcher (admin): switch to `CN02` → voucher lists are empty; switch back.
7. Log in as a WAREHOUSE user: the "Kho" menu is visible; value columns in Tồn kho / Thẻ kho are hidden; the Tồn đầu kỳ grid shows opening values (D36); Cấu hình hệ thống inventory cards are hidden.
8. Khóa sổ to yesterday → editing a voucher dated before then fails with "đã khóa sổ".
9. Settings → Phân quyền: the "Kho" permission group is listed and can be toggled for a custom role.
10. Admin with branch `CN02` selected reloads `/stock-in`: the list shows `CN02` data from the first render (no `CN01` flash).

## Rollback / Recovery

- **Code**: every task is a separate commit on `feat/inventory-round1`; revert the branch, or individual commits, with `git revert`.
- **Database**: the new migrations are `AddBranches`, `AddWarehouses`, `AddStockReasons`, `AddPaymentMethods`, `AddPartnerRoles`, `AddProductInventoryFields`, `AddInventorySettings`, `AddDocumentNumbering`, `AddStockVouchersAndLedger`. To roll back a deployed database, run `dotnet ef database update <migration before AddBranches> --project src/OrderMgmt.Infrastructure --startup-project src/OrderMgmt.WebApi` (currently `AddBanksAndUserBankAccounts`). This drops all inventory data. Before go-live there is no production inventory data, so the rollback is lossless for existing quotation data. The `customers.is_customer` / `is_supplier` and product columns are additive, with defaults.
- **Permissions**: new permission rows are harmless if code is rolled back. Remove them (role assignments first; `\_` escapes the LIKE wildcard):
  ```sql
  DELETE FROM role_permissions WHERE permission_id IN (SELECT id FROM permissions WHERE code LIKE 'stock\_in.%' OR code LIKE 'stock\_out.%' OR code LIKE 'inventory.%' OR code LIKE 'suppliers.%' OR code IN ('branches.manage','branches.access_all','period_lock.manage','reports.inventory'));
  DELETE FROM permissions WHERE code LIKE 'stock\_in.%' OR code LIKE 'stock\_out.%' OR code LIKE 'inventory.%' OR code LIKE 'suppliers.%' OR code IN ('branches.manage','branches.access_all','period_lock.manage','reports.inventory');
  ```
- **Feature exposure and deploy** (D39): every deploy of Round 1 needs one `DbSeeder` run (`Database__AutoMigrateAndSeed=true`) — it creates the permissions, the D23 grants, `KHO01`, the system reasons, the payment methods and the numbering rows. Go-live is after Round 3 (brainstorm). If Round 1 reaches production earlier, revoke the inventory permissions from WAREHOUSE and MANAGER in Settings → Phân quyền right after the deploy (the "Kho" group is visible there after Phase 07 Task 7.6); the seeder grants each code only once, so the revocation survives restarts. Grant them again at go-live.
