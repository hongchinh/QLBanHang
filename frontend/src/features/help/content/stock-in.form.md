---
key: stock-in.form
title: Phiếu nhập kho
routes: [/stock-in/new, /stock-in/:id]
sources: [frontend/src/pages/stock-vouchers/**, frontend/src/features/stock-vouchers/**, backend/src/OrderMgmt.Application/Inventory/StockVouchers/**, frontend/src/components/partner-autocomplete/**, frontend/src/lib/confirm-required.ts, frontend/src/features/cash-common/**, frontend/src/pages/customers/customer-form-fields.tsx, frontend/src/components/voucher-print/voucher-print-button.tsx, frontend/src/components/voucher-print/stock-price-mode-dialog.tsx, frontend/src/pages/quotations/components/product-typeahead-cell.tsx, frontend/src/features/products/components/product-quick-add-dialog.tsx, frontend/src/features/products/components/product-form-fields.tsx, frontend/src/lib/use-dialog-opening.ts]
related: [stock-out.form]
updated: 2026-10-15
---
## Dùng để
Ghi nhận hàng nhập vào kho: mua từ nhà cung cấp, khách trả lại hàng hoặc nhập khác. Phiếu nhập tăng tồn kho và, tùy lý do, ghi nhận công nợ với nhà cung cấp hoặc khách.

## Các bước
1. {#reason} Kiểm tra **Ngày nhập**, chọn **Lý do nhập** (ví dụ Nhập mua hàng) và **Kho**.
2. {#partner} Gõ mã, tên hoặc MST nhà cung cấp (hoặc khách, tùy lý do) ở **Đối tượng**, Enter để chọn; `F4` để thêm nhanh nếu chưa có.
3. {#lines} Nhập hàng ở tab **Hàng hóa**: **Mã hàng**, **SL**, **Đơn giá**. Enter sang ô kế tiếp, `Insert` thêm dòng.
4. {#extras} Nếu có, nhập **Phí vận chuyển**, **CK cả đơn** ở tab **Thanh toán** và **Người giao hàng** ở tab **Thông tin khác**.
5. {#totals} Xem **Tổng thanh toán** ở thanh cuối phiếu; nếu có quyền xem công nợ, xem thêm Phải trả của nhà cung cấp trước và sau phiếu.
6. {#payment} Nếu trả tiền ngay, chọn **HTTT** ở phần đầu phiếu rồi kiểm tra **Số tiền thanh toán** ở tab **Thanh toán**, phiếu chi được tạo tự động.
7. {#save} Bấm **Lưu tạm** hoặc **Lưu và thoát** (**Cập nhật** khi sửa phiếu), rồi in phiếu nếu cần.

## Lưu ý
- Lý do quyết định đối tượng: nhà cung cấp, khách, bất kỳ hoặc không cần (ô hiện Không cần đối tượng).
- Chọn **Đối tượng** trước **Lý do nhập** cũng được: nếu đối tượng không hợp với lý do, hệ thống đổi sang lý do hợp đầu tiên, ô lý do nháy lên và hiện dòng vàng "Đã đổi lý do cho hợp với KH001 (khách hàng)." Lý do yêu cầu khách hàng hay nhà cung cấp thì đối tượng phải đúng vai trò đó.
- Đổi **Lý do nhập** sau khi đã chọn đối tượng thì đối tượng được giữ; nếu không hợp, dòng đỏ hiện dưới ô lý do (ví dụ Lý do "…" cần khách hàng — KH001 không phải khách hàng) nhưng vẫn bấm lưu được, hệ thống từ chối lúc lưu và báo lỗi dưới ô **Đối tượng**. Khi ô lý do đang trống và không lý do nào hợp thì dòng đỏ báo Không có lý do nào hợp với KH001 (khách hàng). Chỉ lý do không dùng đối tượng (ô hiện Không cần đối tượng) mới xóa đối tượng.
- Lý do yêu cầu nhà cung cấp hoặc khách, hay có ghi công nợ, thì bắt buộc chọn đối tượng. Gõ chữ ở **Đối tượng** mà chưa chọn từ danh sách thì chưa lưu được và ô báo Vui lòng chọn đối tượng từ danh sách.
- Danh sách gợi ý của **Đối tượng** gồm khách hàng và nhà cung cấp đang dùng (tìm theo mã, tên, MST), hiện Mã, Tên, MST, Địa chỉ, SĐT, Loại. Enter chọn dòng đang sáng, mã gõ khớp hoàn toàn được ưu tiên; Enter khi kết quả chưa về thì chờ rồi mới chọn. Chọn xong, con trỏ sang ô tên.
- `F4` hoặc nút **+** (cũng là dòng Thêm mới trong danh sách) mở **Thêm nhanh đối tượng**: vai trò chọn sẵn theo lý do, Enter đi qua từng ô, `Ctrl Enter` lưu đối tượng mới (không lưu phiếu), `Esc` hủy và về ô mã. Cần quyền tạo khách hàng hoặc nhà cung cấp.
- `F4` trong ô **Mã hàng**, hoặc dòng **Thêm mới hàng hóa** ở cuối danh sách gợi ý (gõ mã chưa có rồi Enter là chọn dòng này), mở **Thêm nhanh hàng hóa**: chữ đã gõ điền sẵn vào Mã nếu không có dấu cách và không dấu, nếu không thì vào Tên; Enter đi qua từng ô, `Ctrl Enter` hoặc **Lưu và chọn** lưu hàng mới và điền vào dòng (không lưu phiếu), `Esc` hủy và về ô mã. Cần quyền tạo hàng hóa.
- **Xem danh mục đầy đủ** (dòng cuối danh sách) mở danh mục có hai tab Khách hàng và Nhà cung cấp; ↑↓ rồi Enter để chọn. Cần quyền xem khách hàng hoặc nhà cung cấp.
- Đối tượng ngừng sử dụng không hiện trong ô **Đối tượng**; nếu bị ngừng sau khi đã chọn, lúc lưu sẽ báo Đối tượng đã ngừng hoạt động. Phiếu cũ vẫn sửa được.
- **Đơn giá** tự điền giá vốn của hàng và là giá chưa VAT, **%CK** tự điền chiết khấu mua; sửa lại nếu khác. **CK cả đơn** được chia cho các dòng theo giá trị, **Phí vận chuyển** cộng vào tổng thanh toán và vào giá trị hàng nhập kho.
- Hàng tính theo mét (m dài, m², m³) nhập **Số tấm** và **Dài**, **Rộng**, **Dày** theo loại hàng; **SL** tự tính, không nhập tay.
- Ở tab **Thanh toán**, nhập % rồi bấm **Áp dụng** ở **% VAT cho mọi dòng** để đặt thuế cho tất cả dòng. Lý do làm tăng công nợ còn hiện **Hạn thanh toán**, tự tính từ số ngày nợ của đối tượng.
- **HTTT** không gắn với quỹ thì **Số tiền thanh toán** khóa ở 0 và không có phiếu chi. Trả tiền làm quỹ bị âm: tùy cấu hình, hệ thống cho qua, hỏi xác nhận (Quỹ bị âm) hoặc chặn (Không đủ tiền trong quỹ).
- Phiếu đã lưu có thể **Hủy phiếu**, **Khôi phục** hoặc **Xóa** nếu bạn có quyền. Hủy, sửa hay xóa làm tồn kho âm thì tùy cấu hình, hệ thống cho qua, cảnh báo để bạn xác nhận hoặc chặn. Ngày phiếu trong kỳ đã khóa sổ thì không lưu được.
- Phải lưu thay đổi trước khi in.

## Phím tắt
- `Ctrl S` Lưu
- `Alt 1–4` Chuyển tab (Alt 4 chỉ khi sửa phiếu)
- `Insert` Thêm dòng
- `Ctrl Del` Xóa dòng
- `F4` Thêm nhanh đối tượng (ở ô mã **Đối tượng**)
- `F4` Thêm nhanh hàng hóa (ở ô **Mã hàng**)
- `Enter` Sang ô kế tiếp: **Ngày nhập**, **Lý do nhập**, **Kho**, **HTTT**, **Đối tượng** (mã), tên, **Đ.chỉ**, rồi vào bảng hàng
