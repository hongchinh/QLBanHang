---
key: stock-in.form
title: Phiếu nhập kho
routes: [/stock-in/new, /stock-in/:id]
sources: [frontend/src/pages/stock-vouchers/**, frontend/src/features/stock-vouchers/**, backend/src/OrderMgmt.Application/Inventory/StockVouchers/**]
related: [stock-out.form]
updated: 2026-10-15
---
## Dùng để
Ghi nhận hàng nhập vào kho: mua từ nhà cung cấp, khách trả lại hàng hoặc nhập khác. Phiếu nhập tăng tồn kho và, tùy lý do, ghi nhận công nợ với nhà cung cấp hoặc khách.

## Các bước
1. {#reason} Kiểm tra **Ngày nhập**, chọn **Lý do nhập** (ví dụ Nhập mua hàng) và **Kho**.
2. {#partner} Gõ mã, tên hoặc MST nhà cung cấp ở **Mã đối tượng**, Enter để chọn.
3. {#lines} Nhập hàng ở tab **Hàng hóa**: **Mã hàng**, **SL**, **Đơn giá**. Enter sang ô kế tiếp, `Insert` thêm dòng.
4. {#extras} Nếu có, nhập **Phí vận chuyển**, **CK cả đơn** ở tab **Thanh toán** và **Người giao hàng** ở tab **Thông tin khác**.
5. {#totals} Xem **Tổng thanh toán** và công nợ Phải trả của nhà cung cấp (trước và sau phiếu) ở thanh cuối phiếu.
6. {#payment} Nếu trả tiền ngay, chọn **HTTT** ở phần đầu phiếu rồi kiểm tra **Số tiền thanh toán** ở tab **Thanh toán**, phiếu chi được tạo tự động.
7. {#save} Bấm **Lưu tạm** hoặc **Lưu và thoát** (**Cập nhật** khi sửa phiếu), rồi in phiếu nếu cần.

## Lưu ý
- Lý do quyết định loại đối tượng (nhà cung cấp hoặc khách); lý do có ghi công nợ thì bắt buộc chọn đối tượng. Đổi lý do thì đối tượng không còn phù hợp bị bỏ chọn.
- Đối tượng ngừng sử dụng không hiện trong ô **Mã đối tượng**; nếu bị ngừng sau khi đã chọn, lúc lưu sẽ báo Đối tượng đã ngừng hoạt động. Phiếu cũ vẫn sửa được.
- **Đơn giá** tự điền giá vốn của hàng khi chọn **Mã hàng**, sửa lại nếu giá nhập khác.
- Hàng tính theo mét (md, m², m³) nhập **Số tấm** và **Dài**, **Rộng**, **Dày** theo loại hàng; **SL** tự tính, không nhập tay.
- **CK cả đơn** được chia cho các dòng theo giá trị; **Phí vận chuyển** cộng vào tổng thanh toán và vào giá trị hàng nhập kho.
- Ở tab **Thanh toán**, nhập % rồi bấm **Áp dụng** ở **% VAT cho mọi dòng** để đặt thuế cho tất cả dòng. Lý do làm tăng công nợ còn hiện **Hạn thanh toán**, tự tính từ số ngày nợ của đối tượng.
- **HTTT** không gắn với quỹ thì **Số tiền thanh toán** khóa ở 0 và không có phiếu chi.
- Phiếu đã lưu có thể **Hủy phiếu**, **Khôi phục** hoặc **Xóa** nếu bạn có quyền. Hủy, sửa hay xóa làm tồn kho âm thì hệ thống cảnh báo hoặc chặn, tùy cấu hình.
- Phiếu có ngày trong kỳ đã khóa sổ không lưu được, hệ thống báo Ngày chứng từ đã khóa sổ.
- Phải lưu thay đổi trước khi in.

## Phím tắt
- `Ctrl S` Lưu
- `Alt 1–4` Chuyển tab (Alt 4 chỉ khi sửa phiếu)
- `Insert` Thêm dòng
- `Ctrl Del` Xóa dòng
