# QLDonHang Product Goals

## Product Goal

QLDonHang is a quotation-first sales management system. The quotation is the primary document through the sales lifecycle; revenue is recognized when a quotation reaches `Confirmed`.

The scope is being expanded (approved 2026-10-06) to replace the stock voucher workflow of the legacy F-SALE.NET software: stock-in/stock-out vouchers, stock and costing, then cash vouchers and receivables/payables. The business is a trading company (EPS/XPS foam sheets, mineral wool, ...); it does not manufacture or cut goods. Until the planned quotation → sales voucher conversion ships, revenue stays based on confirmed quotations.

## Target Users

- **Sales**: create quotations, maintain customer/product details, send quotations, confirm successful deals and manage their own quotation templates.
- **Manager/Admin**: view cross-user quotations, transfer ownership, manage users/roles, configure lock rules, cancel confirmed quotations and monitor revenue.
- **Accounting/Reporting users**: view revenue reports when granted report permissions; lock periods per branch and recalculate costs. Cash and debt (Round 2A): receipts/payments, fund transfers, document matching and debt offset, opening debts and fund opening balances, credit limits, cash/debt settings and the debt and cash book reports.
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
- **Round 2A — Cash and debt** (implemented; goes live after Round 3). Design: [cash & debt brainstorm](../brainstorms/261007-2252-cash-debt-round2/SUMMARY.md); rules: [Tiền & Công nợ](#tiền--công-nợ):
  - Cash reasons (Lý do thu/chi) with a debt effect, funds (Quỹ) per branch with an opening balance, and payment methods linked to a fund kind.
  - Receipts and payments (Phiếu thu / Phiếu chi) with allocation to the partner's open documents, and fund transfers (Chuyển quỹ).
  - Stock vouchers with a paid amount, a fund and a due date; a paid stock voucher creates and maintains an automatic receipt or payment.
  - Receivable/payable ledger per branch and partner, with opening debts (Nợ đầu kỳ), document matching (Đối trừ chứng từ) and debt offset (Bù trừ công nợ).
  - Customer credit limits (Hạn mức nợ) with a customer debt panel on the stock-out form; credit-limit and negative-fund policies.
  - Branch opening date (Ngày đầu kỳ) shared by opening stock, opening debts and fund opening balances.
  - Reports: Số dư công nợ, Sổ chi tiết công nợ and Sổ quỹ.

## Planned Scope

Approved but not yet implemented. Go-live happens after Round 3. Design: [stock voucher brainstorm](../brainstorms/261006-2139-stock-voucher-clone/SUMMARY.md), [cash & debt brainstorm](../brainstorms/261007-2252-cash-debt-round2/SUMMARY.md).

- **Round 2B — Debt reports**: Công nợ theo chứng từ, Tuổi nợ, Nợ quá hạn, Công nợ theo nhân viên and Biên bản đối chiếu (on screen; its print form comes in Round 3).
- **Round 3 — Output and productivity**: template-based print/Excel export of vouchers (forms 01-VT/02-VT, receipts/payments, Biên bản đối chiếu), Excel import of voucher lines and voucher copy. The cut-over decides whether opening debts are typed in or imported from Excel.
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

### Tiền & Công nợ

- Cash and debt are scoped by branch, like inventory. Documents post debt per partner and side (Phải thu / Phải trả); a balance is increases minus decreases. Matching never changes a balance, only how much of each document is still open, and it works only within one branch, partner and side. Reports show receivable balances as Dư Nợ when positive and payable balances as Dư Có when positive (MISA convention).
- **Cash reasons with debt effect** (Lý do thu/chi): a receipt reason can decrease a receivable or increase a payable, a payment reason can decrease a payable or increase a receivable, and any reason can have no debt effect. The effect limits the reason's partner type, and a reason with an effect requires a partner with that role (customer for receivables, supplier for payables). Stock reasons carry a debt effect too: XBH increases the receivable, NMH the payable, and the new returns NTL / XTL decrease them. A reason used by a voucher keeps its type, partner type and debt effect. The seeded cash reasons and the stock-reason debt effects are seeds (chờ kế toán/BA xác nhận).
- **Funds per branch** (Quỹ): cash boxes and bank accounts of one branch, each with an opening balance at the branch opening date. Every branch starts with the cash fund QTM "Quỹ tiền mặt". A branch has at most one default fund per kind (Tiền mặt, Ngân hàng), and exactly one once it has a fund of that kind: the first fund of a kind becomes the default, choosing another default clears the old one, and the default fund cannot be deactivated. A fund that is used by a document or has an opening balance cannot be deleted or change kind. Each payment method is linked to a fund kind (seeded: TM → Tiền mặt, CK → Ngân hàng) or to none.
- **Receipts and payments with allocation** (Phiếu thu / Phiếu chi): the total is the sum of the lines, computed on the server. A voucher whose reason has a debt effect can be allocated to the partner's open documents when it is saved; the grid suggests the earliest due documents first and the user can change it. An allocation never exceeds the voucher total or a document's open amount; the rest stays unmatched until the matching screen. Fund transfers (Chuyển quỹ) move money between two funds of the same branch and do not touch debt. As with stock vouchers, users act on other users' vouchers only with `edit_all`.
- **Automatic vouchers managed by stock vouchers**: a stock-out with a paid amount creates a receipt (reason TPX) and a stock-in a payment (reason CPN), with the stock voucher's partner, date, fund and owner, matched against the stock voucher's debt as far as both are open. They change only through their stock voucher; the cash voucher screens show them read-only. When the paid amount goes back to 0 the automatic voucher is deleted and its number is never reused, so the series has a gap; a later paid amount creates a new voucher with a new number (P3, chờ kế toán/BA xác nhận). Numbering has one series per type and branch (PT, PC, CQ for fund transfers, BT for offsets), whatever the fund kind (P4, chờ kế toán/BA xác nhận).
- **Paid amount requires a fund-backed method** (P9): a stock voucher's paid amount (Số tiền thanh toán) above 0 needs a payment method linked to a fund kind. It defaults to the total for such a method and to 0 otherwise. The fund defaults to the branch's default fund of that kind and must be active (unless it is the voucher's unchanged saved fund), in the working branch and of the method's kind. The due date defaults to the voucher date plus the partner's credit days on debt-increasing reasons.
- **Credit limit and negative fund policies**: credit limits are set per customer and branch (Hạn mức nợ). A stock voucher that raises a customer's receivable above the limit, and any document that makes a fund balance negative, follow their own policy: Allow (Cho phép), Warn (Cảnh báo, the user confirms and saves) or Block (Chặn). Both policies default to Warn (P7, chờ kế toán/BA xác nhận). As with negative stock, only a change that makes things worse is reported. The credit check runs on saves and restores of receivable-increasing stock vouchers; whether cancelling, deleting or lowering a return (NTL) also needs it is a BA question. The stock-out form shows the customer's balance, limit, remaining credit and overdue amount, for display only.
- **Matching, offset, opening debts**:
  - Đối trừ chứng từ: the user matches a partner's open increase documents against open decrease documents (the screen can suggest oldest first; nothing is matched automatically). Matches made on a receipt/payment or on this screen can be undone here; matches of automatic vouchers and offsets are managed by their own document.
  - Bù trừ công nợ: for a partner who is both customer and supplier, an offset voucher reduces chosen receivable documents and payable documents by the same amount. It can be created, viewed, cancelled and deleted, but not edited or restored. Another document's cancel or edit may still remove its matches after confirmation.
  - Nợ đầu kỳ: one row per legacy document of a partner and side, either a debt or a prepayment, posted at the branch opening date.
  - Editing, cancelling or deleting a document that would remove matches with other documents first lists them and asks for confirmation. Changing the partner or side of a matched document is refused until it is unmatched.
- **Branch opening date locked once documents exist** (P8, Ngày đầu kỳ): one date per branch, used by opening stock, opening debts and fund opening balances, which cannot be saved before it is set. It can change only while the branch has no documents (stock vouchers, receipts/payments, fund transfers or offsets), except that a branch without one may take its first date after documents exist, on or before its earliest document. The old and new dates must both be after the period lock, and it cannot be cleared while opening stock, opening debts or a fund opening balance exist. No document may be dated before the opening date. In reports, opening entries always count as opening balance.
- The period lock also covers receipts/payments, fund transfers, offsets, matching, opening debts and fund opening balances. Automatic matches dated in a locked period stay as they are and are refreshed when the lock moves earlier or is cleared.
- **Permissions** (P5/P6, chờ kế toán/BA xác nhận): ADMIN, MANAGER and ACCOUNTANT get every cash and debt permission. SALES gets only `debt.partner_summary` (the customer debt panel on the stock-out form). WAREHOUSE gets nothing new; automatic vouchers follow the stock voucher permissions.

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
- Cash & debt brainstorm: [../brainstorms/261007-2252-cash-debt-round2/SUMMARY.md](../brainstorms/261007-2252-cash-debt-round2/SUMMARY.md)
- Archived original BD: [../bd/archived/phan-tich-yeu-cau-phan-mem-quan-ly-don-hang.md](../bd/archived/phan-tich-yeu-cau-phan-mem-quan-ly-don-hang.md)
