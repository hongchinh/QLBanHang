# Go-Live Checklist — product quick add

> Branch `feat/product-quick-add` · Plan [SUMMARY.md](SUMMARY.md) · Brainstorm `docs/brainstorms/261015-2033-product-quick-add/SUMMARY.md`

Tick each item on the day the change reaches a running database.

## Deploy

- [ ] 1. On every running database an admin enables, in the permission matrix (menu Phân quyền, `/admin/roles`), the permission **Tạo hàng hóa** (`products.create`, nhóm **Danh mục**) for:
  - **Kho / giao hàng**
  - **Kế toán**
  - **Nhân viên kinh doanh**

  The seeder grants it only to new databases (the permission row already exists, so it grants nothing to existing roles). Without it the row and the F4 hint are not shown and F4 does nothing for that role.
- [ ] 2. Note for the admin: with `products.create` these roles can also create products on `/products/new` and create product groups, not only from the Mã hàng cell.

## Check after deploy

- [ ] 3. Sign in as a Warehouse, an Accountant and a Sales user. On stock-in, stock-out, quotation (and opening stock for a user who may open it), type an unknown code in the **Mã hàng** cell: the row `Thêm mới hàng hóa "<typed>"` appears, F4 opens **Thêm nhanh hàng hóa**, **Lưu và chọn** fills the line and the voucher is not saved.

## Tell the users

- [ ] 4. F4 in the **Mã hàng** cell adds a missing product without leaving the voucher; typing a code that does not exist also offers the row `Thêm mới hàng hóa`. F4 may need Fn on a laptop; pressing Enter on the row does the same.
