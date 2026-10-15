---
key: settings-cash
title: Cấu hình tiền & công nợ
routes: [/settings/cash]
sources: [frontend/src/pages/settings/cash-settings-page.tsx, frontend/src/features/cash-settings/**, frontend/src/features/cash-common/labels.ts, backend/src/OrderMgmt.Application/CashDebt/Settings/**, backend/src/OrderMgmt.Application/CashDebt/Guards/**]
related: []
updated: 2026-10-15
---
## Dùng để
Chọn hệ thống xử lý thế nào khi dư nợ của khách vượt hạn mức, và khi một chứng từ làm quỹ bị âm. Mỗi tình huống có ba mức: **Cho phép**, **Cảnh báo** hoặc **Chặn**.

## Các bước
1. {#credit} Trong khung **Tiền & Công nợ**, chọn mức cho **Vượt hạn mức nợ**.
2. {#fund} Chọn mức cho **Chi âm quỹ**.
3. {#save} Bấm **Lưu**; hệ thống báo Đã lưu cấu hình tiền & công nợ.

## Lưu ý
- Nút ← quay lại Cấu hình nằm ở đầu trang, trước tiêu đề.
- **Cho phép** là cứ lưu chứng từ, không hỏi. **Cảnh báo** là hiện thông báo để bạn xác nhận rồi mới lưu. **Chặn** là không cho lưu.
- **Vượt hạn mức nợ** chỉ áp dụng với khách hàng đã được đặt hạn mức nợ ở chi nhánh của chứng từ; khách chưa có hạn mức thì không bị kiểm tra.
- Cấu hình áp dụng chung cho mọi chi nhánh.
- Ở mức **Cảnh báo**, hộp thoại Vượt hạn mức nợ hoặc Quỹ bị âm hiện lên để bạn xác nhận tiếp tục lưu.
- Ở mức **Chặn**, hộp thoại Vượt hạn mức nợ hoặc Không đủ tiền trong quỹ chỉ báo lý do và chứng từ không lưu được.
