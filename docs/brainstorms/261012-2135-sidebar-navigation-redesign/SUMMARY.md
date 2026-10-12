# Thiết kế lại sidebar trái — nhóm chức năng, tìm kiếm, ghim

**Date:** 2026-10-12 21:35:33
**Status:** Implemented (plan docs/plans/archived/261012-2311-sidebar-navigation-redesign)
**UI design:** [section-01-ui-design.md](section-01-ui-design.md). File này là nguồn đúng cho giao diện, component, edge case và kiểm thử. Mockup: [mockup-01-visual-styles.html](mockup-01-visual-styles.html) · [mockup-02-option-c-states.html](mockup-02-option-c-states.html)
**Related:** [261007-2252-cash-debt-round2](../261007-2252-cash-debt-round2/SUMMARY.md) · plan Round 2B `docs/plans/archived/261012-1709-cash-debt-round2b/` (thêm 5 báo cáo công nợ vào sidebar)

---

## Problem Framing

1. **Sidebar không cuộn được.** `<nav>` trong `components/layout/sidebar/sidebar.tsx` đã có `overflow-y-auto`, nhưng layout cha `components/layout/app-layout.tsx` dùng `grid h-screen grid-rows-[4rem_1fr] overflow-hidden`. `<aside>` là grid item với `min-height: auto`, nên hàng `1fr` (= `minmax(auto, 1fr)`) bị kéo cao bằng toàn bộ menu. Phần thừa bị `overflow-hidden` cắt, `<nav>` không bao giờ tràn nên không có thanh cuộn. Lỗi chỉ xảy ra trên desktop (rộng và thu gọn); mobile drawer là `fixed` + flex column nên vẫn cuộn được.
2. **Không có nhóm hợp lý.** Nhóm "Chức năng" gom tạm 12 mục khác loại: nghiệp vụ hằng ngày (Báo giá, Khách hàng), danh mục ít dùng (Kho, Lý do nhập xuất, Lý do thu/chi, Hình thức thanh toán, Quỹ…), tiện ích (QR), Hạn mức nợ. "Tiền & Công nợ" trộn chứng từ và báo cáo: ban đầu 9 mục, hiện 11 trên nhánh Round 2B, lên **14** khi Round 2B xong. Admin thấy khoảng **40 mục** phẳng, không gập được, không tìm được.
3. **Chi tiết nhỏ.** Nhãn "Setting" bằng tiếng Anh. Icon trùng (`BarChart3` × 4, `ListChecks` × 2, `Wallet` × 2, `FileText` × 2). NavLink `/settings` không có `end` nên sáng cùng lúc với `/settings/my-quotation-settings`.

---

## Goals & Non-Goals

**Goals**
- Sidebar cuộn được ở cả chế độ rộng, thu gọn và mobile drawer.
- Chia nhóm theo loại công việc, mỗi nhóm nghiệp vụ 3–5 mục.
- Nhóm gập/mở được và nhớ trạng thái. Nhóm chứa trang hiện tại tự mở.
- Ô "Tìm chức năng…" lọc menu tức thì, không phân biệt dấu.
- Ghim mục yêu thích lên đầu, tách theo user, lưu localStorage.
- Chế độ thu gọn: mỗi nhóm 1 icon, mở flyout liệt kê các mục.

**Non-Goals**
- Lưu ghim trên server hoặc đồng bộ giữa các thiết bị.
- Trang hub cho Báo cáo/Danh mục (phương án C).
- Đưa chức năng vào ô tìm kiếm Ctrl+K trên header (ô đó chỉ tìm dữ liệu).
- Thay đổi route, quyền, hay backend.

---

## Constraints & Assumptions

- Chỉ sửa frontend (React 18 + Tailwind + Radix). Không thêm dependency: accordion tự làm, flyout dùng `components/ui/popover.tsx` sẵn có.
- Lọc quyền giữ nguyên trong `visibleNavGroups()`. `nav-config.test.ts` đang kiểm tra danh sách mục theo vai trò.
- `ui-store` đã có `persist` (`qldonhang-ui-store`, version 1). Chỉ thêm field nên không cần migration.
- Ctrl+K đã dành cho `HeaderSearch`, nên ô tìm sidebar không gán phím tắt.
- Giả định máy ở quầy có thể dùng chung trình duyệt, nên ghim tách theo `userId`.

---

## Approaches Considered

