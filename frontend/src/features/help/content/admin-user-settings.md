---
key: admin-user-settings
title: Cấu hình báo giá của user
routes: [/admin/user-settings/:userId]
sources: [frontend/src/pages/admin/user-settings-page.tsx, frontend/src/features/admin-user-settings/**, backend/src/OrderMgmt.Application/Identity/UserSettings/Services/UserQuotationSettingsService.cs, backend/src/OrderMgmt.Application/Sales/Quotations/Services/QuotationService.cs, backend/src/OrderMgmt.Application/Identity/UserSettings/Validators/UpdateLockAtRequestValidator.cs, backend/src/OrderMgmt.WebApi/Controllers/AdminUserSettingsController.cs]
related: [admin-users, admin-users-transfer-quotations, settings-my-quotation-settings]
updated: 2026-10-15
---
## Dùng để
Đặt từ trạng thái nào một user không còn sửa được báo giá, và xem template báo giá user đó đang dùng. Dưới tiêu đề là họ tên của user đang cấu hình.

## Các bước
1. {#open} Mở màn hình bằng nút ba chấm ở dòng user trong Quản lý người dùng, chọn **Cấu hình báo giá**.
2. {#lock} Trong khung **Khoá theo trạng thái**, chọn **Không khoá**, **Từ Đã gửi** hoặc **Từ Đã xác nhận**.
3. {#save} Bấm **Lưu**; hệ thống báo Đã cập nhật cấu hình khoá.
4. {#template} Xem khung **Template báo giá của user**: tên file user đã tải lên và ngày cập nhật, hoặc dòng user đang dùng template mặc định.
5. {#transfer} Bấm **Mở trang chuyển nhượng** trong khung **Chuyển nhượng báo giá** khi cần chuyển báo giá của user này sang người khác.

## Lưu ý
- Nút ← quay lại Quản lý người dùng nằm ở đầu trang, trước tiêu đề.
- Với mức khoá đã chọn, user không sửa được báo giá đang ở trạng thái đó hoặc các trạng thái sau nó: mở báo giá đó, user thấy dòng vàng báo cấu hình khoá của bạn không cho phép sửa. **Không khoá** là bỏ giới hạn.
- Người có quyền bypass khoá trạng thái báo giá (mặc định là vai trò ADMIN và MANAGER, nên màn hình ghi Admin/Manager bypass) không bị khoá này.
- Khung **Template báo giá của user** chỉ để xem; user tự tải template của mình ở thẻ Cài đặt báo giá của màn hình Cài đặt của tôi.
- Khung **Chuyển nhượng báo giá** chỉ hiện khi tài khoản của bạn có quyền chuyển báo giá của bất kỳ user nào.
- Lưu không được thì báo Lưu thất bại kèm lý do. Không mở được user thì màn hình chỉ hiện Không tìm thấy user.
