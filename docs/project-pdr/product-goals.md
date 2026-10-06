# QLDonHang Product Goals

## Product Goal

QLDonHang is a quotation-first sales management system. The quotation is the primary document through the sales lifecycle; revenue is recognized when a quotation reaches `Confirmed`.

The scope is being expanded (approved 2026-10-06) to replace the stock voucher workflow of the legacy F-SALE.NET software: stock-in/stock-out vouchers, stock and costing, then cash vouchers and receivables/payables. The business is a trading company (EPS/XPS foam sheets, mineral wool, ...); it does not manufacture or cut goods. Until the planned quotation → sales voucher conversion ships, revenue stays based on confirmed quotations.

## Target Users

- **Sales**: create quotations, maintain customer/product details, send quotations, confirm successful deals and manage their own quotation templates.
- **Manager/Admin**: view cross-user quotations, transfer ownership, manage users/roles, configure lock rules, cancel confirmed quotations and monitor revenue.
- **Accounting/Reporting users**: view revenue reports when granted report permissions. Planned: cash vouchers, receivables/payables, period lock and cost recalculation.
- **Warehouse staff** (planned): stock-in/stock-out vouchers, opening stock and stock reports.

## Current Scope

- Authentication with JWT access tokens and refresh-token rotation.
- Role and permission based authorization.
- Customer and product catalog management.
- Product group and unit lookups.
- Quotation CRUD with line items, status transitions and owner scoping.
- Quotation Excel/PDF export using a default or per-user Excel template.
- Per-user quotation settings, including lock-at status and template upload.
- Admin user CRUD, password reset, account status update and bulk quotation transfer.
- Role-permission matrix management.
- Dashboard summary, revenue series, top customers/products, recent activity and sales leaderboard.
- Sales revenue report.
- Global search, system branding and notifications.
- VietQR payment-QR generation: pick/save a receiving bank account, enter an amount and transfer
  content, get a scannable NAPAS/EMVCo QR code. A quotation's detail page can jump into this screen
  with its total and code pre-filled.

## Planned Scope

Approved but not yet implemented. Delivered in three rounds; go-live happens after all three. Design: [stock voucher brainstorm](../brainstorms/261006-2139-stock-voucher-clone/SUMMARY.md).

- **Round 1 — Inventory**: branches (default branch per user, cross-branch permission), warehouses, a unified partner catalog (customer/supplier flags), stock reasons, payment methods and a per-product inventory-tracking flag; opening stock; stock-in/stock-out vouchers with a quotation-style list and form; stock per warehouse; periodic weighted-average costing; negative-stock policy; period lock per branch; document numbering; stock-on-hand and stock-card reports.
- **Round 2 — Cash and debt**: receipt/payment vouchers, automatic cash vouchers from stock vouchers paid in cash, opening debt balances, customer credit limits, debt offset and a debt balance report.
- **Round 3 — Output and productivity**: template-based print/Excel export of vouchers (forms 01-VT/02-VT), Excel import of voucher lines and voucher copy.
- **Backlog**: FIFO costing, warehouse transfer, stocktake, returns and quotation → sales voucher conversion.

## Core Business Rules

### Quotations

- Quotation status flow is `Draft -> Sent -> Confirmed -> Cancelled`.
- A quotation is owned by one user through `OwnerUserId`.
- Users can only see their own quotations unless they have `quotations.view_all`.
- Revenue is based on confirmed quotations and excludes cancelled quotations.
- Lock-at status prevents normal users from editing quotations at or beyond the configured status unless they have `quotations.bypass_lock`.
- Admin/manager-level permissions control cross-user views and transfers.
- Confirmed quotations can be cancelled only by users with the dedicated cancel permission.
- Export uses the owner user's template when present, then falls back to the default template.

### Inventory (planned)

- Every user has a default branch; only users with the cross-branch permission can switch the working branch. Branches scope the inventory and cash modules only; quotations and catalogs are shared.
- Users see every voucher in their working branch. They can edit, delete or cancel their own vouchers; acting on other users' vouchers requires `edit_all`. Stock-in and stock-out permissions are separate, and viewing cost requires its own permission.
- Customers and suppliers share one partner catalog; a partner can be both. The stock reason decides which partner type a voucher accepts.
- A product has exactly one stock unit, derived from its `PricingMode` (unit, linear metre, m², m³). Lines for metre-based products take a sheet count plus dimensions. Products not tracking inventory (e.g. transport service) never affect stock.
- Vouchers carry a date and time; the stock shown on a voucher is the balance at that moment.
- The order discount is allocated to lines by value before VAT. Inbound value is the line amount after line discount, minus the allocated order discount, plus allocated freight; whether input VAT is included is a setting.
- Cost of goods issued uses periodic weighted average with a configurable period (month, quarter, year) and scope (branch or warehouse). Cost lives only in the inventory ledger, never on voucher lines, and is recalculated automatically after back-dated changes.
- Every operation that changes stock (save, cancel, restore, delete, opening stock edit) is checked against the negative-stock policy (allow, warn or block).
- The period lock can be set to any date and blocks vouchers dated on or before it. Costs of a costing period that has not ended may still be recalculated until the period ends.
- Stock-in vouchers update `Product.CostPrice` (latest purchase price), which is the default cost on quotations.

## Non-Goals

- No separate order document workflow.
- No delivery or handover workflow.
- No group price lists, tiered price policies or reason-based pricing; voucher prices come from the product catalog.
- No unit-of-measure conversions, lots/expiry tracking or barcodes.
- No FTP voucher transfer and no data migration from the legacy database.
- No payment confirmation, bank webhook, or reconciliation for generated VietQR codes — the app has
  no way to know whether a transfer actually happened.
- No direct email/Zalo sending from the app.
- No full accounting module (no chart of accounts or general ledger).

## Related Docs

- Architecture: [../architecture/system-architecture.md](../architecture/system-architecture.md)
- Codebase map: [../codebase/directory-structure.md](../codebase/directory-structure.md)
- Quotation pivot brainstorm: [../brainstorms/260515-1249-quotation-only-pivot/SUMMARY.md](../brainstorms/260515-1249-quotation-only-pivot/SUMMARY.md)
- Stock voucher brainstorm: [../brainstorms/261006-2139-stock-voucher-clone/SUMMARY.md](../brainstorms/261006-2139-stock-voucher-clone/SUMMARY.md)
- Archived original BD: [../bd/archived/phan-tich-yeu-cau-phan-mem-quan-ly-don-hang.md](../bd/archived/phan-tich-yeu-cau-phan-mem-quan-ly-don-hang.md)