| | Phương án | Ưu | Nhược | Độ phức tạp |
|---|---|---|---|---|
| **A** ✅ | **Theo loại công việc:** Bán hàng · Kho · Thu chi · Công nợ · Báo cáo · Danh mục · Hệ thống | Giống MISA/Fast. Nhóm nghiệp vụ ngắn. Danh mục ít dùng gom một chỗ, thường để gập. Báo cáo mới chỉ thêm vào một chỗ. | Báo cáo 14 mục nên cần tiêu đề con. Nhân viên kho phải mở 2 nhóm. SALES phải mở Danh mục để vào Hàng hóa. | Thấp |
| B | **Theo module dọc:** mỗi module chứa đủ chứng từ, báo cáo và danh mục | Người dùng một vai trò thấy mọi thứ trong một nhóm | Tiền & Công nợ khoảng 20 mục. Danh mục dùng chung không biết đặt đâu. Menu 3 tầng. | Thấp–TB |
| C | **A + trang hub** `/reports`, `/catalogs` dạng lưới thẻ | Sidebar ngắn nhất (~15 mục), có mô tả cho từng báo cáo | Thêm 1 lần bấm. Thêm 2 trang. Trạng thái active kém rõ. | TB |

**Chọn A.** Số mục mỗi nhóm cân bằng nhất. Việc người dùng một vai trò phải mở 2 nhóm được bù lại nhờ **Ghim**. Lọc quyền đã ẩn sẵn các nhóm không liên quan.

---

## Recommended Design

### Cây menu (sau khi lọc quyền)

| Nhóm (`id`) | Mục |
|---|---|
| *(cố định)* | Tổng quan |
| **Bán hàng** (`sales`) | Báo giá · Khách hàng · Tạo mã QR thanh toán |
| **Kho** (`inventory`) | Phiếu nhập kho · Phiếu xuất kho · Tồn đầu kỳ |
| **Thu chi** (`cash`) | Phiếu thu · Phiếu chi · Chuyển quỹ |
| **Công nợ** (`debt`) | Đối trừ chứng từ · Bù trừ công nợ · Nợ đầu kỳ · Hạn mức nợ |
| **Báo cáo** (`reports`) | *Doanh thu:* Doanh thu, Doanh thu sale, Doanh thu xe, Hiệu suất sale · *Kho:* Tồn kho, Thẻ kho · *Quỹ:* Sổ quỹ · *Công nợ:* Số dư công nợ, Sổ chi tiết công nợ, Công nợ theo chứng từ, Tuổi nợ, Nợ quá hạn, Công nợ theo nhân viên, Biên bản đối chiếu |
| **Danh mục** (`catalogs`) | Hàng hóa · Nhóm hàng hóa · Nhà cung cấp · Kho hàng · Lý do nhập xuất · Lý do thu/chi · Hình thức thanh toán · Quỹ |
| **Hệ thống** (`system`) | Cài đặt của tôi · Cấu hình hệ thống · Quản lý người dùng · Phân quyền |

- Đổi "Setting" thành "Hệ thống". Mục danh mục "Kho" đổi thành "Kho hàng" để không trùng tên nhóm.
- Mỗi mục một icon riêng, và không trùng icon của nhóm (hiện `Warehouse`, `Settings`, `Scale`, `Wallet`, `BarChart3` đang được cả nhóm lẫn mục dùng). Plan phải có bảng icon cho từng mục. Mỗi nhóm có ô icon màu riêng (phong cách C, bảng màu ở [section-01 §2](section-01-ui-design.md)), dùng cả ở chế độ thu gọn.
- Tiêu đề con không còn mục nào sau khi lọc quyền thì ẩn. Nhóm còn 1 mục vẫn giữ là nhóm.
- Mặc định lần đầu: mọi nhóm mở, trừ **Danh mục** và **Hệ thống**.
  - Đánh đổi đã chấp nhận: SALES (Hàng hóa, Nhóm hàng hóa) và WAREHOUSE (Nhà cung cấp) có mục hay dùng nằm trong Danh mục đang gập. Bù lại: mở nhóm một lần thì được nhớ, nhóm tự mở khi đang ở trang của nó, và có Ghim.

### Bố cục và tương tác

1. **Ô "Tìm chức năng…"** dính ở đầu sidebar.
   - Lọc theo nhãn mục, nhãn nhóm và tiêu đề con, không phân biệt dấu. Ví dụ "doi tru" khớp "Đối trừ chứng từ", "bao cao" ra mọi báo cáo.
   - Khi đang tìm: hiện mọi nhóm có kết quả (bỏ qua trạng thái gập/mở), ẩn khu Ghim và Tổng quan. Tổng quan không nằm trong kết quả tìm.
   - Enter mở kết quả đầu tiên, ↓ chuyển focus vào danh sách kết quả, Esc xóa ô. Không có kết quả thì hiện "Không tìm thấy chức năng". Chuyển trang xong thì tự xóa ô.
