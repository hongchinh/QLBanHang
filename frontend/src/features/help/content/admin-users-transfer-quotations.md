---
key: admin-users-transfer-quotations
title: Chuyển toàn bộ báo giá
routes: [/admin/users/:userId/transfer-quotations]
sources: [frontend/src/pages/admin/bulk-transfer-page.tsx, frontend/src/features/admin-user-settings/**, frontend/src/features/admin-users/**, backend/src/OrderMgmt.Application/Identity/UserSettings/Services/QuotationBulkTransferService.cs, backend/src/OrderMgmt.Application/Identity/UserSettings/Validators/BulkTransferRequestValidator.cs, backend/src/OrderMgmt.WebApi/Controllers/AdminUserSettingsController.cs]
related: [admin-users, admin-user-settings]
updated: 2026-10-15
---
## Dùng để
Chuyển toàn bộ báo giá đang thuộc một user sang user khác, thường khi user đó nghỉ việc. Dòng mô tả dưới tiêu đề hiện mã định danh hệ thống (một chuỗi dài) của user nguồn, không phải username.

## Các bước
1. {#open} Mở màn hình bằng nút ba chấm ở dòng user trong Quản lý người dùng, chọn **Chuyển nhượng báo giá** (hoặc bấm **Mở trang chuyển nhượng** ở cấu hình báo giá của user).
2. {#to} Chọn **User nhận** trong danh sách, mỗi dòng ghi họ tên, username và vai trò.
3. {#reason} Nhập **Lý do (tuỳ chọn)** nếu muốn ghi lại, ví dụ User A nghỉ việc.
4. {#cancelled} Tích **Bao gồm báo giá đã huỷ** nếu cần chuyển cả báo giá đã huỷ.
5. {#go} Bấm **Chuyển nhượng**; hệ thống báo Đã chuyển báo giá kèm số báo giá đã chuyển.

## Lưu ý
- Nút ← quay lại nằm ở đầu trang, trước tiêu đề. Nó đưa về màn hình bạn vừa đến từ đó: Cấu hình báo giá của user, hoặc Quản lý người dùng (giữ nguyên từ khoá tìm và ô Chỉ user đang hoạt động); nếu không rõ thì về Quản lý người dùng.
- Nút **Chuyển nhượng** mờ cho đến khi bạn chọn **User nhận**. Danh sách **User nhận** chỉ có user đang hoạt động, không gồm user nguồn.
- Mặc định chỉ chuyển báo giá chưa huỷ. Chuyển xong, **User nhận** và **Lý do (tuỳ chọn)** được xoá để nhập lần sau; ô **Bao gồm báo giá đã huỷ** giữ nguyên và bạn vẫn ở lại trang này.
- Chạy lại an toàn: user nguồn hết báo giá thì lần sau chỉ báo Đã chuyển 0 báo giá.
- Mỗi báo giá được chuyển ghi lại lịch sử đổi chủ sở hữu và một dòng hoạt động Chuyển chủ sở hữu báo giá hàng loạt, kèm lý do nếu có. **Lý do (tuỳ chọn)** tối đa 500 ký tự.
- Nếu **User nhận** vừa bị khoá sau khi bạn mở trang, hệ thống báo Chuyển thất bại kèm User nhận đang bị vô hiệu hoá; lỗi khác cũng hiện Chuyển thất bại kèm lý do.
- Cần chuyển hết báo giá trước khi xoá user còn báo giá chưa huỷ; việc xoá sẽ báo Không thể xoá và đưa bạn đến trang này.
