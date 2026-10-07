# System Architecture

## Overview

```
Frontend (React + Vite)
  Tailwind/shadcn-style UI, TanStack Query/Table, Zustand auth store
        |
        | HTTPS/JSON through /api
        v
Backend (.NET 9 Web API)
  Clean Architecture, JWT + refresh cookie/token, permission policies
        |
        | EF Core 9 / Npgsql
        v
PostgreSQL 16
```

The product is quotation-first. The quotation is the main sales document; revenue is counted when a quotation reaches `Confirmed`. Orders and delivery are outside the scope. Inventory (Round 1: branches, stock vouchers, ledger, costing and reports) is implemented and goes live after Round 3; cash vouchers and debt are Round 2.

## Backend Layers

```
OrderMgmt.WebApi
  Controllers, middleware, auth policies, startup composition
        |
OrderMgmt.Infrastructure
  EF Core DbContext, migrations, Npgsql, BCrypt, JWT, refresh tokens, export, seed
        |
OrderMgmt.Application
  DTOs, validators, service interfaces/implementations, use-case logic, ports
        |
OrderMgmt.Domain
  Entities, enums, constants, domain exceptions
```

Domain stays dependency-free. Application depends on Domain and defines ports such as `IAppDbContext`, `ICurrentUser`, `IDateTime`, `IJwtTokenGenerator`, `IPasswordHasher` and `IRefreshTokenService`. Infrastructure implements those ports. WebApi wires the graph through DI.

## Auth And Authorization

- `POST /api/auth/login` verifies the BCrypt password, returns an access token and issues a refresh token.
- `POST /api/auth/refresh` rotates refresh tokens and re-loads current role permissions before creating the new access token.
- `POST /api/auth/logout` revokes the refresh token.
- `GET /api/auth/me` reads the authenticated user from JWT claims.
- Access tokens include user, role and `permission` claims.
- Endpoint permissions use `[HasPermission(Permissions.X.Y)]`; dynamic policies are named `perm:<permission_code>`.
- Frontend route guards use `ProtectedRoute` and `useAuthStore.hasPermission(...)`.

Refresh-token reuse detection revokes the active token family for the user. The refresh cookie is configured by `AuthCookie` settings; cross-site production deployments must use `SameSite=None` together with `Secure=true`.

## Role And Permission Management

`AdminRolesController` exposes role and permission management under `/api/admin/roles` and `/api/admin/permissions`.

- ADMIN is a system role and is not mutated through the permission matrix.
- System roles such as SALES, ACCOUNTANT, WAREHOUSE and MANAGER can have permissions customized, but cannot be renamed or deleted.
- Custom roles support CRUD; delete is blocked while users are still assigned.
- `RolePermission` is a join table and is hard-deleted when needed.
- `DbSeeder` gives ADMIN all permissions on startup and only initializes other system role permissions when they have no assignments yet.
- Newly introduced permission codes are granted once to the existing system roles whose defaults include them. Inserting the permission rows and granting them run in one transaction, so a later removal by an admin survives restarts.
- Inventory permissions form the module `inventory` (shown as "Kho" in the role matrix): `stock_in.*`, `stock_out.*` (view/create/edit/delete/cancel/edit_all), `inventory.opening_stock`, `inventory.view_cost`, `inventory.catalogs.manage`, `inventory.settings`, `inventory.recalc_cost`. Related codes: `suppliers.*`, `branches.manage`, `branches.access_all`, `period_lock.manage`, `reports.inventory`.
- Deploy note (D39): every Round 1 deploy needs one seeder run (`Database__AutoMigrateAndSeed=true`) for the permissions, default grants, `KHO01`, system stock reasons, payment methods and numbering rows. Until go-live, revoke the inventory permissions from WAREHOUSE and MANAGER in production right after the deploy; the seeder never grants them again.

## Core Business Flows

### Catalog

Customers and products are standard CRUD modules with search endpoints used by quotation forms and global search. Product pricing supports a pricing mode field. Product groups and units are exposed through lookup endpoints.

### Quotations

Quotation status flow is `Draft -> Sent -> Confirmed -> Cancelled`.

- Create assigns `OwnerUserId` to the current user.
- List/detail operations are scoped to owner unless the user has `quotations.view_all`.
- Users without `quotations.bypass_lock` cannot edit a quotation once it reaches their configured lock-at status.
- Transfer actions write `QuotationOwnerHistory`.
- Nhân bản (Clone) creates a Draft copy owned by the current user; orphan-source cloning requires `quotations.clone_orphan`.
- Confirmed quotations store confirmation metadata and feed revenue reports.
- Export supports Excel and PDF. Excel rendering uses ClosedXML; PDF conversion uses LibreOffice. Template paths in `QuotationExport` are resolved relative to `AppContext.BaseDirectory` unless configured as absolute paths. In local Debug runs this means `templates/...` points under `backend/src/OrderMgmt.WebApi/bin/Debug/net9.0/`. Per-user quotation templates are stored under `QuotationExport:UserTemplatesPath` (default `templates/users`) as `{userId}.xlsx`, with fallback to `QuotationExport:TemplatePath`. Per-user handover templates use `{userId}_handover_with_price.xlsx` or `{userId}_handover_no_price.xlsx`, with fallback to the corresponding system handover template path.

