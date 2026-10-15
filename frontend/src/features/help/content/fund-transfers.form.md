---
key: fund-transfers.form
title: Phiếu chuyển quỹ
routes: [/fund-transfers/new, /fund-transfers/:id]
sources: [frontend/src/pages/fund-transfers/fund-transfer-form-page.tsx, frontend/src/features/fund-transfers/**, backend/src/OrderMgmt.Application/CashDebt/FundTransfers/**]
related: [receipts.form, payments.form]
updated: 2026-10-15
---
## Dùng để
Chuyển tiền giữa hai quỹ của chi nhánh, ví dụ nộp tiền mặt vào tài khoản ngân hàng. Phiếu chuyển quỹ chỉ đổi số dư hai quỹ, không ảnh hưởng công nợ.

## Các bước
1. {#date} Kiểm tra **Ngày giờ** của phiếu (mặc định là lúc mở phiếu), sửa nếu chuyển vào ngày khác.
2. {#funds} Chọn **Quỹ đi** (quỹ trả tiền ra) và **Quỹ đến** (quỹ nhận tiền), hai quỹ phải khác nhau.
3. {#amount} Nhập **Số tiền** chuyển, phải lớn hơn 0.
4. {#note} Ghi **Diễn giải** hoặc **Ghi chú** nếu cần.
5. {#save} Bấm **Lưu tạm** hoặc **Lưu và thoát** (**Cập nhật** khi sửa phiếu).

## Lưu ý
- Chỉ chọn được các quỹ còn dùng của chi nhánh đang làm việc; mỗi quỹ hiện kèm loại (tiền mặt hoặc ngân hàng).
- **Số phiếu** tự cấp khi lưu: ô này hiện Tự động và đầu trang ghi Số phiếu dự kiến.
- Chuyển quá số dư của quỹ đi: tùy chính sách trong cấu hình, hệ thống cho qua, hiện hộp thoại **Quỹ bị âm** để bạn xác nhận, hoặc chặn lưu với **Không đủ tiền trong quỹ**. Hủy hoặc xóa phiếu cũng bị kiểm tra như vậy, vì tiền bị rút lại khỏi quỹ đến.
- Ngày thuộc kỳ đã khóa sổ thì không lưu được và phiếu cũ trong kỳ đó chỉ xem. Ngày trước ngày đầu kỳ của chi nhánh cũng bị từ chối.
- Phiếu của người khác chỉ sửa được nếu bạn có quyền sửa phiếu của mọi người; không có quyền thì thấy thông báo Bạn không có quyền sửa phiếu này.
- Phiếu đã hủy không sửa được; **Khôi phục** tính lại phiếu vào số dư hai quỹ.

## Phím tắt
- `Ctrl S` Lưu
