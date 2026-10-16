---
key: stock-out.form
title: Phiếu xuất kho
routes: [/stock-out/new, /stock-out/:id]
sources: [frontend/src/pages/stock-vouchers/**, frontend/src/features/stock-vouchers/**, backend/src/OrderMgmt.Application/Inventory/StockVouchers/**, frontend/src/components/partner-autocomplete/**, frontend/src/lib/confirm-required.ts, frontend/src/features/cash-common/**, frontend/src/pages/customers/customer-form-fields.tsx, frontend/src/components/voucher-print/stock-price-mode-dialog.tsx, frontend/src/components/voucher-print/voucher-print-button.tsx, frontend/src/components/payment-qr/**, frontend/src/features/voucher-payment-qr/**, frontend/src/features/voucher-print/**, backend/src/OrderMgmt.Application/CashDebt/PaymentQr/**, backend/src/OrderMgmt.Application/Tt133/Vouchers/**, backend/src/OrderMgmt.Infrastructure/Excel/Tt133/Vouchers/**, frontend/src/pages/quotations/components/product-typeahead-cell.tsx, frontend/src/features/products/components/product-quick-add-dialog.tsx, frontend/src/features/products/components/product-form-fields.tsx, frontend/src/features/products/split-typed-text.ts, frontend/src/lib/use-dialog-opening.ts, frontend/src/lib/dimension-columns.ts, frontend/src/lib/use-appeared-keys.ts]
related: [receipts.form, debt-matching]
updated: 2026-10-16
---
## Dùng để
Ghi nhận hàng xuất khỏi kho: bán cho khách, trả hàng nhà cung cấp hoặc xuất dùng nội bộ. Phiếu xuất bán trừ tồn kho và tăng công nợ phải thu của khách.

## Các bước
1. {#reason} Chọn **Lý do xuất** (ví dụ Xuất bán hàng) và **Kho**.
2. {#partner} Gõ mã, tên hoặc MST ở **Đối tượng**, Enter để chọn khách (hoặc nhà cung cấp khi trả hàng); `F4` để thêm nhanh nếu chưa có.
3. {#lines} Nhập hàng ở tab **Hàng hóa**: **Mã hàng**, **SL**, **Đơn giá**. Enter sang ô kế tiếp, `Insert` thêm dòng.
4. {#totals} Xem **Tổng thanh toán** ở thanh cuối phiếu; nếu có quyền xem công nợ, xem thêm Phải thu, Hạn mức và Còn được nợ của khách.
5. {#payment} Nếu khách trả ngay, chọn **HTTT** ở phần đầu phiếu rồi kiểm tra **Số tiền thanh toán** (mặc định bằng tổng tiền, sửa nếu khách trả một phần) ở tab **Thanh toán**, phiếu thu được tạo tự động.
6. {#save} Bấm **Lưu tạm** hoặc **Lưu và thoát** (**Cập nhật** khi sửa phiếu), rồi in phiếu nếu cần.
7. {#qr} Khi khách chuyển khoản, bấm **QR chuyển khoản** (cạnh **In**, sau khi đã lưu) để cho khách quét mã.

## Lưu ý
- Chọn **Đối tượng** trước **Lý do xuất** cũng được: nếu đối tượng không hợp với lý do, hệ thống đổi sang lý do hợp đầu tiên, ô lý do nháy lên và hiện dòng vàng "Đã đổi lý do cho hợp với KH001 (khách hàng)." Lý do yêu cầu khách hàng hay nhà cung cấp thì đối tượng phải đúng vai trò đó.
- Đổi **Lý do xuất** sau khi đã chọn đối tượng thì đối tượng được giữ; nếu không hợp, dòng đỏ hiện dưới ô lý do (ví dụ Lý do "…" cần khách hàng — KH001 không phải khách hàng) nhưng vẫn bấm lưu được, hệ thống từ chối lúc lưu và báo lỗi dưới ô **Đối tượng**. Khi ô lý do đang trống và không lý do nào hợp thì dòng đỏ báo Không có lý do nào hợp với KH001 (khách hàng). Chỉ lý do không dùng đối tượng (ô hiện Không cần đối tượng) mới xóa đối tượng.
- Lý do yêu cầu khách hoặc nhà cung cấp, hay có ghi công nợ, thì bắt buộc chọn đối tượng. Gõ chữ ở **Đối tượng** mà chưa chọn từ danh sách thì chưa lưu được và ô báo Vui lòng chọn đối tượng từ danh sách.
- Danh sách gợi ý của **Đối tượng** gồm khách hàng và nhà cung cấp đang dùng (tìm theo mã, tên, MST), hiện Mã, Tên, MST, Địa chỉ, SĐT, Loại. Enter chọn dòng đang sáng, mã gõ khớp hoàn toàn được ưu tiên; Enter khi kết quả chưa về thì chờ rồi mới chọn. Chọn xong, con trỏ sang ô tên.
- `F4` hoặc nút **+** (cũng là dòng Thêm mới trong danh sách) mở **Thêm nhanh đối tượng**: vai trò chọn sẵn theo lý do, Enter đi qua từng ô, `Ctrl Enter` lưu đối tượng mới (không lưu phiếu), `Esc` hủy và về ô mã. Cần quyền tạo khách hàng hoặc nhà cung cấp.
- `F4` trong ô **Mã hàng**, hoặc dòng **Thêm mới hàng hóa** ở cuối danh sách gợi ý (gõ mã chưa có rồi Enter là chọn dòng này), mở **Thêm nhanh hàng hóa**: chữ đã gõ điền sẵn vào Mã nếu không có dấu cách và không dấu, nếu không thì vào Tên; Enter đi qua từng ô, `Ctrl Enter` hoặc **Lưu và chọn** lưu hàng mới và điền vào dòng (không lưu phiếu), `Esc` hủy và về ô mã. Cần quyền tạo hàng hóa.
- Lưu hàng mới trong **Thêm nhanh hàng hóa** không được (ví dụ Mã hàng '…' đã tồn tại.) thì lỗi hiện ở đầu hộp thoại và hộp thoại vẫn mở để bạn sửa.
- **Xem danh mục đầy đủ** (dòng cuối danh sách) mở danh mục có hai tab Khách hàng và Nhà cung cấp; ↑↓ rồi Enter để chọn. Cần quyền xem khách hàng hoặc nhà cung cấp.
- Đối tượng ngừng sử dụng không hiện trong ô **Đối tượng**; nếu bị ngừng sau khi đã chọn, lúc lưu sẽ báo Đối tượng đã ngừng hoạt động. Phiếu cũ của đối tượng đó vẫn sửa được.
- Vượt hạn mức nợ: tùy chính sách trong cấu hình, hệ thống cho qua, hiện hộp thoại **Vượt hạn mức nợ** để bạn xác nhận, hoặc chặn lưu.
- Hàng tính theo mét (m dài, m², m³) nhập **Số tấm** và **Dài**, **Rộng**, **Dày** theo loại hàng; **SL** tự tính, không nhập tay.
- Các cột **Số tấm**, **Dài**, **Rộng**, **Dày** chỉ hiện khi phiếu có hàng tính theo m dài, m² hoặc m³, theo loại cao nhất trong phiếu: m dài hiện **Số tấm** và **Dài**, m² thêm **Rộng**, m³ thêm **Dày**. Ô mà dòng không dùng hiện —. Nếu lúc lưu có lỗi ở một cột đang ẩn, cột đó hiện ra và lỗi ghi dưới ô.
- Trong **Cột & phím Enter**, mục **Hiện cột** không có bốn cột này vì chúng tự hiện theo hàng có trong phiếu.
- **HTTT** không gắn với quỹ thì **Số tiền thanh toán** khóa ở 0 và không có phiếu thu.
- Phải lưu thay đổi trước khi in.
- **QR chuyển khoản** mở hộp thoại (trên điện thoại là tấm trượt từ dưới lên) có mã VietQR, số tiền, ngân hàng, số tài khoản, chủ tài khoản, nội dung chuyển khoản và quỹ nhận; có nút sao chép từng giá trị và **Tải ảnh QR** (trên điện thoại mở bảng chia sẻ). Số tiền trong mã bằng tổng phiếu trừ phần khách đã trả tiền mặt.
- Nút **QR chuyển khoản** bị khóa khi phiếu có thay đổi chưa lưu (Lưu thay đổi trước khi xem QR) hoặc phiếu đã hủy. Phiếu nhập kho không có nút này.
- Tiền chuyển về quỹ ngân hàng của phiếu; phiếu không có quỹ hoặc ở quỹ tiền mặt thì dùng quỹ ngân hàng mặc định của chi nhánh. Quỹ chưa đủ thông tin thì hộp thoại báo Quỹ … chưa khai ngân hàng, số tài khoản hoặc chủ tài khoản (hoặc Chi nhánh chưa có quỹ ngân hàng mặc định đủ thông tin QR); người có quyền quản lý quỹ thấy **Mở màn Quỹ** để bổ sung.
- Hộp thoại chọn giá in có tùy chọn **Kèm QR chuyển khoản** (mặc định bật, nhớ theo từng người dùng): khối THÔNG TIN CHUYỂN KHOẢN chỉ in khi chọn **Giá trên phiếu**.
- Chỉ phiếu có lý do bán hàng (ghi công nợ phải thu, ví dụ Xuất bán hàng) mới có QR; lý do khác thì hộp thoại báo Lý do xuất không phải bán hàng. Khách đã trả đủ bằng tiền mặt thì báo Phiếu không còn số cần chuyển khoản.

## Phím tắt
- `Ctrl S` Lưu
- `Alt 1–4` Chuyển tab (Alt 4 chỉ khi sửa phiếu)
- `Insert` Thêm dòng
- `Ctrl Del` Xóa dòng
- `F4` Thêm nhanh đối tượng (ở ô mã **Đối tượng**)
- `F4` Thêm nhanh hàng hóa (ở ô **Mã hàng**)
- `Enter` Sang ô kế tiếp: **Ngày xuất**, **Lý do xuất**, **Kho**, **HTTT**, **Đối tượng** (mã), tên, **Đ.chỉ**, rồi vào bảng hàng
