# Coding Conventions

## Backend

- Target framework is `net9.0`; versions for EF Core, ASP.NET Core and Npgsql are pinned in `backend/Directory.Build.props`.
- Nullable reference types and implicit usings are enabled.
- Keep Clean Architecture dependencies one-way: Domain -> none, Application -> Domain, Infrastructure -> Application/Domain, WebApi -> all for composition.
- Put business entities in `OrderMgmt.Domain`; do not place EF or HTTP concerns there.
- Application services own use-case logic and receive dependencies through interfaces in `Application/Common/Interfaces` or feature-specific `Interfaces` folders.
- Controllers should stay thin: authorize, validate route/body binding and delegate to application services.
- Return standard `ApiResponse` wrappers through `ApiControllerBase`.
- Use FluentValidation for request validation and Mapster registrations for DTO mapping where a feature needs mapping configuration.
- Protect endpoints with `[HasPermission(Permissions.<Module>.<Action>)]`; add new permission constants in `Domain/Constants/Permissions.cs` and seed them in `DbSeeder`.
- New persisted entities should have EF configuration under `Infrastructure/Persistence/Configurations`.
- Prefer soft delete for business entities that inherit `BaseEntity`. Use hard delete only for pure join entities such as role-permission assignments.
- Add EF migrations under `Infrastructure/Persistence/Migrations` using the WebApi project as startup project.

## Frontend

- Use React 18 + TypeScript + Vite.
- Keep route screens in `pages/`; keep module server-state access in `features/<module>`.
- A normal feature folder uses `api.ts`, `hooks.ts`, `types.ts`, optional `schema.ts` and optional `keys.ts`.
- Use TanStack Query hooks for API-backed state. Keep query keys centralized per feature when a module has more than trivial fetching.
- Use React Hook Form + Zod for forms that need validation.
- Use shadcn-style primitives from `components/ui` and layout components from `components/layout`.
- Gate pages with `ProtectedRoute permission="..."`; use permission-aware helpers for conditional UI.
- Keep API calls inside feature `api.ts` files and use `lib/api-client.ts` instead of raw Axios instances.
- Keep shared CSS tokens in `src/styles`; avoid one-off layout CSS when an existing token/helper fits.
- For dense action bars, keep button backgrounds restrained and color icons semantically: primary/save blue, send/add cyan, confirm/success emerald, cancel/delete red, clone/copy violet, Excel/export emerald, print indigo, navigation slate. Avoid coloring every button background unless a screen explicitly needs stronger grouping.
- In data tables, right-align numeric and currency columns in both headers and cells, and use `tabular-nums` for readable column scanning.

## Tests And Verification

- Backend integration tests live in `backend/tests/OrderMgmt.IntegrationTests`.
- Integration tests run against a local PostgreSQL through `TEST_DB_CONNECTION`, e.g. `Host=localhost;Port=5432;Database=qldonhang_integtest;Username=postgres;Password=1` (bash: `export TEST_DB_CONNECTION="..."`; PowerShell: `$env:TEST_DB_CONNECTION = "..."`). Without it, Testcontainers starts PostgreSQL in Docker.
- `PostgresFixture` migrates and seeds a template database `<base>_tpl` once per run; every `WebAppFactory` gets its own clone `<base>_<guid>`, dropped on dispose (clones a failing test did not dispose are dropped at the end of the run). The fixture never touches the base database named in `TEST_DB_CONNECTION`, refuses the dev databases `qldonhang_test` / `qldonhang`, and the factory overrides both `ConnectionStrings:Default` and `ConnectionStrings:DefaultConnection` (the app prefers `DefaultConnection`).
- New integration tests construct `new WebAppFactory(_pg)` (or inherit `QuotationTestBase`); derived factories take `PostgresFixture` (`: base(pg)`). The login rate limit is raised for tests via `RateLimiting:LoginPermitLimit`.
- Frontend tests use Vitest and Testing Library. Test files sit next to the behavior they cover, usually as `*.test.ts` or `*.test.tsx`.
- Run backend build/tests from `backend`; run frontend `npm run typecheck`, `npm run test` or `npm run build` from `frontend`.

