# Phase 03 — Chuyển các trang

**Status:** [ ] pending
**Complexity:** L

## Objective

Chuyển 48 file còn lại trong `pages/` và `features/` sang `AppIcon`, theo quy tắc chuyển đổi trong [SUMMARY.md](SUMMARY.md#quy-tắc-chuyển-đổi-dùng-cho-mọi-phase) và bảng tên ở [phase-01](phase-01-foundation.md#bảng-tên-registry). Thực hiện luôn các thay đổi có chủ ý thuộc từng trang.

Năm task độc lập về file, nên chạy được song song: mỗi làn một worktree, merge vào nhánh `feat/app-icon-registry` theo thứ tự task. Chỗ duy nhất mọi làn cùng sửa là mảng `LEGACY` trong `src/components/icons/no-direct-lucide-imports.test.ts`; mỗi làn chỉ xóa dòng của mình, nên khi merge chỉ cần giữ cả hai bên xóa.

**Không** thay ký tự ✕ ▾ ▲▼ ← → ở phase này; phase 04 làm việc đó.

Trước khi bắt đầu: `git merge main`. File mới import lucide trong `pages/` hoặc `features/` (vd. nút In/Excel của plan TT133) → thêm vào làn có cùng thư mục.

## Files

Danh sách đầy đủ theo làn: [research/icon-usages.md](research/icon-usages.md). Mọi đường dẫn tương đối từ `frontend/src/`.

## Mẫu chung cho mỗi task

1. Viết test hỏng: xóa các file của task khỏi `LEGACY`, và thêm các assertion riêng ghi trong task.
2. Chạy `npm run test -- src/components/icons <thư mục test của task>` → FAIL. Test chặn liệt kê các file còn import lucide; assertion riêng hỏng vì chưa có `data-icon`.
3. Chuyển từng file: thay import `lucide-react` bằng `import { AppIcon } from '@/components/icons'`. Thay từng icon theo tên trong task, áp quy tắc cỡ và màu.
4. Chạy lại lệnh bước 2, cộng `npm run typecheck` → PASS.
5. Commit (message ghi trong task).

## Tasks

### Task 1 — Báo giá và khách hàng (8 file)

File: `pages/quotations/quotation-form-page.tsx`, `quotation-list-page.tsx`, `components/line-items-grid.tsx`, `components/product-typeahead-cell.tsx`, `components/totals-panel.tsx`, `pages/customers/customer-form-fields.tsx`, `customer-list-page.tsx`, `customer-quotations-section.tsx`.

Tên:
- **Thao tác:** ArrowLeft → `back`; Save → `save`; Send → `send`; CheckCircle2 → `confirm`; BadgeCheck → `accountingConfirm`; Copy → `clone`; Printer → `print`; FileSpreadsheet → `exportExcel`; Ban → `cancelVoucher`; Plus → `add`; Pencil → `edit`; Trash2 → `delete`.
- **Tiện ích:** ChevronDown → `chevronDown`; MoreHorizontal → `more`; Search → `search`; LayoutList → `listPicker`; QrCode → `paymentQr`; Clock (dòng 766, `h-3.5`) → `time` size 14; X dòng 518 (`h-3 w-3`, xóa chip) → `close` size 12.
- **Lịch sử báo giá:** hàm `activityIcon` (dòng 1084) trả `<AppIcon name=… />` với Created `activityCreated`, Updated `activityUpdated`, Sent `activitySent`, Confirmed `activityConfirmed`, AccountingConfirmed `activityAccountingConfirmed`, Cancelled `activityCancelled`, OwnerTransferred `activityOwnerTransferred`, Cloned `activityCloned`.
- **Loader2:**
  - `quotation-form-page.tsx` dòng 1002 → `loading`.
  - `quotation-list-page.tsx` dòng 192 và 720 → `loading`. Dòng 236, 246, 256, 267, 277, 287 nằm trong nút → `<ButtonLoader className="mr-2" />`.
  - `customer-quotations-section.tsx` dòng 397 → `loading`.
- **Đổi có chủ ý:** nút "KT xác nhận" ở form (dòng 609) dùng `accountingConfirm` với tone mặc định (emerald).

Assertion riêng trong `pages/quotations/quotation-list-page.test.tsx`: một hàng báo giá có `[data-icon="print"]` và `[data-icon="clone"]`; khi một thao tác hàng loạt đang chạy (dùng cách test hiện có kích hoạt trạng thái pending, nếu có), nút chứa `.button-loader`. `quotation-form-page.tsx` chưa có file test: chỉ kiểm bằng test chặn và typecheck, và ghi vào báo cáo phase là đã xem tay màn form báo giá.

Commit: `refactor(quotations): quotation and customer pages use AppIcon`

### Task 2 — Phiếu thu chi, phiếu kho, chuyển quỹ (11 file)

File: `pages/cash-vouchers/cash-voucher-form-page.tsx`, `cash-voucher-list-page.tsx`, `components/cash-line-grid.tsx`, `components/cash-partner-filter.tsx`, `pages/stock-vouchers/stock-voucher-form-page.tsx`, `stock-voucher-list-page.tsx`, `components/negative-stock-dialog.tsx`, `components/stock-grid-settings-popover.tsx`, `components/stock-line-grid.tsx`, `pages/fund-transfers/fund-transfer-form-page.tsx`, `fund-transfer-list-page.tsx`.

Tên:
- **Thao tác:** ArrowLeft → `back`; Save → `save` (trong nút nền đặc `variant` mặc định: `tone="none"`, như hiện tại không màu); Ban → `cancelVoucher`; RotateCcw → `restore`; Trash2 → `delete`; Plus → `add`.
- **Đóng và xóa:** X ở `cash-voucher-form-page.tsx` dòng 479 (nút "Đóng", `text-slate-500`) → `closeForm`; X khác (xóa nháp `h-3 w-3`, bộ lọc đối tượng) → `close` với cỡ tương ứng.
- **Khác:** Search → `search`; Loader2 ở danh sách (dòng 369, 345, 269) → `loading`; AlertTriangle → `warning`; Settings2 → `gridSettings`; Keyboard → `keyboard`.

Assertion riêng:
- `pages/stock-vouchers/stock-voucher-form-page.test.tsx` (chế độ sửa, phiếu đang hiệu lực): nút "Hủy phiếu" chứa `[data-icon="cancelVoucher"]` có class `text-icon-danger`; nút "Xóa" chứa `[data-icon="delete"]` có class `text-icon-danger`.
- `pages/stock-vouchers/components/stock-line-grid.test.tsx`: nút "Thêm dòng" chứa `[data-icon="add"]` có class `text-icon-create`.
- `pages/cash-vouchers/cash-voucher-form-page.test.tsx`: nút "Đóng" chứa `[data-icon="closeForm"]` có class `text-icon-muted`.

Commit: `refactor(vouchers): voucher pages use AppIcon`

### Task 3 — Công nợ, quỹ, hạn mức, tồn đầu kỳ (8 file)

File: `pages/debt/debt-matching-page.tsx`, `debt-offset-form-page.tsx`, `debt-offset-list-page.tsx`, `opening-debt-page.tsx`, `components/settlement-list.tsx`, `pages/funds/fund-list-page.tsx`, `pages/credit-limits/credit-limit-list-page.tsx`, `pages/inventory/opening-stock-page.tsx`.

Tên:
- **Đối trừ:** Check (`debt-matching-page.tsx` dòng 151, nút "Đối trừ") → `applyMatching`; Eraser → `clearMatches`; Sparkles → `autoMatch`; Undo2 (`settlement-list.tsx` dòng 153, `text-red-600`, "Bỏ cả lô") → `unsettle`.
- **Thao tác:** ArrowLeft → `back`; Ban → `cancelVoucher`; Save → `save` (theo quy tắc màu); Trash2 → `delete`; Plus → `add`; Pencil → `edit`.
- **Khác:**
  - Wallet ở `fund-list-page.tsx` dòng 89 (nút "Số dư đầu kỳ", `text-emerald-600`) → `openingBalance`.
  - AlertTriangle → `warning`; Search → `search`; Loader2 (`debt-offset-list-page.tsx` dòng 248) → `loading`.
- **Đổi có chủ ý:** ở `opening-stock-page.tsx`, Plus → `add` (cyan) và Trash2 → `delete` (đỏ), thay cho đang không màu.

Assertion riêng:
- `pages/inventory/opening-stock-page.test.tsx`: nút thêm dòng chứa `[data-icon="add"]` có class `text-icon-create`; nút xóa dòng chứa `[data-icon="delete"]` có class `text-icon-danger`.
- `pages/funds/fund-list-page.test.tsx`: nút "Số dư đầu kỳ" chứa `[data-icon="openingBalance"]` có class `text-icon-confirm`.

Commit: `refactor(debt): debt, fund and opening-stock pages use AppIcon`

### Task 4 — Danh mục (10 file)

File: `pages/cash-reasons/cash-reason-list-page.tsx`, `pages/payment-methods/payment-method-list-page.tsx`, `pages/product-groups/product-group-list-page.tsx`, `pages/products/product-form-page.tsx`, `pages/products/product-list-page.tsx`, `pages/stock-reasons/stock-reason-list-page.tsx`, `pages/suppliers/supplier-list-page.tsx`, `pages/warehouses/warehouse-list-page.tsx`, `features/customers/components/customer-catalog-list.tsx`, `features/products/components/product-catalog-list.tsx`.

Tên: Plus → `add`; Pencil → `edit`; Trash2 → `delete`; Search → `search`; ArrowLeft → `back`. Phân trang "← Trước / Sau →" để nguyên cho phase 04.

Assertion riêng: trong test sẵn có của `pages/warehouses/` (hoặc file test danh mục đầu tiên có sẵn trong 8 trang), một hàng có `[data-icon="edit"]` (`text-icon-edit`) và `[data-icon="delete"]` (`text-icon-danger`). Nếu cả 8 trang đều chưa có test, chỉ dùng test chặn và typecheck.

Commit: `refactor(catalogs): catalog pages use AppIcon`

### Task 5 — Quản trị, cài đặt, báo cáo (11 file)

File: `pages/admin/roles-matrix-page.tsx`, `user-settings-page.tsx`, `users-list-page.tsx`, `components/role-matrix-table.tsx`, `components/user-actions-menu.tsx`, `pages/settings/branches-page.tsx`, `recalc-cost-page.tsx`, `settings-hub-page.tsx`, `pages/reports/revenue-page.tsx`, `sales-performance-page.tsx`, `sales-revenue-detail-page.tsx`.

Tên:
- **`roles-matrix-page.tsx`:** Plus → `add` (đổi có chủ ý: thành cyan); Save → `save` (theo quy tắc màu); nút "Hủy" (dòng 206) RotateCcw → `discardChanges` (đổi có chủ ý: hình Undo2, slate).
- **`users-list-page.tsx`:** UserPlus → `addUser` (đổi có chủ ý: cyan); Search → `search`.
- **`user-settings-page.tsx`:** ArrowRightLeft → `transferOwnership`.
- **Menu:** MoreVertical → `moreVertical`; MoreHorizontal → `more`.
- **`branches-page.tsx`:** CalendarDays → `calendar`; Plus/Pencil/Trash2 → `add`/`edit`/`delete`.
- **`recalc-cost-page.tsx`:** X dòng 142 (`text-red-600`, xóa một dòng) → `remove`; Calculator dòng 164 (`text-blue-600`) → `recalcCost`.
- **`settings-hub-page.tsx`:**
  - Kiểu `icon: LucideIcon` → `icon: IconName`.
  - Các thẻ: Thông tin đơn vị `settingsCompany`, Chi nhánh `settingsBranches`, Khóa sổ `settingsPeriodLock`, Đánh số chứng từ `settingsNumbering`, Cấu hình kho `settingsInventory`, Tính lại giá vốn `settingsRecalcCost`, Cấu hình tiền & công nợ `settingsCash`.
  - Thẻ dòng 95/109/124: Settings `system`, UserCog `mySettings`, Users2 `users`.
  - Tất cả cỡ 20, giữ màu thẻ bằng `tone="none"` nếu thẻ đặt màu ở phần tử bọc ngoài.
- **`revenue-page.tsx`:** Loader2 dòng 169 (trong nút) → `<ButtonLoader className="mr-2" />`. Khi không xuất thì nút hiện `<AppIcon name="exportExcel" className="mr-2" />` (đổi có chủ ý: thêm icon).
- **`sales-performance-page.tsx`:** ArrowUp/ArrowDown → `sortAsc`/`sortDesc`.
- **`sales-revenue-detail-page.tsx`:** ArrowLeft → `back`.

Assertion riêng:
- `pages/admin/roles-matrix-page.test.tsx`: khi có thay đổi chưa lưu, nút "Hủy" chứa `[data-icon="discardChanges"]`; nút thêm vai trò chứa `[data-icon="add"]` có class `text-icon-create`.
- `pages/settings/recalc-cost-page.test.tsx`: nút "Tính lại giá vốn" chứa `[data-icon="recalcCost"]` có class `text-icon-edit`.
- `pages/settings/settings-hub-page.test.tsx`: thẻ "Chi nhánh" chứa `[data-icon="settingsBranches"]`.
- `revenue-page.tsx` và `users-list-page.tsx` chưa có test: kiểm bằng test chặn, typecheck và xem tay; ghi vào báo cáo phase.

Commit: `refactor(admin): admin, settings and report pages use AppIcon`

## Verification

- `npm run test -- --testTimeout=30000 --maxWorkers=3` → chỉ còn 3 lỗi mốc
- `npm run typecheck` → PASS
- `npm run lint` → 0 error, 0 cảnh báo `no-restricted-imports`
- `grep -rlE "from ['\"]lucide-react['\"]" src | grep -v "^src/components/icons/"` → không in gì

## Exit Criteria

- `LEGACY` rỗng; không file nào ngoài `src/components/icons/` import `lucide-react`.
- Các thay đổi nhìn thấy chỉ gồm những chỗ ghi "đổi có chủ ý" trong phase này và phase 02.
- Đã xem tay: form báo giá, danh sách báo giá, form phiếu xuất kho, form phiếu chi, trang Doanh thu, trang Quản lý người dùng, trang Phân quyền.
