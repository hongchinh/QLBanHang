---
key: reports-sales-revenue
title: Doanh thu theo sale
routes: [/reports/sales-revenue, /reports/sales-revenue/:saleUserId]
sources: [frontend/src/pages/reports/sales-revenue-page.tsx, frontend/src/pages/reports/sales-revenue-detail-page.tsx, frontend/src/features/reports/sales-revenue/**, frontend/src/features/reports/sales-revenue-detail/**, frontend/src/components/layout/back-link.tsx]
related: []
updated: 2026-10-15
---
## Dùng để
Xem doanh thu của từng nhân viên kinh doanh trong một khoảng ngày, từ các báo giá đã xác nhận. Bấm một sale để xem từng dòng hàng làm nên doanh thu của sale đó.

## Các bước
1. {#range} Nhập **Từ ngày** và **Đến ngày** trong khung **Bộ lọc**; mặc định từ đầu tháng đến hôm nay.
2. {#sale} Chọn **Nhân viên kinh doanh** (mặc định là chính bạn) hoặc **Tất cả** để xem mọi sale.
3. {#read} Đọc khung **Kết quả**: mỗi sale một dòng với **Số báo giá**, **Doanh thu (gồm thuế)**, **Doanh thu thuần**, dòng cuối là **Tổng cộng**.
4. {#drill} Bấm vào một dòng sale để mở trang **Chi tiết doanh thu** của sale đó trong cùng khoảng ngày.
5. {#lines} Đọc khung **Dòng hàng hóa**: mỗi báo giá một cụm dòng, dòng cuối là **Tổng cộng**.
6. {#quote} Bấm vào một dòng hàng để mở báo giá chứa dòng đó.

## Lưu ý
- Nút ← chỉ hiện ở trang Chi tiết doanh thu, trước tiêu đề, và quay lại báo cáo bạn vừa đến từ đó; mở trực tiếp trang chi tiết thì nó quay về Doanh thu theo sale. Bộ lọc của Doanh thu theo sale không nằm trên địa chỉ trang nên khi quay lại, **Từ ngày**, **Đến ngày** và sale trở về mặc định.
- Ô **Nhân viên kinh doanh** chỉ hiện với tài khoản được xem báo giá của mọi người. Các tài khoản khác chỉ thấy doanh thu của chính mình.
- Sale đã ngừng hoạt động có ghi (đã ngừng) cạnh tên.
- Không có báo giá nào thì khung **Kết quả** báo Không có báo giá đã xác nhận trong khoảng này; trang chi tiết báo Không có dòng hàng nào trong khoảng thời gian này.
- Trang chi tiết lấy khoảng ngày lúc bạn bấm vào dòng sale và không có bộ lọc riêng. Mở thẳng địa chỉ không kèm khoảng ngày thì trang báo Vui lòng truy cập trang này từ báo cáo doanh thu theo sale.
- Tiêu đề trang chi tiết ghi thêm tên sale khi bạn mở từ danh sách.
- Các cột **Đơn giá nhập**, **Thành tiền nhập**, **Lợi nhuận** chỉ hiện khi tài khoản có quyền Xem giá vốn/lợi nhuận báo giá.
- Chỉ tài khoản có quyền Báo cáo doanh thu mới mở được hai trang này; không có quyền thì trang báo Bạn không có quyền xem báo cáo này.