2. **Chip ghim ★** (phong cách C): chỉ hiện khi có mục ghim. Hiện tối đa 6 chip, phần dư gom vào "+N". Nút ☆ hiện khi di chuột trên desktop, luôn hiện mờ trên mobile/touch. Chi tiết ở [section-01 §3.2](section-01-ui-design.md).
3. **Tổng quan**: luôn cố định, không ghim được (đường dẫn đổi giữa `/` và `/admin/dashboard` theo quyền).
4. **Các nhóm dạng accordion**: mở được nhiều nhóm cùng lúc. `button` có `aria-expanded`/`aria-controls`.
   - Mỗi khi `pathname` đổi, nếu nhóm chứa trang hiện tại đang gập thì **mở tạm**: state trong `Sidebar`, không ghi vào store. Nhờ vậy, nhóm người dùng đã gập không bị mở vĩnh viễn chỉ vì ghé qua một trang.
   - Bấm tiêu đề nhóm thì ghi lựa chọn vào store và bỏ trạng thái mở tạm của nhóm đó.
5. Phần nav cuộn được, ô tìm kiếm đứng yên.

**Chế độ thu gọn (64px):**
- Icon 🔍 mở rộng sidebar và focus ô tìm. Việc mở rộng được lưu như khi bấm nút thu gọn; không tự thu lại sau khi chọn kết quả.
- Icon ★ mở flyout danh sách ghim.
- Mỗi nhóm 1 ô icon màu. **Bấm**, Enter hoặc Space mở flyout bên phải; rê chuột chỉ hiện tooltip. Esc đóng và trả focus về icon. Nhóm chứa trang hiện tại có viền và vạch `primary`.

**Mobile drawer:** giống bản rộng.

**Highlight trang hiện tại:** chọn mục có `to` là **tiền tố khớp dài nhất** của `pathname`, thay cho NavLink prefix mặc định. Cách này sửa lỗi `/settings` sáng kép và giữ đúng cho trang chi tiết như `/quotations/123`.
- Khớp theo ranh giới segment: `pathname === to || pathname.startsWith(to + '/')`. Không so chuỗi thuần, vì `/settings` sẽ khớp nhầm `/settings-x`.
- `/` chỉ khớp chính xác, vì nó là tiền tố của mọi đường dẫn.
- Tính cả mục Tổng quan, vì mục này nằm ngoài `groups`.
- Trang không khớp mục nào (ví dụ `/admin/user-settings/:userId`) thì không tô mục nào.

### Chi tiết kỹ thuật

- **`nav-config.ts`:**
  - `NavGroup` thêm `id`, `icon`, `defaultOpen`. `NavItem` thêm `section?: string`. Danh sách vẫn phẳng, không lồng.
  - `visibleNavGroups()` giữ chữ ký, chỉ trả thêm field.
- **`ui-store.ts`:**
  - `sidebarGroupOpen: Record<string, boolean>`: chỉ lưu lựa chọn người dùng tự bấm (việc tự mở theo trang hiện tại không ghi vào đây). Nhóm chưa có giá trị thì lấy `defaultOpen`. Dùng chung cho mọi user trên trình duyệt, vì đây chỉ là sở thích hiển thị.
  - `pinnedNavPaths: Record<userId, string[]>`.
  - Bổ sung cả hai vào `partialize`.
  - Ghim của mục đã mất quyền hoặc không còn route thì ẩn khi render, không xóa.
- **Component** (thư mục `components/layout/sidebar/`):
  - Danh sách component đầy đủ ở [section-01 §4](section-01-ui-design.md): `sidebar`, `sidebar-search`, `sidebar-pin-chips`, `sidebar-group`, `sidebar-item`, `sidebar-rail`, `sidebar-flyout`.
  - `nav-search.ts` (hàm thuần): `normalizeVi()` (NFD bỏ dấu, `đ` thành `d`), `filterNavGroups(groups, query)` và `findActiveItem()` (chọn mục có tiền tố dài nhất).
  - Màu nhóm là CSS variable `--nav-*` trong `index.css` (sáng và tối), map sang `tailwind.config.ts`. Giá trị viết dạng kênh HSL trần như token sẵn có (`--primary: 221.2 83.2% 53.3%`), vì Tailwind đọc qua `hsl(var(--x))`.
