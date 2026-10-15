---
key: stock-out.form
title: Phiếu xuất kho
routes: [/stock-out/new, /stock-out/:id]
sources: [frontend/src/pages/stock-vouchers/**, frontend/src/features/stock-vouchers/**, backend/src/OrderMgmt.Application/Inventory/StockVouchers/**, frontend/src/components/partner-autocomplete/**, frontend/src/lib/confirm-required.ts]
related: []
updated: 2026-10-15
---
## Dùng để
Ghi nhận hàng xuất khỏi kho: bán cho khách, trả hàng nhà cung cấp hoặc xuất dùng nội bộ. Phiếu xuất bán trừ tồn kho và tăng công nợ phải thu của khách.

## Các bước
1. {#reason} Chọn **Lý do xuất** (ví dụ Xuất bán hàng) và **Kho**.
2. {#partner} Gõ mã hoặc tên khách ở **Mã đối tượng**, Enter để chọn.
3. {#lines} Nhập hàng ở tab **Hàng hóa**: **Mã hàng**, **SL**, **Đơn giá**. Enter sang ô kế tiếp, `Insert` thêm dòng.
4. {#totals} Xem **Tổng thanh toán** và công nợ của khách (Phải thu, Hạn mức, Còn được nợ) ở thanh cuối phiếu.
5. {#payment} Nếu khách trả ngay, chọn **HTTT** ở phần đầu phiếu rồi kiểm tra **Số tiền thanh toán** (mặc định bằng tổng tiền, sửa nếu khách trả một phần) ở tab **Thanh toán**, phiếu thu được tạo tự động.
6. {#save} Bấm **Lưu tạm** hoặc **Lưu và thoát** (**Cập nhật** khi sửa phiếu), rồi in phiếu nếu cần.

## Lưu ý
- Khách đang Ngừng sử dụng không hiện trong ô **Mã đối tượng**; nếu khách bị ngừng sau khi đã chọn, lúc lưu sẽ báo Đối tượng đã ngừng hoạt động. Phiếu cũ của khách đó vẫn sửa được.
- Vượt hạn mức nợ: tùy chính sách trong cấu hình, hệ thống cho qua, hiện hộp thoại **Vượt hạn mức nợ** để bạn xác nhận, hoặc chặn lưu.
- **HTTT** không gắn với quỹ thì **Số tiền thanh toán** khóa ở 0 và không có phiếu thu.
- Phải lưu thay đổi trước khi in.

## Phím tắt
- `Ctrl S` Lưu
- `Alt 1–4` Chuyển tab (Alt 4 chỉ khi sửa phiếu)
- `Insert` Thêm dòng
- `Ctrl Del` Xóa dòng
