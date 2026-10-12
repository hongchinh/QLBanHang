# Section 01: Thiết kế UI (phong cách C)

**Date:** 2026-10-12 21:59:38 (reviewed 2026-10-12 22:08)
**Mockups:** [mockup-01-visual-styles.html](mockup-01-visual-styles.html) (so sánh A/B/C) · [mockup-02-option-c-states.html](mockup-02-option-c-states.html) (các trạng thái của C)

Phần này bổ sung cho [SUMMARY.md](SUMMARY.md). Nếu hai file mâu thuẫn, file này là nguồn đúng cho giao diện và component.

---

## 1. Chọn phong cách hiển thị

| | Phong cách | Kết quả |
|---|---|---|
| A | Gọn, gần giống hiện tại: tiêu đề nhóm IN HOA nhỏ, mục active nền xanh đậm | Loại: tiêu đề nhóm và tiêu đề con khó phân biệt |
| B | Nhóm có icon và đường kẻ dẫn hướng; mục con không có icon; active dùng nền nhạt + vạch xanh | Loại |
| **C** ✅ | **Ghim dạng chip, nhóm dạng khối màu**: mỗi nhóm có ô icon màu riêng, mục con giữ icon, active nền xanh đậm | **Chọn** |

Lý do chọn C: màu nhóm giúp nhận diện nhanh, và chip ghim chiếm ít chiều cao. Hai nhược điểm của C được xử lý ở mục 3: tên dài trên chip, và màu ở chế độ tối.

---

## 2. Bảng màu nhóm

Màu khai báo thành CSS variable trong `frontend/src/index.css` (cả `:root` và `.dark`), rồi map sang `tailwind.config.ts` dạng `hsl(var(--nav-*))`. Component **không** được ghi mã màu trực tiếp.

Giá trị token viết dạng **kênh HSL trần**, giống token sẵn có (`--primary: 221.2 83.2% 53.3%`). Không viết `#hex`, `rgb()` hay `hsl()` trong giá trị, vì `hsl(var(--x))` sẽ ra CSS sai. Màu có độ trong suốt ghi alpha ngay trong kênh (`37.7 92.1% 50.2% / 0.12`).

| Nhóm | `id` | Token | Màu (tham chiếu) | Giá trị token | Icon nhóm (lucide) |
|---|---|---|---|---|---|
| Bán hàng | `sales` | `--nav-sales` | blue-600 `#2563eb` | `221.2 83.2% 53.3%` | `ShoppingCart` |
| Kho | `inventory` | `--nav-inventory` | amber-600 `#d97706` | `32.1 94.6% 43.7%` | `Warehouse` |
| Thu chi | `cash` | `--nav-cash` | emerald-600 `#059669` | `161.4 93.5% 30.4%` | `Wallet` |
| Công nợ | `debt` | `--nav-debt` | red-600 `#dc2626` | `0 72.2% 50.6%` | `Scale` |
| Báo cáo | `reports` | `--nav-reports` | violet-600 `#7c3aed` | `262.1 83.3% 57.8%` | `BarChart3` |
| Danh mục | `catalogs` | `--nav-catalogs` | cyan-600 `#0891b2` | `191.6 91.4% 36.5%` | `Library` |
| Hệ thống | `system` | `--nav-system` | slate-600 `#475569` | `215.4 16.3% 34.9%` | `Settings` |

Icon nhóm dành riêng cho nhóm. Các mục đang dùng trùng icon nhóm phải đổi icon: Kho hàng (`Warehouse`), Hình thức thanh toán và Sổ quỹ (`Wallet`), Bù trừ công nợ (`Scale`), Doanh thu (`BarChart3`), Cấu hình hệ thống (`Settings`).

- Ô icon nhóm (26px trên bản mở rộng, 34px trên thanh thu gọn) có nền màu nhóm và icon trắng. Giữ nguyên màu ở chế độ tối.
- Token phụ, mỗi token có bản sáng và bản tối:

| Token | Dùng cho | Sáng | Tối |
|---|---|---|---|
| `--nav-group-bg` | Nền tiêu đề nhóm | `210 40% 97%` | `217 33% 11%` |
| `--nav-pin-bg` | Nền chip ghim | `45 100% 95%` | `37.7 92.1% 50.2% / 0.12` |
| `--nav-pin-fg` | Chữ chip ghim | `30 80% 30%` | `43.3 96.4% 56.3%` |
| `--nav-pin-border` | Viền chip ghim | `45 90% 80%` | `37.7 92.1% 50.2% / 0.35` |
| `--nav-match` | Tô vàng phần khớp tìm kiếm | `48 100% 85%` | `47.9 95.8% 53.1% / 0.3` |

