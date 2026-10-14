# Phase 04 — Thay ký tự, icon KPI, chốt luật chặn và tài liệu

**Status:** [x] done
**Complexity:** M

## Objective

- Thay các ký tự ✕ ▾ ▲▼ ← → đang đóng vai icon bằng `AppIcon`.
- Thêm icon nhãn cho 4 ô KPI trang chủ.
- Xóa danh sách `LEGACY`, nâng luật ESLint lên `error`, thêm test chặn ký tự.
- Cập nhật tài liệu quy ước.

Không đổi ký tự → nằm trong câu chỉ đường menu: "Cài đặt → Chi nhánh" (`opening-debt-page.tsx` dòng 208, `features/opening-debts/hooks.ts` dòng 85, `fund-opening-balance-dialog.tsx` dòng 76) và "Cấu hình → Chi nhánh" (`opening-stock-page.tsx` dòng 208).

Mọi đường dẫn tương đối từ `frontend/src/`.

## Files

- Task 1: `features/dashboard/format.ts`, `format.test.ts`, `features/dashboard/components/delta-arrow.tsx` (mới), `delta-arrow.test.tsx` (mới), `kpi-card.tsx`, `kpi-card.test.tsx`, `sales-leaderboard.tsx`, `pages/reports/sales-performance-page.tsx`
- Task 2: `features/dashboard/components/stat-tile.tsx`, `stat-tile.test.tsx`, `pages/executive-dashboard/components/kpi-row.tsx`, `kpi-row.test.tsx`, `pages/executive-dashboard/executive-dashboard-page.test.tsx`
- Task 3: `features/dashboard/components/tile-card.tsx`, `tile-card.test.tsx`, `pages/executive-dashboard/tabs/cash-tab.tsx`, `debt-tab.tsx`, `inventory-tab.tsx`, `overview-tab.tsx` và test của các tab này
- Task 4: `features/dashboard/components/range-picker.tsx`, `range-picker.test.tsx`, `pages/quotations/components/quotation-date-filter.tsx`, `quotation-date-filter.test.tsx`
- Task 5: `components/icons/no-glyph-icons.test.ts` (mới), `App.tsx`, `features/customers/components/customer-catalog-list.tsx`, `features/products/components/product-catalog-list.tsx`, `features/products/components/product-catalog-detail.tsx`, `pages/quotations/quotation-form-page.tsx`, `pages/stock-vouchers/components/partner-debt-strip.tsx`
- Task 6: `features/dashboard/components/kpi-card.tsx`, `kpi-card.test.tsx`, `pages/dashboard-page.tsx`
- Task 7: `components/icons/no-direct-lucide-imports.test.ts`, `eslint.config.js`, `docs/code-standard/conventions.md`, `docs/codebase/directory-structure.md`

## Tasks

### Task 1 — `formatDelta` trả hướng, mũi tên là icon

1. Viết test hỏng:
   - `features/dashboard/format.test.ts` thêm:
     ```ts
     describe('formatDelta', () => {
       it.each([
         [12.5, { text: '12.5%', tone: 'positive', direction: 'up' }],
         [0, { text: '0.0%', tone: 'positive', direction: 'up' }],
         [-3.2, { text: '3.2%', tone: 'negative', direction: 'down' }],
         [null, { text: '—', tone: 'neutral', direction: null }],
       ])('%s', (input, expected) => expect(formatDelta(input)).toEqual(expected));
     });
     ```
   - `features/dashboard/components/delta-arrow.test.tsx`: `render(<DeltaArrow direction="up" />)` → `getByRole('img', { name: 'tăng' })` có `data-icon="trendUp"`; `direction="down"` → tên `giảm`, `data-icon="trendDown"`.
   - `features/dashboard/components/kpi-card.test.tsx`: thay `getByText(/▲\s*12\.5%/)` bằng `getByText('12.5%')` cộng `getByRole('img', { name: 'tăng' })`; tương tự `/▼\s*3\.2%/` → `'3.2%'` và `giảm`.
2. Chạy `npm run test -- src/features/dashboard` → FAIL.
3. Sửa:
   - `format.ts`: `formatDelta` trả `{ text, tone, direction }`, trong đó `direction: 'up' | 'down' | null`. `text` không còn ký tự ▲▼ (`${Math.abs(deltaPct).toFixed(1)}%`, hoặc `—` khi không có số).
   - Tạo `delta-arrow.tsx`:
     ```tsx
     import { AppIcon } from '@/components/icons';

     // The rise / fall arrow of a delta; named for screen readers.
     export function DeltaArrow({ direction }: { direction: 'up' | 'down' }) {
       return <AppIcon name={direction === 'up' ? 'trendUp' : 'trendDown'} size={12} label={direction === 'up' ? 'tăng' : 'giảm'} />;
     }
     ```
   - `kpi-card.tsx` (`DeltaBadge` nhận thêm `direction`), `sales-leaderboard.tsx` (dòng 59), `sales-performance-page.tsx` (dòng 124): render `{delta.direction && <DeltaArrow direction={delta.direction} />}{delta.text}` trong span đã có, thêm class `inline-flex items-center gap-0.5`.
