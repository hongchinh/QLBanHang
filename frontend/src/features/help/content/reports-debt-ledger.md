---
key: reports-debt-ledger
title: Sổ chi tiết công nợ
routes: [/reports/debt-ledger]
sources: [frontend/src/pages/cash-reports/debt-ledger-page.tsx, frontend/src/pages/cash-reports/debt-ledger-table.tsx, frontend/src/pages/cash-reports/report-partner-picker.tsx, frontend/src/pages/cash-reports/document-code-link.tsx, frontend/src/pages/cash-reports/document-link.ts, frontend/src/features/cash-reports/hooks.ts, frontend/src/components/period-preset-select/**, frontend/src/components/report-scope-select/**, frontend/src/components/tt133-export/**]
related: []
updated: 2026-10-15
---
## Dùng để
Xem từng dòng phát sinh công nợ của một khách hàng hoặc nhà cung cấp trong một khoảng ngày, từ số dư đầu kỳ đến số dư cuối kỳ. Có thể xuất sổ S12 theo mẫu TT133 ra Excel hoặc PDF.

## Các bước
1. {#side} Chọn **Phía**: **Phải thu** (khách hàng) hoặc **Phải trả** (nhà cung cấp).
2. {#partner} Gõ mã hoặc tên vào ô **Đối tượng** rồi chọn một đối tượng trong danh sách.
3. {#period} Chọn nhanh trong **Kỳ** (Tháng này, Quý trước...) hoặc nhập **Từ ngày** và **Đến ngày**.
4. {#read} Đọc bảng: dòng **Số dư đầu kỳ**, các dòng phát sinh, rồi **Cộng phát sinh** và **Số dư cuối kỳ**.
5. {#open} Bấm vào số chứng từ ở cột **Số CT** để mở chứng từ đó.
6. {#export} Bấm **Xuất theo mẫu TT133**, chọn **Excel** hoặc **PDF**, chọn đối tượng cần xuất rồi bấm **Xuất**.

## Lưu ý
- Nút ← chỉ hiện khi mở từ báo cáo khác, và quay lại báo cáo đó với bộ lọc cũ. Số dư công nợ giữ phía, khoảng ngày, ô tìm kiếm và phạm vi trên địa chỉ trang nên khi quay lại vẫn như lúc bạn rời đi.
- Chưa chọn **Đối tượng** thì màn hình báo Chọn đối tượng để xem sổ chi tiết công nợ. Đổi **Phía** sẽ bỏ đối tượng đã chọn vì mỗi phía có danh sách riêng.
- Mặc định xem từ ngày đầu tháng đến hôm nay. Nếu **Từ ngày** sau **Đến ngày**, màn hình báo Từ ngày không được sau Đến ngày, không hiện sổ và nút **Xuất theo mẫu TT133** bị mờ.
- Cột **Tăng**, **Giảm** để trống khi không phát sinh. Cột **Số dư** ghi Nợ hoặc Có theo phía đang xem. Khoảng ngày không có phát sinh thì bảng báo Không có phát sinh trong khoảng thời gian này.
- Cột **Số CT** chỉ bấm được khi tài khoản của bạn mở được màn hình của chứng từ đó; không thì chỉ hiện số chứng từ.
- Ô **Phạm vi** (Chi nhánh đang làm việc hoặc Toàn công ty) chỉ hiện khi tài khoản được làm việc ở mọi chi nhánh; người khác luôn xem theo chi nhánh đang làm việc.
- Hộp thoại xuất cho chọn **Đang xem** đối tượng hiện tại, **Chọn nhiều** đối tượng, hoặc **Tất cả có phát sinh trong kỳ**; nút **Xuất** bị mờ khi chọn nhiều mà chưa thêm đối tượng nào.
- Chỉ tài khoản có quyền Báo cáo công nợ mới mở được màn hình này.
