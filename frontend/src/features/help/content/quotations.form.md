---
key: quotations.form
title: Báo giá
routes: [/quotations/new, /quotations/:id]
sources: [frontend/src/pages/quotations/quotation-form-page.tsx, frontend/src/components/partner-autocomplete/**, frontend/src/features/customers/use-quotation-customer-search.ts, frontend/src/pages/customers/customer-form-fields.tsx, frontend/src/pages/quotations/components/**, frontend/src/pages/quotations/utils/**, frontend/src/features/quotations/**, backend/src/OrderMgmt.Application/Sales/Quotations/**, frontend/src/features/products/components/product-quick-add-dialog.tsx, frontend/src/features/products/components/product-form-fields.tsx, frontend/src/features/products/split-typed-text.ts, frontend/src/lib/use-dialog-opening.ts, frontend/src/lib/dimension-columns.ts, frontend/src/lib/use-appeared-keys.ts]
related: [customers.list]
updated: 2026-10-16
---
## Dùng để
Lập và sửa báo giá gửi khách: chọn khách, nhập hàng, xem tổng tiền rồi lưu, gửi, xác nhận hoặc in. Trong báo cáo Doanh thu, doanh thu của sale được ghi nhận khi báo giá được xác nhận.

## Các bước
1. {#customer} Gõ mã, tên, MST, địa chỉ hoặc SĐT ở **Khách hàng**, Enter để chọn; chưa có khách thì `F4` hoặc nút **+** để thêm nhanh.
2. {#header} Kiểm tra **Ngày báo giá**, **Ngày giao** và điền **Địa chỉ giao**, **Người nhận**, **Điện thoại** nếu cần.
3. {#lines} Nhập hàng ở **Chi tiết hàng hóa**: gõ **Mã hàng** để chọn sản phẩm, rồi nhập **SL** và **Đơn giá bán**.
4. {#totals} Nhập **CK**, **VC**, **Thuế %**, **Tạm ứng** ở khung **Tổng cộng**, rồi xem tổng tiền và **Còn lại** (khi có tạm ứng).
5. {#save} Bấm **Lưu tạm** (ở lại), **Lưu và thoát** hoặc **Lưu và in** (mở PDF); khi sửa báo giá bấm **Cập nhật** (lưu rồi về danh sách).
6. {#status} Bấm **Gửi** (báo giá Nháp), **Xác nhận** (báo giá Đã gửi) hoặc **Hủy** để đổi trạng thái khi sửa báo giá.
7. {#output} Bấm **In** hoặc **Excel** khi sửa báo giá, rồi chọn Báo giá hoặc Biên bản bàn giao (có tiền, không tiền).

## Lưu ý
- Gõ chữ ở **Khách hàng** mà chưa chọn từ danh sách thì chưa lưu được và ô báo Vui lòng chọn khách hàng từ danh sách.
- Phải chọn khách, có ít nhất 1 dòng, và mỗi dòng cần **Tên hàng**, **ĐVT** và số lượng khác 0, nếu không hệ thống báo lỗi ngay trên màn hình.
- Chọn khách sẽ tự điền Địa chỉ giao, Người nhận, Điện thoại nếu các ô này đang trống. Danh sách gợi ý chỉ hiện khách đang dùng, gồm Mã, Tên, MST, Địa chỉ, SĐT, Loại; Enter chọn dòng đang sáng (mã gõ khớp hoàn toàn được ưu tiên), chọn xong con trỏ sang ô tên; báo giá cũ của khách đã Ngừng sử dụng vẫn mở được, kèm dòng cảnh báo.
- `F4` hoặc nút **+** (cũng là dòng Thêm mới trong danh sách) mở **Thêm nhanh khách hàng**: Enter đi qua từng ô, `Ctrl Enter` lưu khách mới (không lưu báo giá), `Esc` hủy và về ô mã. Cần quyền tạo khách hàng.
- `F4` trong ô **Mã hàng**, hoặc dòng **Thêm mới hàng hóa** ở cuối danh sách gợi ý (gõ mã chưa có rồi Enter là chọn dòng này), mở **Thêm nhanh hàng hóa**: chữ đã gõ điền sẵn vào Mã nếu không có dấu cách và không dấu, nếu không thì vào Tên; Enter đi qua từng ô, `Ctrl Enter` hoặc **Lưu và chọn** lưu hàng mới và điền vào dòng (không lưu báo giá), `Esc` hủy và về ô mã. Cần quyền tạo hàng hóa.
- Lưu hàng mới trong **Thêm nhanh hàng hóa** không được (ví dụ Mã hàng '…' đã tồn tại.) thì lỗi hiện ở đầu hộp thoại và hộp thoại vẫn mở để bạn sửa.
- Muốn nhập dòng hàng chưa có trong danh mục, gõ mã rồi bấm `Esc` để đóng danh sách; Enter sẽ sang **Tên hàng**.
- Con trỏ vào ô **Mã hàng** đã có mã không tự mở danh sách gợi ý; gõ để tìm lại.
- **Xem danh mục đầy đủ** (dòng cuối danh sách) mở danh mục khách hàng; ↑↓ rồi Enter để chọn. Cần quyền xem khách hàng.
- Hàng tính theo m dài, m² hoặc m³: nhập **Số tấm** và **Dài**, **Rộng**, **Dày** theo loại hàng, **SL** tự tính. Các cột **Số tấm**, **Dài**, **Rộng**, **Dày** chỉ hiện khi báo giá có hàng tính theo m dài, m² hoặc m³, theo loại cao nhất trong báo giá (như phiếu nhập, xuất kho): m dài hiện **Số tấm** và **Dài**, m² thêm **Rộng**, m³ thêm **Dày**. Ô mà dòng không dùng hiện —. Chọn hàng khác cho một dòng thì kích thước mà loại hàng mới không dùng bị xóa, các kích thước còn lại được giữ.
- Xóa dòng bằng `Ctrl Delete` đưa con trỏ sang dòng bên cạnh: cùng ô nếu dòng đó dùng ô ấy, nếu không thì ô **Mã hàng**; xóa bằng nút thùng rác đưa con trỏ về ô **Mã hàng** của dòng bên cạnh. Xóa dòng cuối cùng còn lại thì con trỏ sang nút **Thêm dòng**.
- Báo giá mới tự lưu nháp trên máy này, hiện Nháp chưa lưu; mở lại màn hình sẽ khôi phục, bấm dấu x cạnh nhãn để bỏ.
- Khi sửa báo giá, **Gửi**, **Xác nhận**, **In** và **Excel** làm việc trên bản đã lưu, nên hãy lưu thay đổi trước.
- **Hủy** báo giá Nháp hoặc Đã gửi chạy ngay, không hỏi lại, và báo giá đã hủy không sửa lại được. Hủy báo giá Đã xác nhận có hỏi, cần quyền riêng và trừ doanh thu của sale. **KT xác nhận** chỉ hiện cho báo giá Đã xác nhận, với người có quyền kế toán xác nhận, và không hoàn tác trực tiếp được.
- Báo giá đã hủy, báo giá có chủ sở hữu đã ngừng hoạt động, hoặc báo giá đã tới trạng thái bị khóa theo cấu hình của bạn hiện cảnh báo vàng kèm nút **Nhân bản**: lưu, **Gửi**, **Xác nhận** sẽ bị từ chối, nhưng vẫn in và xuất Excel được. Người có quyền bỏ qua khóa vẫn sửa được báo giá bị khóa.
- Khi sửa báo giá, biểu tượng QR cạnh tổng tiền mở màn hình tạo QR thanh toán và nút **Lịch sử** cho xem ai đã làm gì. Giá nhập, lợi nhuận chỉ hiện với người có quyền xem giá vốn. In và Excel dùng mẫu riêng của bạn nếu đã tải lên, không thì mẫu chung.

## Phím tắt
- `Ctrl S` Lưu (Lưu tạm khi lập mới, Cập nhật khi sửa)
- `Enter` Sang ô kế tiếp
- `Shift Enter` Về ô trước
- `Ctrl Enter` Xuống dòng trong ô Tên hàng
- `F4` Thêm nhanh khách hàng (ở ô mã **Khách hàng**)
- `F4` Thêm nhanh hàng hóa (ở ô **Mã hàng**)
- `Insert` Thêm dòng
- `Ctrl Delete` Xóa dòng đang chọn
