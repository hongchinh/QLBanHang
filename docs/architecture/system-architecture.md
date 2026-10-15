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

The product is quotation-first. The quotation is the main sales document; revenue is counted when a quotation reaches `Confirmed`. Orders and delivery are outside the scope. Inventory (Round 1: branches, stock vouchers, ledger, costing and reports) and cash & debt (Round 2A: receipts and payments, funds and fund transfers, the receivable/payable ledger, matching, debt offsets, credit limits and the reports Số dư công nợ, Sổ chi tiết công nợ and Sổ quỹ) are implemented and go live after Round 3. The debt reports by document, aging, overdue, by employee and the reconciliation record (Round 2B) are implemented too. The cash, stock and debt reports export as Excel and PDF in the layout of Circular 133/2016/TT-BTC (see TT133 Report Forms). The executive dashboard (`/dashboard/executive`) shows sales from stock-out vouchers, cash, debt and inventory on one page for directors and managers and refreshes after every document save (see Executive Dashboard). A new branch can be created as a demo branch that a background job fills with sample data for training; company-wide figures leave demo branches out (see Demo Branches And Sample Data).

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

Domain stays dependency-free. Application depends on Domain and defines ports such as `IAppDbContext`, `ICurrentUser`, `IActingIdentity` (the user and branch of background work), `IDateTime`, `IJwtTokenGenerator`, `IPasswordHasher` and `IRefreshTokenService`. Infrastructure implements those ports. WebApi wires the graph through DI.

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
- Cash & debt permissions form the module `cash` (shown as "Tiền & Công nợ" in the role matrix): `receipts.*` and `payments.*` (view/create/edit/delete/cancel/edit_all), `fund_transfers.*` (the same six), `debt_offset.view/create/cancel/delete` (no edit, no edit_all), `debt.matching`, `debt.opening_balance`, `debt.partner_summary` ("Xem công nợ đối tượng trên phiếu nhập/xuất") (an existing database keeps the old stored name; the debt strip now shows on both stock-in and stock-out forms), `cash.opening_balance`, `cash.catalogs.manage` (cash reasons and funds), `cash.settings` (cash policies and the numbering of the four cash document types) and `credit_limit.manage`. `reports.cash` (Sổ quỹ) sits in the report module next to `reports.debt`. `GET /api/debt/partner-summary` accepts `debt.partner_summary` or `reports.debt`.
- Default grants (P5): ADMIN and MANAGER get every new code; ACCOUNTANT gets every new code (it already had `reports.debt`); SALES gets only `debt.partner_summary`; WAREHOUSE gets nothing new, because automatic receipts and payments follow the stock voucher permissions. Codes are granted once, like the Round 1 codes.
- `settings.company` ("Thông tin đơn vị", in the system module) guards `PUT /api/settings/company`; the GET needs only a signed-in user, because every report page reads the profile to warn about an empty company name. ADMIN and MANAGER get it with every other code and it is in the default list of ACCOUNTANT, who fills in the tax code and the signers; it is granted once, like the other new codes. The TT133 exports have no permission of their own: each route carries the permission of its source report (`reports.cash`, `reports.debt`, `reports.inventory`), S06 also needs `inventory.view_cost`, and the company scope needs `branches.access_all`.
- `dashboard.executive` ("Bảng điều hành", in the report module) opens `/dashboard/executive` and guards every route of `ExecutiveDashboardController`. Each route also needs the permission of its source report: sales `reports.profit`, cash `reports.cash`, debt `reports.debt`, inventory `reports.inventory`; the branch comparison needs `branches.access_all` instead. Cost, gross profit, margin and stock value are `null` without `inventory.view_cost`. The attention list and the recent documents need nothing more and return only the parts the user may see. ADMIN and MANAGER get the code (they hold every code); ACCOUNTANT does not get it by default. It is granted once, like the other new codes, and arrives in the JWT claims at the next sign-in.
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
  export it as PNG. The backend does not generate a QR image for this screen; the only QR pictures it
  makes are the ones on the printed vouchers (see "Payment QR on receipts and stock-outs" under the
  TT133 voucher print forms).
- `UserBankAccount` records a user's own saved receiving accounts, CRUD'd under
  `/api/me/bank-accounts` and always scoped to `ICurrentUser.UserId`. The first saved account
  becomes the default automatically; creating another as default (or `PUT .../default`) unsets the
  previous default; deleting the current default promotes another remaining account.
- All Payments endpoints only require `[Authorize]` — no dedicated permission constants.
- The quotation detail page links into the QR page with the quotation's total/code pre-filled via
  query params (`amount`, `content`); a user's default saved account pre-fills the bank/account
  fields when one exists.

### Inventory (Round 1)

**Working branch.** Every user has a default branch. The frontend sends the chosen branch in `X-Branch-Id`; `ICurrentBranch` honours it only for users with `branches.access_all` and otherwise uses the default branch; background work without a request uses the branch of its acting identity (see Demo Branches And Sample Data). Warehouses, vouchers, opening stock, numbering, period locks and reports are scoped to the working branch; quotations and catalogs are shared.

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

**Reports.** `GET /api/reports/debt-balance` (Số dư công nợ) and `GET /api/reports/debt-ledger` (Sổ chi tiết công nợ) need `reports.debt`; `GET /api/reports/cash-book` (Sổ quỹ) needs `reports.cash`. The debt balance, the debt ledger and the reconciliation record take `scope` (see TT133 Report Forms). Cash book rows carry `ReasonName`; debt ledger rows carry `ReasonName`, `DueDate` and, in company scope, `BranchCode`.

- **Window rule (P29):** in every report window, `Opening` entries and fund `OpeningAmount` count as the opening balance and never as period movements, even when the report starts on the opening date.
- **Balance sign (P24):** a positive receivable shows as Dư Nợ and a negative one as Dư Có; a positive payable shows as Dư Có and a negative one as Dư Nợ.
- **Cash book with several funds (P25):** each fund group shows its transfer rows, so its running balance reconciles. In the grand total, a transfer whose two funds are both selected is left out, because the two halves cancel; a transfer with only one selected fund counts.

**Debt position reports (Round 2B).** `DebtPositionReportsController` (route `api/reports`, `reports.debt`) calls `IDebtPositionReportService` (`Application/CashDebt/Reports/Services/DebtPositionReportService`): `GET /api/reports/debt-documents` (open amount per document), `debt-aging` and `debt-overdue` (both take `buckets`, default `30,60,90`) and `debt-by-salesperson` (grouped by `DebtEntry.OwnerUserId`; `Side` defaults to Receivable). `GET /api/reports/debt-reconciliation` (Biên bản đối chiếu of one partner, both sides plus the net balance) lives on `CashDebtReportsController` and `CashDebtReportService`, because it reuses `BuildDebtLedgerAsync` in the same snapshot. Every report runs in `ITransactionRunner.RunSnapshotAsync` and returns zero-filled arrays when there are no rows (B12).

- **Shared loader:** `DebtPositionQuery` (internal static) loads the entries and settlements of one branch for all four position reports; `DebtAging` (`Application/CashDebt/Common`, pure) parses and applies the bucket boundaries.
- **"As at D" rule (B3):** an entry counts at D when `PostedAt < StartOfNextDay(D)` or its source is `Opening` (P29). A settlement counts at D only when both of its entries count at D. The open amount of an entry at D is its amount minus the settlements that count at D. Hence documents, aging and by-employee totals equal the Số dư công nợ closing balance (B4).
- **Unallocated (B6):** open Decrease entries (prepayments, unmatched receipts and returns). Documents and aging subtract them; the overdue report only shows them next to the partner total.
- `GET /api/opening-debts/owners` (`debt.opening_balance`) lists the active users offered as the owner of an opening debt; `/admin/users` needs `users.view`, which the opening-debt screen does not require.
- Frontend: pages `cash-reports/debt-documents-page`, `debt-aging-page`, `debt-overdue-page`, `debt-by-salesperson-page` and `debt-reconciliation-page` under routes `/reports/debt-documents`, `/reports/debt-aging`, `/reports/debt-overdue`, `/reports/debt-by-salesperson` and `/reports/debt-reconciliation`, with five nav items (permission `reports.debt`).