4. Chạy `npm run test -- src/features/dashboard src/pages/reports` và `npm run typecheck` → PASS.
5. `git commit -m "refactor(dashboard): delta arrows are icons"`

### Task 2 — ▲▼ ở `stat-tile` và tỷ suất của `kpi-row`

1. Viết test hỏng:
   - `stat-tile.test.tsx`:
     - Dòng 40–56: `getByText('▲ 12,5%')` → `getByText('12,5%')`; class emerald/rose kiểm trên phần tử bọc (`closest('span')`); thêm `getByRole('img', { name: 'tăng' })` (hoặc `giảm`).
     - Dòng 65, 71: `queryByText(/[▲▼]/)` → `queryByRole('img', { name: /tăng|giảm/ })`.
     - Dòng 159: `getByText('12,5%').closest('span')` có `whitespace-nowrap`.
   - `kpi-row.test.tsx`:
     - Dòng 204/206: `'▲ 12,4%'` → `'12,4%'`, kèm `getByRole('img', { name: 'tăng' })` trong cùng ô.
     - Dòng 214: `queryByText(/[▲▼] \d+(,\d)?%/)` → `queryByRole('img', { name: /tăng|giảm/ })`.
     - Dòng 231–253: textContent kỳ vọng bỏ ký tự, ví dụ `'Tỷ suất 14,5% (0,8 điểm)'`; trong hint có `getByRole('img', { name: 'tăng' })` (hoặc `giảm` ở các case ▼).
     - Dòng 267: `not.toMatch(/[▲▼]/)` → hint không chứa `role="img"`.
   - `executive-dashboard-page.test.tsx`: cập nhật các kỳ vọng có ▲▼ theo cùng cách (chạy test để thấy dòng hỏng).
2. Chạy `npm run test -- src/features/dashboard src/pages/executive-dashboard` → FAIL.
3. Sửa:
   - `stat-tile.tsx` `DeltaBadge`: `<span className={cn('mr-1.5 inline-flex items-center gap-0.5 whitespace-nowrap font-medium', …)}><DeltaArrow direction={rise ? 'up' : 'down'} />{formatPct(Math.abs(delta))}</span>`.
   - `kpi-row.tsx`: `marginHint` trả `ReactNode`. Với `change === 0` vẫn trả chuỗi `${margin} (±0 điểm)`. Ngược lại trả `<>{margin} (<DeltaArrow direction={change > 0 ? 'up' : 'down'} />{formatNumber(Math.abs(change))} điểm)</>`. Sửa comment dòng 27 và 35 cho khớp và bỏ ký tự ▲ trong đó (lệnh grep ở Final Verification không bỏ qua comment).
4. Chạy lại lệnh bước 2 và `npm run typecheck` → PASS.
5. `git commit -m "refactor(executive): stat and margin deltas use icon arrows"`

### Task 3 — Mũi tên → trong nút hành động của ô dashboard

1. Viết test hỏng:
   - `tile-card.test.tsx` dòng 209–252: nhãn `'Sổ quỹ →'`, `'Nợ quá hạn →'`, `'Chi tiết →'` thành `'Sổ quỹ'`, `'Nợ quá hạn'`, `'Chi tiết'` ở cả input và kỳ vọng. Thêm kỳ vọng: link "Sổ quỹ" chứa `[data-icon="arrowRight"]`.
   - Test của các tab dashboard điều hành tìm link/nút theo tên có `→` → bỏ ` →`. Tìm bằng `grep -rn "→'" src/pages/executive-dashboard --include=*.test.tsx`.
2. Chạy `npm run test -- src/features/dashboard src/pages/executive-dashboard` → FAIL.
3. Sửa:
   - `TileActionControl` trong `tile-card.tsx`: cả link và nút render `<>{label}<AppIcon name="arrowRight" size={14} /></>`. Thêm `inline-flex items-center gap-1` vào `ACTION_CLASS` nếu chưa có.
   - Bỏ ` →` khỏi mọi nhãn: `cash-tab.tsx` dòng 97; `debt-tab.tsx` dòng 107, 114, 122, 132, 140, 148; `inventory-tab.tsx` dòng 56; `overview-tab.tsx` dòng 97, 115, 139, 151, 161.
