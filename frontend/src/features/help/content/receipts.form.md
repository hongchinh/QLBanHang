---
key: receipts.form
title: Phiếu thu
routes: [/receipts/new, /receipts/:id]
sources: [frontend/src/pages/cash-vouchers/**, frontend/src/features/cash-vouchers/**, frontend/src/features/cash-common/**, backend/src/OrderMgmt.Application/CashDebt/CashVouchers/**, frontend/src/lib/confirm-required.ts, frontend/src/components/voucher-print/voucher-print-button.tsx, frontend/src/components/partner-autocomplete/**, backend/src/OrderMgmt.Application/CashDebt/Guards/**, frontend/src/pages/customers/customer-form-fields.tsx, frontend/src/components/payment-qr/**, frontend/src/features/voucher-payment-qr/**, frontend/src/features/voucher-print/**, backend/src/OrderMgmt.Application/CashDebt/PaymentQr/**]
related: [payments.form]
updated: 2026-10-15
---
## Dùng để
Ghi nhận tiền thu vào một quỹ: thu tiền khách hàng, nhận hoàn tiền từ nhà cung cấp hoặc thu khác. Phiếu thu cộng vào quỹ và, tùy lý do, giảm công nợ phải thu hoặc tăng công nợ phải trả.

## Các bước
1. {#reason} Kiểm tra **Lý do** (mặc định Thu tiền khách hàng), **Quỹ** (mặc định quỹ tiền mặt) và **Ngày giờ**, đổi nếu cần.
2. {#partner} Gõ mã, tên hoặc MST ở **Đối tượng**, Enter để chọn khách hàng hoặc nhà cung cấp; tên và địa chỉ tự điền, `F4` để thêm nhanh nếu chưa có.
3. {#person} Điền **Người nộp**, **Kèm theo** (số chứng từ gốc); **Lý do nộp** tự điền theo lý do và đối tượng, sửa nếu cần.
4. {#lines} Nhập từng dòng ở bảng: **Diễn giải** (tự điền như **Lý do nộp**) và **Số tiền**. `Insert` hoặc **Thêm dòng** để thêm dòng, **Tổng** là số tiền thu.
5. {#allocation} Nếu có **Phân bổ chứng từ**, kiểm tra cột **Phân bổ** (gợi ý sẵn chứng từ đến hạn sớm nhất), sửa hoặc bấm **Gợi ý**.
6. {#save} Bấm **Lưu tạm** (ở lại phiếu) hoặc **Lưu và thoát** (**Cập nhật** khi sửa phiếu); muốn in thì lưu xong bấm **In**, chọn PDF hoặc Excel.
7. {#qr} Khi khách chuyển khoản, bấm **QR chuyển khoản** (cạnh **In**, sau khi đã lưu) để cho khách quét mã.

## Lưu ý
- Chọn **Đối tượng** trước **Lý do** cũng được: nếu đối tượng không hợp với lý do, hệ thống đổi sang lý do hợp đầu tiên, ô lý do nháy lên và hiện dòng vàng "Đã đổi lý do cho hợp với KH001 (khách hàng)." Thu tiền khách hàng chỉ nhận khách hàng, Thu hoàn tiền NCC chỉ nhận nhà cung cấp.
- Đổi **Lý do** sau khi đã chọn đối tượng thì đối tượng được giữ; nếu không hợp, dòng đỏ hiện dưới ô lý do (ví dụ Lý do "…" cần khách hàng — KH001 không phải khách hàng) nhưng vẫn bấm lưu được, hệ thống từ chối lúc lưu và báo lỗi dưới ô **Đối tượng**. Khi ô lý do đang trống và không lý do nào hợp thì dòng đỏ báo Không có lý do nào hợp với KH001 (khách hàng). Chỉ lý do không dùng đối tượng (ô hiện Không cần đối tượng) mới xóa đối tượng.
- Lý do có ảnh hưởng công nợ thì bắt buộc chọn đối tượng (báo Lý do này ảnh hưởng công nợ, vui lòng chọn đối tượng). Gõ chữ ở **Đối tượng** mà chưa chọn từ danh sách thì chưa lưu được và ô báo Vui lòng chọn đối tượng từ danh sách.
- Danh sách gợi ý của **Đối tượng** gồm khách hàng và nhà cung cấp đang dùng (tìm theo mã, tên, MST), hiện Mã, Tên, MST, Địa chỉ, SĐT, Loại. Enter chọn dòng đang sáng, mã gõ khớp hoàn toàn được ưu tiên; Enter khi kết quả chưa về thì chờ rồi mới chọn. Chọn xong, con trỏ sang ô tên. Phiếu không có ô MST, MST lấy từ danh mục.
- `F4` hoặc nút **+** (cũng là dòng Thêm mới trong danh sách) mở **Thêm nhanh đối tượng**: vai trò chọn sẵn theo lý do, Enter đi qua từng ô, `Ctrl Enter` lưu đối tượng mới (không lưu phiếu), `Esc` hủy và về ô mã. Cần quyền tạo khách hàng hoặc nhà cung cấp.
- **Xem danh mục đầy đủ** (dòng cuối danh sách) mở danh mục có hai tab Khách hàng và Nhà cung cấp; ↑↓ rồi Enter để chọn. Cần quyền xem khách hàng hoặc nhà cung cấp.
- **Phân bổ chứng từ** chỉ hiện khi lý do có ảnh hưởng công nợ và đã chọn đối tượng. Tiền chưa phân bổ vẫn để đó, đối trừ sau ở màn Đối trừ chứng từ. Khi hiện Phân bổ vượt số còn nợ hoặc Tổng phân bổ vượt số tiền phiếu thì chưa lưu được.
- Thu tiền cộng vào quỹ nên thường không làm quỹ âm, nhưng sửa, hủy hoặc xóa phiếu thu cũ có thể làm quỹ âm. Khi đó, tùy chính sách trong cấu hình, hệ thống cho qua, hiện hộp thoại **Quỹ bị âm** để bạn xác nhận, hoặc chặn lưu với **Không đủ tiền trong quỹ**.
- Ngày thuộc kỳ đã khóa sổ thì không lưu được và phiếu cũ trong kỳ đó chỉ xem (đầu phiếu báo Bạn không có quyền sửa phiếu này). Ngày trước ngày đầu kỳ của chi nhánh cũng bị từ chối.
- Phiếu thu tự động từ phiếu xuất có nhãn **Tự động** và chỉ xem. Bấm **Mở phiếu kho** để sửa, hoặc **Đóng** để thoát.
- **Hủy phiếu**, **Khôi phục** và **Xóa** chỉ hiện khi bạn có quyền tương ứng và phiếu là của bạn (hoặc bạn có quyền sửa phiếu của mọi người).
- Hủy, xóa hoặc sửa phiếu đã được đối trừ với chứng từ khác thì có thể hiện hộp thoại **Bỏ đối trừ chứng từ** để bạn xác nhận. Phiếu đã hủy không sửa được, khôi phục thì đối trừ cũ không quay lại.

- **QR chuyển khoản** mở hộp thoại (trên điện thoại là tấm trượt từ dưới lên) có mã VietQR, số tiền, ngân hàng, số tài khoản, chủ tài khoản và nội dung chuyển khoản. Bấm biểu tượng sao chép cạnh số tài khoản hoặc nội dung để chép, **Sao chép thông tin** để chép cả bộ, **Tải ảnh QR** để lưu ảnh.
- Nút **QR chuyển khoản** bị khóa khi phiếu có thay đổi chưa lưu (Lưu thay đổi trước khi xem QR) hoặc phiếu đã hủy. Phiếu chi không có nút này.
- Tiền chuyển về quỹ ngân hàng của chính phiếu; phiếu ở quỹ tiền mặt thì dùng quỹ ngân hàng mặc định của chi nhánh. Quỹ thiếu ngân hàng, số tài khoản hoặc chủ tài khoản thì hộp thoại báo thiếu gì, và quản trị viên bấm **Mở màn Quỹ** để bổ sung.
- Menu **In** có tùy chọn **Kèm QR chuyển khoản** (mặc định bật, nhớ theo từng người dùng): mã QR in cạnh các dòng của phiếu thu.
- Phiếu thu tự động từ phiếu xuất đã trả tiền mặt không có QR.

## Phím tắt
- `Ctrl S` Lưu
- `Insert` Thêm dòng
- `F4` Thêm nhanh đối tượng (ở ô mã **Đối tượng**)
- `Enter` Sang ô kế tiếp: **Ngày giờ**, **Quỹ**, **Đối tượng** (mã), tên, **Lý do**, **Người nộp**, **Địa chỉ**, **Lý do nộp**, **Kèm theo**, **Ghi chú**, rồi vào bảng dòng (**Diễn giải**, **Số tiền**, **Ghi chú**)
- `Shift Enter` Về ô trước; `Enter` ở **Ghi chú** của dòng cuối thêm một dòng mới
