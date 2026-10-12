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

The product is quotation-first. The quotation is the main sales document; revenue is counted when a quotation reaches `Confirmed`. Orders and delivery are outside the scope. Inventory (Round 1: branches, stock vouchers, ledger, costing and reports) and cash & debt (Round 2A: receipts and payments, funds and fund transfers, the receivable/payable ledger, matching, debt offsets, credit limits and the reports Số dư công nợ, Sổ chi tiết công nợ and Sổ quỹ) are implemented and go live after Round 3. The remaining debt reports (by document, aging, overdue, by employee, reconciliation) are Round 2B.

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
- Cash & debt permissions form the module `cash` (shown as "Tiền & Công nợ" in the role matrix): `receipts.*` and `payments.*` (view/create/edit/delete/cancel/edit_all), `fund_transfers.*` (the same six), `debt_offset.view/create/cancel/delete` (no edit, no edit_all), `debt.matching`, `debt.opening_balance`, `debt.partner_summary` ("Xem công nợ khách trên phiếu xuất"), `cash.opening_balance`, `cash.catalogs.manage` (cash reasons and funds), `cash.settings` (cash policies and the numbering of the four cash document types) and `credit_limit.manage`. `reports.cash` (Sổ quỹ) sits in the report module next to `reports.debt`. `GET /api/debt/partner-summary` accepts `debt.partner_summary` or `reports.debt`.
- Default grants (P5): ADMIN and MANAGER get every new code; ACCOUNTANT gets every new code (it already had `reports.debt`); SALES gets only `debt.partner_summary`; WAREHOUSE gets nothing new, because automatic receipts and payments follow the stock voucher permissions. Codes are granted once, like the Round 1 codes.
- Deploy note (Round 2A): the deploy needs one seeder run for the new permissions and grants, the system cash reasons (`TKH`, `THNCC`, `TK`, `TNCC`, `CHKH`, `CK`, and the automatic `TPX` / `CPN`), the stock reasons `NTL` / `XTL`, the default cash fund `QTM` of every branch, and the numbering rows of `Receipt` (`PT`), `Payment` (`PC`), `FundTransfer` (`CQ`) and `DebtOffset` (`BT`). Existing test stock vouchers are not backfilled, so reset the dev and test databases after the deploy (a paid voucher whose payment method has no fund kind can no longer be saved).

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
3. locks: the branch gate (shared), then one branch-independent key per product; product and warehouse references are re-checked after the locks, because catalog deletes take the same locks;
4. period lock check (read after the gate);
5. on create, the document number from the atomic counter row; codes already issued (for example after a reset-policy change) are skipped by advancing the counter;
6. voucher header and lines with totals from `StockVoucherCalculator` (never from the client); on update, a line with the same id and product keeps its stored product snapshot (stock tracking, pricing mode, unit, VAT-inclusive price);
7. ledger replacement for the voucher (`IInventoryLedgerService`): rows rewritten, `RunningQty` and `StockBalance` recomputed from the earliest affected time;
8. negative-stock policy (`Allow` / `Warn` / `Block`), counting only pairs the operation makes worse (D31);
9. cost recalculation of the affected product scopes;
10. `Product.CostPrice` recompute from the latest active stock-in (D35);
11. activity log.

The cash and debt steps of this flow (debt locks in step 3, debt entry and automatic voucher sync, credit and fund capture and check) live in `StockVoucherCashDebt` (`IStockVoucherCashDebt`); `StockVoucherService` calls it and does not use the debt lock, ledger or guards directly.

| Operation | Steps |
|---|---|
| Create | 1 (create permission, branch), 2, 3, 4, 5, 6, 7–11 |
| Update | 1–4, 6, 7–11 |
| Cancel / Delete | 1, 3, 4 (stored date), 7 (empty list), 8–11 |
| Restore | 1, 3, 4 (stored date), 7 (stored lines unchanged; rejected if the product's stock unit or the warehouse's branch changed), 8–11 |

Opening stock (`/api/inventory/opening-stock`) follows the same flow with source type `Opening`, posted at 00:00 VN of the branch opening date (`Branch.OpeningDate`, Round 2A); saves of one warehouse are serialized by an opening key.

