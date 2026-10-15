---
key: stock-out.form
title: Phiếu xuất kho
routes: [/stock-out/new, /stock-out/:id]
sources: [frontend/src/pages/stock-vouchers/**, frontend/src/features/stock-vouchers/**, backend/src/OrderMgmt.Application/Inventory/StockVouchers/**, frontend/src/components/partner-autocomplete/**, frontend/src/lib/confirm-required.ts, frontend/src/features/cash-common/**, frontend/src/pages/customers/customer-form-fields.tsx]
related: [receipts.form, debt-matching]
updated: 2026-10-15
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

## Lưu ý
- Chọn **Đối tượng** trước **Lý do xuất** cũng được: nếu đối tượng không hợp với lý do, hệ thống đổi sang lý do hợp đầu tiên, ô lý do nháy lên và hiện dòng vàng "Đã đổi lý do cho hợp với KH001 (khách hàng)." Lý do yêu cầu khách hàng hay nhà cung cấp thì đối tượng phải đúng vai trò đó.
- Đổi **Lý do xuất** sau khi đã chọn đối tượng thì đối tượng được giữ; nếu không hợp, dòng đỏ hiện dưới ô lý do (ví dụ Lý do "…" cần khách hàng — KH001 không phải khách hàng) nhưng vẫn bấm lưu được, hệ thống từ chối lúc lưu và báo lỗi dưới ô **Đối tượng**. Khi ô lý do đang trống và không lý do nào hợp thì dòng đỏ báo Không có lý do nào hợp với KH001 (khách hàng). Chỉ lý do không dùng đối tượng (ô hiện Không cần đối tượng) mới xóa đối tượng.
- Lý do yêu cầu khách hoặc nhà cung cấp, hay có ghi công nợ, thì bắt buộc chọn đối tượng. Gõ chữ ở **Đối tượng** mà chưa chọn từ danh sách thì chưa lưu được và ô báo Vui lòng chọn đối tượng từ danh sách.
- Danh sách gợi ý của **Đối tượng** gồm khách hàng và nhà cung cấp đang dùng (tìm theo mã, tên, MST), hiện Mã, Tên, MST, Địa chỉ, SĐT, Loại. Enter chọn dòng đang sáng, mã gõ khớp hoàn toàn được ưu tiên; Enter khi kết quả chưa về thì chờ rồi mới chọn. Chọn xong, con trỏ sang ô tên.
- `F4` hoặc nút **+** (cũng là dòng Thêm mới trong danh sách) mở **Thêm nhanh đối tượng**: vai trò chọn sẵn theo lý do, Enter đi qua từng ô, `Ctrl Enter` lưu đối tượng mới (không lưu phiếu), `Esc` hủy và về ô mã. Cần quyền tạo khách hàng hoặc nhà cung cấp.
- **Xem danh mục đầy đủ** (dòng cuối danh sách) mở danh mục có hai tab Khách hàng và Nhà cung cấp; ↑↓ rồi Enter để chọn. Cần quyền xem khách hàng hoặc nhà cung cấp.
- Đối tượng ngừng sử dụng không hiện trong ô **Đối tượng**; nếu bị ngừng sau khi đã chọn, lúc lưu sẽ báo Đối tượng đã ngừng hoạt động. Phiếu cũ của đối tượng đó vẫn sửa được.
- Vượt hạn mức nợ: tùy chính sách trong cấu hình, hệ thống cho qua, hiện hộp thoại **Vượt hạn mức nợ** để bạn xác nhận, hoặc chặn lưu.
- **HTTT** không gắn với quỹ thì **Số tiền thanh toán** khóa ở 0 và không có phiếu thu.
- Phải lưu thay đổi trước khi in.

## Phím tắt
- `Ctrl S` Lưu
- `Alt 1–4` Chuyển tab (Alt 4 chỉ khi sửa phiếu)
- `Insert` Thêm dòng
- `Ctrl Del` Xóa dòng
- `F4` Thêm nhanh đối tượng (ở ô mã **Đối tượng**)
- `Enter` Sang ô kế tiếp: **Ngày xuất**, **Lý do xuất**, **Kho**, **HTTT**, **Đối tượng** (mã), tên, **Đ.chỉ**, rồi vào bảng hàng