- Màu chỉ để trang trí: tên nhóm luôn có chữ (bản mở rộng) hoặc tooltip và `aria-label` (thanh thu gọn).

---

## 3. Các trạng thái

### 3.1 Bản mở rộng (240–250px), từ trên xuống
1. **Ô "Tìm chức năng…"**: dính ở đầu, không cuộn theo menu.
2. **Vùng chip ghim**: chỉ hiện khi có ghim, ngăn với phần dưới bằng đường kẻ đứt.
3. **Tổng quan.**
4. **7 nhóm.**
   - Tiêu đề nhóm gồm: ô icon màu, tên nhóm (đậm), mũi tên `ChevronDown`/`ChevronRight`.
   - Mục con thụt vào 8px, mỗi mục có icon riêng. Mục active có nền `primary` và chữ `primary-foreground`.
   - Nhóm Báo cáo có tiêu đề con (chữ 11px, đậm, màu `muted-foreground`): Doanh thu / Kho / Quỹ / Công nợ.
5. **Cuộn:** phần nav cuộn được; ô tìm vẫn đứng yên ở đầu.

### 3.2 Chip ghim
- **Kích thước:** chip rộng tối đa 120px; tên dài cắt bằng "…" và có tooltip hiện tên đầy đủ.
- **Giới hạn hiển thị:** hiện **tối đa 6 chip**, phần còn lại gom vào chip viền đứt **"+N"**.
  - Bấm "+N" thì xổ hết chip ngay tại chỗ, chip cuối đổi thành "Thu gọn".
  - Trạng thái xổ không lưu lại.
- **Thứ tự:** theo thứ tự ghim, mục ghim sau đứng cuối. Chip của trang đang mở **không** được đẩy lên đầu.
- **Chip của trang đang mở:** nền `primary`.
- **Bỏ ghim:** rê chuột hoặc focus vào chip thì hiện nút **×**. Trên thiết bị cảm ứng, × luôn hiện.
- **Nút ghim trong danh sách:**
  - Mục đã ghim có **★ vàng luôn hiện**.
  - Mục chưa ghim chỉ hiện ☆ khi rê chuột hoặc focus. Trên thiết bị cảm ứng, ☆ luôn hiện mờ.
- **Số lượng ghim** không giới hạn; chỉ phần hiển thị bị giới hạn.

### 3.3 Đang tìm kiếm
- **Ẩn đi:** chip ghim và Tổng quan.
- **Hiện ra:** chỉ các nhóm có kết quả, và các nhóm này tự mở ra, không xét trạng thái gập đã lưu. Bên phải tiêu đề nhóm hiện số kết quả thay cho mũi tên.
- **So khớp:**
  - Không phân biệt dấu (`normalizeVi`: tách dấu NFD rồi bỏ, `đ` đổi thành `d`, chuyển chữ thường).
  - Phần khớp được tô bằng `--nav-match`.
  - Khớp tên nhóm hoặc tiêu đề con thì hiện **toàn bộ mục** bên trong. Ví dụ "cong no" ra cả "Tuổi nợ", vì mục này nằm dưới tiêu đề con "Công nợ".
- **Phím:**
  - Trong ô tìm: Enter mở kết quả đầu tiên. ↓ chuyển focus thật sang link kết quả đầu tiên.
  - Trong danh sách kết quả: ↑/↓ di chuyển focus giữa các link kết quả; ↑ ở dòng đầu quay về ô tìm. Enter trên link là hành vi mặc định của link. Link đang focus có `focus-visible` ring.
  - Esc (ở ô tìm hoặc trong kết quả) hoặc nút × thì xóa ô tìm và trả focus về ô tìm.
- **Số kết quả** được đọc qua vùng `aria-live="polite"` ẩn, ví dụ "5 kết quả".
- **Không có kết quả:** hiện icon `SearchX`, dòng "Không tìm thấy chức năng" và gợi ý "Thử từ khóa ngắn hơn".
- **Ô tìm tự xóa** khi chuyển trang hoặc khi thu gọn sidebar.

### 3.4 Thanh thu gọn (64px) và flyout
- **Thanh dọc, từ trên xuống:**
  - 🔍: mở rộng sidebar và focus ô tìm. Việc mở rộng được lưu như khi bấm nút thu gọn; không tự thu lại sau khi chọn kết quả.
  - ★: mở flyout danh sách ghim; chỉ hiện khi có ghim.
  - Đường kẻ ngăn.
  - Tổng quan.
  - 7 ô icon nhóm có màu.
