---
key: admin-users
title: Quản lý người dùng
routes: [/admin/users]
sources: [frontend/src/pages/admin/users-list-page.tsx, frontend/src/pages/admin/components/user-actions-menu.tsx, frontend/src/pages/admin/components/user-form-dialog.tsx, frontend/src/pages/admin/components/reset-password-dialog.tsx, frontend/src/features/admin-users/**, backend/src/OrderMgmt.Application/Identity/Admin/Services/AdminUserService.cs, backend/src/OrderMgmt.Application/Identity/Admin/Validators/*User*, backend/src/OrderMgmt.Application/Identity/Admin/Validators/ResetPasswordRequestValidator.cs]
related: [admin-user-settings, admin-users-transfer-quotations]
updated: 2026-10-15
---
## Dùng để
Xem danh sách tài khoản đăng nhập, tạo thêm user, sửa thông tin, đặt lại mật khẩu, khoá hoặc mở khoá và xoá user. Từ đây cũng mở được cấu hình báo giá và chuyển nhượng báo giá của từng user.

## Các bước
1. {#search} Gõ username hoặc họ tên vào ô tìm kiếm, tích **Chỉ user đang hoạt động** nếu chỉ cần user còn đăng nhập được; danh sách tự lọc sau giây lát.
2. {#add} Bấm **Thêm user**, nhập **Username**, **Họ tên**, **Email**, **Số điện thoại**, chọn **Vai trò**, **Trạng thái**, **Chi nhánh mặc định**, nhập **Mật khẩu** rồi bấm **Tạo**.
3. {#edit} Bấm nút ba chấm ở cuối dòng, chọn **Sửa**, đổi thông tin rồi bấm **Lưu**.
4. {#reset} Chọn **Đặt lại mật khẩu**, nhập **Mật khẩu mới** và **Xác nhận mật khẩu** rồi bấm **Đặt lại**.
5. {#lock} Chọn **Khoá tài khoản** (hoặc **Mở khoá tài khoản** với user đã nghỉ), đọc hộp thoại rồi bấm **Khoá** hoặc **Mở khoá**.
6. {#delete} Chọn **Xoá**, đọc hộp thoại **Xoá user?** rồi bấm **Xoá**.
7. {#more} Chọn **Cấu hình báo giá** hoặc **Chuyển nhượng báo giá** để sang màn hình riêng của user đó.

## Lưu ý
- Nút ← quay lại Cấu hình nằm ở đầu trang, trước tiêu đề.
- Từ khoá tìm và ô **Chỉ user đang hoạt động** được giữ trên địa chỉ trang. Tìm theo họ tên không cần gõ dấu.
- Cột **Trạng thái** là **Đang dùng** hoặc **Đã nghỉ**. User đã xoá vẫn nằm trong danh sách với **Đã nghỉ** nhưng không sửa, đặt lại mật khẩu hay mở khoá được nữa (hệ thống báo không tìm thấy); tích **Chỉ user đang hoạt động** để ẩn.
- Các nút **Thêm user**, **Sửa**, **Đặt lại mật khẩu**, khoá/mở khoá, **Cấu hình báo giá**, **Chuyển nhượng báo giá** và **Xoá** chỉ hiện khi tài khoản của bạn có quyền tương ứng. Cả màn hình chỉ mở được khi bạn có quyền cấu hình thiết lập của user khác. Danh sách còn cần quyền xem người dùng; thiếu quyền này thì trang hiện lỗi thay cho danh sách.
- **Username** từ 3 đến 50 ký tự, chỉ gồm chữ, số và các ký tự . _ - ; không sửa được sau khi tạo. **Mật khẩu** tối thiểu 8 ký tự và có cả chữ lẫn số. **Họ tên** và **Email** là bắt buộc.
- Để trống **Chi nhánh mặc định** khi tạo thì user thuộc Chi nhánh chính. Username hoặc Email đã có thì hệ thống báo đã tồn tại.
- Khoá tài khoản, đặt user về **Đã nghỉ**, đặt lại mật khẩu hay xoá đều đăng xuất user khỏi mọi thiết bị.
- Trên dòng của chính bạn, **Khoá tài khoản** (khi bạn đang dùng) và **Xoá** bị mờ, không bấm được.
- Hệ thống không cho khoá, đặt về **Đã nghỉ**, xoá hoặc đổi vai trò của quản trị viên đang dùng cuối cùng: báo Không thể thao tác — đây là quản trị viên Active duy nhất của hệ thống.
- Xoá user còn báo giá chưa huỷ thì báo Không thể xoá kèm số báo giá; nếu bạn có quyền chuyển nhượng, màn hình chuyển sang trang Chuyển nhượng báo giá để xử lý trước.
