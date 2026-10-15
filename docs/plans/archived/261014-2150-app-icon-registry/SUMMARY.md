# AppIcon registry — bước 1 của phương án D

> Nguồn: [brainstorm 261014-2115-notch-icon-analysis](../../../brainstorms/261014-2115-notch-icon-analysis/SUMMARY.md) (mục "Bước 1 — làm ngay"). Mockup: [mockup-01-home.html](../../../brainstorms/261014-2115-notch-icon-analysis/mockup-01-home.html), [mockup-02-stock-out.html](../../../brainstorms/261014-2115-notch-icon-analysis/mockup-02-stock-out.html) (chế độ "Bước 1").

## Goal

Mọi icon của frontend đi qua một component `AppIcon` và một file map `icon-registry.ts`.
- Registry đặt tên theo nghiệp vụ, mỗi tên trỏ tới một icon lucide và một `tone` màu (token CSS).
- Cỡ quyết định độ dày nét theo một bảng chung.
- Một test và một luật ESLint chặn import `lucide-react` ngoài registry.
- Các ký tự ✕ ▾ ▲▼ ← → đang đóng vai icon được thay bằng icon.
- Bốn ô KPI trang chủ có icon ở nhãn.

Giao diện gần như không đổi. Giá trị nằm ở tính nhất quán, và ở chỗ sau này đổi bộ icon (ví dụ sang Notch) chỉ cần sửa file map.

## Scope

**In scope**
- `frontend/src/components/icons/`: `icon-registry.ts`, `app-icon.tsx`, test cho token, registry, `AppIcon` và test chặn import.
- Token `--icon-*` trong `frontend/src/index.css` (`:root` và `.dark`), màu `icon.*` trong `frontend/tailwind.config.ts`.
- Chuyển 78 file đang import `lucide-react` (danh sách: [research/icon-usages.md](research/icon-usages.md)) và 2 file test (`nav-config.test.ts`, `nav-search.test.ts`).
- `NavItem.icon`/`NavGroup.icon` đổi từ component sang `IconName`; test tính duy nhất của icon sidebar chuyển sang so hình vẽ qua registry.
- Spinner chia vai trò: `ButtonLoader` trong nút, `AppIcon name="loading"` ở mọi chỗ khác.
- Thay ký tự: ✕ (`App.tsx`), ▾ (2 bộ chọn kỳ), ▲▼ (`formatDelta`, `stat-tile`, `kpi-row`), ← Trước / Sau → (2 danh sách catalog), → trong nút hành động của ô dashboard điều hành, "← Thông tin chung", "← Chọn sản phẩm…", "→ sau phiếu" và "{from} → {to}".
- Icon ở nhãn 4 ô KPI của `dashboard-page.tsx`.
- Cập nhật `docs/code-standard/conventions.md` (mục Frontend).
- Luật ESLint `no-restricted-imports` cho `lucide-react`: `warn` từ phase 01, `error` ở phase 04.

**Out of scope**
- Bộ icon Notch, đổi sidebar style C, đổi bảng màu (chỉ sửa các chỗ trong danh sách "Thay đổi nhìn thấy có chủ ý").
- Ký tự → nằm trong câu chỉ đường menu ("Cài đặt → Chi nhánh", "Cấu hình → Chi nhánh"): đây là chữ, không phải icon, giữ nguyên.
- Icon cho ô KPI của dashboard quản trị, trang Doanh thu và dashboard điều hành (`stat-tile`).
- Icon PWA trong `public/icons/`.
- Backend.

## Quy tắc chuyển đổi (dùng cho mọi phase)

