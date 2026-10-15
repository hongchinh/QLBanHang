---
key: inventory-stock-card
title: Thẻ kho
routes: [/inventory/stock-card]
sources: [frontend/src/pages/inventory/stock-card-page.tsx, frontend/src/features/inventory-reports/**, frontend/src/pages/quotations/components/product-typeahead-cell.tsx, frontend/src/components/period-preset-select/**, frontend/src/components/tt133-export/**, frontend/src/components/layout/back-link.tsx, backend/src/OrderMgmt.Application/Inventory/Reports/**]
related: [stock-in.form, stock-out.form, inventory-opening-stock, settings-inventory]
updated: 2026-10-15
---
## Dùng để
Xem mọi lần nhập, xuất của một mặt hàng trong khoảng ngày, kèm số tồn sau từng lần. Có thể xuất sổ S08 (số lượng) hoặc S06 (kèm giá trị) theo mẫu TT133.

## Các bước
1. {#product} Gõ mã hoặc tên vào ô **Hàng hóa** rồi chọn một mặt hàng.
2. {#warehouse} Chọn **Kho**, hoặc để **Tất cả kho**.
3. {#period} Chọn nhanh trong **Kỳ** hoặc nhập **Từ ngày** và **Đến ngày**.
4. {#read} Đọc khối tóm tắt **Tồn đầu**, **Nhập**, **Xuất**, **Tồn cuối** rồi đến bảng từng lần phát sinh.
5. {#open} Bấm vào số chứng từ ở cột **Số CT** để mở phiếu nhập kho hoặc phiếu xuất kho đó.
6. {#export} Bấm **Xuất theo mẫu TT133**, chọn mẫu và định dạng (**Excel** hoặc **PDF**), chọn mặt hàng cần xuất rồi bấm **Xuất**.

## Lưu ý
- Nút ← chỉ hiện khi mở bằng cách bấm một dòng trong Tồn kho hoặc Tổng hợp nhập – xuất – tồn, và quay lại báo cáo đó. Tổng hợp nhập – xuất – tồn vẫn giữ khoảng ngày và phạm vi, còn kho, nhóm hàng và ô tìm kiếm trở về mặc định; Tồn kho thì mọi bộ lọc trở về mặc định.
- Mở từ báo cáo khác thì mặt hàng đã được chọn sẵn, còn **Kho** theo dòng bạn bấm ở Tồn kho hoặc theo bộ lọc kho của Tổng hợp nhập – xuất – tồn. **Từ ngày** và **Đến ngày** luôn đặt lại từ đầu tháng đến hôm nay, kể cả khi báo cáo trước đang xem kỳ khác; chọn lại kỳ nếu cần.
- Chưa chọn hàng thì màn hình báo Chọn hàng hóa để xem thẻ kho. Thiếu ngày thì báo Chọn đủ Từ ngày và Đến ngày; **Từ ngày** sau **Đến ngày** thì báo Từ ngày không được sau Đến ngày. Khi đó không hiện thẻ và nút xuất bị mờ. Không có phát sinh thì bảng báo Không có phát sinh trong khoảng thời gian này.
- Với dòng không phải phiếu nhập hay phiếu xuất, cột **Số CT** chỉ ghi tên lý do và không bấm được.
- Các cột và dòng về giá trị (**Đơn giá**, **Giá trị nhập**, **Giá vốn xuất**, **Giá trị tồn**) chỉ hiện khi tài khoản có quyền Xem giá vốn, giá trị tồn kho. Có quyền này thì mới thấy mẫu S06 trong menu xuất; mẫu S08 chỉ có số lượng.
- Khi Cấu hình kho đặt Phạm vi tính giá là Theo chi nhánh mà bạn chọn một **Kho**, kho đó không có giá trị tồn riêng: màn hình báo Giá trị tồn tính theo chi nhánh — bỏ lọc kho để xem, ẩn cột **Giá trị tồn** và giá trị của **Tồn đầu**, **Tồn cuối**.
- Huy hiệu **Giá vốn tạm tính** cạnh tiêu đề báo giá vốn chưa chốt (chỉ hiện với người xem được giá vốn).
- Hộp thoại xuất cho chọn **Đang xem** mặt hàng hiện tại, **Chọn nhiều** mặt hàng, hoặc **Tất cả có phát sinh trong kỳ**; nút **Xuất** bị mờ khi chọn nhiều mà chưa thêm mặt hàng nào.
- Chỉ tài khoản có quyền Báo cáo tồn kho, thẻ kho mới mở được màn hình này.
