---
key: quotations.form
title: Báo giá
routes: [/quotations/new, /quotations/:id]
sources: [frontend/src/pages/quotations/quotation-form-page.tsx, frontend/src/pages/quotations/components/**, frontend/src/pages/quotations/utils/**, frontend/src/features/quotations/**, backend/src/OrderMgmt.Application/Sales/Quotations/**]
related: [customers.list]
updated: 2026-10-15
---
## Dùng để
Lập và sửa báo giá gửi khách: chọn khách, nhập hàng, xem tổng tiền rồi lưu, gửi, xác nhận hoặc in. Doanh thu của sale được ghi nhận khi báo giá được xác nhận.

## Các bước
1. {#customer} Gõ mã, tên, MST hoặc SĐT ở **Mã KH**, Enter để chọn; chưa có khách thì bấm nút dấu cộng bên cạnh.
2. {#header} Kiểm tra **Ngày báo giá**, **Ngày giao** và điền **Địa chỉ giao**, **Người nhận**, **Điện thoại** nếu cần.
3. {#lines} Nhập hàng ở **Chi tiết hàng hóa**: gõ **Mã hàng** để chọn sản phẩm, rồi nhập **SL** và **Đơn giá bán**.
4. {#totals} Nhập **CK**, **VC**, **Thuế %**, **Tạm ứng** ở khung **Tổng cộng**, rồi xem tổng tiền và **Còn lại** (khi có tạm ứng).
5. {#save} Bấm **Lưu tạm** (ở lại), **Lưu và thoát** hoặc **Lưu và in** (mở PDF); khi sửa báo giá bấm **Cập nhật**.
6. {#status} Bấm **Gửi** (báo giá Nháp), **Xác nhận** (báo giá Đã gửi) hoặc **Hủy** để đổi trạng thái khi sửa báo giá.
7. {#output} Bấm **In** hoặc **Excel** khi sửa báo giá, rồi chọn Báo giá hoặc Biên bản bàn giao (có tiền, không tiền).

## Lưu ý
- Phải chọn khách, có ít nhất 1 dòng, và mỗi dòng cần **Tên hàng**, **ĐVT** và số lượng khác 0, nếu không hệ thống báo lỗi ngay trên form.
- Chọn khách sẽ tự điền Địa chỉ giao, Người nhận, Điện thoại nếu các ô này đang trống. Ô chọn khách chỉ hiện khách đang dùng; khách đã Ngừng sử dụng thì báo giá cũ vẫn mở được và có dòng cảnh báo.
- Hàng tính theo m, m² hoặc m³: **SL** tự tính từ **D × R × Dày × Tấm**, các ô không dùng bị khóa.
- Ngày doanh thu chỉ để xem, hiện Chưa ghi nhận cho đến khi báo giá được xác nhận.
- Báo giá mới tự lưu nháp trên máy này, hiện Nháp chưa lưu; mở lại màn hình sẽ khôi phục, bấm dấu x cạnh nhãn để bỏ.
- Báo giá đã hủy, hoặc đang ở trạng thái mà cấu hình khóa của bạn không cho sửa, hiện cảnh báo vàng và chỉ còn **Nhân bản**. Người có quyền bỏ qua khóa vẫn sửa được.
- Hủy báo giá đã xác nhận cần quyền riêng và trừ doanh thu của sale. **KT xác nhận** chỉ hiện cho người có quyền kế toán xác nhận và không hoàn tác trực tiếp được.
- **Nhân bản** tạo báo giá mới từ báo giá này; thay đổi chưa lưu không được chép sang.
- Biểu tượng QR cạnh tổng tiền mở màn hình tạo QR thanh toán, và nút **Lịch sử** xem ai đã làm gì trên báo giá.
- Giá nhập, lợi nhuận chỉ hiện với người có quyền xem giá vốn. In và Excel dùng mẫu riêng của bạn nếu đã tải lên, không thì mẫu chung.

## Phím tắt
- `Ctrl S` Lưu (Lưu tạm khi lập mới, Cập nhật khi sửa)
- `Enter` Sang ô kế tiếp
- `Shift Enter` Về ô trước
- `Ctrl Enter` Xuống dòng trong ô Tên hàng
- `Insert` Thêm dòng
- `Ctrl Delete` Xóa dòng đang chọn