**Lock order (D30).** Branch gate → product keys → document counter row, always before the first `SaveChanges` (Round 2A inserts partner, fund and reason keys before the counter row; see Cash & Debt). Each set of keys is taken in one round trip in ascending key order, one advisory lock per product, so a whole-catalog opening stock grid stays within `max_locks_per_transaction`. Settings changes and manual recalculation take the branch gate **exclusive** (every branch) and no product keys, so they wait for in-flight postings and postings wait for them. Setting a period lock also takes the exclusive gate. Deleting a product takes its product key and deleting a warehouse takes the exclusive branch gate before checking for stock activity.

**Concurrency (D29).** `StockVoucher.Version` maps to PostgreSQL `xmin`. Update, cancel, restore and delete always mark the header modified, so a stale `Version` fails with 409 `CONCURRENCY` even when only lines change. The voucher DTO's `CanEdit` / `CanCancel` / `CanDelete` flags are false inside the period lock.

**Allocation (D10).** Order discount (capped at each line's net) and stock-in freight are split by line value; the rounding remainder goes to the last line with a positive base, and when that would leave a line negative or above its base the split falls back to largest remainder. The frontend preview (`compute-stock-line.ts`) implements the same rule.

**Instants (D27).** Every `DateTimeOffset` written or used as a query parameter is UTC. VN-date rules (period lock, costing period, numbering period, report dates) are UTC ranges built with `VnTime.StartOfDay/StartOfNextDay`.

**Derived data.** `InventoryLedgerEntry`, `InventoryCostPeriod`, `StockBalance` and `DocumentCounter` do not inherit `BaseEntity`; they are hard-deleted and rewritten. Ledger posting order is `PostedAt, SourceType (Opening < StockIn < StockOut), SourceCode, LineSortOrder, Id`.

**Costing.** Periodic weighted average per period (month/quarter/year) and scope (branch or warehouse). The opening of a run is summed from the ledger before the period (D7); a zero closing quantity pushes the rounding residual to the last outbound row (D8); a negative average uses the previous average (D37). Fully locked periods are frozen; a partially locked period is recomputed. Changing the costing period, scope or `PurchaseCostIncludesVat` recalculates every branch from the first unlocked period (D11, D33). Report values follow the costing scope (D32); values of a period that has not ended are flagged provisional.

**Error codes.** 422 `NEGATIVE_STOCK_WARNING` (resend with `acknowledgeNegativeStock: true`) and `NEGATIVE_STOCK_BLOCKED`, with `details` keyed `"{productCode}@{warehouseCode}"`; 409 `CONCURRENCY`; 409 `DUPLICATE` (unique index violation); 400 `PERIOD_LOCKED`; 400 `VALIDATION` with camelCase keys such as `lines[0].width`.

### Cash & Debt (Round 2A)

Receipts and payments (Phiếu thu / Phiếu chi), funds (Quỹ) with opening balances, fund transfers (Chuyển quỹ), the receivable/payable ledger, matching (Đối trừ chứng từ), debt offsets (Bù trừ công nợ), opening debts (Nợ đầu kỳ), credit limits (Hạn mức nợ) and the reports Số dư công nợ, Sổ chi tiết công nợ and Sổ quỹ. There is no chart of accounts. Everything is scoped to the working branch except the cash reasons and the singleton `CashDebtSettings` (Id = 1: `CreditLimitPolicy` and `NegativeFundPolicy`, each `Allow` / `Warn` / `Block`, default `Warn`). Code lives in `Application/CashDebt/<Feature>`, with shared pieces in `Application/CashDebt/{Common,Interfaces,Ledger,Guards,AutoVouchers}`.

**Data model.** The ledger has two derived tables. Like the inventory ledger they do not inherit `BaseEntity`, have no soft delete and are hard-deleted:

- `DebtEntry`: one side of a document's effect on a partner's debt (`Side` Receivable / Payable, `Direction` Increase / Decrease, `Amount`, `PostedAt` UTC, `DocDate` VN date, `DueDate` on Increase rows only, `OwnerUserId` of the source, `DocCode`, `Description`). There is at most one entry per source × side (unique `SourceType, SourceId, Side`). Each save of the source updates its entry **in place**, so the entry id and its settlements survive edits.
- `DebtSettlement`: matches part of an Increase entry against a Decrease entry of the same branch, partner and side (`Amount`, `Origin`, `BatchId`, `EffectiveAt` = the later `PostedAt` of the two entries). Σ settlements never exceeds either entry's amount.

A balance is Σ Increase − Σ Decrease. Settlements move no balance: they only say which documents are still open (open items, due amounts, overdue). `CashDocumentActivity` is the one append-only activity table of receipts, payments, fund transfers and debt offsets. The document entities (`CashVoucher` + `CashVoucherLine`, `FundTransfer`, `DebtOffsetVoucher`, `OpeningDebt`, `Fund`, `CashReason`, `CreditLimit`) inherit `BaseEntity` and soft-delete. Receipts, payments, fund transfers and offsets have `DocumentStatus` `Active` / `Cancelled` and an `xmin` version (D29).

**Posting sources.** All of them write through `IDebtLedgerService` (`PostAsync` on save, `RemoveSourceAsync` on cancel/delete):

| Source (`DebtSourceType`) | Entry | Notes |
|---|---|---|
| Stock voucher (`StockIn` / `StockOut`) | The side and direction of `StockReason.DebtEffect`, Amount = `Total`; none when `DebtEffect = None` or `Total = 0` | Seeds: `XBH` R+, `NMH` P+, `NTL` (customer return) R−, `XTL` (return to supplier) P−, `NKH` / `XKH` none. `DueDate` defaults to VN date + `Customer.CreditDays` on Increase reasons (P27). |
| Manual receipt / payment (`Receipt` / `Payment`) | The side and direction of `CashReason.DebtEffect`, Amount = Σ lines | A receipt allows `None` / R− / P+, a payment `None` / P− / R+ (P19). Allocation at voucher time creates `AtVoucher` settlements. |
| Automatic receipt / payment (`Receipt` / `Payment`, `SourceStockVoucherId` set) | The stock entry's side, opposite direction, Amount = `PaidAmount`; none when the stock reason has `DebtEffect = None` (the money still moves) | Created, updated, cancelled or soft-deleted by the stock voucher (P20, P3); reasons `TPX` / `CPN`. Paid amount back to 0 soft-deletes it and its number is never reused. A direct edit, cancel or delete returns 409 `MANAGED_BY_STOCK_VOUCHER`. |
| Opening debt (`Opening`) | `Debt` → Increase, `Prepayment` → Decrease, posted at 00:00 VN of the branch opening date | One row per legacy document, with the optional `OwnerUserId` typed on the row. |
| Debt offset (`Offset`) | Two Decrease entries (Receivable and Payable) of one partner that is both customer and supplier | Create, view, cancel and delete only (P23). Its items become `Offset` settlements. |

**Settlement origins (P21, P22).**

| Origin | Created by | Managed by |
|---|---|---|
| `AtVoucher` | The allocation grid of a receipt or payment (`BatchId` = that voucher's id) | The voucher that made them: an edit with an allocation list silently replaces its own `AtVoucher` settlements (`null` keeps them). Another voucher's `AtVoucher` settlement against it (a refund allocated against a prepayment) is treated like a `Manual` one. They can also be removed on the matching screen. |
| `Manual` | The matching screen (`POST /api/debt/settlements`, `debt.matching`) | The matching screen (`DELETE /api/debt/settlements/{id}`, `DELETE /api/debt/settlement-batches/{batchId}`) |
| `Auto` | The stock voucher, between its own entry and its automatic voucher's entry: `min(PaidAmount, stock entry open excluding Auto, automatic entry open excluding Auto)` | The stock voucher. The matching screen returns 409 `SETTLEMENT_MANAGED`. |
| `Offset` | The debt offset voucher | Cancelling or deleting the offset (409 `SETTLEMENT_MANAGED` on the matching screen) |

A caller first removes its own `AtVoucher` or `Auto` settlements (`RemoveOwnSettlementsAsync`: the settlements of that origin whose `BatchId` is the caller's document), without confirmation or activity. Every other settlement that a save would drop is subject to the confirmation and blocked-edit rules below, and its removal writes `SettlementRemoved` on both documents (`StockVoucherActivityAction.SettlementRemoved = 6` on stock vouchers, which also log `AutoVoucherChanged = 7` when their automatic voucher is created or deleted).

`IAutoCashVoucherService.RecomputeAutoAsync` sets the `Auto` settlement of every affected stock voucher back to the formula above whenever foreign settlements are removed. The ledger records both entries of every non-`Auto` settlement it removes in the scoped `SettlementChangeCollector`, and `AutoRecomputeHook` (an `IBeforeCommitHook`) runs the recompute just before the outermost commit in `EfTransactionRunner`, so a new write path needs no call of its own. Only the outermost `ITransactionRunner.RunAsync` runs the hooks: a ledger write under `RunSnapshotAsync`, or in a transaction opened outside the runner, skips the recompute. The removals covered are: cash voucher edit, cancel and delete, opening debt edit and delete, the stock voucher paths, manual unmatching, and offset cancel and delete. The freed room goes back to the paid amount. An `Auto` settlement dated on or before `LockedUntil` is left unchanged, so the open-period write still goes through. When `PUT /api/branches/{id}/lock` moves the lock earlier or clears it, `RecomputeReopenedAsync` recomputes the `Auto` settlements of the reopened days under the exclusive branch gate, without writing an activity. A `lockedUntil` in year 9999 is rejected with 400 `VALIDATION` (key `lockedUntil`), because the next day overflows and such a lock could never be moved earlier or cleared.

Another document may strip an offset's `Offset` settlements (a cancelled or shrunk sale, confirmed by the user). The offset then stays Active, and its amount may exceed the sum of its items. Offset cancel and delete remove the offset's own `Offset` settlements silently, but honour `confirmRemoveSettlements` for any other settlement made later on its entries.

**Confirmations (P10).** `ConfirmationRequiredException` maps to 422 with `code`, `message` and `details`. The client resends with the flag and keeps the flags it already confirmed. When several apply, the server reports them in this order:

1. `NEGATIVE_STOCK_WARNING` / `NEGATIVE_STOCK_BLOCKED` (`acknowledgeNegativeStock`, Round 1);
2. `DEBT_SETTLEMENTS_WILL_BE_REMOVED` (`confirmRemoveSettlements`, always confirmable): a save that shrinks a settled entry, or a cancel/delete that removes it, drops other documents' settlements. `details` are keyed by the other document's code. The error carries `token`, the ids of the listed settlements (distinct, sorted, `N` format, comma-separated; `null` on every other error); the client resends it as `confirmedSettlements` with the flag. The ledger accepts the confirmation when the token is absent (accept-all) or covers every settlement it would remove, and otherwise re-asks with a new 422 listing the current ones. Merged 422s (stock voucher plus automatic voucher) union their tokens, and `StockVoucherCashDebt` runs the stock step unconfirmed first so a stale token converges. On a stock voucher, the first 422 lists both the stock entry's and the automatic entry's foreign settlements;
3. `CREDIT_LIMIT_WARNING` / `CREDIT_LIMIT_BLOCKED` (`acknowledgeCreditLimit`): checked only on stock voucher saves and restores whose reason is `ReceivableIncrease`, comparing the customer's balance before and after against `CreditLimit` (customer × branch). Customer-return (`NTL`) paths that also raise the receivable (cancel, delete, a lower total, a higher paid amount) are not checked; this is an open BA question;
4. `NEGATIVE_FUND_WARNING` / `NEGATIVE_FUND_BLOCKED` (`acknowledgeNegativeFund`).

The 422 body does not serialize `CanConfirm`. The frontend treats the `_BLOCKED` codes as not confirmable.

**Blocked edits (P11).** A save that changes the partner, branch, side or direction of an entry that still has settlements, or that removes that side (`DebtEffect` becomes `None`, or the total becomes 0), returns 409 `DEBT_HAS_SETTLEMENTS` ("hãy bỏ đối trừ trước"). Removing an entry on cancel or delete asks `DEBT_SETTLEMENTS_WILL_BE_REMOVED` instead.

**Locks (P14).** Every cash or debt write takes the shared branch gate (`IInventoryLock.AcquireBranchGateAsync(exclusive: false)`), so it serialises with `SetLockAsync` and with the opening-date change, which take it exclusive. The global order is:

1. branch gate;
2. product keys (stock vouchers only);
3. partner keys `debt-partner:{branchId:N}:{partnerId:N}`;
4. fund keys `cash-fund:{fundId:N}`;
5. reason keys: `cash-reason:{id:N}`, shared on cash voucher saves and exclusive on a cash reason edit or delete; `stock-reason:{id:N}`, shared on stock voucher saves and restores and exclusive on a stock reason edit or delete. This way a reason's in-use check sees every voucher saved with it;
6. document counter row.

Each key set is taken in one statement, ordered `COLLATE "C"` (shared helper `AdvisoryLocks`). A write takes the partner and fund keys of both the old and the new state. Settings, `LockedUntil`, `OpeningDate`, credit limits, balances, fund data and reasons are read **after** the locks, and the stock voucher re-checks its reason and fund there.

**Funds (P17, P18).** A fund (`Cash` or `Bank`) belongs to one branch, with a code unique per branch. There is exactly one default fund per branch × kind, enforced by a partial unique index (migration `AddFundDefaultUniqueIndex`). A default fund cannot be deactivated. A referenced fund, or one with `OpeningAmount ≠ 0`, cannot be deleted or change kind (409). Every branch gets cash fund `QTM`. A fund's running balance is `OpeningAmount` first (before every document, whatever its date), then Active, non-deleted receipts (+Total) and payments (−Total), automatic ones included, and fund transfers (−Amount on `FromFundId`, +Amount on `ToFundId`). Movements are ordered by `VoucherAt`, then inflows before outflows, then code. As in D31, the negative-fund check flags only when the new minimum from the affected instant is below 0 and lower than before, or when the first negative point moves earlier. On a stock voucher, `PaidAmount > 0` needs a payment method with `FundKind ≠ None`, and `FundId` defaults to the branch's default fund of that kind (P9).

**Opening data (P8).** `Branch.OpeningDate` dates the opening stock, opening debts and fund opening balances. Saving any of them without it returns 400 `OPENING_DATE_NOT_SET`. `PUT /api/branches/{id}/opening-date` works only while the branch has no non-deleted stock voucher, cash voucher, fund transfer or debt offset (409 `BRANCH_HAS_DOCUMENTS`), and both dates must be after `LockedUntil`. One exception: a branch that never had an opening date may take its first one after documents exist, on or before the VN date of its earliest document (400 `openingDate` otherwise), because it has no opening data to re-post. Once the date is set, stock vouchers, cash vouchers, fund transfers and debt offsets dated before it are rejected (`OpeningDateGuard`, 400 `voucherAt`), so opening balances always come first. A branch with documents or opening data cannot be deleted (409). Clearing the date is refused while opening stock, opening debts or a fund with `OpeningAmount ≠ 0` exists (409 `OPENING_DATA_EXISTS`). A change re-posts the opening stock and moves the opening debt entries and their settlements.

**Reports.** `GET /api/reports/debt-balance` (Số dư công nợ) and `GET /api/reports/debt-ledger` (Sổ chi tiết công nợ) need `reports.debt`; `GET /api/reports/cash-book` (Sổ quỹ) needs `reports.cash`.

- **Window rule (P29):** in every report window, `Opening` entries and fund `OpeningAmount` count as the opening balance and never as period movements, even when the report starts on the opening date.
- **Balance sign (P24):** a positive receivable shows as Dư Nợ and a negative one as Dư Có; a positive payable shows as Dư Có and a negative one as Dư Nợ.
- **Cash book with several funds (P25):** each fund group shows its transfer rows, so its running balance reconciles. In the grand total, a transfer whose two funds are both selected is left out, because the two halves cancel; a transfer with only one selected fund counts.

Other read endpoints: `GET /api/debt/partner-summary` (receivable balance, credit limit, available amount and overdue amount, shown on the stock-out form), `GET /api/debt/open-items` (with `forSourceType` / `forSourceId`, the open amount adds back the edited voucher's own `AtVoucher` settlements, the ones its save replaces, P26) and `GET /api/debt/partners`.

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
| `ConflictException` (code `CONFLICT`, or a specific code: `DEBT_HAS_SETTLEMENTS`, `MANAGED_BY_STOCK_VOUCHER`, `SETTLEMENT_MANAGED`, `BRANCH_HAS_DOCUMENTS`, `OPENING_DATA_EXISTS`) | 409 |
| `DbUpdateConcurrencyException` (code `CONCURRENCY`) | 409 |
| `DbUpdateException` from a PostgreSQL unique violation `23505` (code `DUPLICATE`) | 409 |
| `NegativeStockException` (`NEGATIVE_STOCK_WARNING` / `NEGATIVE_STOCK_BLOCKED`) | 422 |
| `ConfirmationRequiredException` (`DEBT_SETTLEMENTS_WILL_BE_REMOVED`, `CREDIT_LIMIT_WARNING` / `CREDIT_LIMIT_BLOCKED`, `NEGATIVE_FUND_WARNING` / `NEGATIVE_FUND_BLOCKED`) | 422 |
| Rate-limit rejection | 429 |
| Unhandled exceptions | 500 |

A 422 is a confirmation request: the error carries `code`, `message` and `details`, and the client resends the request with the matching flag (`acknowledgeNegativeStock`, `confirmRemoveSettlements`, `acknowledgeCreditLimit`, `acknowledgeNegativeFund`). The `_BLOCKED` codes cannot be confirmed. `DomainException` codes returned as 400 include `PERIOD_LOCKED` and `OPENING_DATE_NOT_SET`.

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
- `OrderMgmt.Infrastructure/Persistence/Migrations`: quotation owner, owner history, user quotation settings, system branding and notifications, then the Round 1 inventory migrations (`AddBranches` … `AddStockVouchersAndLedger`) and the Round 2A cash & debt migrations: `AddCashDebtSettings`, `AddBranchOpeningDate`, `AddCashReasons`, `AddFunds`, `AddFundDefaultUniqueIndex` (one default fund per branch × kind; not in the original plan), `AddPaymentMethodFundKind`, `AddStockReasonDebtEffect`, `AddCustomerCreditDaysAndCreditLimits`, `AddDebtLedger`, `AddOpeningDebts`, `AddCashVouchers`, `AddFundTransfers`, `AddStockVoucherDebtFields`, `AddDebtOffsets`, `AddOpeningDebtVersion` (the `xmin` version of opening debts, added after the review). The model snapshot stays in `OrderMgmt.Infrastructure/Migrations` and is updated in place.

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

The working branch lives in `stores/branch-store.ts` and is sent as `X-Branch-Id` on every request. `AppLayout` renders pages only after `useBranchContext` has resolved the branch, so no page fetches with the wrong branch. Switching branch (`useSwitchWorkingBranch`) clears the service-worker API cache and resets the query cache, and `AppLayout` keys the routed page by the working branch so page state from the previous branch is discarded. Inventory queries live under the `['inventory']` root key, and branch-scoped cash and debt queries (cash vouchers, funds, fund transfers, open items, settlements, offsets, opening debts, credit limits, partner summary, reports) under `['cash']` (`features/cash-common/keys.ts`, P28). Shared catalogs and singletons keep their own roots (`['cash-reasons']`, `['cash-settings']`). Stock voucher mutations invalidate both `['inventory']` and `['cash']`. Cash voucher, settlement, debt offset and opening debt mutations invalidate `['cash']` and `['inventory']`, because stock voucher lists show settlement-driven state; fund, fund transfer and credit limit mutations stay within `['cash']`. The service worker never caches branch- or user-scoped API data (`lib/sw-routes.ts`; no cash path is added there), and logout or a failed refresh clears its API cache.

Confirmation flow (P10): `lib/confirm-required.ts` turns a 422 with one of the seven known codes into a `ConfirmationRequest` (`toConfirmationRequest`). Because the body has no `canConfirm`, a code without a resend flag (the `_BLOCKED` codes) is marked blocked. `withConfirmed` adds the confirmed flag to the flags already sent. `components/confirm-required-dialog` shows the request and its `details` items. The settlement token travels with the flags (`confirmedSettlements`). The stock voucher, cash voucher, fund transfer and debt offset forms share this flow in `hooks/useDocumentActions.ts` (busy state, the 422 chain, the 409 reload, validation and generic toasts); the opening debt and fund opening balance dialogs call `toConfirmationRequest` / `withConfirmed` directly. Both resend with the accumulated flags until the save succeeds or a blocked code stops it.
