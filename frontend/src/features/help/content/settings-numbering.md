---
key: settings-numbering
title: Đánh số chứng từ
routes: [/settings/numbering]
sources: [frontend/src/pages/settings/numbering-settings-page.tsx, frontend/src/features/inventory-settings/**, frontend/src/features/cash-common/labels.ts, backend/src/OrderMgmt.Application/Inventory/Settings/**, backend/src/OrderMgmt.Application/Inventory/Numbering/**]
related: [settings-inventory, settings-cash]
updated: 2026-10-15
---
## Dùng để
Đặt mẫu số cho từng loại chứng từ của chi nhánh làm việc: ký hiệu, độ dài phần số, cách đặt lại số và mẫu ghép. Mỗi loại chứng từ có một khung riêng.

## Các bước
1. {#card} Tìm khung của loại chứng từ cần đổi, ví dụ **Phiếu nhập kho** hay **Phiếu thu**.
2. {#prefix} Nhập **Ký hiệu** và **Độ dài số**.
3. {#reset} Chọn **Đặt lại số**: **Không**, **Theo tháng** hoặc **Theo năm**.
4. {#pattern} Nhập **Mẫu** ghép từ các ký hiệu `{KH}`, `{STT}`, `{THANG}`, `{NAM}`; dòng **Ví dụ** bên dưới cho xem trước số đầu tiên.
5. {#save} Bấm **Lưu** trong khung đó; hệ thống báo Đã lưu đánh số chứng từ.

## Lưu ý
- Nút ← quay lại Cấu hình nằm ở đầu trang, trước tiêu đề.
- Các khung gồm **Phiếu nhập kho**, **Phiếu xuất kho**, **Phiếu thu**, **Phiếu chi**, **Chuyển quỹ** và **Bù trừ công nợ**. Bạn chỉ thấy khung mình được sửa: hai loại phiếu kho cần quyền cấu hình kho, bốn loại còn lại cần quyền cấu hình tiền và công nợ.
- Cài đặt này theo chi nhánh làm việc đang chọn ở đầu trang; mỗi chi nhánh có mẫu riêng.
- **Mẫu** phải có `{STT}`. Chọn **Theo tháng** thì mẫu phải có cả `{THANG}` và `{NAM}`; chọn **Theo năm** thì mẫu phải có `{NAM}`. Sai thì lỗi hiện ngay dưới ô **Mẫu**.
- **Độ dài số** từ 1 đến 10, **Ký hiệu** tối đa 20 ký tự, **Mẫu** tối đa 100 ký tự. Số chứng từ sinh ra không được dài quá 50 ký tự.
- Mẫu mới chỉ dùng cho chứng từ lập sau khi lưu; số của chứng từ đã lập giữ nguyên.
