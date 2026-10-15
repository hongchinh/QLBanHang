---
key: inventory-opening-stock
title: Tồn đầu kỳ
routes: [/inventory/opening-stock]
sources: [frontend/src/pages/inventory/opening-stock-page.tsx, frontend/src/features/opening-stock/**, backend/src/OrderMgmt.Application/Inventory/OpeningStocks/**]
related: [stock-in.form, stock-out.form]
updated: 2026-10-15
---
## Dùng để
Khai báo hàng đang có trong từng kho tại ngày đầu kỳ: số lượng và giá trị của mỗi mặt hàng. Đây là số dư gốc của tồn kho, sau đó tồn kho thay đổi qua phiếu nhập kho và phiếu xuất kho.

## Các bước
1. {#warehouse} Chọn **Kho** cần khai báo, mỗi kho có danh sách riêng. **Ngày đầu kỳ** hiện bên cạnh.
2. {#add} Bấm **Thêm dòng**, rồi chọn hàng ở cột **Mã hàng**.
3. {#qty} Nhập **Số lượng** và **Giá trị** (tổng giá trị của số hàng đó), cột **Đơn giá** tự tính.
4. {#remove} Bớt hàng thì bấm nút xóa ở cuối dòng.
5. {#save} Bấm **Lưu** để ghi danh sách của kho này.

## Lưu ý
- **Lưu** thay toàn bộ danh sách tồn đầu kỳ của kho: hàng đã xóa khỏi danh sách sẽ mất tồn đầu kỳ.
- Ngày đầu kỳ đặt theo chi nhánh. Chưa có thì trang báo Chi nhánh chưa có ngày đầu kỳ và chưa lưu được, cần đặt ở Cấu hình, mục Chi nhánh.
- Mỗi hàng một dòng, chỉ chọn hàng có theo dõi tồn kho; nếu không, trang báo Hàng đã có trong danh sách hoặc Hàng không theo dõi tồn kho.
- Mỗi dòng phải có **Số lượng** lớn hơn 0 và **Giá trị** không âm.
- Ngày đầu kỳ nằm trong kỳ đã khóa sổ thì không sửa được, hệ thống báo Ngày tồn đầu kỳ đã khóa sổ.
- Nếu giảm tồn đầu kỳ làm tồn kho âm, hệ thống cảnh báo để bạn xác nhận hoặc chặn, tùy cấu hình.
