# Phase 05 — User help and docs

**Status:** [x] done
**Complexity:** S

## Objective

Ship the in-app help of both overview screens (AGENTS.md "User Guide Rule"), remove both routes from `HELP_PENDING`, refresh every help file that `help:stale` flags, and update the project docs.

## Files

- Create: `frontend/src/features/help/content/dashboard-overview.md` (route `/`)
- Create: `frontend/src/features/help/content/dashboard-admin.md` (route `/admin/dashboard`)
- Modify: `frontend/src/features/help/help-pending.ts` (remove `'/'` and `'/admin/dashboard'`)
- Modify: whatever `npm run help:stale` lists. Expected: `frontend/src/features/help/content/dashboard-executive.md`, because `live-status.tsx` is in its sources and the hook it uses moved.
- Modify: `docs/project-pdr/product-goals.md`, `docs/architecture/system-architecture.md`, `docs/code-standard/conventions.md`, and the matching rows of `docs/SUMMARY.md` if their descriptions change.

## Tasks

### Task 5.1 — Help files for `/` and `/admin/dashboard`

1. Remove `'/'` and `'/admin/dashboard'` from `HELP_PENDING`.
2. Run `npm run test -- src/features/help` from `frontend/`. Expected: FAIL, because `help-guard.test.ts` reports two routes with neither a file nor a pending entry. This is the RED evidence.
3. Write both files with the `help-content` skill. Use the frontmatter of `dashboard-executive.md` as the model: `key`, `title`, `routes`, `sources`, `related: [dashboard-executive]`, `updated: <today>`.
   - Sources for `dashboard-overview`: `frontend/src/pages/dashboard-page.tsx`, `frontend/src/features/dashboard/**`, `frontend/src/hooks/use-dashboard-signal.ts`, `backend/src/OrderMgmt.Application/Sales/Quotations/Services/DashboardService.cs`, `backend/src/OrderMgmt.Application/Dashboards/Common/SalesRevenueSource.cs`.
   - Sources for `dashboard-admin`: the same, with `frontend/src/pages/admin/admin-dashboard-page.tsx` in place of the first one.
   - The content must state:
     - Doanh thu = phiếu xuất bán (XBH) trừ hàng bán trả lại (NTL), trước VAT, không gồm cước, theo **ngày chứng từ**, của chi nhánh đang chọn ở đầu trang.
     - Sale thường chỉ thấy phiếu mình phụ trách.
     - **Tổng báo giá**, **Đã huỷ**, **Phễu trạng thái**, **Hoạt động gần đây** vẫn tính theo báo giá của cả công ty.
     - Muốn xem toàn công ty thì mở **Điều hành**.
     - Trạng thái **Trực tiếp** / **Mất kết nối**.
     - On the admin page: the **Tất cả sale** select filters by Nhân viên phụ trách, and the leaderboard shows số phiếu, grouped by the Nhân viên phụ trách of the voucher (who may not be a sales user, plan risk R6).
     - Báo cáo Doanh thu vẫn tính theo báo giá đã chốt, nên số có thể khác Tổng quan.
   - Every bold label must be real on-screen text from the Phase 04 pages.
4. Run `npm run test -- src/features/help`. Expected: PASS.
5. Commit: `git commit -m "docs(help): user guide for the overview dashboards"`

### Task 5.2 — Stale help and project docs

1. Run `npm run help:stale` from `frontend/` and note every file it lists.
2. Update each listed file so every sentence matches the code, and set `updated` to today. For `dashboard-executive.md`, add a note in **Lưu ý**: Doanh thu ở Tổng quan dùng cùng cách tính.
3. Update the project docs:
   - `product-goals.md`:
     - Rewrite the line "Quotation dashboards (`/` and `/admin/dashboard`) …" to say revenue blocks come from stock-outs of the working branch, sales users see their own vouchers, and quotation counts stay company-wide.
     - In the executive bullet, change "Sales figures have no report yet (the Doanh thu reports count confirmed quotations)" to say the overview dashboards share the same sales figure while the Doanh thu reports still count quotations.
   - `system-architecture.md`:
     - Hub groups: quotation viewers now join their dashboard group.
     - The `quotations` group and the `QuotationsChanged` event.
     - `SalesRevenueSource` shared by both dashboards.
   - `conventions.md`: `[QuotationSignal]` (no skip attribute; every non-GET quotation action writes), `SalesRevenueSource` as the only place for the XBH/NTL revenue rule, `QuotationRevenueReportService` as the place of the quotation revenue rule of the Doanh thu report, and `useDashboardSignal`.
   - `system-architecture.md` (also): `/api/reports/revenue/*` serves the Doanh thu report; `/api/dashboard/*` serves only the overview dashboards and `quotation-stats`.
   - `/reports/revenue` stays in `HELP_PENDING`: its labels and figures do not change (plan assumption A7).
4. Run `npm run help:stale` (expected: `Không có cảnh báo.`) and `npm run test -- src/features/help` (expected: PASS).
5. Commit: `git commit -m "docs: overview dashboard revenue by branch from stock-outs"`

## Verification

- `npm run test -- src/features/help` passes.
- `npm run help:stale` prints `Không có cảnh báo.`

## Exit Criteria

- Both routes have help files and are absent from `HELP_PENDING`.
- The docs no longer say the overview dashboards count quotation revenue.
