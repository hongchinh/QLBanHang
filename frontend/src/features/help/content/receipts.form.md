---
key: receipts.form
title: Phiếu thu
routes: [/receipts/new, /receipts/:id]
sources: [frontend/src/pages/cash-vouchers/**, frontend/src/features/cash-vouchers/**, frontend/src/features/cash-common/**, backend/src/OrderMgmt.Application/CashDebt/CashVouchers/**, frontend/src/lib/confirm-required.ts, frontend/src/components/voucher-print/voucher-print-button.tsx, frontend/src/components/partner-autocomplete/**, backend/src/OrderMgmt.Application/CashDebt/Guards/**]
related: [payments.form]
updated: 2026-10-15
---
## Dùng để
Ghi nhận tiền thu vào một quỹ: thu tiền khách hàng, nhận hoàn tiền từ nhà cung cấp hoặc thu khác. Phiếu thu cộng vào quỹ và, tùy lý do, giảm công nợ phải thu hoặc tăng công nợ phải trả.

## Các bước
1. {#reason} Kiểm tra **Lý do** (mặc định Thu tiền khách hàng), **Quỹ** (mặc định quỹ tiền mặt) và **Ngày giờ**, đổi nếu cần.
2. {#partner} Gõ mã, tên hoặc MST ở **Đối tượng**, Enter để chọn khách hàng hoặc nhà cung cấp; tên, MST, địa chỉ tự điền.
3. {#person} Điền **Người nộp**, **Kèm theo** (số chứng từ gốc) và **Lý do nộp** nếu cần.
4. {#lines} Nhập từng dòng ở bảng: **Diễn giải** và **Số tiền**. `Insert` hoặc **Thêm dòng** để thêm dòng, **Tổng** là số tiền thu.
5. {#allocation} Nếu có **Phân bổ chứng từ**, kiểm tra cột **Phân bổ** (gợi ý sẵn chứng từ đến hạn sớm nhất), sửa hoặc bấm **Gợi ý**.
6. {#save} Bấm **Lưu tạm** (ở lại phiếu) hoặc **Lưu và thoát** (**Cập nhật** khi sửa phiếu); muốn in thì lưu xong bấm **In**, chọn PDF hoặc Excel.

## Lưu ý
- Ô **Đối tượng** phụ thuộc **Lý do**: chưa chọn lý do thì ô khóa, lý do không dùng đối tượng thì báo Không cần đối tượng. Thu tiền khách hàng chỉ nhận khách hàng, Thu hoàn tiền NCC chỉ nhận nhà cung cấp. Lý do có ảnh hưởng công nợ thì bắt buộc chọn đối tượng (báo Lý do này ảnh hưởng công nợ, vui lòng chọn đối tượng). Đổi lý do làm đối tượng không còn hợp thì đối tượng bị bỏ.
- **Phân bổ chứng từ** chỉ hiện khi lý do có ảnh hưởng công nợ và đã chọn đối tượng. Tiền chưa phân bổ vẫn để đó, đối trừ sau ở màn Đối trừ chứng từ. Khi hiện Phân bổ vượt số còn nợ hoặc Tổng phân bổ vượt số tiền phiếu thì chưa lưu được.
- Thu tiền cộng vào quỹ nên thường không làm quỹ âm, nhưng sửa, hủy hoặc xóa phiếu thu cũ có thể làm quỹ âm. Khi đó, tùy chính sách trong cấu hình, hệ thống cho qua, hiện hộp thoại **Quỹ bị âm** để bạn xác nhận, hoặc chặn lưu với **Không đủ tiền trong quỹ**.
- Ngày thuộc kỳ đã khóa sổ thì không lưu được và phiếu cũ trong kỳ đó chỉ xem (đầu phiếu báo Bạn không có quyền sửa phiếu này). Ngày trước ngày đầu kỳ của chi nhánh cũng bị từ chối.
- Phiếu thu tự động từ phiếu xuất có nhãn **Tự động** và chỉ xem. Bấm **Mở phiếu kho** để sửa, hoặc **Đóng** để thoát.
- **Hủy phiếu**, **Khôi phục** và **Xóa** chỉ hiện khi bạn có quyền tương ứng và phiếu là của bạn (hoặc bạn có quyền sửa phiếu của mọi người).
- Hủy, xóa hoặc sửa phiếu đã được đối trừ với chứng từ khác thì có thể hiện hộp thoại **Bỏ đối trừ chứng từ** để bạn xác nhận. Phiếu đã hủy không sửa được, khôi phục thì đối trừ cũ không quay lại.

## Phím tắt
- `Ctrl S` Lưu
- `Insert` Thêm dòng
