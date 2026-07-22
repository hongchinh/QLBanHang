# QLDonHang Product Goals

## Product Goal

QLDonHang is a quotation-first sales management system. The quotation is the primary document through the sales lifecycle; revenue is recognized when a quotation reaches `Confirmed`.

## Target Users

- **Sales**: create quotations, maintain customer/product details, send quotations, confirm successful deals and manage their own quotation templates.
- **Manager/Admin**: view cross-user quotations, transfer ownership, manage users/roles, configure lock rules, cancel confirmed quotations and monitor revenue.
- **Accounting/Reporting users**: view revenue reports when granted report permissions.

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

## Core Business Rules

- Quotation status flow is `Draft -> Sent -> Confirmed -> Cancelled`.
- A quotation is owned by one user through `OwnerUserId`.
- Users can only see their own quotations unless they have `quotations.view_all`.
- Revenue is based on confirmed quotations and excludes cancelled quotations.
- Lock-at status prevents normal users from editing quotations at or beyond the configured status unless they have `quotations.bypass_lock`.
- Admin/manager-level permissions control cross-user views and transfers.
- Confirmed quotations can be cancelled only by users with the dedicated cancel permission.
- Export uses the owner user's template when present, then falls back to the default template.

## Non-Goals

- No separate order document workflow.
- No delivery, warehouse issue note or handover workflow.
- No inventory tracking.
- No multi-stage payment, debt or advance-payment tracking.
- No payment confirmation, bank webhook, or reconciliation for generated VietQR codes — the app has
  no way to know whether a transfer actually happened.
- No direct email/Zalo sending from the app.
- No full accounting module.

## Related Docs

- Architecture: [../architecture/system-architecture.md](../architecture/system-architecture.md)
- Codebase map: [../codebase/directory-structure.md](../codebase/directory-structure.md)
- Quotation pivot brainstorm: [../brainstorms/260515-1249-quotation-only-pivot/SUMMARY.md](../brainstorms/260515-1249-quotation-only-pivot/SUMMARY.md)
- Archived original BD: [../bd/archived/phan-tich-yeu-cau-phan-mem-quan-ly-don-hang.md](../bd/archived/phan-tich-yeu-cau-phan-mem-quan-ly-don-hang.md)
