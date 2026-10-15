---
key: settings-quotation
title: Cấu hình hệ thống báo giá
routes: [/settings/quotation]
sources: [frontend/src/pages/settings/quotation-system-settings-page.tsx, frontend/src/features/quotation-settings/**, backend/src/OrderMgmt.Application/Sales/Quotations/Services/QuotationSystemSettingsService.cs, backend/src/OrderMgmt.Application/Reports/Common/RevenueFilterHelper.cs]
related: [quotations.list]
updated: 2026-10-15
---
## Dùng để
Chọn trường ngày dùng để lọc và tổng hợp doanh thu trên các báo cáo doanh thu và dashboard. Đây là cài đặt chung của cả hệ thống.

## Các bước
1. {#field} Trong khung **Trường ngày doanh thu**, mở ô **Trường ngày**.
2. {#choose} Chọn một trong ba cách: **Theo ngày báo giá (mặc định)**, **Theo ngày khách xác nhận** hoặc **Theo ngày kế toán xác nhận nhận tiền**.
3. {#save} Bấm **Lưu**; hệ thống báo Đã lưu cấu hình.

## Lưu ý
- Nút ← quay lại Cấu hình nằm ở đầu trang, trước tiêu đề.
- Thay đổi có hiệu lực ngay lập tức, nên số doanh thu trên báo cáo và dashboard đổi theo.
- Dưới ô chọn có dòng Cập nhật lần cuối, ghi thời điểm và người đã lưu lần gần nhất.
- Lưu không được thì hệ thống báo Lưu thất bại kèm lý do.