Other read endpoints: `GET /api/debt/partner-summary` (receivable and payable balance, credit limit, available amount and overdue amount, shown in the debt strip under the totals bar of the stock in/out form), `GET /api/debt/open-items` (with `forSourceType` / `forSourceId`, the open amount adds back the edited voucher's own `AtVoucher` settlements, the ones its save replaces, P26) and `GET /api/debt/partners`.

### TT133 Report Forms

Eight forms in the layout of Circular 133/2016/TT-BTC (Phụ lục 4), as Excel and PDF. Each is a presentation of a report the screens already show, so the file equals the screen. There is no chart of accounts: the "TK đối ứng" column of the official forms is replaced by "Lý do". Decisions and layouts: [TT133 report forms plan](../plans/archived/261013-1926-tt133-report-forms/SUMMARY.md).

| Form | Source report | Export route (`/api/reports/…`) | Permission |
|---|---|---|---|
| S04a-DNN Sổ quỹ tiền mặt / S05-DNN Sổ tiền gửi ngân hàng | `cash-book` (`kind=Cash` / `kind=Bank`) | `GET cash-book/export` | `reports.cash` |
| S12-DNN Sổ chi tiết thanh toán với người mua (người bán) | `debt-ledger` | `POST debt-ledger/export` | `reports.debt` |
| Bảng tổng hợp chi tiết thanh toán | `debt-balance` | `GET debt-balance/export` (a `partnerId` is refused with 400) | `reports.debt` |
| Biên bản đối chiếu công nợ | `debt-reconciliation` | `GET debt-reconciliation/export` | `reports.debt` |
| S07-DNN Bảng tổng hợp chi tiết vật liệu, dụng cụ, sản phẩm, hàng hóa | `stock-summary` | `GET stock-summary/export` | `reports.inventory` |
| S08-DNN Thẻ kho | `stock-card` (quantities) | `POST stock-card/export` | `reports.inventory` |
| S06-DNN Sổ chi tiết vật liệu, dụng cụ, sản phẩm, hàng hóa | `stock-card` (with values) | `POST stock-detail/export` | `reports.inventory` and `inventory.view_cost` (checked in the service) |

**Export flow.** `Tt133ExportController` (route `api/reports`, one literal action per report, `format=xlsx|pdf` in the query) validates the request and calls `ITt133ExportService` (`Application/Tt133/Services/Tt133ExportService`):

1. scope and cost permission checks (`ReportScopeGuard`, `inventory.view_cost` for S06);
2. one `ITransactionRunner.RunSnapshotAsync` for the data of the export: the form context (`Tt133FormContext`: company profile, working branch, opening date, signers, print date), the targets, and one call of the existing report service per sheet (`ICashDebtReportService`, `IInventoryReportService`). The report services' own snapshots join the open one, so the target list and every sheet read the same state. `EnsureSheetCount` runs here;
3. after the snapshot is closed, `ITt133FormRenderer` (`Infrastructure/Excel/Tt133/Tt133FormRenderer`) builds the workbook with ClosedXML: one form class per form on top of `Tt133FormBuilder`, one sheet per fund, partner or product × warehouse;
4. for `format=pdf`, `ISpreadsheetPdfConverter` (LibreOffice, the converter of the quotation exports) converts the workbook; a failure becomes 503 `PDF_EXPORT_FAILED`. The conversion is synchronous and limited by `QuotationExport:ConversionTimeoutSeconds`; each conversion runs `soffice` with its own profile directory, so conversions can run at the same time, and when the wait is cancelled (timeout or request abort) the converter kills the `soffice` process tree;
5. the file is returned with the name `{code}_{from:yyyyMMdd}_{to:yyyyMMdd}.{xlsx|pdf}` (`S04a-DNN`, `S05-DNN`, `S06-DNN`, `S07-DNN`, `S08-DNN`, `S12-DNN`, `TongHopCongNo`, `DoiChieuCongNo_{PartnerCode}`).

**Targets and limits.** The three multi-target forms take their request as a JSON body: `partnerIds` or `all` for S12, `productIds` (with an optional `warehouseId`) or `all` for S06 and S08; `all` means every target with activity in the period. An export without a sheet returns 422 `EXPORT_NO_TARGETS`, and one with more sheets than `Tt133Export:MaxSheets` (default 300) returns 422 `EXPORT_TOO_MANY_SHEETS`. Each sheet costs one call of the source report (N+1 by design). Measured in Phase 05 of the plan on 300 generated sheets of 20 rows: the workbook takes about 3 s and the PDF conversion in the backend image 19 to 61 s (S06 is the slowest), which is why the conversion timeout is 130 s; the client waits 120 s. Each conversion uses its own LibreOffice profile directory: on the shared default profile about one in three conversions started together failed (risk R2 of the plan), and with separate profiles nine parallel conversions succeeded.

**Scope.** `ReportScope` (`Branch`, the default, or `Company`) is a parameter of `debt-balance`, `debt-ledger`, `debt-reconciliation` and `stock-summary`, on the screen report and on its export. `Company` drops the working-branch filter and skips demo branches, and nothing else (settlements stay inside a branch), needs `branches.access_all` (`ReportScopeGuard`, 403 otherwise) and cannot be combined with a warehouse filter on the stock summary. The cash book, S06 and S08 always read the working branch. The form header prints `{company}` for `Company` and `{company} – {branch}` for `Branch`; "Ngày mở sổ" is the branch's opening date, or the earliest opening date of any non-demo branch for `Company`. Document numbers repeat across branches, so company-scope ledger rows carry `BranchCode`, shown as `[code]` in front of "Diễn giải" on the screen, on S12 and on the reconciliation record. A summary exported with a search text prints it as a "Điều kiện lọc" line.

**Stock summary (S07).** `GET /api/reports/stock-summary` (`reports.inventory`, `InventoryReportsController`) returns opening, in, out and closing quantity and value per product for a period, filtered by warehouse, product group and search text. It uses the stock card's window rule: a ledger row before the start of `from` is opening, anything else is in the period, so opening stock posted on the opening date appears under "Nhập trong kỳ" in the first period. Values need `inventory.view_cost` and are left out for a warehouse filter under branch costing (`ValuesAtScopeOnly`, D32).

**Company profile.** `CompanyProfile` is one row (`Id = 1`, inserted empty by the migration `AddCompanyProfile`; it does not inherit `BaseEntity` and every text field is nullable): name, address, tax code, chief accountant, legal representative and an optional accounting-service note. It feeds the header block and the signature block of every form. An empty profile does not block an export; the header lines stay empty.

**Voucher print forms.** The four vouchers the software records are also printed in the layout of Circular 133/2016/TT-BTC (Phụ lục 3), as Excel and PDF, one voucher per sheet / page: 01-TT Phiếu thu and 02-TT Phiếu chi (`CashVoucher`, A5 landscape), 01-VT Phiếu nhập kho and 02-VT Phiếu xuất kho (`StockVoucher`, A4 portrait, table header repeated on every page). Routes: `GET api/cash-vouchers/{id}/print` and `POST api/cash-vouchers/print` (`CashVouchersController`), `GET api/stock-vouchers/{id}/print?priceMode=` and `POST api/stock-vouchers/print` (`StockVouchersController`), with `format=xlsx|pdf`; a batch goes by POST because the id list does not fit a URL, and the POST actions carry `[SkipDashboardSignal]` because they change nothing. The flow:

1. the controller calls `IVoucherPrintService` (`Application/Tt133/Vouchers/VoucherPrintService`). Checks run per id in request order, as the voucher page does: a missing or deleted voucher is 404, a missing view permission of the voucher's type (`receipts.view`, `payments.view`, `stock_in.view`, `stock_out.view`) is 403, a voucher of another branch than the working branch is 403 "Phiếu thuộc chi nhánh khác."; the first failing id fails the whole request. Before any read, an undefined `priceMode` is 400 and `Cost` without `inventory.view_cost` is 403;
2. `Tt133ExportSupport.EnsureSheetCount` refuses an empty list (422 `EXPORT_NO_TARGETS`) and more vouchers than `Tt133Export:MaxSheets` (300; 422 `EXPORT_TOO_MANY_SHEETS`, "Số phiếu cần in (…) vượt giới hạn …");
3. one `ITransactionRunner.RunSnapshotAsync` loads the form context, the vouchers (soft-deleted partners, reasons, warehouses and owners keep their names, lines removed by an edit are not printed), the inventory ledger rows for `Cost` and the inventory settings, and turns them into flat print models with the fallbacks, price-mode rows, spec text and warehouses already applied;
4. after the snapshot is closed, `IVoucherFormRenderer` (`Infrastructure/Excel/Tt133/Vouchers/VoucherFormRenderer`) builds the workbook with ClosedXML (`CashVoucherForm` / `StockVoucherForm` on `VoucherFormBuilder`), sheets in `VoucherAt` then `Code` order; for `format=pdf` the same LibreOffice converter as the other exports runs (failure: 503 `PDF_EXPORT_FAILED`). The file is `{Code}.{xlsx|pdf}` for one voucher and `PhieuThu_`, `PhieuChi_`, `PhieuNhapKho_` or `PhieuXuatKho_` plus `{yyyyMMdd-HHmm}` for several.

A stock voucher prints in one of three price modes (`StockPrintPriceMode`), chosen in the print dialog: `Voucher` (the voucher's own prices; the rows under the table add up to the voucher total, including the VAT that was really added to it), `Cost` (the cost of each tracked line read from its stock ledger row, so the print equals the stock card; needs `inventory.view_cost`; a cancelled voucher has no ledger rows and prints as `None` with a note) and `None` (no money). The company profile fills the unit block and the signers; an empty profile does not block a print. The frontend adds `VoucherPrintButton` to the voucher pages (enabled for a saved voucher without unsaved changes) and to the receipt, payment and stock voucher lists (a button per row, and a checkbox column with a "Đã chọn N phiếu" bar for the voucher type on the page).

**Payment QR on receipts and stock-outs.** A receipt (01-TT, not a payment) and a stock-out for a sale (02-VT, reason with `DebtEffect = ReceivableIncrease`) can carry a VietQR transfer QR, on paper and on the voucher page, so the customer scans instead of typing the account, amount and content. It reuses `VietQrPayloadBuilder` (see Payments / VietQR) and, like it, only creates the QR: nothing confirms the transfer. One service, `VoucherPaymentQrService` (`Application/CashDebt/PaymentQr`, rules in the pure `VoucherPaymentQrRules`), answers both the screen and the paper:

- Screen: the voucher page's "QR chuyển khoản" button opens `PaymentQrDialog`, which calls `GET api/cash-vouchers/{id}/payment-qr` or `GET api/stock-vouchers/{id}/payment-qr` (every time it opens, never cached). The service checks the voucher (404), the view permission of its type (`receipts.view` or `stock_out.view`; a payment or stock-in id is also checked against its own type, then refused as not applicable) and the working branch (403 "Phiếu thuộc chi nhánh khác."), applies the rules and returns `VoucherPaymentQrDto` (payload, amount, total, cash already paid, bank, account number, holder, content, receiving fund, source stock-out code). `qrcode.react` draws the payload client-side. A voucher that gets no QR is 422 `PAYMENT_QR_UNAVAILABLE` with the reason in the message.
- Paper: `?includeQr=true` on `GET …/print` (`includeQr: true` in the POST body) makes `VoucherPrintService` call `ResolveManyAsync` inside the same `RunSnapshotAsync`, after its own access check, for the vouchers it loaded (stock-outs only in the `Voucher` price mode). The vouchers that get a QR carry the DTO in their print model; the others print as before. After the snapshot `VoucherFormBuilder` has `QrPngRenderer` (QRCoder, error correction M) turn the payload into a PNG and places it in the workbook: beside the lines of 01-TT, and in a "THÔNG TIN CHUYỂN KHOẢN" block before the signatures on 02-VT. The picture is sized to 25 mm on paper (never wider than its two columns, so a receipt shrunk below about 62 % prints it smaller, e.g. 19.2 mm at 48 %); because LibreOffice shrinks the sheet to fit the page, the builder computes that zoom from the finished sheet (for A5 landscape, fitted by height) and draws the picture at the real zoom (so in the sheet it is larger than 25 mm).
- Rules: the amount of a receipt is its total; of a stock-out, its total minus the paid amount that went into a cash fund (a bank fund's paid amount is still asked for). The receiving account is the voucher's own bank fund; if that fund is incomplete there is no QR (no fall-back to another account); a voucher without a bank fund (cash fund, or none) uses the branch's default bank fund. The transfer content is the voucher code, or the source stock-out code for an automatic receipt, which gets no QR at all when its fund is not a bank fund. A cancelled voucher, a zero amount and a stock-out for a reason other than a sale get none.
- Fund fields: `Fund.BankId` (references `Banks`, which holds the BIN) and `Fund.AccountHolderName`, added by the migration `AddFundBankAccount`, are kept only for a `Bank` fund and are edited on the Quỹ screen (`FundDto` also returns `BankShortName`). The old free-text `BankName` stays and still prints on the bank book form (S05); it is not matched to `Banks`. A fund is QR-ready when it is active, a bank fund, its bank is active, and it has an account number and a holder.
- Frontend memory: the "Kèm QR chuyển khoản" switch of the receipt print menu and the stock-out price dialog is kept per user in `localStorage` (`qldh:voucher-print-qr:{userId}`, on by default). The API default is `includeQr=false`.

**Frontend.** Sổ quỹ, Số dư công nợ, Sổ chi tiết công nợ, Biên bản đối chiếu, Thẻ kho and the new page Tổng hợp nhập xuất tồn (`/inventory/stock-summary`) show the "Xuất theo mẫu TT133" button (`components/tt133-export`); S12, S06 and S08 first open the target dialog. Both formats download as a blob through `features/tt133-export`. The scope select appears only for users with `branches.access_all`. Thông tin đơn vị is `/settings/company` (`settings.company`).

### Executive Dashboard

`/dashboard/executive` (Bảng điều hành) shows directors and managers the sales, cash, debt and inventory figures of the whole company or of one branch, the items that need action and the documents created today, and refreshes within a second or two after a document is saved. The module is read-only: it has no table, migration or cache of its own. The balance figures equal their reports and are tested against them; the other figures follow the reports' rules without a comparison test (see "Figures and reports" below). Its revenue comes from stock-out vouchers; the quotation dashboards (`/api/dashboard`) keep counting confirmed quotations. Design: [executive dashboard brainstorm](../brainstorms/261013-2247-executive-dashboard/SUMMARY.md); plan and execution report: [archived plan](../plans/archived/261013-2313-executive-dashboard/SUMMARY.md).

**Request flow.** `ExecutiveDashboardController` (route `api/dashboard/executive`) validates the query with the injected FluentValidation validator and calls one service of `Application/Dashboards/Executive/Services`: `SalesMetricsService`, `CashMetricsService`, `DebtMetricsService`, `InventoryMetricsService`, or `ExecutiveOverviewService`. The overview service builds the branch comparison and the attention list from per-scope methods of the four metrics services, and reads the recent documents itself, straight from `CashVouchers` and `StockVouchers` (see the `recent` row below). Every public service method:

1. runs inside one `ITransactionRunner.RunSnapshotAsync`, so all figures of a response read the same state. The overview service calls the metrics services' per-scope methods (which check no permission) inside its own snapshot;
2. resolves the scope with `IExecutiveScopeResolver` into an `ExecutiveScope` (branch ids, `IsCompanyWide`). A user with `branches.access_all` gets every branch except the demo branches when `branchId` is omitted, and the named branch otherwise, a demo branch included (404 for an unknown id). Any other user always gets the own default branch, and a `branchId` of another branch is 403;
3. builds the period with `ExecutivePeriod.Create(from, to, today)`: VN dates, by default day 1 of the month up to VN today, plus the previous period of the same length ending the day before `from`, for the deltas. Balance figures (fund balances, debt, attention) are "as at" `at`, default VN today. The UI never sends `at`; the tests do, so they do not depend on the clock;
4. reads the data of the scope's branches. Debt is read per branch and side through `DebtPositionQuery` and added up; cash loads the movements of every fund of the scope with one `FundMovementQuery` call; sales, inventory and the recent documents read all branches of the scope in one query each (`branchIds.Contains(...)`). Names of partners, products, users and branches come from `ExecutiveLookups`: separate queries by id with `IgnoreQueryFilters()`, never inside a query that reads documents.

| Domain | Read through | Rules |
|---|---|---|
| Sales | `StockVouchers` and their lines, `InventoryLedger` | A sale is an Active voucher with the system reason `XBH`, a return one with `NTL` (`StockReasonCodes.Sale` / `StockReasonCodes.SaleReturn`); a reason created by a user never counts. Revenue is before VAT: per line `Amount − DiscountAmount − OrderDiscountAllocated`, minus `VatAmount` when a stock-out line is priced VAT-inclusive; freight is not revenue. Cost is the `CostAmount` of the sale's `StockOut` ledger rows; a return reduces it by the `InValue` of its `StockIn` rows. A return counts negative on revenue and cost. Cost and gross profit are provisional (`CostProvisional`) while the costing period holding the last day of the range has not ended. |
| Cash | `FundMovementQuery` | A fund's balance at `at` is its opening amount plus every movement before the end of `at`, as on Sổ quỹ. Receipts and payments of the period include the automatic ones. |
| Debt | `DebtPositionQuery`, per branch and side | The open items at `at` (the "as at D" rule of the debt reports), summed by `DebtPositionMath`: balance (open documents minus unallocated), the overdue buckets of Nợ quá hạn with its default boundaries (`DebtAging`: 1–30, 31–60, 61–90, 91+ days), documents due within 7 days and credit limits exceeded (customer × branch). |
| Inventory | `InventoryLedger`, `StockBalances` | Stock value now (every ledger row, as Tồn kho without a date), product and warehouse counts, product × warehouse pairs below zero, the stock-in value and stock-out cost of the period. Under branch costing a warehouse's value is each product's branch value split by quantity (`StockValueSplit`, shared with Tồn kho). |

**Figures and reports.** The report services were not rewritten to share this aggregation; for the figures below, an integration test that compares them with the report route for the same branch and date is the guard against drift:

| Dashboard figure | Report it is tested against | Test |
|---|---|---|
| Cash summary: receipts and payments of the period, balance per fund and in total | Sổ quỹ (`cash-book`): grand in, grand out, closing per fund and in total | `CashSummaryTests.Summary_equals_the_cash_book` |
| Debt summary, both sides: overdue and overdue per bucket; open, unallocated and balance; balance | Nợ quá hạn (`debt-overdue`); Tuổi nợ (`debt-aging`); Số dư công nợ (`debt-balance`) closing balance | `DebtSummaryTests.Summary_equals_the_reports` |
| Debt by employee | Công nợ theo nhân viên (`debt-by-salesperson`) | `DebtBreakdownTests.By_employee_equals_the_report` |
| Stock value | Tồn kho (`stock-on-hand`) total value | `InventorySummaryTests.Stock_value_equals_the_stock_on_hand_report` |
| Stock value by warehouse, under branch and under warehouse costing | Tồn kho rows of each warehouse | `InventoryBreakdownTests.By_warehouse_equals_the_report_under_both_scopes` |

The other figures use the same rules and helpers but have no comparison test, and some have no report at all:

- Sales figures (revenue, cost, gross profit, margin, series, top lists) have no report: the Doanh thu reports count confirmed quotations. The Kinh doanh tab has no report link.
- Cash by reason and the largest documents, top debtors, credit limits exceeded, payables due, the inventory counts, value by product group, top products and the negative pairs are read with the report rules but not compared with a report.
- "Nhập trong kỳ" (`InValue`) counts stock-in vouchers only and leaves out opening stock, which Tổng hợp nhập xuất tồn counts as received in the first period. This difference is intended.
- A link opens the closest report, not always the same figure. The Phải thu tile shows the receivable balance and links to Nợ quá hạn, which totals the overdue part. The Phải trả tile and the other as-at debt figures that link to Số dư công nợ pass the period's `from`/`to`, so for a period that ends before today the report's closing balance differs from the figure, which is as at today. In company scope a link opens the working branch's report.

Company-wide reads load the debt entries and fund movements of every branch on each refresh; an index or a short cache is a separate decision if a route takes more than one second.

**Routes and permissions.** The class carries `[HasPermission(Permissions.Dashboard.Executive)]` and each action its own `[HasPermission]`; both are required. Every route takes `ExecutiveQuery` (`from`, `to`, `branchId`, `at`) except `recent`, which takes `RecentQuery` (`branchId`, `limit` 1–50, default 10). The series add `granularity` (`Day`, `Week`, `Month`; a daily series covers at most 366 days), and `sales/top` adds `by` (`Customer`, `Product`, `Employee` = the voucher's creator), `sort` (`Revenue`, `GrossProfit`) and `limit` (1–50). A range is at most 1,100 days (400 `VALIDATION` with the key `to`).

| Route (`GET /api/dashboard/executive/…`) | Permission besides `dashboard.executive` | Returns |
|---|---|---|
| `sales/summary`, `sales/series`, `sales/top` | `reports.profit` | Revenue, cost, gross profit and margin with the previous period's delta, the series per bucket, top customers / products / employees. Cost figures are `null` without `inventory.view_cost` (`CanViewCost` says so). |
| `cash/summary`, `cash/series`, `cash/breakdown` | `reports.cash` | Receipts, payments and net of the period, fund balances at `at` (cash, bank, per fund), the series, totals per reason and the ten largest documents. |
| `debt/summary`, `debt/breakdown` | `reports.debt` | Receivable and payable at `at` with the term structure, top debtors, credit limits exceeded, by employee, payables due within 30 days. |
| `inventory/summary`, `inventory/breakdown` | `reports.inventory` | Stock value, counts, period in/out, value by warehouse and by product group, negative pairs, top products by value. Values are `null` without `inventory.view_cost`; counts and the negative list are always returned. |
| `branches` | `branches.access_all` | One row per branch: revenue, margin, receivable, stock value. A column is `null` without its domain permission (margin and stock value also need `inventory.view_cost`), and a domain the user may not see is not read. |
| `attention` | — | Overdue receivables, credit limits exceeded and payables due within 7 days (`reports.debt`), negative stock (`reports.inventory`), negative funds (`reports.cash`); an item is `null` without its permission. |
| `recent` | — | Receipts, payments, stock-in and stock-out vouchers created today (VN), Active, newest first. Automatic cash vouchers are left out (their stock voucher is listed). Cash rows need `reports.cash`, stock rows `reports.inventory`. |

**Real-time signal.** A saved document tells the open dashboards which domains of which branch changed; the signal carries no figures.

- `[DashboardSignal(<domains>)]` (`WebApi/Filters/DashboardSignalAttribute.cs`) on a document controller adds `DashboardSignalFilter`. The filter runs the action first, so the service has committed, and then sends one signal when the method is not GET, HEAD or OPTIONS, the action has no `[SkipDashboardSignal]`, it ended without an exception and was not short-circuited, and its status code is absent or below 400. The branch is the working branch (`ICurrentBranch`), which is why a tagged controller may only write documents of the working branch. The send ignores the request's cancellation and has its own 3-second limit; a failure is logged as a warning and never fails the write.
- Tagged controllers (`DashboardDomains`: `Sales`, `Cash`, `Debt`, `Inventory`, `All`):

  | Controller | Domains |
  |---|---|
  | `StockVouchersController` | `All` (`POST stock-at` only reads and carries `[SkipDashboardSignal]`) |
  | `CashVouchersController` | `Cash \| Debt` |
  | `FundTransfersController`, `FundsController` (opening balances) | `Cash` |
  | `DebtOffsetsController`, `DebtController` (settlements), `OpeningDebtsController`, `CreditLimitsController` | `Debt` |
  | `OpeningStocksController`, `InventoryCostController` (opening stock and recalculation change costs) | `Inventory \| Sales` |

  Settings and branch controllers are not tagged; their effect reaches the dashboards with the 60-second refresh. A finished sample-data run sends one signal itself (see Demo Branches And Sample Data).
- `IRealtimeNotifier.NotifyDashboardChangedAsync(branchId, domains)`: `SignalRNotifier` sends the event `DashboardChanged` with `{ branchId, domains }` (`domains` = `"sales"`, `"cash"`, `"debt"`, `"inventory"` for the flags set) once to the groups `dashboard-all` and `dashboard-branch-{branchId}` of the existing `NotificationHub` (`/hubs/notifications`). `DashboardDomains.None` sends nothing. The names live in `WebApi/Hubs/DashboardGroups.cs`.
- `NotificationHub.OnConnectedAsync` keeps the `user-{id}` group and adds the connection to `DashboardGroups.GroupFor(...)`: no group without `dashboard.executive`, `dashboard-all` with `branches.access_all`, otherwise `dashboard-branch-{id}` of the user's default branch (read from `Users`). A failed read fails the connect: SignalR closes the connection, the client shows "Mất kết nối" and retries after 5 seconds; a client that leaves during the read is not a failure. The group follows the token's claims and the default branch at connection time. A change of the user's default branch or permissions reaches the connection only when it reconnects; until then it keeps the old group, and its figures still refresh with the 60-second poll.
- Client: `hooks/useNotificationHub.ts` (mounted once in `AppLayout`) forwards the event to `lib/realtime-bus.ts` and marks every `['executive']` query stale without refetching (`refetchType: 'none'`), so a dashboard that is not open refetches when it is opened again. It reports the connection status to the bus (`connecting`, `connected`, `disconnected`), reconnects without end (2, 4, 6 … seconds, then every 30), and starts the connection again 5 seconds after a failed start or a close. The connection belongs to the signed-in user: it is not rebuilt when the token is refreshed (`accessTokenFactory` reads the current token), but another user signing in rebuilds it, because the server put the old connection in the groups of the old user.
- `useExecutiveLive(branchId)` on the page skips a signal of another branch when one branch is shown, collects the domains of the signals of one second, then invalidates `['executive', <domain>]` for each known domain plus `['executive', 'cross']` (branch comparison, attention, recent documents). After a reconnect it invalidates every `['executive']` query, because signals sent while the connection was down are lost. Fallback: every executive query refetches every 60 seconds while the browser tab is visible (`executiveQueryOptions`). `LiveStatus` shows "Trực tiếp · cập nhật HH:mm:ss", "Đang kết nối…" or "Mất kết nối · số liệu lúc HH:mm".

**Frontend.** The page (`pages/executive-dashboard`, route `/dashboard/executive` behind `ProtectedRoute permission="dashboard.executive"`, sidebar item "Điều hành" in the Báo cáo group, section "Tổng hợp") has a header with the live status, the comparison period ("So với kỳ trước: …"), the branch select (only with `branches.access_all`) and the range picker (Hôm nay, Tuần này, Tháng này, Năm nay). The tabs are Tổng quan (seven KPI tiles, the revenue–cost–gross profit chart, cash flow with fund balances, the receivable term structure, the branch comparison in company scope, top customers, "Cần chú ý" and "Vừa phát sinh"), Kinh doanh, Tiền, Công nợ and Kho. A tab, a tile and its request exist only when the user holds the domain's permission; the period, the tab and the scope live in the URL (`from`, `to`, `tab`, `scope`). Choosing a branch in the select makes it the working branch (`useSwitchWorkingBranch`), so the header switcher, the dashboard and every report link show the same branch. With "Toàn công ty" a report link opens the report of the working branch and says so in its `title`; so does the "Hạn mức nợ" link, because the credit limits screen also shows the working branch only. When the branch list cannot be loaded for a user with `branches.access_all`, the page shows a retry message instead of the tabs and sends no dashboard request, because the scope is unknown. The range picker refuses a custom period the API would reject (from after to, more than 1,100 days), and a URL with such a period falls back to the default period, which follows the VN date even when the page stays open past midnight. A document code is a link only when the document belongs to the working branch, because the document screens reject a document of another branch with 403.

### Demo Branches And Sample Data

A customer trains on the real database. Ticking "Tạo kèm dữ liệu mẫu để đào tạo" in the Thêm chi nhánh dialog creates a demo branch, which a background job fills with a shared sample catalog, two warehouses, three funds, opening balances and about three months of documents. The job writes everything through the business services in date order, so the ledger, costing, debt, automatic vouchers and settlements are what a user would have produced. "Xóa chi nhánh demo" hard-deletes the branch with everything in it, and company-wide figures leave demo branches out. Design: [brainstorm](../brainstorms/261014-1601-branch-sample-data/SUMMARY.md); plan: [261014-1616-branch-sample-data](../plans/archived/261014-1616-branch-sample-data/SUMMARY.md).

**Model.** The migration `AddBranchSampleData` adds `Branch.IsDemo`, `SampleDataStatus` (`None`, `Running`, `Ready`, `Failed`), `SampleDataProgress` (0–100) and `SampleDataError` (at most 1000 characters), and `IsSample` on `Product` and `Customer` for the rows of the sample catalog. `BranchDto` carries the four branch fields, so `GET /api/branches` and `/api/me/branches` show them.

**Flow.**

1. `POST /api/branches` with `withSampleData: true` (`branches.manage`): `BranchService.CreateAsync` saves the branch with `IsDemo = true` and `SampleDataStatus = Running`, with its numbering rows and its `QTM` fund as for any branch. Only after the save does it enqueue `SampleDataJob(branchId, requestedBy)`, so the worker always finds the row. This is the only place that sets `Running`.
2. `ISampleDataQueue` (`SampleDataQueue`) is a singleton, unbounded `Channel` with a single reader. Its writer is never completed.
3. `SampleDataHostedService` (`WebApi/BackgroundJobs`, a `BackgroundService`) reads the jobs one at a time, so two demo branches never seed at the same time. Each job runs `ISampleDataRunner.RunJobAsync` in a fresh scope, inside a log scope with `UserId` (the requester) and `CorrelationId` `sample-data-{branchId:N}`.
4. `SampleDataRunner.RunJobAsync` reads the branch only while it is a demo branch in `Running`; for a missing, real or finished branch it writes nothing. It resolves the owners once (S5: up to three active, non-deleted `SALES` users ordered by username, or the requester when there is none), asks `ISampleDataGenerator` for a plan with the branch code, VN today, the owner count and `InventorySettings.NetExcludesVat`, and runs the plan.
5. The plan items run in this order: `CATALOG`, the warehouses (`KHO-K1`, `KHO-K2`), the bank funds (`QUY-VCB`, `QUY-TCB`), the opening date (`NGAY-DAU-KY`), the fund opening balances (`SO-DU-{fund}`), the opening stock (`TON-K1`, `TON-K2`), the opening debts (`OD-nn`), the credit limits (`HAN-MUC-{partner}`), then the document steps in time order: stock vouchers, receipts and payments, fund transfers, the debt offset and cancels (`SampleStepExecutor`).
6. Each item runs in its own fresh DI scope (`IServiceScopeFactory.CreateAsyncScope()`) that acts as one user (see the acting identity below). It calls the same service a controller would, after the request's `IValidator<T>` when one is registered, because no controller runs. The service's own `ITransactionRunner` gives one transaction per document, and there is no outer transaction, so no lock is held for long. Requests set the `Acknowledge*` flags for the `Warn` policies; under `Block` the generator's margins keep stock, funds and credit within the limits.

**Acting identity.** `IActingIdentity` (`Application/Common/Interfaces`, scoped `ActingIdentityHolder`) holds `ActingIdentity(UserId, Username, FullName, BranchId, Permissions)`. When it is set, `CurrentUser` answers every member from it (authenticated, no roles, no email) and `CurrentBranch.GetIdAsync` returns its `BranchId` before any header or default-branch logic. HTTP requests never set it. The runner sets it once per item scope, with every permission code of the `Permissions` table (as ADMIN has), so the services' own permission checks pass. `Set` throws on a second call, because `AppDbContext` fills `CreatedBy` from the current user at save time. Setup items act as the requester; a document step acts as the owner its plan index names (`owners[index % count]`, or the requester when the index is empty), and an opening debt carries its owner in `OwnerUserId`.

**Amounts taken at run time (S11).** The generator only estimates totals. A receipt or payment with targets allocates `min(open now, MaxAmount ?? open now)` to each target and its total is the sum; the debt offset takes `min(open receivable, open payable)`. A step with nothing to allocate or offset is skipped and counted. A step that names the key of a skipped step finds nothing open there: a receipt or payment leaves that target out (and is skipped when nothing is left), and an offset or a cancel is skipped. Plan errors fail the run: a cash step with both a fixed amount and targets, or with neither; the same target key twice in one step; a key the plan never defined; an unknown step type.

**Sample catalog (S13).** `CATALOG` writes the group `DM` "Dữ liệu mẫu" and the `DM-` products and partners straight through `IAppDbContext` (`SampleCatalogWriter`), because the create requests carry no `IsSample`. Every demo branch shares these rows:

- a live sample row is reused and made usable again: an inactive product becomes active and gets the `DM` group back when it has no group; a partner becomes active and gains the customer or supplier role the plan needs;
- a soft-deleted sample row is restored;
- a live real row (`IsSample = false`) holding a sample code fails the run with `CATALOG: Mã … đã được dùng cho dữ liệu thật.`; a `DM` group counts as the sample group only when its name is "Dữ liệu mẫu";
- a reused product must have the spec's `PricingMode`, `TrackInventory` and `PriceIncludesVat`. Without `InventoryLedger` rows they are reset; with ledger rows (the guard `ProductService.UpdateAsync` applies to these fields) the run fails with a `CATALOG` message.

Locking: the item runs in one `ITransactionRunner.RunAsync` (READ COMMITTED). Before it reads any product state, it takes the product keys (`IInventoryLock.AcquireProductsAsync`, one sorted statement) of every existing sample product of the plan, soft-deleted ones included. These are the keys a product edit takes to change those fields, and the keys the catalog cleanup of the last demo delete holds, so the item never interleaves with either. The isolation level matters: under READ COMMITTED each read after the lock sees what was committed before it, while a REPEATABLE READ snapshot (`RunSnapshotAsync`) starts at the first query, before the lock, and would read stale rows.

**Status and recovery.**

- `None` (a normal branch) → `Running` (set at create) → `Ready` (progress 100) or `Failed` (with `SampleDataError`).
- The runner never sets `Running`. It writes status, progress and error with `ExecuteUpdateAsync`, each in its own scope, and every write targets the branch only while it is a demo branch still in `Running`. A branch that is no longer `Running` is left unchanged, with a Warning log. Progress is items done × 100 / items total, at most 99 before `Ready`, written every 10 items. A progress write that finds the branch no longer `Running` stops the run (key `TIEN-DO`); a progress write that fails is only logged. Writing `Failed` is tried three times.
- `SampleDataError` is `"{key}: {message}"`, cut to 1000 characters. The key names the failing item; `KE-HOACH` is the work before the first item and a generator failure, `HOAN-TAT` the final write. `GET /api/branches` shows the error to every signed-in user, so `FormatError` keeps the message only for validation errors (the first one), negative stock and confirmation errors (code, message and first detail), domain exceptions, the runner's plan errors, concurrency errors and unique violations. A generator failure shows "Không tạo được kế hoạch dữ liệu mẫu. Hãy xóa chi nhánh demo và tạo lại." and keeps its cause as the inner exception for the log. Anything else becomes "Lỗi hệ thống khi tạo dữ liệu mẫu. Hãy xóa chi nhánh demo và tạo lại.", and the exception goes to the log.
- Cancellation: once the host's stopping token is cancelled, every exception propagates (a cancellation, a disposed scope, a broken connection), and the branch stays `Running`.
- Recovery: `SampleDataHostedService` overrides `StartAsync` and awaits `RecoverInterruptedAsync` before `base.StartAsync`. Every demo branch still in `Running` becomes `Failed` with "Bị gián đoạn do ứng dụng khởi động lại. Hãy xóa chi nhánh demo và tạo lại." (its progress stays). The recovery ends before the first request because of two framework defaults: hosted services start one after another (`HostOptions.ServicesStartConcurrently = false`), and the web server's hosted service is registered last. Turning on concurrent start would let a request create and queue a demo branch that the recovery then fails. A recovery that throws (the test template host has no tables yet) is logged as a Warning and the worker starts anyway. A branch that was saved but never queued (a crash between the save and the enqueue) is failed by the same recovery.
- The job loop logs a failing job as an Error and goes on with the next one. On shutdown it logs Information and stops; a job handed out after the stop was requested is left alone.
- **One instance.** The queue lives in memory, so the app assumes a single backend instance. Jobs queued in a process that stops are lost, and their branches fail at the next start. During a rolling deploy the new instance's recovery fails a branch the old instance is still filling; the old run then finds the branch no longer `Running` at its next status write and stops without overwriting it.

**Dashboard signal (S18).** Only the `[DashboardSignal]` controller filter sends signals, so the service calls of a run send none. After a successful run the runner sends one `NotifyDashboardChangedAsync(branchId, DashboardDomains.All)`, in its own scope with a 3-second limit; a failure is logged and ignored. `BranchesController` stays untagged (`DashboardSignalTests` pins the tagged list).

**Generator.** `SampleDataGenerator` (`Application/Organization/SampleData/Generation`) is pure: no database, no clock, no `Guid.NewGuid`. It turns `SampleDataGeneratorInput` (branch code, `Today`, owner count, `NetExcludesVat`) into a `SampleDataPlan`. Its random stream is `new Random(seed)`, seeded with the FNV-1a 32-bit hash of the UTF-8 branch code, so one code always yields the same plan on one .NET runtime; the sequence of a seeded `System.Random` is not guaranteed across major .NET versions. The plan holds:

- the opening date, the 1st of the month three months before `Today`; documents fall on working days (Monday to Saturday) up to yesterday;
- the `DM-` catalog (partner names end with " (mẫu)"), warehouses K1 "Kho chính" and K2 "Kho phụ" (codes `{BranchCode}-K1` / `-K2`, the branch code cut to 47 characters when the code would pass 50), the funds `QTM` (it exists already; the plan sets its opening amount), `VCB` and `TCB`, and the credit limits;
- the opening stock per warehouse and the opening debts;
- the documents, from a day-by-day simulation that tracks stock per warehouse, estimated fund balances and open debt items. Stock never goes negative, every estimated fund balance stays above a margin, and credit balances stay under a share of their limit (S12). The simulation also shapes the end state at `T`: each month's revenue lands near a target band, open receivables stay within a band, every default overdue bucket holds an amount, a few products end low, and an end hold in the last days keeps an open invoice for every owner.

The numbers live in `SampleDataVolumes`. The input also has `NetExcludesVat`, which the runner reads from `InventorySettings.NetExcludesVat` (the company-wide inventory setting), so the estimated voucher totals, and with them the cash the plan moves, follow the stock voucher service: when VAT is left out of the net amount, a weekly cash deposit sized on totals with VAT would drain the cash fund below zero.

**Demo delete.** `GET /api/branches/{id}/demo-deletion` returns the preview: the number of stock vouchers, cash vouchers, fund transfers and debt offsets (automatic, cancelled and soft-deleted ones included), and the number of users whose default branch it is. `DELETE /api/branches/{id}/demo` returns the users reassigned and the sample products and partners kept. Both need `branches.manage` and call `IDemoBranchService` (`DemoBranchService`). Both refuse an unknown branch (404), a branch that is not demo (409 `NOT_A_DEMO_BRANCH`) and one in `Running` (409 `SAMPLE_DATA_RUNNING`). The delete runs in one `ITransactionRunner.RunAsync` and removes rows with `IgnoreQueryFilters()` + `ExecuteDeleteAsync`, children before parents, because the foreign keys between them are `Restrict`. The step numbers are those of the comments in `DemoBranchService`:

- **0.** The exclusive branch gate, the guards again behind it, then the product keys of every sample product and of the products on the branch's stock-ins. 409 `DEMO_WAREHOUSE_IN_USE` when a voucher, voucher line or opening stock of another branch uses one of its warehouses.
- **1–2.** The settlements of its debt entries, then the entries.
- **3–6.** The activities of its cash vouchers, fund transfers and debt offsets, the cash voucher lines, the cash vouchers (before the stock vouchers their automatic ones follow), the fund transfers and the debt offsets.
- **7–8.** The stock voucher activities, lines and vouchers.
- **9.** The inventory ledger, cost periods and stock balances.
- **10.** The opening stock, opening debts and credit limits.
- **11–12.** The funds and warehouses.
- **13.** The document counters and numbering rows.
- **14.** The users whose default branch it was (soft-deleted ones too) move to the main branch (`BranchDefaults.MainBranchId`, S14).
- **15.** The branch row.
- **16.** `ProductCostPrices.RecomputeAsync` for the products of its stock-ins: `CostPrice` follows the latest remaining active stock-in (D35). A product with no stock-in left keeps its price and only loses `CostPriceUpdatedOn` (S17).
- **17.** When no non-deleted demo branch is left, the catalog cleanup.

The normal delete (`BranchService.DeleteAsync`, a soft delete) refuses a demo branch with 409 `DEMO_BRANCH_USE_DEMO_DELETE`.

**Catalog cleanup.** It hard-deletes the sample products that no stock voucher line, opening stock, quotation line, ledger row, stock balance or cost period references. Then it deletes the sample partners that no stock voucher, cash voucher, debt entry, debt offset, opening debt, credit limit or quotation references, with their addresses first. Last, it deletes the `DM` group named "Dữ liệu mẫu" when no product is left in it; a real `DM` group is never touched. There is one reference check per table, each ignoring the query filters, so a sample row still used by real data stays, and the result counts the rows kept. `DemoBranchDeleteTests` reads from `pg_constraint` every foreign key into the tables the delete and the cleanup touch and compares them with a known list: a new foreign key fails the test until the delete or the cleanup handles it.

**Busy.** Some writes take no branch gate: a new fund, warehouse or credit limit of the branch, a user's default branch, a document of a real branch that names a sample partner. Such a write can land after the delete has cleared its table, or after the cleanup has checked a partner's references. The foreign key error it then causes (23503, or 23001 `restrict_violation`, which PostgreSQL 18 raises for a `RESTRICT` key) becomes the retryable 409 `DEMO_BRANCH_BUSY`, and everything rolls back. Only the two places a race can hit are mapped: the branch row delete and the sample partner delete. A foreign key error anywhere else is a bug and surfaces as it is.

**Company scope (S15).** Demo branches stay out of every company-wide figure:

- Reports: the `Company` scope of `debt-balance`, `debt-ledger`, `debt-reconciliation` (`CashDebtReportService`) and `stock-summary` (`InventoryReportService`) adds `!_db.Branches.IgnoreQueryFilters().Any(b => b.Id == e.BranchId && b.IsDemo)`. A soft-deleted demo branch therefore stays out, while rows of a soft-deleted real branch still count. The TT133 company "Ngày mở sổ" (`Tt133ExportSupport.BuildContextAsync`) is the earliest opening date among the non-demo branches. The exports follow their reports.
- Executive dashboard: `ExecutiveScopeResolver` leaves demo branches out of the company-wide branch list, so every company-wide query, the branch comparison, the attention list and the recent documents skip them, and the dashboard keeps matching the reports.
- A demo branch named explicitly (the working branch, a report in branch scope, the dashboard's `branchId`) still shows its own figures.
- Writes in a demo branch still signal the `dashboard-all` group. Company viewers refetch, and their figures stay right because the queries filter.

**Frontend.** On Cài đặt → Chi nhánh (`pages/settings/branches-page.tsx`):

- `BranchFormDialog` shows the checkbox "Tạo kèm dữ liệu mẫu để đào tạo" on create only (`withSampleData`).
- The "Loại" column shows the "Demo" badge, plus "Đang tạo dữ liệu {progress}%" while `Running`, or "Lỗi tạo dữ liệu" with `sampleDataError` as its tooltip when `Failed`. `useBranches` refetches the list every 2 seconds while a branch is `Running` and the browser tab is visible (`branchesRefetchInterval` in `features/branches/sample-data.ts`).
- On a demo row the delete button (disabled while `Running`) opens `DemoBranchDeleteDialog`. The dialog shows the preview counts (`useDemoBranchDeletionPreview`, key `branchKeys.demoDeletion(id)`, refetched every time it opens). "Xóa vĩnh viễn" is enabled only when the typed text equals the branch code and the preview has loaded without error and is not refetching. The success toast says how many sample items were kept, when any were; when the deleted branch was the working branch, the page switches to the user's default branch.
- The header branch switcher and the executive dashboard's branch select add " (demo)" after a demo branch's name.

### Dashboard, Reports, Search, Branding And Notifications

- Quotation dashboard endpoints under `/api/dashboard` (`DashboardController`) expose summary, revenue series, top customers, top products, recent activity and sales leaderboard. The executive dashboard has its own controller under `/api/dashboard/executive` (see Executive Dashboard).
- `ReportsController` currently exposes sales revenue reporting gated by `reports.revenue`.
- `SearchController` provides global search.
- `SettingsController` manages branding/logo settings; writes require `user_settings.manage`. `CompanyProfileController` shares its route prefix (`/api/settings/company`).
- `NotificationsController` exposes list, unread count and read/mark-all-read actions.
- Web push: `NotificationService` → `PushSenderService` (VAPID) → service worker `push` / `notificationclick` → `NAVIGATE` message handled by `SwNavigateListener`. `GET /api/settings/branding/icon/{180|192|512}` serves the PWA and iOS icons. Platform limits and the push flow: [pwa-mobile.md](pwa-mobile.md).

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
| `ConflictException` (code `CONFLICT`, or a specific code: `DEBT_HAS_SETTLEMENTS`, `MANAGED_BY_STOCK_VOUCHER`, `SETTLEMENT_MANAGED`, `BRANCH_HAS_DOCUMENTS`, `OPENING_DATA_EXISTS`; demo branches: `NOT_A_DEMO_BRANCH`, `SAMPLE_DATA_RUNNING`, `DEMO_WAREHOUSE_IN_USE`, `DEMO_BRANCH_BUSY`, `DEMO_BRANCH_USE_DEMO_DELETE`) | 409 |
| `DbUpdateConcurrencyException` (code `CONCURRENCY`) | 409 |
| `DbUpdateException` from a PostgreSQL unique violation `23505` (code `DUPLICATE`) | 409 |
| `NegativeStockException` (`NEGATIVE_STOCK_WARNING` / `NEGATIVE_STOCK_BLOCKED`) | 422 |
| `ConfirmationRequiredException` (`DEBT_SETTLEMENTS_WILL_BE_REMOVED`, `CREDIT_LIMIT_WARNING` / `CREDIT_LIMIT_BLOCKED`, `NEGATIVE_FUND_WARNING` / `NEGATIVE_FUND_BLOCKED`) | 422 |
| `UnprocessableException` (`EXPORT_NO_TARGETS`, `EXPORT_TOO_MANY_SHEETS`) | 422 |
| Rate-limit rejection | 429 |
| Unhandled exceptions | 500 |
| `ServiceUnavailableException` (`PDF_EXPORT_FAILED`) | 503 |

A 422 from `NegativeStockException` or `ConfirmationRequiredException` is a confirmation request: the error carries `code`, `message` and `details`, and the client resends the request with the matching flag (`acknowledgeNegativeStock`, `confirmRemoveSettlements`, `acknowledgeCreditLimit`, `acknowledgeNegativeFund`). The `_BLOCKED` codes cannot be confirmed, and neither can the `EXPORT_*` codes of `UnprocessableException`, which the client shows as a plain error. `DomainException` codes returned as 400 include `PERIOD_LOCKED` and `OPENING_DATE_NOT_SET`.

## Persistence, Soft Delete And Audit

- Business entities inherit `BaseEntity` with audit fields and soft-delete flags.
- `AppDbContext.SaveChangesAsync` fills create/update audit data from `ICurrentUser` and `IDateTime`.
- Query filters exclude `IsDeleted=true` records.
- Filtered unique indexes allow reusing codes/usernames/emails after soft delete.
- Soft-delete cascade applies through child collections that also implement `ISoftDeletable`.
- The demo branch delete is the one hard delete of business entities: it removes every row of a demo branch, soft-deleted ones included (see Demo Branches And Sample Data).
- String values are normalized through EF conventions, including trimming.

## Migration And Seed

Migrations currently live in two folders because early migrations were generated before the final `Persistence/Migrations` path:

- `OrderMgmt.Infrastructure/Migrations`: initial permissions, filtered indexes, refresh tokens, snake_case, product pricing, quotations, unaccent, quotation confirmed/cancelled audit fields, and (most recently) `banks`/`user_bank_accounts`. New migrations land wherever the EF tool's last-migration lookup points, which by default follows whichever of the two folders holds the most recent migration ID — not a fixed choice.
- `OrderMgmt.Infrastructure/Persistence/Migrations`: quotation owner, owner history, user quotation settings, system branding and notifications, then the Round 1 inventory migrations (`AddBranches` … `AddStockVouchersAndLedger`) and the Round 2A cash & debt migrations: `AddCashDebtSettings`, `AddBranchOpeningDate`, `AddCashReasons`, `AddFunds`, `AddFundDefaultUniqueIndex` (one default fund per branch × kind; not in the original plan), `AddPaymentMethodFundKind`, `AddStockReasonDebtEffect`, `AddCustomerCreditDaysAndCreditLimits`, `AddDebtLedger`, `AddOpeningDebts`, `AddCashVouchers`, `AddFundTransfers`, `AddStockVoucherDebtFields`, `AddDebtOffsets`, `AddOpeningDebtVersion` (the `xmin` version of opening debts, added after the review), `AddCompanyProfile` (the company profile table with its one empty row), and `AddBranchSampleData` (the demo branch flags and status columns, and `is_sample` on products and customers). The model snapshot stays in `OrderMgmt.Infrastructure/Migrations` and is updated in place.

`Database:AutoMigrateAndSeed` is `true` in development and `false` in base production settings. Development seed creates roles, permissions, admin user, product groups and units. Production deployments should provide `Seed__AdminPassword` only when initial seeding is intended.

## Logging And Health

- Serilog writes console output and rolling files under `backend/src/OrderMgmt.WebApi/logs`.
- `LoggingContextMiddleware` enriches logs with correlation id and user id. Background jobs have no request, so the sample data worker and runner open a log scope with `UserId` (the requester) and `CorrelationId` (`sample-data-{branchId:N}`).
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

The working branch lives in `stores/branch-store.ts` and is sent as `X-Branch-Id` on every request. `AppLayout` renders pages only after `useBranchContext` has resolved the branch, so no page fetches with the wrong branch. Switching branch (`useSwitchWorkingBranch`) clears the service-worker API cache and resets the query cache, and `AppLayout` keys the routed page by the working branch so page state from the previous branch is discarded. Inventory queries live under the `['inventory']` root key, and branch-scoped cash and debt queries (cash vouchers, funds, fund transfers, open items, settlements, offsets, opening debts, credit limits, partner summary, reports) under `['cash']` (`features/cash-common/keys.ts`, P28). Shared catalogs and singletons keep their own roots (`['cash-reasons']`, `['cash-settings']`). Stock voucher mutations invalidate both `['inventory']` and `['cash']`. Cash voucher, settlement, debt offset and opening debt mutations invalidate `['cash']` and `['inventory']`, because stock voucher lists show settlement-driven state; fund, fund transfer and credit limit mutations stay within `['cash']`. Executive dashboard queries live under `['executive']` (`features/executive-dashboard/keys.ts`, one sub-key per domain plus `cross`); no mutation invalidates them, the dashboard signal and the 60-second refetch do. The service worker never caches branch- or user-scoped API data (`lib/sw-routes.ts`; no cash path is added there), and logout or a failed refresh clears its API cache.

The help panel is route-keyed: `matchRoutes` runs over the `routes` of the help files (`features/help/registry.ts`). It lives at layout level (`AppLayout`, with its button and first-visit hint in `AppHeader`), loads its content as one lazy chunk that the service worker precaches, has no backend, and links to YouTube for videos.

Confirmation flow (P10): `lib/confirm-required.ts` turns a 422 with one of the seven known codes into a `ConfirmationRequest` (`toConfirmationRequest`). Because the body has no `canConfirm`, a code without a resend flag (the `_BLOCKED` codes) is marked blocked. `withConfirmed` adds the confirmed flag to the flags already sent. `components/confirm-required-dialog` shows the request and its `details` items. The settlement token travels with the flags (`confirmedSettlements`). The stock voucher, cash voucher, fund transfer and debt offset forms share this flow in `hooks/useDocumentActions.ts` (busy state, the 422 chain, the 409 reload, validation and generic toasts); the opening debt and fund opening balance dialogs call `toConfirmationRequest` / `withConfirmed` directly. Both resend with the accumulated flags until the save succeeds or a blocked code stops it.
