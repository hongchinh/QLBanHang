---
key: debt-opening
title: Nợ đầu kỳ
routes: [/debt/opening]
sources: [frontend/src/pages/debt/opening-debt-page.tsx, frontend/src/pages/debt/opening-debt-form-dialog.tsx, frontend/src/features/opening-debts/**, frontend/src/features/cash-common/labels.ts, frontend/src/lib/confirm-required.ts, backend/src/OrderMgmt.Application/CashDebt/OpeningDebts/**]
related: [debt-matching, customers.list]
updated: 2026-10-15
---
## Dùng để
Nhập các khoản nợ còn tồn từ trước khi dùng phần mềm: mỗi dòng là một chứng từ cũ của khách hàng (phải thu) hoặc nhà cung cấp (phải trả), nợ hoặc trả trước. Các khoản này được ghi vào ngày đầu kỳ của chi nhánh.

## Các bước
1. {#date} Xem **Ngày đầu kỳ** dưới tiêu đề; chưa đặt thì đặt ở Cài đặt → Chi nhánh, nếu không nút **Thêm** bị khóa.
2. {#add} Bấm **Thêm**, chọn **Phía** (Phải thu hoặc Phải trả) và **Loại** (**Nợ** hoặc **Trả trước**).
3. {#partner} Chọn **Đối tượng** (khách hàng với phải thu, nhà cung cấp với phải trả), có thể chọn thêm **Nhân viên** phụ trách.
4. {#doc} Nhập **Số CT**, **Ngày CT**, **Hạn TT** (chỉ với khoản nợ) và **Số tiền**, có thể ghi **Ghi chú**.
5. {#save} Bấm **Lưu**.
6. {#manage} Dùng ô tìm kiếm, ô lọc đối tượng và ô lọc phía để tìm dòng; sửa hoặc xóa bằng hai nút cuối dòng.

## Lưu ý
- **Ngày CT** mặc định là ngày đầu kỳ và không được sau ngày đó.
- Khoản **Trả trước** không có hạn thanh toán; **Hạn TT** của khoản nợ không được trước **Ngày CT**.
- **Số tiền** phải lớn hơn 0.
- Cột **Đã đối trừ** cho biết số đã trừ với chứng từ khác; có thể đối trừ ở màn hình Đối trừ chứng từ hoặc khi lập phiếu thu, phiếu chi.
- Xóa dòng đã đối trừ, hoặc giảm **Số tiền** dưới số đã đối trừ, thì hiện hộp thoại **Bỏ đối trừ chứng từ** để bạn xác nhận. Dòng đã đối trừ không đổi được **Đối tượng**, **Phía** hay **Loại**: bỏ đối trừ trước.
- Kỳ đã khóa sổ thì không thêm, sửa hoặc xóa nợ đầu kỳ được; màn hình báo Ngày chứng từ đã khóa sổ.
