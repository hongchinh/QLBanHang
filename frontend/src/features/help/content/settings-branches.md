---
key: settings-branches
title: Chi nhánh
routes: [/settings/branches]
sources: [frontend/src/pages/settings/branches-page.tsx, frontend/src/pages/settings/branch-form-dialog.tsx, frontend/src/pages/settings/demo-branch-delete-dialog.tsx, frontend/src/features/branches/**, backend/src/OrderMgmt.Application/Organization/Branches/**]
related: [settings-period-lock, debt-opening, inventory-opening-stock]
updated: 2026-10-15
---
## Dùng để
Quản lý danh sách chi nhánh: thêm, sửa, xóa và đặt ngày đầu kỳ. Có thể tạo thêm chi nhánh demo kèm dữ liệu mẫu để đào tạo.

## Các bước
1. {#add} Bấm **Thêm chi nhánh**, nhập **Mã chi nhánh** và **Tên chi nhánh**, có thể nhập **Địa chỉ**.
2. {#demo} Muốn chi nhánh để đào tạo, tích **Tạo kèm dữ liệu mẫu để đào tạo** trước khi bấm **Tạo mới**.
3. {#edit} Sửa tên hoặc địa chỉ bằng nút hình bút ở cuối dòng (Sửa chi nhánh), rồi bấm **Cập nhật**.
4. {#opening} Đặt ngày bắt đầu sổ sách bằng nút hình lịch ở cuối dòng (Đặt ngày đầu kỳ), chọn **Ngày đầu kỳ** rồi bấm **Lưu**.
5. {#delete} Xóa chi nhánh bằng nút hình thùng rác ở cuối dòng (Xóa chi nhánh), rồi bấm **Xóa** để xác nhận.
6. {#demo-delete} Với chi nhánh demo, nút thùng rác là Xóa chi nhánh demo: nhập mã chi nhánh để xác nhận rồi bấm **Xóa vĩnh viễn**.

## Lưu ý
- Nút ← quay lại Cấu hình nằm ở đầu trang, trước tiêu đề.
- **Mã chi nhánh** chỉ nhập khi tạo và không đổi được sau đó; tối đa 50 ký tự. **Tên chi nhánh** tối đa 255 ký tự, **Địa chỉ** tối đa 1000.
- Mã đã có thì hệ thống báo Mã chi nhánh đã tồn tại.
- Chi nhánh demo có nhãn **Demo** ở cột **Loại**. Trong lúc tạo dữ liệu mẫu, nhãn hiện Đang tạo dữ liệu kèm phần trăm, và nút xóa demo bị khóa; nếu tạo hỏng thì nhãn hiện Lỗi tạo dữ liệu.
- Hộp thoại xóa demo cho biết số chứng từ sẽ mất; toàn bộ dữ liệu của chi nhánh bị xóa vĩnh viễn, kể cả chứng từ do bạn tự nhập. Người dùng đang lấy chi nhánh này làm mặc định được chuyển về Chi nhánh chính.
- **Ngày đầu kỳ** chỉ đổi được khi chi nhánh chưa có chứng từ, và không được sau ngày của chứng từ sớm nhất. Đã có tồn đầu kỳ, công nợ đầu kỳ hoặc số dư đầu quỹ thì không xóa được ngày này.
- Không xóa được chi nhánh chính, chi nhánh là mặc định của người dùng, chi nhánh còn kho, hoặc đã có chứng từ hay số dư đầu kỳ; hệ thống báo lý do cụ thể.
- Cột **Khóa sổ đến** chỉ để xem; đặt ngày khóa ở mục Khóa sổ.
