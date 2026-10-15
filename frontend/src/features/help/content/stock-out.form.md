---
key: stock-out.form
title: Phiếu xuất kho
routes: [/stock-out/new, /stock-out/:id]
sources: [frontend/src/pages/stock-vouchers/**, frontend/src/features/stock-vouchers/**, backend/src/OrderMgmt.Application/Inventory/StockVouchers/**, frontend/src/components/partner-autocomplete/**, frontend/src/lib/confirm-required.ts, frontend/src/components/voucher-print/stock-price-mode-dialog.tsx, frontend/src/components/payment-qr/**, frontend/src/features/voucher-payment-qr/**, frontend/src/features/voucher-print/**, backend/src/OrderMgmt.Application/CashDebt/PaymentQr/**]
related: [receipts.form, debt-matching]
updated: 2026-10-15
---
## Dùng để
Ghi nhận hàng xuất khỏi kho: bán cho khách, trả hàng nhà cung cấp hoặc xuất dùng nội bộ. Phiếu xuất bán trừ tồn kho và tăng công nợ phải thu của khách.

## Các bước
1. {#reason} Chọn **Lý do xuất** (ví dụ Xuất bán hàng) và **Kho**.
2. {#partner} Gõ mã hoặc tên khách ở **Mã đối tượng**, Enter để chọn.
3. {#lines} Nhập hàng ở tab **Hàng hóa**: **Mã hàng**, **SL**, **Đơn giá**. Enter sang ô kế tiếp, `Insert` thêm dòng.
4. {#totals} Xem **Tổng thanh toán** ở thanh cuối phiếu; nếu có quyền xem công nợ, xem thêm Phải thu, Hạn mức và Còn được nợ của khách.
5. {#payment} Nếu khách trả ngay, chọn **HTTT** ở phần đầu phiếu rồi kiểm tra **Số tiền thanh toán** (mặc định bằng tổng tiền, sửa nếu khách trả một phần) ở tab **Thanh toán**, phiếu thu được tạo tự động.
6. {#save} Bấm **Lưu tạm** hoặc **Lưu và thoát** (**Cập nhật** khi sửa phiếu), rồi in phiếu nếu cần.
7. {#qr} Khi khách chuyển khoản phần còn lại, bấm **QR chuyển khoản** (cạnh **In**, sau khi đã lưu) để cho khách quét mã.

## Lưu ý
- Khách đang Ngừng sử dụng không hiện trong ô **Mã đối tượng**; nếu khách bị ngừng sau khi đã chọn, lúc lưu sẽ báo Đối tượng đã ngừng hoạt động. Phiếu cũ của khách đó vẫn sửa được.
- Vượt hạn mức nợ: tùy chính sách trong cấu hình, hệ thống cho qua, hiện hộp thoại **Vượt hạn mức nợ** để bạn xác nhận, hoặc chặn lưu.
- **HTTT** không gắn với quỹ thì **Số tiền thanh toán** khóa ở 0 và không có phiếu thu.
- Phải lưu thay đổi trước khi in.
- **QR chuyển khoản** mở hộp thoại (trên điện thoại là tấm trượt từ dưới lên) có mã VietQR, số tiền, ngân hàng, số tài khoản, chủ tài khoản và nội dung chuyển khoản; có nút sao chép từng giá trị và **Tải ảnh QR**. Số tiền trong mã bằng tổng phiếu trừ phần khách đã trả tiền mặt.
- Nút **QR chuyển khoản** bị khóa khi phiếu có thay đổi chưa lưu (Lưu thay đổi trước khi xem QR) hoặc phiếu đã hủy. Phiếu nhập kho không có nút này.
- Tiền chuyển về quỹ ngân hàng của phiếu; phiếu ở quỹ tiền mặt thì dùng quỹ ngân hàng mặc định của chi nhánh. Quỹ thiếu ngân hàng, số tài khoản hoặc chủ tài khoản thì hộp thoại báo thiếu gì, và quản trị viên bấm **Mở màn Quỹ** để bổ sung.
- Hộp thoại chọn giá in có tùy chọn **Kèm QR chuyển khoản** (mặc định bật, nhớ theo từng người dùng): khối THÔNG TIN CHUYỂN KHOẢN chỉ in khi chọn **Giá trên phiếu**.
- Phiếu thu tự động của phiếu xuất đã trả tiền mặt không có QR.

## Phím tắt
- `Ctrl S` Lưu
- `Alt 1–4` Chuyển tab (Alt 4 chỉ khi sửa phiếu)
- `Insert` Thêm dòng
- `Ctrl Del` Xóa dòng
