# Go-Live Checklist — cash voucher form redesign

> Branch `feat/cash-voucher-form-redesign` · Plan [SUMMARY.md](SUMMARY.md) · Behaviour spec [section-01-behaviour-spec.md](../../brainstorms/261015-0644-cash-voucher-form-redesign/section-01-behaviour-spec.md)

Tick each item on the day the change reaches a running database.

## Deploy

- [ ] 1. Deploy the backend before the frontend. The new frontend sends `activeOnly=true` on both partner search endpoints (`GET /api/stock-vouchers/partners`, `GET /api/cash-vouchers/partners`); an old backend ignores the parameter and returns inactive partners to the forms.
- [ ] 2. On every running database an admin enables, in the permission matrix (Phân quyền, `/admin/roles`):
  - **Kế toán**: `customers.create`, `suppliers.view`, `suppliers.create`
  - **Kho / giao hàng**: `customers.create`, `suppliers.create`

  The seeder grants these only to new databases (the permission rows already exist, so it grants nothing to existing roles). Without them the `+` button, F4 and the "Thêm mới" row are hidden for that role, and without the view permissions the "Xem danh mục đầy đủ" row is hidden.

## Check after deploy

- [ ] 3. Sign in as an Accountant and as a Warehouse user. F4 opens the quick add on receipts, payments, stock-in and stock-out; the roles are preselected by the reason; a role the user has no permission for is disabled.
- [ ] 4. Type Vietnamese with Telex and with VNI in the partner code, the partner name and the reason fields of the three forms (cash voucher, stock voucher, quotation): the Enter that commits a composition must not move the focus.
- [ ] 5. F4 on a laptop may need Fn: tell users the `+` button does the same.

## Tell the users

- [ ] 6. What changed:
  - Enter now walks the receipt/payment form and the line grid.
  - Lý do nộp and the line descriptions fill themselves ("Tên lý do - Tên đối tượng") and follow the reason and the partner until edited by hand.
  - The MST box is gone (the MST of the catalog is used).
  - A partner picked first sets the reason.
- [ ] 7. Known limit: the quotation search covers customers only; typing a supplier's name there offers "Thêm mới" instead of turning the supplier into a customer.

## Payment QR plan

- [ ] 8. The payment QR plan (`feat/voucher-payment-qr`) was not merged when this branch was finished (no `PaymentQrButton` in the cash voucher page). If it is still not merged, nothing else to do. If it is merged, open a receipt and check that its QR button follows the Print rule: disabled while the form is dirty.