4. Chạy lại lệnh bước 2 và `npm run typecheck` → PASS.
5. `git commit -m "refactor(executive): tile action arrow is an icon"`

### Task 4 — ▾ và → ở bộ chọn kỳ

1. Viết test hỏng:
   - `range-picker.test.tsx` dòng 24, 30, 59, 123, 127, 133, 142: `'Tuỳ chỉnh ▾'` → `'Tuỳ chỉnh'`. Thêm kỳ vọng: nút "Tuỳ chỉnh" chứa `[data-icon="chevronDown"]`; khoảng ngày hiện `[data-icon="arrowRight"]` giữa hai ngày.
   - `quotation-date-filter.test.tsx`: mọi `'Tuỳ chỉnh ▾'` → `'Tuỳ chỉnh'`, kèm kỳ vọng `[data-icon="chevronDown"]`.
2. Chạy `npm run test -- src/features/dashboard/components/range-picker.test.tsx src/pages/quotations/components/quotation-date-filter.test.tsx` → FAIL.
3. Sửa:
   - `range-picker.tsx` dòng 92: `Tuỳ chỉnh <AppIcon name="chevronDown" size={14} />`. Dòng 122: `{from} <AppIcon name="arrowRight" size={12} label="đến" /> {to}`, phần tử bọc thêm `inline-flex items-center gap-1`.
   - `quotation-date-filter.tsx` dòng 79: như dòng 92 của `range-picker.tsx`.
4. Chạy lại lệnh bước 2 và `npm run typecheck` → PASS.
5. `git commit -m "refactor(filters): custom-range caret and date arrow are icons"`

### Task 5 — Các ký tự còn lại và test chặn ký tự

1. Viết test hỏng `components/icons/no-glyph-icons.test.ts`. Chép hàm `walk` của `no-direct-lucide-imports.test.ts` vào file này. Không tách ra file chung: file không phải test nằm trong `tsconfig.app.json` (`types` không có `node`), nên `node:fs` và `__dirname` ở đó làm hỏng `typecheck`/`build`. Test quét mọi file `.ts`/`.tsx` không phải test trong `src/`, bỏ dòng comment (dòng bắt đầu bằng `//`, `*` hoặc `/*`) và phần comment cuối dòng (cắt từ ` // ` đến hết dòng; ví dụ `features/reports/sales-revenue-detail/types.ts` dòng 4 có `// DateOnly → "YYYY-MM-DD"`), rồi kỳ vọng:
   - không còn `▲`, `▼`, `▾`, `✕`;
   - không còn `←` hoặc `→` đứng đầu hay cuối một đoạn chữ JSX, một chuỗi nhãn, hoặc một dòng. Áp regex `/(^|[>'"}])\s*[←→]|[←→]\s*($|[<'"{])/` cho từng dòng. Bốn câu chỉ đường "Cài đặt → Chi nhánh" / "Cấu hình → Chi nhánh" có chữ ở cả hai bên mũi tên nên không khớp. Dòng phân trang `← Trước` / `Sau →` khớp nhờ `^`/`$`; mẫu `{from} → {to}` khớp nhờ `}`/`{`.

   Kết quả mong đợi là danh sách vi phạm rỗng.
2. Chạy `npm run test -- src/components/icons/no-glyph-icons.test.ts` → FAIL. Danh sách vi phạm gồm `App.tsx`, `customer-catalog-list.tsx`, `product-catalog-list.tsx`, `product-catalog-detail.tsx`, `quotation-form-page.tsx`, `partner-debt-strip.tsx`.
3. Sửa:
   - `App.tsx` dòng 776: `✕` → `<AppIcon name="close" />` (nút đã có `aria-label="Đóng"`).
   - `customer-catalog-list.tsx` dòng 141/149 và `product-catalog-list.tsx` dòng 151/158: `← Trước` → `<AppIcon name="chevronLeft" size={14} /> Trước`; `Sau →` → `Sau <AppIcon name="chevronRight" size={14} />`. Nút thêm `inline-flex items-center gap-1` nếu chưa có.
   - `product-catalog-detail.tsx` dòng 50: `<AppIcon name="arrowLeft" size={14} /> Chọn sản phẩm để xem chi tiết`.
   - `quotation-form-page.tsx` dòng 767: `'← Thông tin chung'` → `<><AppIcon name="arrowLeft" size={14} /> Thông tin chung</>`.
   - `partner-debt-strip.tsx` dòng 37 và 41: ` → sau phiếu ` → ` <AppIcon name="arrowRight" size={12} className="inline align-middle" /> sau phiếu `.
