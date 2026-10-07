# Execution Report — Inventory Round 1 (Đợt 1 — Kho)

**Plan:** `docs/plans/261006-2259-inventory-round1/SUMMARY.md`
**Executed:** 2026-10-06 → 2026-10-07 (hoàn tất 2026-10-07 22:09)
**Branch:** `feat/inventory-round1` (base `main` @ `2271a41`)
**Mode:** Batch (từng phase có review riêng; Phase 07 và 09.1–9.2 chạy song song bằng agent)
**Result:** 12/12 phase hoàn tất. Verification tự động đạt baseline, smoke test thủ công 10 bước đạt (user xác nhận).

## Phases

| Phase | Trạng thái | Ghi chú |
| --- | --- | --- |
| 00 — Baseline commit & fast test database | [x] complete | Template DB clone mỗi test; fixture từ chối `qldonhang_test` / `qldonhang`. |
| 01 — Permissions & branch foundation | [x] complete | `Branch`, `User.DefaultBranchId`, `X-Branch-Id` → `ICurrentBranch`, seeder cấp quyền mới một lần (D23). |
| 02 — Catalogs, partner roles & settings | [x] complete | Kho, lý do nhập/xuất, HTTT, vai trò KH/NCC + `/api/suppliers`, trường kho trên sản phẩm, `InventorySettings`, đánh số. |
| 03 — Pure calculators | [x] complete | `VnTime`, `CostingPeriodCalendar`, `StockVoucherCalculator`, `PeriodicAverageCalculator`. |
| 04 — Ledger & posting engine | [x] complete | Ledger + running qty + `StockBalance`, costing, advisory locks, counter, D31. |
| 05 — Stock voucher API | [x] complete | CRUD/cancel/restore/delete, `xmin`, 422 âm kho, defaults, stock-at, partner search. Có vòng fix review 04–05. |
| 06 — Opening stock, recalc & reports API | [x] complete | Tồn đầu kỳ, tính lại giá vốn (thủ công + khi đổi cấu hình), Tồn kho, Thẻ kho, bộ invariant. Có vòng fix review 06. |
| 07 — Frontend foundations | [x] complete | Quyền, route rules, working-branch store/switcher, nhóm menu "Kho". |
| 08 — Frontend catalogs & settings | [x] complete | Màn danh mục, NCC, field KH/SP, chi nhánh, khóa sổ, đánh số, cấu hình kho, tính lại giá vốn. |
| 09 — Frontend stock vouchers | [x] complete | List + form hai chiều, grid dòng, preview calculator, dialog âm kho, draft. |
| 10 — Frontend opening stock & reports | [x] complete | Màn tồn đầu kỳ, Tồn kho, Thẻ kho. |
| 11 — Docs & final verification | [x] complete | Docs PDR / architecture / directory / conventions; vòng review dev-lead + fix; verification. |

## Files changed

`git diff --stat main...HEAD`: 393 file, +52.8k / −0.4k dòng (khoảng 1.5k là migration Designer/Snapshot sinh tự động).

| Khu vực | Nội dung chính |
| --- | --- |
| `backend/src/OrderMgmt.Domain` | Entity `Organization/Branch`, `Inventory/*` (voucher, line, activity, ledger, cost period, balance, counter, opening stock, settings, numbering, kho, lý do, HTTT), `InventoryEnums`, `Permissions`, `BranchDefaults`, `PricingQuantity`. |
| `backend/src/OrderMgmt.Application` | `Organization/Branches`, `Inventory/{Common,Numbering,Ledger,Costing,Posting,StockVouchers,OpeningStocks,Reports,Settings,Warehouses,StockReasons,PaymentMethods}`, `PartnerPermissionGuard`, thay đổi Customer/Product/Search/Quotation/AdminUser. |
| `backend/src/OrderMgmt.Infrastructure` | 9 migration (`AddBranches` → `AddStockVouchersAndLedger`), `InventoryConfiguration`, `OrganizationConfiguration`, `PostgresInventoryLock`, `PostgresDocumentCounter`, `EfTransactionRunner`, `DbSeeder`. |
| `backend/src/OrderMgmt.WebApi` | 12 controller mới/sửa, `CurrentBranch`, middleware (422 âm kho, 409 `CONCURRENCY` / `DUPLICATE`). |
| `backend/tests/OrderMgmt.IntegrationTests` | Fixture template DB; `Organization/*`, `Catalog/*`, `Inventory/*` (unit, engine, voucher, report, invariant, lock, boundary). |
| `frontend/src` | `features/{branches,warehouses,stock-reasons,payment-methods,suppliers,inventory-settings,opening-stock,inventory-reports,stock-vouchers}`, `pages/{stock-vouchers,inventory,settings,warehouses,stock-reasons,payment-methods,suppliers}`, layout/header/branch switcher, `lib/{api-client,permissions,route-permissions,sw-routes,vn-datetime,pricing-quantity,round,stock-quantity}`, `stores/branch-store`. |
| `docs/` | `project-pdr/product-goals.md`, `architecture/system-architecture.md`, `codebase/directory-structure.md`, `code-standard/conventions.md`, `SUMMARY.md`, plan D10/D35. |

