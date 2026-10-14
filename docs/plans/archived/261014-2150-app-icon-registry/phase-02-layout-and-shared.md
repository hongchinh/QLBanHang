# Phase 02 — Layout, sidebar, header, ui và component dùng chung

**Status:** [x] done
**Complexity:** M

## Objective

Chuyển toàn bộ `frontend/src/components/**` (29 file nguồn) và 2 file test sidebar sang `AppIcon`. Đổi kiểu icon của sidebar từ component sang `IconName`. Áp quy tắc chuyển đổi trong [SUMMARY.md](SUMMARY.md#quy-tắc-chuyển-đổi-dùng-cho-mọi-phase); bảng tên ở [phase-01](phase-01-foundation.md#bảng-tên-registry).

Trước khi bắt đầu: `git merge main` vào nhánh, chạy test chặn. Nếu có file mới import lucide trong `src/components/`, thêm vào task phù hợp dưới đây.

## Files

- Task 1: `components/layout/nav-config.ts`, `components/layout/nav-config.test.ts`, `components/layout/app-layout.tsx`, `components/layout/sidebar/sidebar-group.tsx`, `sidebar-item.tsx`, `sidebar-rail.tsx`, `sidebar-flyout.tsx`, `sidebar-test-utils.tsx`, `nav-search.test.ts`, `sidebar.test.tsx`
- Task 2: `components/layout/header/app-header.tsx`, `brand-block.tsx`, `header-notifications.tsx`, `header-search.tsx`, `header-search-mobile-sheet.tsx`, `header-user-menu.tsx`, `search-results-list.tsx`, `components/layout/sidebar/sidebar.tsx`, `sidebar-pin-chips.tsx`, `sidebar-search.tsx`
- Task 3: `components/ui/dialog.tsx`, `dropdown-menu.tsx`, `multi-select.tsx`, `select.tsx`, `sheet.tsx`, `toaster.tsx`
- Task 4: `components/activity-history/activity-history.tsx` (+ test), `components/confirm-required-dialog/confirm-required-dialog.tsx`, `components/customer-autocomplete/customer-autocomplete.tsx` (+ test), `components/partner-autocomplete/partner-autocomplete.tsx`, `components/search-combobox/search-combobox.tsx`, `components/tt133-export/export-targets-dialog.tsx`, `components/tt133-export/tt133-export-button.tsx` (+ test), `components/voucher-print/voucher-print-button.tsx` (+ test)
- Mọi task: `components/icons/no-direct-lucide-imports.test.ts` (bỏ file khỏi `LEGACY`)

Mọi đường dẫn tương đối từ `frontend/src/`.

## Tasks

### Task 1 — Icon sidebar là tên registry

1. Viết test hỏng:
   - Trong `components/layout/nav-config.test.ts`: bỏ `import { Presentation } from 'lucide-react'`; dòng 99 kỳ vọng `icon: 'executiveDashboard'`. Thay test "every item icon is unique and differs from every group icon" bằng:
     ```ts
     it('every item drawing is unique and differs from every group drawing', () => {
       const groups = all();
       const drawing = (name: IconName) => iconDef(name).icon;
       const itemIcons = groups.flatMap((g) => g.items.map((i) => drawing(i.icon)));
       expect(new Set(itemIcons).size).toBe(itemIcons.length);
       const groupIcons = new Set(groups.map((g) => drawing(g.icon)));
       expect(groupIcons.size).toBe(groups.length);
       expect(itemIcons.filter((icon) => groupIcons.has(icon))).toEqual([]);
     });
     ```
     (import `iconDef, type IconName` từ `@/components/icons`. `all()` là helper sẵn có của file test; giữ kiểm tra icon nhóm không trùng nhau như test cũ.)
   - Trong `components/layout/sidebar/nav-search.test.ts`: bỏ import `LayoutDashboard`, dùng `icon: 'overview'`.
   - Trong `components/layout/sidebar/sidebar.test.tsx`: thêm một test render sidebar đầy đủ (dùng helper sẵn có trong `sidebar-test-utils.tsx`), mở nhóm Kho, kỳ vọng `container.querySelector('[data-icon="stockOut"]')` khác null và `container.querySelector('[data-icon="inventory"]')` khác null.
   - Bỏ khỏi `LEGACY`: `components/layout/nav-config.ts`, `components/layout/nav-config.test.ts`, `components/layout/app-layout.tsx`, `components/layout/sidebar/sidebar-test-utils.tsx`, `components/layout/sidebar/nav-search.test.ts`, `components/layout/sidebar/sidebar-group.tsx`, `components/layout/sidebar/sidebar-item.tsx`, `components/layout/sidebar/sidebar-rail.tsx`. (`sidebar-flyout.tsx` không import lucide nên không có trong `LEGACY`.)
2. Chạy `npm run test -- src/components/layout src/components/icons` → FAIL (kiểu `icon` sai, không có `data-icon`, file còn import lucide).
3. Sửa:
   - `nav-config.ts`: `NavItem.icon` và `NavGroup.icon` có kiểu `IconName`; mọi `icon: X` đổi sang tên theo bảng sidebar ở phase-01; bỏ import lucide.
   - `app-layout.tsx` và `sidebar-test-utils.tsx`: `icon: 'overview'`.
   - `sidebar-group.tsx`: `<AppIcon name={group.icon} />` trong ô màu (ô có `text-white`, không truyền tone vì tên nhóm không có tone).
   - `sidebar-item.tsx` (dòng 51): `<AppIcon name={item.icon} />` (`shrink-0` đã có sẵn trong `AppIcon`). `Star` (dòng 81) → `<AppIcon name="pin" size={14} className={cn(pinned && 'fill-current')} />`.
   - `sidebar-flyout.tsx`: prop `icon` đổi kiểu thành `IconName`, render `<AppIcon name={icon} />`.
   - `sidebar-rail.tsx`: dòng 122 `<AppIcon name={group.icon} />`; `Search` và `Star` theo quy tắc chung (`search`, `pin`).
4. Chạy `npm run test -- src/components/layout src/components/icons` và `npm run typecheck` → PASS.
5. `git commit -m "refactor(sidebar): nav icons are AppIcon registry names"`

### Task 2 — Header và phần còn lại của sidebar

1. Viết test hỏng:
   - Bỏ khỏi `LEGACY`: 7 file `components/layout/header/*.tsx` trong danh sách Files, và `components/layout/sidebar/sidebar.tsx`, `components/layout/sidebar/sidebar-pin-chips.tsx`, `components/layout/sidebar/sidebar-search.tsx`.
   - Trong `components/layout/sidebar/sidebar.test.tsx`: test tìm kiếm không có kết quả kỳ vọng `[data-icon="searchEmpty"]`.
2. Chạy `npm run test -- src/components/icons src/components/layout` → FAIL.
3. Sửa theo quy tắc chung:
   - `app-header.tsx` Menu → `menu` (size 20).
   - `brand-block.tsx` FileText → `brandFallback` (24); PanelLeftClose/Open → `sidebarCollapse`/`sidebarExpand`.
   - `header-notifications.tsx` Bell → `notifications`; Loader2 → `loading` (bỏ `animate-spin`, `AppIcon` tự quay).
   - `header-search.tsx`, `header-search-mobile-sheet.tsx`: Search → `search`. Icon nằm trên header màu nên giữ màu hiện tại bằng `tone="none"` nếu đang không có class màu.
   - `search-results-list.tsx`: Loader2 → `loading`, Search → `search`.
   - `header-user-menu.tsx` ChevronDown → `chevronDown`.
   - `sidebar.tsx` SearchX → `searchEmpty` (24), X → `close`.
   - `sidebar-pin-chips.tsx`, `sidebar-search.tsx`: X → `close`, Search → `search`.
4. Chạy `npm run test -- src/components/layout src/components/icons` và `npm run typecheck` → PASS.
5. `git commit -m "refactor(layout): header and sidebar icons via AppIcon"`

### Task 3 — Primitive trong `components/ui`

1. Viết test hỏng:
   - Bỏ khỏi `LEGACY`: `components/ui/dialog.tsx`, `dropdown-menu.tsx`, `multi-select.tsx`, `select.tsx`, `sheet.tsx`, `toaster.tsx`.
   - Trong `components/ui/dialog.test.tsx`: kỳ vọng nút đóng của dialog chứa `[data-icon="close"]`.
2. Chạy `npm run test -- src/components/icons src/components/ui` → FAIL.
3. Sửa: X → `close` (giữ `sr-only` "Close"/"Đóng" có sẵn); Check → `check`; ChevronRight/ChevronDown → `chevronRight`/`chevronDown`; Circle (`h-2 w-2 fill-current`) → `<AppIcon name="radioIndicator" size={8} className="fill-current" />`.
4. Chạy `npm run test -- src/components/ui src/components/icons` và `npm run typecheck` → PASS.
5. `git commit -m "refactor(ui): primitives use AppIcon"`

### Task 4 — Component dùng chung

1. Viết test hỏng:
   - Bỏ khỏi `LEGACY` 8 file nguồn của task này.
   - `components/activity-history/activity-history.test.tsx`: kỳ vọng hoạt động `Created` render `[data-icon="activityCreated"]` có class `text-icon-create`, và `Deleted` render `[data-icon="activityDeleted"]` có class `text-icon-danger`.
   - `components/customer-autocomplete/customer-autocomplete.test.tsx`: kỳ vọng nút thêm khách hàng chứa `[data-icon="add"]` có class `text-icon-create` (thay đổi có chủ ý: trước đây không màu).
   - `components/tt133-export/tt133-export-button.test.tsx`: khi đang xuất (`isPending`), nút chứa `.button-loader` và không chứa `svg.animate-spin`; khi rảnh, nút chứa `[data-icon="exportExcel"]`.
   - `components/voucher-print/voucher-print-button.test.tsx`: khi đang in (`isPending`), nút "In" và nút icon của dòng chứa `.button-loader` và không chứa `svg.animate-spin`; khi rảnh, nút chứa `[data-icon="print"]` và `[data-icon="chevronDown"]`; mục menu "In (PDF)" chứa `[data-icon="exportPdf"]`, mục "Excel" chứa `[data-icon="exportExcel"]`.
2. Chạy `npm run test -- src/components/icons src/components/activity-history src/components/customer-autocomplete src/components/tt133-export src/components/voucher-print` → FAIL.
3. Sửa:
   - `activity-history.tsx`: `ACTIVITY_ICONS` thành `Record<DocumentActivityAction, IconName>` = `{ Created: 'activityCreated', Updated: 'activityUpdated', Cancelled: 'activityCancelled', Restored: 'activityRestored', Deleted: 'activityDeleted', SettlementRemoved: 'activitySettlementRemoved', AutoVoucherChanged: 'activityAutoVoucherChanged' }`; `ActivityIcon` render `<AppIcon name={entry} />`. Loader2 dòng 29 → `<AppIcon name="loading" />` (bỏ `text-blue-600`).
   - `confirm-required-dialog.tsx`: AlertTriangle → `warning` (giữ màu hiện tại theo quy tắc).
   - `customer-autocomplete.tsx`: Plus → `add` (tone mặc định); LayoutList → `listPicker`; X → `close`.
   - `partner-autocomplete.tsx`, `search-combobox.tsx`, `export-targets-dialog.tsx`: X → `close`.
   - `tt133-export-button.tsx`: Loader2 → `<ButtonLoader />` (`@/components/ui/button-loader`); FileSpreadsheet → `exportExcel`; FileText → `exportPdf`.
   - `voucher-print-button.tsx` (thêm từ PR #15): Loader2 dòng 70 → `<ButtonLoader />` (cả hai biến thể đều nằm trong nút); Printer → `print` với `tone="none"` (đang không màu); ChevronDown → `chevronDown`; FileText `text-indigo-600` → `exportPdf`, FileSpreadsheet `text-emerald-700` → `exportExcel` (bỏ class màu vì trùng tone mặc định).
4. Chạy lại lệnh test bước 2 và `npm run typecheck` → PASS.
5. `git commit -m "refactor(components): shared components use AppIcon"`

## Verification

- `npm run test -- src/components` → PASS
- `npm run typecheck` → PASS
- `grep -rlE "from ['\"]lucide-react['\"]" src/components | grep -v "^src/components/icons/"` → không in gì

## Exit Criteria

- Không file nào trong `src/components/` (ngoài `icons/`) import `lucide-react`; `LEGACY` chỉ còn file trong `pages/`, `features/`.
- Sidebar, header, dialog, select và lịch sử hoạt động hiển thị như cũ, trừ nút thêm khách hàng (cyan) và spinner nút xuất TT133 và nút In phiếu (`ButtonLoader`).