- **Nhóm chứa trang đang mở:** viền `primary` 2px quanh ô icon, cộng vạch `primary` 3px ở mép trái.
- **Mở flyout:** **bấm** hoặc Enter/Space. Rê chuột chỉ hiện tooltip tên nhóm, không mở flyout.
- **Nội dung flyout** (Radix `Popover`, `side="right"`):
  - Tiêu đề có ô icon màu và tên nhóm.
  - Danh sách mục có tiêu đề con và nút ☆, giống bản mở rộng.
- **Đóng flyout:** bấm ra ngoài, nhấn Esc (focus trả về icon), chọn một mục, hoặc khi chuyển trang. Mỗi lúc chỉ mở 1 flyout.
- **Flyout dài hơn màn hình:** cuộn bên trong (`max-h` theo khoảng trống còn lại, có `collisionPadding`).

### 3.5 Chế độ tối
- Dùng token `.dark` sẵn có của app, cộng các token phụ ở mục 2.
- Ô icon nhóm giữ nguyên màu.

### 3.6 Mobile drawer
Giống bản mở rộng. Khác biệt duy nhất: nút ☆ và × luôn hiện vì không có hover.

---

## 4. Component

Thư mục `frontend/src/components/layout/sidebar/`:

| File | Trách nhiệm |
|---|---|
| `sidebar.tsx` | Chọn hiển thị bản mở rộng hay thanh thu gọn. Tính `activeItem`, danh sách ghim còn hiển thị được, kết quả lọc. Giữ state ô tìm và nhóm đang **mở tạm** theo trang hiện tại |
| `sidebar-search.tsx` | Ô tìm (`type="search"`), phím ↓/Enter/Esc, nút ×, vùng `aria-live` báo số kết quả |
| `sidebar-pin-chips.tsx` | Chip ghim: giới hạn 6 chip, chip "+N"/"Thu gọn", × bỏ ghim, chip active |
| `sidebar-group.tsx` | Tiêu đề nhóm (`button` có `aria-expanded`/`aria-controls`) và danh sách mục có tiêu đề con |
| `sidebar-item.tsx` | Một mục: icon, tô phần khớp, nút ☆/★ (`aria-pressed`), trạng thái active |
| `sidebar-rail.tsx` | Thanh thu gọn: 🔍, ★, Tổng quan, các ô icon nhóm |
| `sidebar-flyout.tsx` | Radix `Popover` mở khi bấm; dùng lại `sidebar-item` |
| `nav-search.ts` | Hàm thuần: `normalizeVi`, `filterNavGroups` (trả kèm vị trí khớp), `findActiveItem` (chọn mục có tiền tố dài nhất theo ranh giới segment: `pathname === to \|\| pathname.startsWith(to + '/')`; `/` chỉ khớp chính xác; nhận cả mục Tổng quan) |

**Data model**
- `nav-config.ts`:
  - `NavGroup` có `id`, `label`, `icon`, `color` (token), `defaultOpen`.
  - `NavItem` có thêm `section?: string`.
  - Mỗi mục một icon riêng, thay các icon đang trùng: `BarChart3` ×4, `ListChecks` ×2, `Wallet` ×2, `FileText` ×2 (Báo giá, Công nợ theo chứng từ). Không mục nào dùng icon của nhóm (xem §2).
- `ui-store.ts`:
  - `sidebarGroupOpen: Record<string, boolean>`: chỉ lưu lựa chọn người dùng bấm; dùng chung mọi user trên trình duyệt.
  - `pinnedNavPaths: Record<userId, string[]>`.
  - Action: `toggleGroup(id)`, `togglePin(userId, path)`.
  - Bổ sung 2 field vào `partialize`. Chỉ thêm field nên không cần migration.

**Luồng dữ liệu:**
1. `AppLayout` gọi `visibleNavGroups()` như hiện nay rồi truyền xuống `Sidebar`.
2. `Sidebar` đọc store và `pathname`, rồi tính các giá trị dẫn xuất.
3. Khi đổi `pathname`: ô tìm tự xóa, flyout đóng. Nếu nhóm chứa mục active đang gập thì được **mở tạm** (state cục bộ trong `Sidebar`, không ghi store).
4. Nhóm hiển thị mở khi: đang mở tạm, hoặc `sidebarGroupOpen[id]`, hoặc (chưa có giá trị) `defaultOpen`. Bấm tiêu đề nhóm thì gọi `toggleGroup(id)` dựa trên trạng thái đang hiển thị, đồng thời bỏ mở tạm của nhóm đó.

**Sửa lỗi scroll:** đổi thành `grid-rows-[4rem_minmax(0,1fr)]` trong `app-layout.tsx`. Bên trong sidebar mới, vùng cuộn dưới ô tìm dùng `flex-1 min-h-0 overflow-y-auto`.

---

## 5. Edge case