## Local Development

- PostgreSQL and pgAdmin are provided by `docker-compose.yml`.
- Development backend configuration enables `Database:AutoMigrateAndSeed` and seeds `admin` / `Admin@123`.
- `appsettings.Development.json` currently points to `qldonhang_test` with `postgres` / `1`; when using the database from `docker-compose.yml`, override `ConnectionStrings__Default` to `qldonhang` with `qldh` / `qldh_dev_password`.
- Frontend dev server proxies `/api` to the backend through Vite config.

## Deployment Notes

- Backend and frontend each have a Dockerfile for Railway-style separate services.
- `VITE_API_BASE_URL` is a frontend build-time variable; changing it requires rebuilding the frontend image.
- For cross-domain auth, configure backend CORS and refresh cookie settings together.

## Inventory Patterns (Round 1)

- Type-dependent permissions (for example `stock_in.*` vs `stock_out.*`) are checked in the service when `[HasPermission]` cannot express them.
- Derived data (`InventoryLedgerEntry`, `InventoryCostPeriod`, `StockBalance`, `DocumentCounter`) does not inherit `BaseEntity`; it is hard-deleted and rewritten.
- Write use cases that touch stock run inside `ITransactionRunner` and call `IInventoryPostingService.AcquireLocksAsync` (shared branch gate, then product keys) before the first `SaveChangesAsync`. Read `InventorySettings` and `Branch.LockedUntil` after the locks, and re-check referenced products/warehouses there. `IInventoryLock` takes each key set in one statement; keep one advisory lock per product.
- Check-then-act guards on stock activity (product/warehouse delete, stock-field changes) run under the same locks as the postings they guard.
- A unique-index violation surfaces as 409 `DUPLICATE`; services still check uniqueness first to return a field-level 400.
- Every `DateTimeOffset` reaching EF is UTC (D27). VN-date rules are UTC ranges built with `VnTime.StartOfDay/StartOfNextDay`; never compare `.Date` of an instant.
- Never configure `HasDefaultValue(true)` on a non-nullable `bool` without `.HasSentinel(true)` (D28).
- New child entities (lines, activities, opening-stock rows) are added through their `DbSet.Add`: `BaseEntity` pre-assigns ids, so adding only through a navigation collection sends an UPDATE.
- Validation `details` keys are camelCase in both layers (`orderDiscount`, `lines[0].vatRate`); `ValidationDomainException` carries a Vietnamese summary message (its first detail by default).
- Pure calculators (`PricingQuantity`, `StockVoucherCalculator`, `PeriodicAverageCalculator`, `CostingPeriodCalendar`, `DocumentNumberFormatter`) live next to their feature and are unit-tested in `tests/OrderMgmt.IntegrationTests/Inventory/Unit` without a database.
- Frontend:
  - `npm run typecheck` runs `tsc -p tsconfig.app.json`.
  - Error handling uses `getApiError` / `formatApiErrorDetails` from `lib/api-client.ts`.
  - Dates go through `lib/vn-datetime.ts`; never derive a local date with `toISOString().slice(0, 10)`.
  - Money in previews uses `roundAwayFromZero` (`lib/round.ts`); stock quantities use `formatStockQuantity`.
  - Branch-scoped queries (including `warehouseKeys`) live under the `['inventory']` root key. Change the working branch only through `useSwitchWorkingBranch`; pages are remounted per branch, so don't sync branch-derived state by hand.
  - Warehouse pickers use `selectableWarehouses` (active ones, plus the current inactive selection). Quantity inputs parse with `parseQuantityInput` (`lib/stock-quantity.ts`), money with `parseMoneyInput`.
  - Long synchronous calls (cost recalculation, costing settings) pass an explicit `timeout` and treat `isRequestTimeout` as "may still be running".
  - The service worker caches only allow-listed reference data that is the same for every signed-in user (`CACHEABLE_API_PREFIXES` in `lib/sw-routes.ts`); never add a branch-, user- or permission-scoped path there.

