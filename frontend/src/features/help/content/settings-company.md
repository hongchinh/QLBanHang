---
key: settings-company
title: Thông tin đơn vị
routes: [/settings/company]
sources: [frontend/src/pages/settings/company-profile-page.tsx, frontend/src/features/company-profile/**, backend/src/OrderMgmt.Application/Organization/Company/**]
related: []
updated: 2026-10-15
---
## Dùng để
Khai báo tên, địa chỉ, mã số thuế và người ký của đơn vị. Các thông tin này in trên các sổ theo mẫu TT133.

## Các bước
1. {#unit} Nhập **Tên đơn vị** (bắt buộc), **Địa chỉ** và **Mã số thuế** trong khung **Đơn vị**.
2. {#signers} Trong khung **Người ký**, nhập **Kế toán trưởng** và **Người đại diện theo pháp luật**.
3. {#note} Nhập **Ghi chú dịch vụ kế toán** nếu cần.
4. {#save} Bấm **Lưu**; hệ thống báo Đã lưu thông tin đơn vị.

## Lưu ý
- Nút ← quay lại Cấu hình nằm ở đầu trang, trước tiêu đề.
- **Mã số thuế** gồm 10 số, hoặc 10 số, dấu gạch rồi 3 số; để trống cũng được.
- Độ dài tối đa: **Tên đơn vị** 255 ký tự, **Địa chỉ** 500, **Kế toán trưởng** và **Người đại diện theo pháp luật** 150, **Ghi chú dịch vụ kế toán** 500.
- Lưu không được thì hệ thống báo Không thể lưu kèm lý do, và ô sai hiện lỗi ngay dưới ô đó.
