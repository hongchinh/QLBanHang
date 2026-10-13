# Phase 01 — Mockup HTML + user confirmation gate

**Status:** [x] complete
**Complexity:** S

## Objective
Tạo `mockup-table-header.html` trong thư mục plan, render visual mục tiêu của header bảng list (nền `#005bac`, chữ trắng `font-semibold`). Mockup demo 2 state Card (có/không `overflow-hidden`) cạnh nhau để user quyết định có cần fix corner artifact hay không. User confirm visual trước khi Phase 02 chạy.

## Files
- `docs/plans/260513-1647-table-header-brand-color/mockup-table-header.html` (new — confirmation artifact, không phải file production)

## Tasks

1. **Tạo `mockup-table-header.html`** (đã được Plan tạo cùng folder).
   - Self-contained HTML, inline `<style>`, không phụ thuộc framework/asset ngoài.
   - 2 card cạnh nhau (stacked nếu màn hình hẹp):
     - **Card A — "Without overflow-hidden"**: Card có `border-radius: 8px` (mô phỏng shadcn `rounded-lg`) NHƯNG không `overflow: hidden`. Header table tràn ra ngoài rounded corner → user thấy rõ "square blue corner artifact".
     - **Card B — "With overflow-hidden"**: Card y hệt, nhưng có `overflow: hidden`. Header bo theo corner của Card → clean.
   - Mỗi card chứa:
     - Card padding/border mô phỏng shadcn (`bg-card`, `border`, `shadow-sm`, `rounded-lg`).
     - Table 8 cột: Mã / Tên / MST / Địa chỉ / SĐT / Loại / Status / Actions.
     - Header band: `background: #005bac`, `color: #fff`, `font-weight: 600`, `height: 40px`, `padding: 0 8px`, `text-align: left`.
     - 1 cột (ví dụ "Tên") có sort icon SVG (mũi tên dùng `currentColor`) để demo icon tự thừa kế trắng.
     - 5 sample rows khách hàng (KH001..KH005) — text mặc định, hover light gray.
   - Annotation block phía trên/dưới mockup ghi rõ:
     - `Header background: #005bac`
     - `Header text: #ffffff, font-weight 600 (semibold)`
     - `Contrast ratio 5.69:1 — WCAG AA pass cho normal text`
     - Hỏi rõ: "Nếu Card A trông xấu → Phase 02 sẽ apply `overflow-hidden` cho `<Card>` ở 3 trang list."

2. **User confirmation gate**:
   - In đường dẫn tuyệt đối cho user: `d:\Projects\QLBanHang\docs\plans\260513-1647-table-header-brand-color\mockup-table-header.html`.
   - Dùng `AskUserQuestion` với 4 options:
     - `Confirm — apply primitive change ONLY (Card A is fine)` → Phase 02 chỉ sửa `table.tsx`.
     - `Confirm — apply primitive change + Card overflow-hidden (need clean rounded corners)` → Phase 02 sửa `table.tsx` + 3 file list page.
     - `Need adjustment` (user nêu cụ thể: màu, weight, padding, v.v.)
     - `Cancel plan`

3. **Xử lý kết quả**:
   - Nếu chọn 1 hoặc 2: ghi nhận quyết định vào phase note ("Card fix: ON/OFF"), mark phase complete, Phase 02 đọc note để biết có sửa Card không.
   - Nếu `Need adjustment`: sửa mockup theo feedback (CHỈ HTML, không sửa SUMMARY/decisions). Re-ask.
   - Nếu `Cancel plan`: dừng, không sửa file production.

## Verification
- File `mockup-table-header.html` mở được trực tiếp trong Chrome/Edge (double-click hoặc `start mockup-table-header.html`).
- Render không lỗi (không cần devserver).
- User reply confirm qua AskUserQuestion (kèm Card-fix decision).

## Exit Criteria
- File `mockup-table-header.html` tồn tại trong thư mục plan.
- User đã reply confirm visual + chốt có/không Card overflow-hidden.
- Quyết định Card-fix được ghi vào phần đầu file Phase 02 (dưới header) để execute-plan đọc chính xác.
- Phase status → `[x] complete`.