- **Ghim của mục mất quyền hoặc không còn route:** ẩn đi nhưng vẫn giữ trong store. Không tính vào giới hạn 6 hay số "+N".
- **Trang không khớp mục nào** (ví dụ `/admin/user-settings/:userId`): không tô mục nào; nhóm giữ nguyên trạng thái mở hay gập.
- **Trang chi tiết** như `/quotations/123` hoặc `/settings/branches`: chọn mục có tiền tố dài nhất, khớp theo ranh giới segment. Cách này cũng sửa lỗi `/settings` đang sáng cùng lúc với "Cài đặt của tôi".
- **Tổng quan:** `/` chỉ khớp chính xác. Không ghim được (đường dẫn đổi giữa `/` và `/admin/dashboard` theo quyền) và không nằm trong kết quả tìm.
- **Từ khóa tìm** rỗng sau khi trim: coi như không tìm.
- **Kết quả tìm thay đổi khi gõ tiếp:** Enter ở ô tìm luôn mở dòng đầu của kết quả mới nhất.
- **Người dùng gập nhóm rồi vào trang thuộc nhóm đó:** nhóm mở tạm; rời sang trang nhóm khác thì nhóm gập lại như lựa chọn đã lưu.
- **Nhóm khớp tên chỉ có 1 mục:** vẫn hiện tiêu đề nhóm.
- **Người dùng chưa có gì trong store:** mọi nhóm mở, trừ Danh mục và Hệ thống. Chưa có ghim nào thì không hiện vùng chip. Đánh đổi đã chấp nhận: SALES (Hàng hóa) và WAREHOUSE (Nhà cung cấp) phải mở Danh mục một lần.
- **Tiêu đề con không còn mục nào sau khi lọc quyền:** ẩn đi. Nhóm còn 1 mục vẫn giữ là nhóm.

---

## 6. Accessibility

- **Tiêu đề nhóm:** là `button` có `aria-expanded` và `aria-controls`.
- **Ô tìm:** `input type="search"` có `aria-label` và `aria-controls` trỏ tới vùng nav. **Không** dùng mẫu `combobox` + `listbox`: kết quả là link nằm trong nhóm và có nút ☆ bên trong, trong khi `option` không được chứa phần tử tương tác. Điều hướng bằng focus thật (§3.3), số kết quả báo qua `aria-live`.
- **Nút ☆ và × trên chip:** là `button` có `aria-label` ("Ghim Phiếu thu" / "Bỏ ghim Phiếu thu"). Nút ☆ có thêm `aria-pressed`. Nhấn Tab tới được các nút này; chúng hiện ra khi `focus-visible`.
- **Icon trên thanh thu gọn:** có `aria-label` là tên nhóm; `aria-haspopup="dialog"` và `aria-expanded` do Radix `PopoverTrigger` tự gắn. Flyout chứa link nên không dùng `role="menu"`. Esc đóng flyout và trả focus về icon.

---

## 7. Kiểm thử (Vitest + RTL, test đặt cạnh file)

| File test | Trường hợp |
|---|---|
| `nav-search.test.ts` | "doi tru" khớp "Đối trừ chứng từ"; gõ tên nhóm hoặc tiêu đề con thì trả về mọi mục bên trong; trả đúng vị trí cần tô; `findActiveItem` cho `/settings/my-quotation-settings`, `/quotations/123`, `/reports/sales-revenue/:saleUserId`, `/` chỉ khớp chính xác, trang không khớp mục nào |
| `nav-config.test.ts` | SALES, WAREHOUSE và admin ra đúng thứ tự nhóm; tiêu đề con không còn mục thì ẩn; icon các mục không trùng nhau và không trùng icon nhóm |
| `ui-store.test.ts` | Ghim tách theo user, đúng thứ tự thêm; `toggleGroup`; dữ liệu version 1 cũ vẫn load được |
| `sidebar-pin-chips.test.tsx` | 7 ghim thì hiện 6 chip + "+1"; bấm "+1" thì xổ hết; × bỏ được ghim; chip của mục mất quyền bị ẩn |
| `sidebar.test.tsx` | Bấm tiêu đề nhóm thì `aria-expanded` đổi; đổi trang thì nhóm tự mở nhưng store không đổi; Enter ở ô tìm mở dòng đầu; ↓ chuyển focus sang link đầu; Esc xóa ô tìm và trả focus; đang tìm thì chip và Tổng quan bị ẩn |
| `sidebar-rail.test.tsx` | Bấm icon nhóm thì flyout mở; Esc đóng và trả focus; 🔍 mở rộng sidebar và focus ô tìm |

**Kiểm tra thủ công** (jsdom không đo được layout): cuộn được ở cửa sổ cao 700px; chế độ tối; mobile drawer; tài khoản admin, SALES và WAREHOUSE.
