---
key: stock-in.form
title: Phiếu nhập kho
routes: [/stock-in/new, /stock-in/:id]
sources: [frontend/src/pages/stock-vouchers/**, frontend/src/features/stock-vouchers/**, backend/src/OrderMgmt.Application/Inventory/StockVouchers/**, frontend/src/components/partner-autocomplete/**, frontend/src/lib/confirm-required.ts]
related: [stock-out.form]
updated: 2026-10-15
---
## Dùng để
Ghi nhận hàng nhập vào kho: mua từ nhà cung cấp, khách trả lại hàng hoặc nhập khác. Phiếu nhập tăng tồn kho và, tùy lý do, ghi nhận công nợ với nhà cung cấp hoặc khách.

## Các bước
1. {#reason} Kiểm tra **Ngày nhập**, chọn **Lý do nhập** (ví dụ Nhập mua hàng) và **Kho**.
2. {#partner} Gõ mã, tên hoặc MST nhà cung cấp (hoặc khách, tùy lý do) ở **Mã đối tượng**, Enter để chọn.
3. {#lines} Nhập hàng ở tab **Hàng hóa**: **Mã hàng**, **SL**, **Đơn giá**. Enter sang ô kế tiếp, `Insert` thêm dòng.
4. {#extras} Nếu có, nhập **Phí vận chuyển**, **CK cả đơn** ở tab **Thanh toán** và **Người giao hàng** ở tab **Thông tin khác**.
5. {#totals} Xem **Tổng thanh toán** ở thanh cuối phiếu; nếu có quyền xem công nợ, xem thêm Phải trả của nhà cung cấp trước và sau phiếu.
6. {#payment} Nếu trả tiền ngay, chọn **HTTT** ở phần đầu phiếu rồi kiểm tra **Số tiền thanh toán** ở tab **Thanh toán**, phiếu chi được tạo tự động.
7. {#save} Bấm **Lưu tạm** hoặc **Lưu và thoát** (**Cập nhật** khi sửa phiếu), rồi in phiếu nếu cần.

## Lưu ý
- Lý do quyết định đối tượng: nhà cung cấp, khách, bất kỳ hoặc không cần (ô hiện Không cần đối tượng). Lý do yêu cầu nhà cung cấp hoặc khách, hay có ghi công nợ, thì bắt buộc chọn đối tượng. Đổi lý do thì đối tượng không còn phù hợp bị bỏ chọn.
- Đối tượng ngừng sử dụng không hiện trong ô **Mã đối tượng**; nếu bị ngừng sau khi đã chọn, lúc lưu sẽ báo Đối tượng đã ngừng hoạt động. Phiếu cũ vẫn sửa được.
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
