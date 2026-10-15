---
key: settings-period-lock
title: Khóa sổ
routes: [/settings/period-lock]
sources: [frontend/src/pages/settings/period-lock-page.tsx, frontend/src/features/branches/**, backend/src/OrderMgmt.Application/Organization/Branches/**]
related: []
updated: 2026-10-15
---
## Dùng để
Khóa sổ từng chi nhánh đến một ngày: chứng từ có ngày bằng hoặc trước ngày đó không còn được thêm, sửa, xóa hay hủy. Dùng sau khi đã chốt số liệu của kỳ.

## Các bước
1. {#find} Tìm chi nhánh trong bảng (cột **Mã** và **Chi nhánh**); cột **Khóa sổ đến** cho biết ngày khóa hiện tại.
2. {#date} Chọn ngày ở ô **Khóa sổ đến** của chi nhánh đó.
3. {#save} Bấm **Lưu**; hệ thống báo Đã khóa sổ.
4. {#unlock} Muốn mở lại, bấm **Bỏ khóa**; hệ thống báo Đã bỏ khóa sổ.

## Lưu ý
- Nút ← quay lại Cấu hình nằm ở đầu trang, trước tiêu đề.
- Nút **Lưu** chỉ bấm được khi ô ngày có giá trị; nút **Bỏ khóa** chỉ bấm được khi chi nhánh đang có ngày khóa.
- Chứng từ bị chặn vì khóa sổ thì màn hình báo Ngày chứng từ đã khóa sổ, kèm ngày khóa.
- Mỗi chi nhánh có ngày khóa riêng, nên khóa chi nhánh nào chỉ tác động chứng từ của chi nhánh đó.
