---
key: payments.form
title: Phiếu chi
routes: [/payments/new, /payments/:id]
sources: [frontend/src/pages/cash-vouchers/**, frontend/src/features/cash-vouchers/**, frontend/src/features/cash-common/**, backend/src/OrderMgmt.Application/CashDebt/CashVouchers/**]
related: [receipts.form]
updated: 2026-10-15
---
## Dùng để
Ghi nhận tiền chi ra từ một quỹ: trả tiền nhà cung cấp, hoàn tiền cho khách hoặc chi khác. Phiếu chi trừ vào quỹ và, tùy lý do, giảm công nợ phải trả hoặc tăng công nợ phải thu.

## Các bước
1. {#reason} Chọn **Lý do** (ví dụ Trả tiền nhà cung cấp) và **Quỹ**, rồi kiểm tra **Ngày giờ**.
2. {#partner} Gõ mã, tên hoặc MST ở **Đối tượng**, Enter để chọn người nhận tiền.
3. {#person} Điền **Người nhận**, **Lý do chi** và **Kèm theo** (số chứng từ gốc) nếu cần.
4. {#lines} Nhập từng dòng ở bảng: **Diễn giải** và **Số tiền**. `Insert` hoặc **Thêm dòng** để thêm dòng, **Tổng** là số tiền chi.
5. {#allocation} Nếu có phần **Phân bổ chứng từ**, kiểm tra cột **Phân bổ** (tự gợi ý chứng từ đến hạn trước), sửa hoặc bấm **Gợi ý** để gợi ý lại.
6. {#save} Bấm **Lưu tạm** hoặc **Lưu và thoát** (**Cập nhật** khi sửa phiếu), rồi in phiếu bằng **In** nếu cần.

## Lưu ý
- Ô **Đối tượng** phụ thuộc **Lý do**: chưa chọn lý do thì ô khóa, lý do không dùng đối tượng thì báo Không cần đối tượng. Trả tiền nhà cung cấp chỉ nhận nhà cung cấp, Chi hoàn tiền khách hàng chỉ nhận khách hàng. Đổi lý do làm đối tượng không còn hợp thì đối tượng bị bỏ.
- Phần **Phân bổ chứng từ** chỉ hiện khi lý do có ảnh hưởng công nợ và đã chọn đối tượng. Tiền chưa phân bổ vẫn để đó, đối trừ sau ở màn Đối trừ chứng từ.
- Khi hiện Phân bổ vượt số còn nợ hoặc Tổng phân bổ vượt số tiền phiếu thì chưa lưu được.
- Chi làm quỹ âm: tùy chính sách trong cấu hình, hệ thống cho qua, hiện hộp thoại **Quỹ bị âm** để bạn xác nhận, hoặc chặn lưu với **Không đủ tiền trong quỹ**.
- Ngày thuộc kỳ đã khóa sổ thì không lưu được và phiếu cũ trong kỳ đó chỉ xem. Ngày trước ngày đầu kỳ của chi nhánh cũng bị từ chối.
- Phiếu chi tự động từ phiếu nhập có nhãn **Tự động** và chỉ xem. Bấm **Mở phiếu kho** để sửa, hoặc **Đóng** để thoát.
- Phiếu của người khác chỉ sửa được nếu bạn có quyền sửa phiếu của mọi người.
- Sửa, hủy hoặc xóa phiếu đã được đối trừ với chứng từ khác thì có thể hiện hộp thoại **Bỏ đối trừ chứng từ** liệt kê các chứng từ đó để bạn xác nhận. Phiếu đã hủy không sửa được, khôi phục thì đối trừ cũ không quay lại.
- Phải lưu thay đổi trước khi in.

## Phím tắt
- `Ctrl S` Lưu
- `Insert` Thêm dòng
