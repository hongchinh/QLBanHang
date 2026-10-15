---
key: customers.list
title: Khách hàng
routes: [/customers]
sources: [frontend/src/pages/customers/customer-list-page.tsx, frontend/src/features/customers/**, backend/src/OrderMgmt.Application/Catalog/Customers/**]
related: [quotations.form]
updated: 2026-10-15
---
## Dùng để
Xem, tìm, thêm, sửa và xóa danh mục khách hàng. Khách trong danh mục là người bạn chọn khi lập báo giá và phiếu xuất kho.

## Các bước
1. {#search} Gõ tên, mã, số điện thoại hoặc MST vào ô tìm kiếm, danh sách tự lọc sau giây lát.
2. {#read} Đọc **Mã KH**, **Tên khách hàng**, **Nhóm** (Công ty, Đại lý, Khách lẻ, Công trình) và **Trạng thái** (Đang dùng hoặc Ngừng).
3. {#add} Bấm **Thêm khách hàng** để tạo khách mới.
4. {#edit} Bấm biểu tượng cây bút ở cuối dòng để sửa thông tin khách.
5. {#delete} Bấm biểu tượng thùng rác ở cuối dòng, đọc hộp thoại **Xóa khách hàng?** rồi bấm **Xóa**.
6. {#paging} Khi có hơn 20 khách, dùng **Trước** và **Sau** ở cuối danh sách để đổi trang.

## Lưu ý
- Nút thêm, sửa, xóa chỉ hiện khi tài khoản của bạn có quyền tương ứng.
- Khách đã có phiếu nhập/xuất kho, phiếu thu/chi hoặc công nợ không xóa được: hệ thống báo Không thể xóa kèm lý do. Muốn ngưng giao dịch, đặt Trạng thái Ngừng sử dụng trong màn hình sửa khách.
- Khách đồng thời là nhà cung cấp chỉ xóa được khi bạn cũng có quyền xóa nhà cung cấp.
