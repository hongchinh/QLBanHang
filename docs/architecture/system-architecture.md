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

The product is quotation-first. The quotation is the main sales document; revenue is counted when a quotation reaches `Confirmed`. Orders, delivery, warehouse and debt tracking are intentionally outside the current implementation.

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