- **Sửa scroll:** đổi thành `grid-rows-[4rem_minmax(0,1fr)]` trong `app-layout.tsx`. Sửa ở tầng track nên không phụ thuộc từng grid item. Trong sidebar mới, vùng cuộn (dưới ô tìm) cần `flex-1 min-h-0 overflow-y-auto`.

---

## Delivery

### Xếp thứ tự với Round 2B

Phase 04/05 của Round 2B thêm 5 báo cáo vào nhóm "Tiền & Công nợ" (đã xong 2: Công nợ theo chứng từ, Tuổi nợ), và `nav-config.test.ts` kiểm tra đúng danh sách mục của nhóm này. Làm song song thì sẽ xung đột.

1. **Hotfix scroll ngay:** PR nhỏ 1 dòng (`minmax(0,1fr)`) vào `main`.
2. **Thiết kế lại sau khi Round 2B merge:** tạo nhánh mới từ `main`, chuyển 5 báo cáo công nợ vào *Báo cáo › Công nợ*.

### Các phase triển khai (mỗi phase là một commit, test xanh)

1. Chia lại nhóm, thêm `id`/`icon`/`section`, thay icon trùng, highlight theo tiền tố dài nhất. `nav-config.test.ts` gần như phải viết lại vì tên nhóm đổi. Test ở `roles-matrix-page` và `cash-settings-page` có chữ "Tiền & Công nợ" là nhóm **quyền**, không phải nhóm menu, nên không bị ảnh hưởng.
2. Accordion, lưu trạng thái mở/gập, tự mở nhóm chứa trang hiện tại.
3. Tìm kiếm không phân biệt dấu.
4. Ghim theo user.
5. Flyout ở chế độ thu gọn, nút 🔍 mở rộng sidebar.
6. Cập nhật docs: `code-standard/conventions.md` (cách khai báo nav item và section) và `codebase/directory-structure.md`.

### Kiểm thử (Vitest + RTL, test đặt cạnh file)

Danh sách đầy đủ theo từng file test ở [section-01 §7](section-01-ui-design.md). Tóm tắt:

- **Hàm thuần:** `normalizeVi`, `filterNavGroups` (cả trường hợp khớp theo nhãn nhóm hoặc tiêu đề con), chọn mục active (`/settings/...`, `/quotations/123`, `/reports/sales-revenue/:saleUserId`, `/` chỉ khớp chính xác, trang không khớp mục nào).
- **`nav-config`:** mỗi mục có icon khác nhau và khác icon nhóm.
- **`visibleNavGroups`:** SALES, WAREHOUSE và admin ra đúng thứ tự nhóm. Tiêu đề con không còn mục thì ẩn.
- **`ui-store`:** ghim tách theo user, field mới được persist, state version 1 cũ vẫn load được.
- **Component:**
  - Bấm tiêu đề nhóm thì `aria-expanded` đổi.
  - Đổi trang thì nhóm chứa trang đó tự mở, nhưng store không đổi.
  - Tìm kiếm: Enter chuyển trang, Esc xóa ô.
  - Ghim và bỏ ghim được.
  - Flyout mở bằng click hoặc bàn phím, Esc đóng.
- **Thủ công** (jsdom không đo layout): tài khoản admin, cửa sổ cao khoảng 700px, thử chế độ rộng, thu gọn và mobile drawer. Thử thêm tài khoản SALES và WAREHOUSE.

### Rollout

- Chỉ sửa frontend: không API, không migration. State persist chỉ thêm field.
- Người dùng phải làm quen vị trí mới. Ghim và tìm kiếm giúp giảm bớt việc này. Thông báo ngắn trong nhóm chat khi deploy.

---

## Open Questions

- ~~Thứ tự mục trong *Báo cáo › Công nợ* lấy theo plan Round 2B. Cần xác nhận lại khi Round 2B xong.~~ Đã chốt: dùng đúng thứ tự Round 2B đã merge: Số dư công nợ, Sổ chi tiết công nợ, Công nợ theo chứng từ, Tuổi nợ, Nợ quá hạn, Công nợ theo nhân viên, Biên bản đối chiếu.
- ~~Giới hạn số mục ghim?~~ Đã chốt: không giới hạn số ghim; chỉ hiện tối đa 6 chip, phần dư gom vào "+N".

---

## Next Steps

1. PR hotfix scroll (`grid-rows-[4rem_minmax(0,1fr)]`) vào `main`.
2. Hoàn tất và merge Round 2B (phase 04–06).
3. Chạy `write-plan` từ tài liệu này trên nhánh mới tạo từ `main`.
