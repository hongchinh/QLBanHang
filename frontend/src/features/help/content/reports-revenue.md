---
key: reports-revenue
title: Báo cáo doanh thu
routes: [/reports/revenue]
sources: [frontend/src/pages/reports/revenue-page.tsx, frontend/src/features/dashboard/components/range-picker.tsx, frontend/src/features/dashboard/components/kpi-card.tsx, frontend/src/features/dashboard/components/revenue-area-chart.tsx, frontend/src/features/dashboard/components/top-list-card.tsx, frontend/src/features/dashboard/use-dashboard-params.ts, frontend/src/features/reports/sales-revenue-detail/**, frontend/src/components/layout/back-link.tsx]
related: []
updated: 2026-10-15
---
## Dùng để
Xem doanh thu từ các báo giá đã xác nhận trong một khoảng ngày: bảng chi tiết từng dòng hàng, tổng doanh thu, biểu đồ theo ngày và khách hàng mua nhiều nhất. Có thể xuất bảng chi tiết ra Excel.

## Các bước
1. {#range} Chọn khoảng ngày bằng các nút **Hôm nay**, **7N**, **30N**, **Tháng này**, **Tháng trước**, hoặc bấm **Tuỳ chỉnh**, nhập **Từ ngày**, **Đến ngày** rồi bấm **Áp dụng**.
2. {#sale} Chọn sale ở ô lọc đầu trang (mặc định là chính bạn); chọn **Tất cả sale** để xem mọi sale.
3. {#detail} Đọc bảng **Chi tiết doanh thu**: mỗi báo giá một cụm dòng hàng, dòng cuối là **Tổng cộng**.
4. {#kpi} Xem ba chỉ số **Tổng doanh thu**, **Số báo giá xác nhận**, **Trung bình / BG**.
5. {#chart} Chọn **Ngày**, **Tuần** hoặc **Tháng** để đổi cách gộp ở biểu đồ, rồi xem **Bảng doanh thu theo ngày** và **Top khách hàng**.
6. {#export} Bấm **Xuất Excel** để tải bảng chi tiết của khoảng đang xem.

## Lưu ý
- Nút ← chỉ hiện khi mở từ báo cáo khác, và quay lại báo cáo đó với bộ lọc cũ. Mở từ Hiệu suất sale thì ← quay lại đúng báo cáo đó; Hiệu suất sale giữ kỳ đã chọn trên địa chỉ trang nên kỳ vẫn như cũ.
- Khoảng ngày và sale được giữ trên địa chỉ trang. Vào màn hình mà chưa có khoảng ngày thì tự đặt là hôm nay.
- Ô chọn sale chỉ hiện với tài khoản được xem báo giá của mọi người. Các tài khoản khác chỉ thấy doanh thu của chính mình và tiêu đề ghi Báo cáo doanh thu của tên bạn.
- Trong hộp **Tuỳ chỉnh**, nếu Đến ngày trước Từ ngày thì hộp báo Đến ngày phải sau hoặc bằng từ ngày và nút **Áp dụng** bị mờ.
- Các cột **Giá nhập**, **Thành tiền nhập**, **Chênh lệch**, **Chênh + cước** chỉ hiện khi tài khoản có quyền Xem giá vốn/lợi nhuận báo giá.
- Không có dòng hàng thì bảng báo Không có dòng hàng nào trong khoảng thời gian này. Chưa có doanh thu thì biểu đồ và bảng theo ngày báo Chưa có dữ liệu.
- Nút **Xuất Excel** bị mờ khi đang tải file; nếu lỗi sẽ báo Không thể xuất Excel. Vui lòng thử lại.
- Chỉ tài khoản có quyền Báo cáo doanh thu mới mở được màn hình này.