### Payments / VietQR

Generates a NAPAS 247 / EMVCo-compliant VietQR payload so any Vietnamese banking app can scan it to
auto-fill a bank transfer. There is no third-party VietQR API call and no payment confirmation —
the app has no way to know whether a transfer actually happened.

- `Banks` is a seeded, code-maintained reference table (~30 major Vietnamese NAPAS-member banks with
  their BIN codes). `GET /api/banks` lists active banks; there is no admin CRUD for it.
- `VietQrPayloadBuilder` (`Application/Payments/Services`) builds the EMVCo TLV payload and its
  CRC16-CCITT-FALSE checksum from scratch (tag/length/value encoding, Vietnamese-diacritic
  stripping for the merchant name/content fields). `POST /api/payment-qr/generate` returns the
  payload string; the frontend renders it into a QR image client-side (`qrcode.react`) and can
  export it as PNG. The backend never generates a QR image itself.
- `UserBankAccount` records a user's own saved receiving accounts, CRUD'd under
  `/api/me/bank-accounts` and always scoped to `ICurrentUser.UserId`. The first saved account
  becomes the default automatically; creating another as default (or `PUT .../default`) unsets the
  previous default; deleting the current default promotes another remaining account.
- All Payments endpoints only require `[Authorize]` — no dedicated permission constants.
- The quotation detail page links into the QR page with the quotation's total/code pre-filled via
  query params (`amount`, `content`); a user's default saved account pre-fills the bank/account
  fields when one exists.

### Inventory (Round 1)

**Working branch.** Every user has a default branch. The frontend sends the chosen branch in `X-Branch-Id`; `ICurrentBranch` honours it only for users with `branches.access_all` and otherwise uses the default branch. Warehouses, vouchers, opening stock, numbering, period locks and reports are scoped to the working branch; quotations and catalogs are shared.

**Posting flow.** Every write that changes stock runs in one `ITransactionRunner` transaction:

1. permission by voucher type, working branch, ownership or `edit_all`, `Version` (xmin) set as the original value;
2. reference and amount validation (reasons, partner roles, warehouses of the branch, dimensions, discounts, dates);
3. locks: the branch gate (shared), then the `(product, branch)` keys and the branch-independent product keys;
4. period lock check (read after the gate);
5. on create, the document number from the atomic counter row;
6. voucher header and lines with totals from `StockVoucherCalculator` (never from the client);
7. ledger replacement for the voucher (`IInventoryLedgerService`): rows rewritten, `RunningQty` and `StockBalance` recomputed from the earliest affected time;
8. negative-stock policy (`Allow` / `Warn` / `Block`), counting only pairs the operation makes worse (D31);
9. cost recalculation of the affected product scopes;
10. `Product.CostPrice` recompute from the latest active stock-in (D35);
11. activity log.

| Operation | Steps |
|---|---|
| Create | 1 (create permission, branch), 2, 3, 4, 5, 6, 7–11 |
| Update | 1–4, 6, 7–11 |
| Cancel / Delete | 1, 3, 4 (stored date), 7 (empty list), 8–11 |
| Restore | 1, 3, 4 (stored date), 7 (stored lines unchanged; rejected if the product's stock unit or the warehouse's branch changed), 8–11 |

Opening stock (`/api/inventory/opening-stock`) follows the same flow with source type `Opening`, posted at 00:00 VN of the opening date; saves of one warehouse are serialized by an opening key.

**Lock order (D30).** Branch gate → `(product, branch)` keys → product keys → document counter row, always before the first `SaveChanges`. Settings changes and manual recalculation take the branch gate **exclusive** (every branch, ascending id) and no product keys, so they wait for in-flight postings and postings wait for them. Setting a period lock also takes the exclusive gate.

**Concurrency (D29).** `StockVoucher.Version` maps to PostgreSQL `xmin`. Update, cancel, restore and delete always mark the header modified, so a stale `Version` fails with 409 `CONCURRENCY` even when only lines change.

**Instants (D27).** Every `DateTimeOffset` written or used as a query parameter is UTC. VN-date rules (period lock, costing period, numbering period, report dates) are UTC ranges built with `VnTime.StartOfDay/StartOfNextDay`.

**Derived data.** `InventoryLedgerEntry`, `InventoryCostPeriod`, `StockBalance` and `DocumentCounter` do not inherit `BaseEntity`; they are hard-deleted and rewritten. Ledger posting order is `PostedAt, SourceType (Opening < StockIn < StockOut), SourceCode, LineSortOrder, Id`.

