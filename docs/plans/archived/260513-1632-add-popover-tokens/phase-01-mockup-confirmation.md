# Phase 01 — Mockup HTML + user confirmation gate

**Status:** [x] complete
**Complexity:** S

## Objective
Tạo file `mockup-dropdown.html` trong thư mục plan, render đúng visual mục tiêu của dropdown search Khách hàng (light mode) sau khi `--popover` tokens được apply. User mở file trực tiếp trong trình duyệt, confirm trước khi Phase 02 chạy.

## Files
- `docs/plans/260513-1632-add-popover-tokens/mockup-dropdown.html` (new — tạo ở Phase 01, không phải file production)

## Tasks
1. **Tạo `mockup-dropdown.html`** (đã được Plan tạo sẵn cùng folder).
   - Self-contained HTML (inline `<style>`), không phụ thuộc framework/asset ngoài.
   - Layout: 1 input search ở trên (mô phỏng input của `CustomerAutocomplete`), 1 dropdown panel bên dưới (background trắng đặc — màu mục tiêu `hsl(0 0% 100%)`), header meta + kbd hints (Tab/Enter/Esc), table 6 cột (Mã, Tên, MST, Địa chỉ, SĐT, Loại) với 5 sample rows, 1 row được highlight bằng `bg-accent` (HSL `210 40% 96.1%`).
   - Dưới dropdown render thêm 1 vài "background sample rows" (ví dụ 1 card form với input khác) để user **thấy rõ** dropdown KHÔNG bị xuyên thấu — đây chính là bug đang fix.
   - Footer mockup ghi rõ chú thích: "Đây là mockup confirm trước khi apply tokens. Sau khi user confirm → Phase 02 sẽ thêm `--popover: 0 0% 100%` (light) / `222.2 84% 4.9%` (dark) vào index.css."

2. **User confirmation gate**:
   - Hiển thị đường dẫn tuyệt đối cho user: `d:\Projects\QLBanHang\docs\plans\260513-1632-add-popover-tokens\mockup-dropdown.html`.
   - Dùng `AskUserQuestion` với options:
     - `Confirm mockup → proceed Phase 02`
     - `Need adjustment` (yêu cầu user nêu cụ thể: màu nền, kbd hints, sample rows, v.v.)
     - `Cancel plan`

3. **Xử lý kết quả**:
   - Nếu `Confirm`: mark phase complete, continue.
   - Nếu `Need adjustment`: sửa mockup theo feedback (chỉ sửa HTML, **không** sửa SUMMARY/tokens), re-ask cho tới khi confirm.
   - Nếu `Cancel plan`: dừng execute, không sửa file production.

## Verification
- File `mockup-dropdown.html` mở được trực tiếp trong Chrome/Edge (double-click hoặc `start mockup-dropdown.html`).
- Render không lỗi (không cần devserver).
- User reply `Confirm` qua AskUserQuestion.

## Exit Criteria
- File `mockup-dropdown.html` tồn tại trong thư mục plan.
- User đã reply confirm visual.
- Phase status → `[x] complete`.