4. Chạy `npm run test -- src/components/icons src/features/customers src/features/products src/pages/stock-vouchers src/pages/quotations` và `npm run typecheck` → PASS. Test nào đang tìm theo chữ `← Trước`, `Sau →`, `→ sau phiếu` thì sửa sang chữ không có mũi tên ngay trong bước này.
5. `git commit -m "refactor(ui): replace remaining glyph icons and guard against them"`

### Task 6 — Icon nhãn KPI trang chủ

1. Viết test hỏng trong `kpi-card.test.tsx`:
   - `render(<KpiCard label="Doanh thu hôm nay" icon="kpiRevenueToday" … />)` → có `[data-icon="kpiRevenueToday"]` không có class `text-icon-*`.
   - Không truyền `icon` → không có `svg[data-icon]` trong vùng nhãn.
2. Chạy `npm run test -- src/features/dashboard/components/kpi-card.test.tsx` → FAIL.
3. Sửa:
   - `kpi-card.tsx`: thêm prop `icon?: IconName`. Nhãn thành `<div className="flex items-center justify-between gap-2"><p …>{label}</p>{icon && <AppIcon name={icon} className="text-muted-foreground" tone="none" />}</div>`.
   - `pages/dashboard-page.tsx`: 4 thẻ truyền lần lượt `icon="kpiRevenueToday"`, `"kpiRevenueRange"`, `"kpiQuotations"`, `"kpiCancelled"`.
   - `admin-dashboard-page.tsx`, `revenue-page.tsx` và `stat-tile.tsx` không đổi.
4. Chạy `npm run test -- src/features/dashboard src/pages` (lọc theo `dashboard`) và `npm run typecheck` → PASS.
5. `git commit -m "feat(dashboard): label icons on the home KPI cards"`

### Task 7 — Chốt luật chặn và tài liệu

1. Viết test hỏng: trong `no-direct-lucide-imports.test.ts`, xóa hằng `LEGACY` và đổi kỳ vọng thành `expect(offenders).toEqual([])`; sửa comment cho khớp. (Test này đã xanh nếu phase 03 xong. Nếu đỏ, nó liệt kê file còn sót, ví dụ file do plan khác merge sau.)
2. Chạy `npm run test -- src/components/icons` → PASS, hoặc FAIL nếu còn file sót. Nếu FAIL, chuyển file sót theo quy tắc chung trước khi đi tiếp.
3. Sửa:
   - `eslint.config.js`: `'no-restricted-imports'` từ `'warn'` thành `'error'`.
   - `docs/code-standard/conventions.md`, mục Frontend:
     - Bullet sidebar: thay "a lucide `icon` not used by any other item or group" bằng "an `icon` registry name whose drawing is not used by any other item or group".
     - Thay bullet "For dense action bars…" bằng: "Icons: render `<AppIcon name=… />` from `@/components/icons`; never import `lucide-react` outside `src/components/icons/icon-registry.ts` (ESLint and `no-direct-lucide-imports.test.ts` enforce it). Name icons by business meaning; add a missing name to the registry with a test line. Size by the `size` prop (8/12/14/16/18/20/24; stroke follows the size). Colour comes from the name's `tone` (`--icon-*` tokens: create cyan, edit blue, danger red, confirm emerald, info sky, export dark emerald, print indigo, accent violet, muted slate, warn amber); pass `tone="none"` on filled or coloured surfaces. Spinners: `ButtonLoader` inside buttons, `<AppIcon name="loading" />` elsewhere. Do not use characters (▲ ▼ ▾ ✕ ← →) as icons."
   - `docs/codebase/directory-structure.md`: thêm dòng cho `frontend/src/components/icons/` (registry, `AppIcon`, test chặn import và ký tự).
4. Chạy `npm run lint` → 0 error; `npm run test -- --testTimeout=30000 --maxWorkers=3` → chỉ còn 3 lỗi mốc; `npm run build` → PASS.
5. `git commit -m "chore(icons): enforce AppIcon-only icons and document the convention"`

## Verification

Chạy toàn bộ mục "Final Verification" trong [SUMMARY.md](SUMMARY.md#final-verification).

## Exit Criteria

- Không còn ký tự ▲ ▼ ▾ ✕ làm icon; mũi tên ← → chỉ còn trong câu chỉ đường menu.
- Trang chủ có icon ở nhãn 4 ô KPI, khớp chế độ "Bước 1" của [mockup-01-home.html](../../../brainstorms/261014-2115-notch-icon-analysis/mockup-01-home.html).
- `npm run lint` báo `error` nếu có ai import `lucide-react` ngoài registry; tài liệu quy ước mô tả cách dùng.