1. **Chọn tên** theo nghĩa của chỗ dùng, tra bảng trong [phase-01](phase-01-foundation.md#bảng-tên-registry). Không có tên phù hợp thì thêm tên mới vào `icon-registry.ts` trong cùng commit, kèm một dòng trong `icon-registry.test.ts`.
2. **Cỡ:** `h-2 w-2` → `size={8}`, `h-3 w-3` → 12, `h-3.5 w-3.5` → 14, `h-4 w-4` → 16 (mặc định, bỏ prop), `h-5 w-5` → 20, `h-6 w-6` → 24. Bỏ class `h-* w-*`; giữ các class khác (`mr-2`, `shrink-0`, `fill-current`…) trong `className`.
3. **Màu, giữ nguyên như đang thấy:**
   - Icon đang có class màu trùng tone mặc định của tên → bỏ class màu.
   - Icon đang **không** có class màu mà tên có tone → truyền `tone="none"`, trừ khi chỗ đó nằm trong danh sách dưới đây.
   - Icon có class màu khác tone của tên → dùng tên có tone đúng màu đó. Không có tên nào khớp thì dừng lại và báo, không tự thêm tone.
   - Class màu không phải màu icon (vd. `text-muted-foreground`, `text-white`, `text-header-fg`) → giữ trong `className` và truyền `tone="none"`. Ngoại lệ: các chỗ trong mục 4 (vd. `search` với `text-muted-foreground` → tone `muted`).
4. **Thay đổi nhìn thấy có chủ ý** (chỉ những chỗ này được đổi màu hoặc hình):
   - `add` (Plus) không màu ở `roles-matrix-page`, `customer-autocomplete`, `opening-stock-page` → cyan (tone mặc định).
   - `delete` (Trash2) không màu ở `opening-stock-page` → đỏ.
   - `addUser` (UserPlus) ở `users-list-page` → cyan.
   - `accountingConfirm` (BadgeCheck) không màu ở `quotation-form-page` → emerald như ở danh sách.
   - `search` dùng `text-muted-foreground` → tone `muted` (slate-500). Ngoại lệ: icon nằm trên nền màu (header) vẫn giữ màu cũ với `tone="none"`.
   - `loading` (Loader2) bỏ mọi class màu (`text-blue-600`, `text-slate-400`), lấy màu chữ xung quanh.
   - `Loader2` nằm trong nút → `ButtonLoader` (`quotation-list-page` dòng 236–287, `revenue-page` dòng 169, `tt133-export-button` dòng 39, `voucher-print-button` dòng 70).
   - `roles-matrix-page` nút "Hủy": `RotateCcw` → `discardChanges` (Undo2, slate).
   - `revenue-page` nút xuất Excel có thêm icon `exportExcel`.
   - Độ dày nét ở cỡ 18–24px theo bảng (1,85 / 1,75 thay vì 2).
5. **Không đổi** aria-label, text hay thứ tự phần tử ngoài các chỗ ghi trong task. Icon trang trí luôn `aria-hidden` (mặc định của `AppIcon`).

## Assumptions

- Nhánh làm việc `feat/app-icon-registry` tạo từ `main` mới nhất. Remote PR là `qlbanhang`, không commit thư mục `source/`.
- `lucide-react` 0.453 có `ChevronLeft`, `ArrowRight`, `TrendingDown` (đã có trong `node_modules`).
- So với brainstorm, plan thêm tone thứ mười là `info` (sky-600). Tone này giữ màu hiện tại của icon "KT xác nhận" trong lịch sử báo giá (`text-sky-600`).
- Spinner: theo lựa chọn ngày 2026-10-14, `ButtonLoader` dùng trong nút, `AppIcon name="loading"` dùng ở mọi chỗ khác.
- Ở dark mode, token `--icon-*` giữ cùng giá trị với light, vì hiện icon dùng cùng class màu ở cả hai chế độ (app chưa có nút chuyển theme).
- Lệnh test frontend: `npm run test` từ `frontend`. Khi máy đang tải nặng, dùng `npm run test -- --testTimeout=30000 --maxWorkers=3`. Mốc hiện tại có 3 test lỗi sẵn, không liên quan: bank-accounts-tab "adds a new account", useNotificationHub "invalidates unread-count", payment-qr-page "generates and displays a QR".
- Vitest chạy jsdom, dùng được `node:fs` trong test (đọc `index.css` và quét `src/`).
- TT133 in phiếu (PR #15) và PWA push iOS (PR #16) đã merge vào `main` (`9d3e407`) và đã được tính vào danh sách. Plan khác còn mở (dữ liệu mẫu chi nhánh, mobile hub) có thể thêm file import lucide trước khi plan này chạy xong. Test chặn import sẽ phát hiện; file mới được thêm vào làn phù hợp.

## Risks

- **Xung đột với plan đang chạy song song:** các file form phiếu và danh sách là nơi plan TT133 in phiếu cũng sửa. Merge `main` vào nhánh này trước mỗi phase. TT133 in phiếu đã merge (PR #15, `6742f12`): nút In/Excel của nó nằm trong `components/voucher-print/voucher-print-button.tsx` và được chuyển ở phase 02 task 4; các trang phiếu chỉ dùng lại component này, không thêm icon lucide.
- **Test so ký tự** (▲, ▾, →) ở 12 file test phải cập nhật ở phase 04: `kpi-card`, `format`, `range-picker`, `stat-tile`, `tile-card`, `kpi-row`, `executive-dashboard-page`, 4 test tab của dashboard điều hành, `quotation-date-filter`. Mọi thay đổi accessible name được liệt kê trong task.
- **Đổi kiểu `NavItem.icon`** chạm toàn bộ sidebar và các test sidebar. Làm một lần ở phase 02 task 1–2.
- **Đổi độ dày nét** ở cỡ 18–24px là thay đổi nhìn thấy nhỏ, đã được duyệt trong brainstorm.

## Phases

- [x] Phase 01 — Nền tảng: token, registry, AppIcon, test chặn (M) — `phase-01-foundation.md`
- [x] Phase 02 — Layout, sidebar, header, ui và component dùng chung (M) — `phase-02-layout-and-shared.md`
- [x] Phase 03 — Chuyển các trang (L; 5 task độc lập, chạy song song được) — `phase-03-pages.md`
- [x] Phase 04 — Thay ký tự, icon KPI, chốt luật chặn và tài liệu (M) — `phase-04-glyphs-kpi-finalize.md`

## Final Verification

Từ `frontend/`:

```bash
npm run typecheck
npm run lint            # 0 error; không còn cảnh báo no-restricted-imports
npm run test -- --testTimeout=30000 --maxWorkers=3   # chỉ còn 3 lỗi mốc nêu ở Assumptions
npm run build
grep -rlE "from ['\"]lucide-react['\"]" src | grep -v "^src/components/icons/"   # không in gì
grep -rnE "▲|▼|▾|✕" src --include=*.tsx --include=*.ts | grep -v "\.test\."   # không in gì
```

Kiểm tra bằng mắt (chạy `npm run dev`, đăng nhập dev): trang chủ, form Phiếu xuất kho, danh sách báo giá, sidebar thu gọn/mở rộng. So với chế độ "Bước 1" của hai mockup: chỉ khác ở các chỗ trong danh sách "Thay đổi nhìn thấy có chủ ý".

## Rollback / Recovery

- Mỗi task một commit; mỗi phase kết thúc ở trạng thái build và test xanh. Lỗi trong một phase → `git revert` các commit của task đó.
- Toàn bộ: revert merge commit của PR. Không có migration dữ liệu hay thay đổi backend, nên revert là đủ.
