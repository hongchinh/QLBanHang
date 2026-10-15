---
key: reports-debt-documents
title: Công nợ theo chứng từ
routes: [/reports/debt-documents]
sources: [frontend/src/pages/cash-reports/debt-documents-page.tsx, frontend/src/pages/cash-reports/report-partner-picker.tsx, frontend/src/pages/cash-reports/document-code-link.tsx, frontend/src/pages/cash-reports/document-link.ts, frontend/src/features/cash-reports/hooks.ts, backend/src/OrderMgmt.Application/CashDebt/Reports/**]
related: [debt-matching]
updated: 2026-10-15
---
## Dùng để
Xem những chứng từ còn nợ của từng khách hàng hoặc nhà cung cấp tại một ngày, gồm số tiền đã đối trừ, số còn lại và số ngày quá hạn. Dùng khi cần biết khoản nợ đến từ chứng từ nào.

## Các bước
1. {#side} Chọn **Phía**: **Phải thu** (khách hàng) hoặc **Phải trả** (nhà cung cấp).
2. {#at} Chọn **Tại ngày**; mặc định là hôm nay.
3. {#partner} Chọn **Đối tượng** để xem riêng một đối tượng, hoặc để trống để xem tất cả.
4. {#read} Đọc từng khối theo đối tượng: bảng **Chứng từ còn nợ**, bảng **Chưa đối trừ** (nếu có) và dòng **Số dư**.
5. {#open} Bấm vào số chứng từ ở cột **Số CT** để mở chứng từ đó.

## Lưu ý
- Nút ← chỉ hiện khi mở từ báo cáo khác, và quay lại báo cáo đó với bộ lọc cũ. Tuổi nợ giữ phía, ngày, đối tượng và các mốc tuổi nợ trên địa chỉ trang nên khi quay lại vẫn như lúc bạn rời đi.
- Đổi **Phía** sẽ bỏ **Đối tượng** đã chọn vì mỗi phía có danh sách riêng. Muốn xem lại tất cả đối tượng, bấm nút X ở cuối ô **Đối tượng**.
- Bảng **Chứng từ còn nợ** có các cột **Số tiền**, **Đã đối trừ**, **Còn lại**, **Số ngày nợ** và **Quá hạn (ngày)**; số ngày quá hạn chỉ hiện (màu đỏ) khi chứng từ đã quá **Hạn TT**.
- Bảng **Chưa đối trừ** liệt kê các khoản giảm nợ (như phiếu thu, phiếu chi) còn phần chưa đối trừ vào chứng từ nào.
- Cuối trang có ba tổng: **Còn lại**, **Chưa đối trừ** và **Tổng số dư**. Không có chứng từ nào thì màn hình báo Không có chứng từ còn nợ tại ngày này.
- Cột **Số CT** chỉ bấm được khi tài khoản của bạn mở được màn hình của chứng từ đó; không thì chỉ hiện số chứng từ.
- Số liệu tính theo chi nhánh đang làm việc; màn hình này không có ô Phạm vi.
- Màn hình này không có nút xuất. Chỉ tài khoản có quyền Báo cáo công nợ mới mở được.
