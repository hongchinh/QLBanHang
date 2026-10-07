# QLDonHang Product Goals

## Product Goal

QLDonHang is a quotation-first sales management system. The quotation is the primary document through the sales lifecycle; revenue is recognized when a quotation reaches `Confirmed`.

The scope is being expanded (approved 2026-10-06) to replace the stock voucher workflow of the legacy F-SALE.NET software: stock-in/stock-out vouchers, stock and costing, then cash vouchers and receivables/payables. The business is a trading company (EPS/XPS foam sheets, mineral wool, ...); it does not manufacture or cut goods. Until the planned quotation → sales voucher conversion ships, revenue stays based on confirmed quotations.

## Target Users

- **Sales**: create quotations, maintain customer/product details, send quotations, confirm successful deals and manage their own quotation templates.
- **Manager/Admin**: view cross-user quotations, transfer ownership, manage users/roles, configure lock rules, cancel confirmed quotations and monitor revenue.
- **Accounting/Reporting users**: view revenue reports when granted report permissions; lock periods per branch and recalculate costs. Planned (Round 2): cash vouchers and receivables/payables.
- **Warehouse staff**: stock-in/stock-out vouchers, opening stock and stock reports.

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
- **Round 1 — Inventory** (implemented; goes live after Round 3):
  - Branches with a default branch per user and a header switch for users with `branches.access_all`.
  - Warehouses, stock reasons and payment methods per branch.
  - One partner catalog with customer/supplier flags and separate supplier screens.
  - Product inventory fields: inventory tracking, purchase/sales discount, VAT-inclusive price.
  - Opening stock per warehouse.
  - Stock-in/stock-out vouchers with a quotation-style list, a form with live totals, unsaved drafts and a negative-stock confirmation.
  - Stock per warehouse with periodic weighted-average costing and manual cost recalculation.
  - Negative-stock policy, period lock per branch and document numbering per branch.
  - Stock-on-hand and stock-card reports.

## Planned Scope

Approved but not yet implemented. Go-live happens after Round 3. Design: [stock voucher brainstorm](../brainstorms/261006-2139-stock-voucher-clone/SUMMARY.md).

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

### Inventory

- Every user has a default branch; only users with the cross-branch permission can switch the working branch. Branches scope the inventory and cash modules only; quotations and catalogs are shared.
- Users see every voucher in their working branch. They can edit, delete or cancel their own vouchers; acting on other users' vouchers requires `edit_all`. Stock-in and stock-out permissions are separate, and viewing cost requires its own permission.
- Customers and suppliers share one partner catalog; a partner can be both. An inactive partner cannot be newly chosen on a voucher. The stock reason decides which partner type a voucher accepts. Editing or deleting a partner requires the update/delete permission of every role it has, so sales users cannot edit a partner that is also a supplier without `suppliers.*`.
- A product has exactly one stock unit, derived from its `PricingMode` (unit, linear metre, m², m³). Lines for metre-based products take a sheet count plus dimensions. Products not tracking inventory (e.g. transport service) never affect stock.
- Vouchers carry a date and time; the stock shown on a voucher is the balance at that moment.
- The order discount is allocated to lines by value before VAT and never exceeds a line's own value. Inbound value is the line amount after line discount, minus the allocated order discount, plus allocated freight; whether input VAT is included is a setting.
- Cost of goods issued uses periodic weighted average with a configurable period (month, quarter, year) and scope (branch or warehouse). Cost lives only in the inventory ledger, never on voucher lines, and is recalculated automatically after back-dated changes. Costs of a period that has not ended are shown as provisional ("tạm tính"). Report values follow the costing scope: under branch scope a warehouse's value is the branch value split by quantity.
- Every operation that changes stock (save, cancel, restore, delete, opening stock edit) is checked against the negative-stock policy (allow, warn or block; default warn). Only an operation that makes stock worse is reported: a receipt that only reduces an existing deficit always passes (pending BA confirmation).
- The period lock can be set to any date. Creating, editing, cancelling, restoring or deleting a voucher dated on or before it, and editing opening stock dated on or before it, are rejected, and those actions are hidden on locked vouchers. Fully locked costing periods are frozen; a partially locked period is still recalculated.
- A stock reason already used by vouchers keeps its direction and partner type; only its name can change.
- Voucher numbers never repeat in a branch: after a numbering change, codes already issued are skipped.
- `Product.CostPrice` (the default cost on quotations) follows the latest active stock-in line: it is recomputed after every stock-in create, edit, cancel, restore or delete (pending BA confirmation).

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