## Verification

Chạy trên `feat/inventory-round1` sau vòng fix review (merge `841814e`).

| Lệnh | Kết quả |
| --- | --- |
| Backend build | 0 error (build `tests/OrderMgmt.IntegrationTests` ra thư mục tạm, vì dev server `OrderMgmt.WebApi` đang khóa `src/OrderMgmt.WebApi/bin`) |
| Backend `dotnet test` (DB `qldonhang_integtest`) | 413 pass / 19 fail / 432 — 19 fail **trùng hệt baseline** (AdminRolesCrudTests ×6, AuthTests.Refresh ×1, HandoverExportTests ×2, QuotationExportTests ×2, QuotationStateMachineTests ×5, RevenueLineItemsExportTests ×2, SalesRevenueReportTests ×1) |
| Frontend `npm run typecheck` | PASS |
| Frontend `npm run lint` | 3 error + 14 warning — 3 error có sẵn trên `main` (`components/ui/table.tsx`, `customer-catalog-list.tsx`, `product-catalog-list.tsx`), không do branch này |
| Frontend `npm run test` | 457 pass / 3 fail / 460 — 3 fail baseline (bank-accounts-tab, useNotificationHub, payment-qr-page) |
| Frontend `npm run build` | PASS |
| Smoke test thủ công 10 bước (SUMMARY.md) | PASS — user xác nhận 2026-10-07 |

## Deviations from plan

1. **Vòng review dev-lead trước khi đóng Phase 11 (2026-10-07).** Review toàn branch theo 5 mảng tìm ra 2 High + ~12 Medium + nhiều Low; tất cả lỗi không cần quyết định nghiệp vụ đã được sửa trong các commit `fix(...)` cùng test:
   - High: tồn đầu kỳ có thể ghi dòng kho A sang kho B; 2 advisory lock/sản phẩm có thể cạn lock table của PostgreSQL (nay 1 lock/sản phẩm, lấy trong một câu lệnh, thứ tự `COLLATE "C"`; có test đếm lock với grid 1.500 mã).
   - Medium: trùng số chứng từ sau khi đổi reset policy (counter nhảy qua mã đã cấp + validator numbering); `Allocate` có thể làm Net′ âm (thêm fallback largest-remainder, ghi chú vào D10); Update phiếu lấy lại snapshot sản phẩm hiện tại cho dòng không đổi; lý do đang dùng đổi được hướng/loại đối tượng; 409 `DUPLICATE` cho unique violation; lỗi filter list ở trang ≥ 2; `voucherAt` cũ từ cache defaults; trang không remount khi đổi chi nhánh; service worker cache dữ liệu theo user; timeout 30s khi tính lại giá vốn; link search NCC dẫn tới /403.
   - D35 thêm tie-break theo `CreatedAt`, `Code` của phiếu (đã cập nhật SUMMARY.md).
2. **Thứ tự advisory lock (D30)** giữ nguyên ý nghĩa branch gate → product keys → counter row, nhưng khóa `(product, branch)` bị bỏ vì khóa `product` đã bao trùm; `IInventoryLock.AcquireAsync` bị xóa. `system-architecture.md` đã cập nhật.
3. **D32 phần dư làm tròn** được gán cho kho cuối cùng có số lượng ≠ 0 (thay vì "kho cuối theo mã") để dòng số lượng 0 không mang giá trị.

## Residual risks / follow-ups

- **Cần PO/BA quyết định (không tự sửa):** che `Product.CostPrice` với người không có `inventory.view_cost` và lost-update từ form sản phẩm; đánh số lại khi phiếu chuyển kỳ; chặn bỏ vai trò KH/NCC khi partner đang được dùng; guard đổi `NetExcludesVat` sau khi có dữ liệu; giới hạn đọc `/api/branches`; `period_lock.manage` khóa được mọi chi nhánh. Cùng các go-live gate trong SUMMARY.md (D8, D31, D35, D39, `PurchaseCostIncludesVat`).
- **Hiệu năng (để sau, khi dữ liệu lớn):** gộp query trong ledger/costing service (≈5 query/cặp hàng-kho), index ledger bắt đầu bằng `branch_id`, báo cáo tồn kho quét ledger cả chi nhánh và không phân trang.
- **Fallback `Product.CostPrice` trong costing** có thể đổi giá vốn kỳ cũ chưa khóa khi có xuất âm, rồi tính lại sau một lần nhập mới (chấp nhận, đã ghi nhận).
- `stock-line-grid.tsx` và grid báo giá có thể chuyển sang `lib/stock-quantity.ts#parseQuantityInput` dùng chung; màn phiếu vẫn liệt kê kho ngưng hoạt động (có thể dùng `selectableWarehouses`).
- Baseline fail có sẵn (19 backend, 3 frontend, 3 lint error) vẫn cần xử lý ngoài plan này.
- Còn 2 worktree `.claude/worktrees/agent-*` từ vòng fix; dọn bằng `git worktree remove` sau khi kiểm tra `.claude/settings.local.json` trong đó.