**Costing.** Periodic weighted average per period (month/quarter/year) and scope (branch or warehouse). The opening of a run is summed from the ledger before the period (D7); a zero closing quantity pushes the rounding residual to the last outbound row (D8); a negative average uses the previous average (D37). Fully locked periods are frozen; a partially locked period is recomputed. Changing the costing period, scope or `PurchaseCostIncludesVat` recalculates every branch from the first unlocked period (D11, D33). Report values follow the costing scope (D32); values of a period that has not ended are flagged provisional.

**Error codes.** 422 `NEGATIVE_STOCK_WARNING` (resend with `acknowledgeNegativeStock: true`) and `NEGATIVE_STOCK_BLOCKED`, with `details` keyed `"{productCode}@{warehouseCode}"`; 409 `CONCURRENCY`; 400 `PERIOD_LOCKED`; 400 `VALIDATION` with camelCase keys such as `lines[0].width`.

### Dashboard, Reports, Search, Branding And Notifications

- Dashboard endpoints under `/api/dashboard` expose summary, revenue series, top customers, top products, recent activity and sales leaderboard.
- `ReportsController` currently exposes sales revenue reporting gated by `reports.revenue`.
- `SearchController` provides global search.
- `SettingsController` manages branding/logo settings; writes require `user_settings.manage`.
- `NotificationsController` exposes list, unread count and read/mark-all-read actions.

## API Contract

Successful responses are wrapped in `ApiResponse<T>`:

```json
{
  "success": true,
  "data": {},
  "error": null,
  "timestamp": "2026-05-19T00:00:00+07:00"
}
```

Failures use the same envelope with `success=false` and an `error` object. `GlobalExceptionMiddleware` maps common exceptions to HTTP status codes:

| Exception | HTTP Status |
| --------- | ----------- |
| FluentValidation `ValidationException` | 400 |
| `DomainException` | 400 |
| `AuthenticationException` | 401 |
| `UnauthorizedAccessException` | 401 |
| `ForbiddenException` | 403 |
| `NotFoundException` | 404 |
| `ConflictException` | 409 |
| `DbUpdateConcurrencyException` (code `CONCURRENCY`) | 409 |
| `NegativeStockException` (`NEGATIVE_STOCK_WARNING` / `NEGATIVE_STOCK_BLOCKED`) | 422 |
| Rate-limit rejection | 429 |
| Unhandled exceptions | 500 |

## Persistence, Soft Delete And Audit

- Business entities inherit `BaseEntity` with audit fields and soft-delete flags.
- `AppDbContext.SaveChangesAsync` fills create/update audit data from `ICurrentUser` and `IDateTime`.
- Query filters exclude `IsDeleted=true` records.
- Filtered unique indexes allow reusing codes/usernames/emails after soft delete.
- Soft-delete cascade applies through child collections that also implement `ISoftDeletable`.
- String values are normalized through EF conventions, including trimming.

## Migration And Seed

Migrations currently live in two folders because early migrations were generated before the final `Persistence/Migrations` path:

- `OrderMgmt.Infrastructure/Migrations`: initial permissions, filtered indexes, refresh tokens, snake_case, product pricing, quotations, unaccent, quotation confirmed/cancelled audit fields, and (most recently) `banks`/`user_bank_accounts`. New migrations land wherever the EF tool's last-migration lookup points, which by default follows whichever of the two folders holds the most recent migration ID — not a fixed choice.
- `OrderMgmt.Infrastructure/Persistence/Migrations`: quotation owner, owner history, user quotation settings, system branding and notifications.

`Database:AutoMigrateAndSeed` is `true` in development and `false` in base production settings. Development seed creates roles, permissions, admin user, product groups and units. Production deployments should provide `Seed__AdminPassword` only when initial seeding is intended.

## Logging And Health

- Serilog writes console output and rolling files under `backend/src/OrderMgmt.WebApi/logs`.
- `LoggingContextMiddleware` enriches logs with correlation id and user id.
- Health endpoints include liveness and readiness checks; readiness includes database connectivity.

## Frontend Layering

```
pages/       Route-level screens
features/    Module API clients, hooks, schemas and types
components/  Shared UI, layout, auth helpers and domain widgets
routes/      Auth initialization and protected routes
stores/      Zustand auth/UI state
lib/         API client, query client, route permissions, helpers
styles/      Shared CSS tokens and form/grid utilities
```

Axios interceptors unwrap the backend API envelope, attach access tokens and use refresh flow for expired sessions. TanStack Query owns server-state caching and invalidation.

The working branch lives in `stores/branch-store.ts` and is sent as `X-Branch-Id` on every request. `AppLayout` renders pages only after `useBranchContext` has resolved the branch, so no page fetches with the wrong branch. Switching branch resets the query cache. Inventory queries live under the `['inventory']` root key. The service worker never caches branch- or user-scoped API data (`lib/sw-routes.ts`), and logout or a failed refresh clears its API cache.
