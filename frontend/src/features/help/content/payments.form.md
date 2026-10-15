---
key: payments.form
title: Phiếu chi
routes: [/payments/new, /payments/:id]
sources: [frontend/src/pages/cash-vouchers/**, frontend/src/features/cash-vouchers/**, frontend/src/features/cash-common/**, backend/src/OrderMgmt.Application/CashDebt/CashVouchers/**, frontend/src/lib/confirm-required.ts, frontend/src/components/voucher-print/voucher-print-button.tsx, frontend/src/components/partner-autocomplete/**, backend/src/OrderMgmt.Application/CashDebt/Guards/**, frontend/src/pages/customers/customer-form-fields.tsx, frontend/src/lib/enter-focus.ts, frontend/src/lib/use-dialog-opening.ts]
related: [receipts.form]
updated: 2026-10-15
---
## Dùng để
Ghi nhận tiền chi ra từ một quỹ: trả tiền nhà cung cấp, hoàn tiền cho khách hoặc chi khác. Phiếu chi trừ vào quỹ và, tùy lý do, giảm công nợ phải trả hoặc tăng công nợ phải thu.

## Các bước
1. {#reason} Kiểm tra **Lý do** (mặc định Trả tiền nhà cung cấp), **Quỹ** (mặc định quỹ tiền mặt) và **Ngày giờ**, đổi nếu cần.
2. {#partner} Gõ mã, tên hoặc MST ở **Đối tượng**, Enter để chọn nhà cung cấp hoặc khách hàng; tên và địa chỉ tự điền, `F4` để thêm nhanh nếu chưa có.
3. {#person} Điền **Người nhận**, **Kèm theo** (số chứng từ gốc); **Lý do chi** tự điền theo lý do và đối tượng, sửa nếu cần.
4. {#lines} Nhập từng dòng ở bảng: **Diễn giải** (tự điền như **Lý do chi**) và **Số tiền**. `Insert` hoặc **Thêm dòng** để thêm dòng, **Tổng** là số tiền chi.
5. {#allocation} Nếu có **Phân bổ chứng từ**, kiểm tra cột **Phân bổ** (gợi ý sẵn chứng từ đến hạn sớm nhất), sửa hoặc bấm **Gợi ý**.
6. {#save} Bấm **Lưu tạm** (ở lại phiếu) hoặc **Lưu và thoát** (**Cập nhật** khi sửa phiếu); muốn in thì lưu xong bấm **In**, chọn PDF hoặc Excel.

## Lưu ý
- Chọn **Đối tượng** trước **Lý do** cũng được: nếu đối tượng không hợp với lý do, hệ thống đổi sang lý do hợp đầu tiên, ô lý do nháy lên và hiện dòng vàng "Đã đổi lý do cho hợp với KH001 (khách hàng)." Trả tiền nhà cung cấp chỉ nhận nhà cung cấp, Chi hoàn tiền khách hàng chỉ nhận khách hàng.
- Đổi **Lý do** sau khi đã chọn đối tượng thì đối tượng được giữ; nếu không hợp, dòng đỏ hiện dưới ô lý do (ví dụ Lý do "…" cần khách hàng — KH001 không phải khách hàng) nhưng vẫn bấm lưu được, hệ thống từ chối lúc lưu và báo lỗi dưới ô **Đối tượng**. Khi ô lý do đang trống và không lý do nào hợp thì dòng đỏ báo Không có lý do nào hợp với KH001 (khách hàng). Chỉ lý do không dùng đối tượng (ô hiện Không cần đối tượng) mới xóa đối tượng.
- Lý do có ảnh hưởng công nợ thì bắt buộc chọn đối tượng (báo Lý do này ảnh hưởng công nợ, vui lòng chọn đối tượng). Gõ chữ ở **Đối tượng** mà chưa chọn từ danh sách thì chưa lưu được và ô báo Vui lòng chọn đối tượng từ danh sách.
- Danh sách gợi ý của **Đối tượng** gồm khách hàng và nhà cung cấp đang dùng (tìm theo mã, tên, MST), hiện Mã, Tên, MST, Địa chỉ, SĐT, Loại. Enter chọn dòng đang sáng, mã gõ khớp hoàn toàn được ưu tiên; Enter khi kết quả chưa về thì chờ rồi mới chọn. Chọn xong, con trỏ sang ô tên. Phiếu không có ô MST, MST lấy từ danh mục.
- `F4` hoặc nút **+** (cũng là dòng Thêm mới trong danh sách) mở **Thêm nhanh đối tượng**: vai trò chọn sẵn theo lý do, Enter đi qua từng ô, `Ctrl Enter` lưu đối tượng mới (không lưu phiếu), `Esc` hủy và về ô mã. Cần quyền tạo khách hàng hoặc nhà cung cấp.
- **Xem danh mục đầy đủ** (dòng cuối danh sách) mở danh mục có hai tab Khách hàng và Nhà cung cấp; ↑↓ rồi Enter để chọn. Cần quyền xem khách hàng hoặc nhà cung cấp.
- **Phân bổ chứng từ** chỉ hiện khi lý do có ảnh hưởng công nợ và đã chọn đối tượng. Tiền chưa phân bổ vẫn để đó, đối trừ sau ở màn Đối trừ chứng từ. Khi hiện Phân bổ vượt số còn nợ hoặc Tổng phân bổ vượt số tiền phiếu thì chưa lưu được.
- Chi làm quỹ âm: tùy chính sách trong cấu hình, hệ thống cho qua, hiện hộp thoại **Quỹ bị âm** để bạn xác nhận, hoặc chặn lưu với **Không đủ tiền trong quỹ**.
- Ngày thuộc kỳ đã khóa sổ thì không lưu được và phiếu cũ trong kỳ đó chỉ xem (đầu phiếu báo Bạn không có quyền sửa phiếu này). Ngày trước ngày đầu kỳ của chi nhánh cũng bị từ chối.
- Phiếu chi tự động từ phiếu nhập có nhãn **Tự động** và chỉ xem. Bấm **Mở phiếu kho** để sửa, hoặc **Đóng** để thoát.
- **Hủy phiếu**, **Khôi phục** và **Xóa** chỉ hiện khi bạn có quyền tương ứng và phiếu là của bạn (hoặc bạn có quyền sửa phiếu của mọi người).
- Hủy, xóa hoặc sửa phiếu đã được đối trừ với chứng từ khác thì có thể hiện hộp thoại **Bỏ đối trừ chứng từ** để bạn xác nhận. Phiếu đã hủy không sửa được, khôi phục thì đối trừ cũ không quay lại.

## Phím tắt
- `Ctrl S` Lưu
- `Insert` Thêm dòng
- `F4` Thêm nhanh đối tượng (ở ô mã **Đối tượng**)
- `Enter` Sang ô kế tiếp: **Ngày giờ**, **Quỹ**, **Đối tượng** (mã), tên, **Lý do**, **Người nhận**, **Địa chỉ**, **Lý do chi**, **Kèm theo**, **Ghi chú**, rồi vào bảng dòng (**Diễn giải**, **Số tiền**, **Ghi chú**)
- `Shift Enter` Về ô trước; `Enter` ở **Ghi chú** của dòng cuối thêm một dòng mới
