---
key: settings-my-quotation-settings
title: Cài đặt của tôi
routes: [/settings/my-quotation-settings]
sources: [frontend/src/pages/settings/my-quotation-settings-page.tsx, frontend/src/features/me-settings/**, frontend/src/features/bank-accounts/**, frontend/src/features/branding/**, backend/src/OrderMgmt.Application/Identity/UserSettings/**, backend/src/OrderMgmt.Application/Payments/Services/UserBankAccountService.cs, backend/src/OrderMgmt.Application/Branding/**]
related: [quotations.list]
updated: 2026-10-15
---
## Dùng để
Quản lý cài đặt riêng của bạn: file Excel mẫu khi xuất báo giá và biên bản bàn giao, tài khoản ngân hàng nhận chuyển khoản qua mã QR, và (với người được phép) logo công ty.

## Các bước
1. {#tab} Chọn thẻ **Cài đặt báo giá**, **Tài khoản ngân hàng** hoặc **Logo công ty**.
2. {#template} Ở thẻ **Cài đặt báo giá**, bấm **Tải lên template riêng** (hoặc **Tải lên thay thế**) rồi chọn file .xlsx cho **Template Excel**.
3. {#handover} Làm tương tự ở hai khung **Template Biên bản bàn giao (có tiền)** và **Template Biên bản bàn giao (không tiền)**.
4. {#bank} Ở thẻ **Tài khoản ngân hàng**, bấm **Thêm tài khoản**, chọn **Ngân hàng**, nhập **Số tài khoản** và **Chủ tài khoản**, rồi bấm **Lưu**.
5. {#default} Chọn tài khoản dùng trước bằng nút **Đặt mặc định**; sửa hoặc bỏ tài khoản bằng **Sửa** và **Xoá**.
6. {#logo} Ở thẻ **Logo công ty**, bấm **Chọn file** trong khung **Logo ngang** hoặc **Logo vuông**, rồi bấm **Lưu thay đổi**.

## Lưu ý
- Nút ← quay lại Cấu hình nằm ở đầu trang, trước tiêu đề.
- Chưa tải template riêng thì hệ thống dùng template mặc định (Đang dùng template mặc định của hệ thống). Có template riêng thì dùng **Tải về**, **Tải lên thay thế** hoặc **Xoá**; xoá xong hệ thống quay về template mặc định.
- **Tải template hiện tại** tải về file đang được dùng, **Tải template mặc định** tải file gốc của hệ thống để bạn sửa làm mẫu.
- File template phải là .xlsx và tối đa 5MB; quá thì hệ thống báo File quá lớn hoặc Tải lên thất bại kèm lý do.
- Khung **Khoá theo trạng thái** chỉ để xem, do quản trị viên đặt. Hiện thông báo báo giá từ trạng thái **Đã gửi** hoặc **Đã xác nhận** trở lên không cho bạn chỉnh sửa; chưa cấu hình thì hiện Không có giới hạn nào.
- **Số tài khoản** gồm 6 đến 19 chữ số, **Chủ tài khoản** không được để trống. Tài khoản đầu tiên tự thành mặc định, và xoá tài khoản mặc định thì một tài khoản còn lại được thay vào.
- Thẻ **Logo công ty** chỉ hiện với người có quyền quản lý cài đặt người dùng. Logo là PNG, JPEG hoặc SVG, tối đa 2MB; **Logo ngang** hiện khi thanh bên mở rộng, **Logo vuông** khi thanh bên thu gọn hoặc trên màn hình nhỏ. Cần chọn ít nhất một logo mới lưu được.
