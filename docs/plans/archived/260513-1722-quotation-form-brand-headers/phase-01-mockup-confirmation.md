# Phase 01 — Mockup HTML + user confirmation gate

**Status:** [x] complete
**Complexity:** S

## Objective
Tạo `mockup-quotation-form-headers.html` preview cả 3 surface (Customer popover + Product popover + Line-items grid header) cùng lúc trong một trang HTML self-contained. User mở browser, xem cohesive whole, confirm trước khi Phase 02 chạy.

## Files
- `docs/plans/260513-1722-quotation-form-brand-headers/mockup-quotation-form-headers.html` (new — confirmation artifact, không phải production)

## Tasks

1. **Tạo `mockup-quotation-form-headers.html`** (đã được Plan tạo cùng folder).
   - Self-contained, inline `<style>`, không có framework dependency, double-click mở trong Chrome/Edge.
   - Layout: 1 trang mô phỏng `/quotations/new`, gồm:
     - Card "Thông tin chung" có Customer search popover **OPEN**:
       - Meta band phía trên: `bg-muted/30` muted text "Tìm thấy 5 khách hàng đang hoạt động" + kbd hints. **Stays muted** (không đổi).
       - Column `<thead>`: nền `#005bac`, chữ trắng `font-weight: 600`, 6 cột (Mã / Tên / MST / Địa chỉ / SĐT / Loại).
       - 5 sample rows muted body — KH001..KH005.
     - Card "Chi tiết hàng hóa" có:
       - `.accounting-grid` header row: nền `#005bac` toàn bộ (kể cả cell `#`), chữ trắng, 10–11 cột (# / Mã hàng / Tên hàng / ĐVT / Loại / D×R×Dày×Tấm / SL / Đơn giá / Giá vốn / Thành tiền).
       - **Không có sọc xám 1px** giữa các header cell (demo border-color override).
       - 2 body rows: row-no cell muted (intentional), input cells xanh nhẹ với value sample (1 row có "Mã hàng" cell + Product typeahead popover OPEN).
     - Product typeahead popover (overlay trên cell "Mã hàng" của row 1):
       - Meta band muted "Tìm thấy 4 sản phẩm" + kbd hints.
       - Column thead: `#005bac` + trắng, 5 cột (Mã / Tên / Loại / Quy cách / Giá bán).
       - 4 sample rows product.
   - Annotation block phía trên/dưới mockup:
     - `Header bg: #005bac`
     - `Header text: #ffffff, font-weight 600 (semibold)`
     - `Contrast 5.69:1 — WCAG AA pass cho normal text`
     - `Only the column thead is re-skinned. Meta band stays muted. Body rows stay unchanged.`
   - (Optional) Hiển thị 1 "before vs after" mini block hoặc dải swatch để user thấy diff so với spreadsheet gray cũ — KHÔNG bắt buộc.

2. **User confirmation gate**:
   - In đường dẫn tuyệt đối: `d:\Projects\QLBanHang\docs\plans\260513-1722-quotation-form-brand-headers\mockup-quotation-form-headers.html`.
   - Dùng `AskUserQuestion` với 3 options:
     - `Confirm — proceed to Phase 02` → apply 3-file diff.
     - `Need adjustment` (user nêu cụ thể).
     - `Cancel plan`.

3. **Xử lý kết quả**:
   - `Confirm`: mark Phase 01 complete, continue Phase 02.
   - `Need adjustment`: sửa mockup theo feedback (CHỈ HTML, không sửa SUMMARY/decisions). Re-ask.
   - `Cancel plan`: dừng, không sửa file production.

## Verification
- File `mockup-quotation-form-headers.html` mở được trong Chrome/Edge.
- Render không lỗi, không cần devserver.
- User reply confirm qua AskUserQuestion.

## Exit Criteria
- File mockup tồn tại trong thư mục plan.
- User đã reply confirm.
- Phase status → `[x] complete`.