## Cash & Debt Patterns (Round 2A)

- A warning the user may confirm is a `ConfirmationRequiredException` (`Code`, `Message`, `Details`), mapped to **422** like `NegativeStockException`. Codes: `DEBT_SETTLEMENTS_WILL_BE_REMOVED` (always confirmable), `CREDIT_LIMIT_WARNING` / `CREDIT_LIMIT_BLOCKED`, `NEGATIVE_FUND_WARNING` / `NEGATIVE_FUND_BLOCKED`. The request skips a warning with its flag: `confirmRemoveSettlements`, `acknowledgeCreditLimit`, `acknowledgeNegativeFund` (plus Round 1's `acknowledgeNegativeStock`). When several apply, the server reports them in this order: negative stock → settlements removal → credit limit → negative fund. `CanConfirm` is not serialized: a `_BLOCKED` code is never confirmable.
- Period lock checks in new code use `PeriodLockGuard.EnsureNotLocked` (`Application/Organization/Branches`), with instants or VN dates; settlements are checked on `EffectiveAt`. Round 1's private copies stay as they are.
- Every numbered document takes its code from `IDocumentCodeAllocator.NextAsync` (`peek: true` for `/defaults`). The cash series are `Receipt` (`PT`), `Payment` (`PC`), `FundTransfer` (`CQ`) and `DebtOffset` (`BT`), one counter per type × branch. A soft-deleted document's number is never reused.
- Every cash/debt write runs inside `ITransactionRunner`, takes the shared branch gate (`IInventoryLock.AcquireBranchGateAsync(..., exclusive: false)`) and then `ICashDebtLock`. Global order: branch gate → product keys → partner keys `debt-partner:{branch:N}:{partner:N}` → fund keys `cash-fund:{fund:N}` → reason key (`cash-reason:{id:N}` / `stock-reason:{id:N}`, shared on voucher saves, exclusive on reason edit/delete) → counter row. Pass both the old and the new partner/fund ids on update. Read settings, `LockedUntil`, `OpeningDate`, credit limits, fund data and the reason **after** the locks (or re-check them there, as `StockVoucherService` does).
- Debt is written only through `IDebtLedgerService`: `PostAsync` on save (upserts entries in place by source and side) and `RemoveSourceAsync` on cancel/delete. Remove your own origin first with `RemoveOwnSettlementsAsync` (cash voucher edit with an allocation list: `AtVoucher`; stock voucher save: `Auto`; offset cancel/delete: `Offset`); that needs no confirmation and writes no activity. Every other settlement then asks `DEBT_SETTLEMENTS_WILL_BE_REMOVED`, and changing the partner or side of a settled entry returns 409 `DEBT_HAS_SETTLEMENTS`. A write that may remove another document's settlements reads `CounterpartEntryIdsAsync` first and calls `IAutoCashVoucherService.RecomputeAutoAsync` after its own new settlements.
- Cash/debt integration tests live in `tests/OrderMgmt.IntegrationTests/CashDebt` (pure tests in `CashDebt/Unit`), inherit `CashDebtTestBase : InventoryTestBase`, use a fresh branch and partners, and end every ledger, settlement and debt-source scenario with `AssertCashDebtInvariantsAsync()`.
- Frontend:
  - Branch-scoped cash and debt queries live under the `['cash']` root key (`cashKeys.all` in `features/cash-common/keys.ts`). Stock voucher mutations invalidate `['inventory']` and `['cash']`; cash mutations invalidate `['cash']` and `['inventory']`.
  - A form handles a 422 with `toConfirmationRequest` / `withConfirmed` (`lib/confirm-required.ts`) and `ConfirmRequiredDialog`: it keeps the flags already confirmed and resends them all with the new one. The client treats a code without a flag (`_BLOCKED`) as blocked.
  - Never add a cash or debt path to `lib/sw-routes.ts`.
