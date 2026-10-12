# Section 04 — Quyết định sau review

> Review BA lead / Solution Architect: 2026-10-07 23:01:34. Kết luận: **duyệt có điều kiện**. Các mục dưới đây đã được sửa vào [section-01](section-01-decisions.md), [section-02](section-02-design.md), [section-03](section-03-delivery.md) và [SUMMARY](SUMMARY.md). Những mục cần BA/kế toán xác nhận đã chuyển vào Open questions của SUMMARY.

## 1. Phải sửa trước khi viết plan (đã sửa)

| # | Phát hiện | Quyết định |
|---|---|---|
| R1 | Phía/chiều công nợ suy ra từ loại phiếu kho + cờ `PostsDebt`. Hàng bán bị trả lại (Nhập, KH) thành tăng phải trả; xuất trả NCC thành tăng phải thu; `PartnerType = Any` không xác định được phía | Bỏ `PostsDebt`, thay bằng **`StockReason.DebtEffect`** dùng chung enum với `CashReason`, có ràng buộc tổ hợp theo hướng nhập/xuất. Phiếu tự động cùng phía, ngược chiều với phiếu kho. Đề xuất seed thêm NTL "Nhập hàng bán bị trả lại" và XTL "Xuất trả hàng NCC". Lý do tự động đổi tên thành TPX/CPN cho đúng cả khi trả hàng |
| R2 | Số tiền TT > 0 với HTTT không có quỹ: không sinh phiếu thu, công nợ ghi đủ Total, còn hạn mức lại trừ PaidAmount → lệch | `PaidAmount > 0` **bắt buộc** HTTT có `FundKind ≠ None`. Hạn mức đọc từ ledger (gồm phiếu tự động), không dùng PaidAmount |
| R3 | Chưa có rule PaidAmount ≤ Total, nên settlement Auto có thể vượt Amount của dòng phiếu kho | Cho phép trả dư. Settlement Auto = min(PaidAmount, phần còn mở của dòng phiếu kho); phần dư là khoản trả trước chưa đối trừ |
| R4 | Thiếu nghiệp vụ chuyển tiền giữa các quỹ | Thêm chứng từ **`FundTransfer`** (quỹ đi, quỹ đến, số tiền) cùng chi nhánh, có số, có quyền `fund_transfers.*`, vào sổ quỹ và kiểm âm quỹ, không ảnh hưởng công nợ |

## 2. Làm rõ thiết kế (đã sửa)

| # | Phát hiện | Quyết định |
|---|---|---|
| C5 | `EffectiveAt` được lưu, nên cũ đi khi sửa ngày chứng từ | Tính lại EffectiveAt của mọi settlement liên quan khi PostedAt đổi, trong cùng transaction |
| C6 | Quy tắc "hủy chứng từ chưa khóa kéo theo settlement đã khóa thì chặn" không bao giờ xảy ra | Bỏ quy tắc, thay bằng ghi chú giải thích |
| C7 | "Khôi phục không khôi phục settlement" mâu thuẫn với phiếu tự động tự đối trừ | Ngoại lệ: settlement Auto được tạo lại khi khôi phục |
| C8 | Sửa phiếu thu/chi gửi lại toàn bộ phân bổ: chưa rõ số phận settlement Manual | Chỉ thay settlement AtVoucher; Manual/Offset giữ nguyên; vượt Total thì 422 `ALLOCATION_EXCEEDS_TOTAL` |
| C9 | DueDate null thì không bao giờ quá hạn | Dòng Increase luôn có DueDate; không có hạn TT thì = DocDate (đến hạn ngay) |
| C12 | Hạn mức kiểm cả khi sửa giảm | Chỉ vi phạm khi số dư sau > hạn mức **và** > số dư trước |
| C13 | Sửa phiếu xuống dưới phần đã đối trừ bị chặn cứng, gây khó cho nghiệp vụ giảm giá sau thanh toán | Dùng pattern xác nhận `DEBT_SETTLEMENTS_WILL_BE_REMOVED` như hủy phiếu |
| C14 | Đổi ngày đầu kỳ khi đã có phát sinh phải ghi lại toàn bộ dữ liệu | `Branch.OpeningDate` chỉ sửa được khi chi nhánh chưa có chứng từ |
| — | Tài liệu: `reports.debt` ghi "6 báo cáo"; SUMMARY ghi "8 báo cáo" nhưng có 7 gạch đầu dòng; DebtEffect của lý do tự động lưu thế nào | Sửa thành 7 báo cáo công nợ + Sổ quỹ, đánh số 1–8; lý do tự động có `IsAuto`, lưu DebtEffect = None, engine suy ra |
| — | Sổ ngân hàng có theo dõi nhiều tài khoản được không (câu hỏi của user) | Được: mỗi tài khoản là một `Fund` Kind = Bank. Sổ quỹ thêm lọc theo loại quỹ; chọn Ngân hàng + tất cả quỹ là sổ tiền gửi của mọi tài khoản, nhóm theo tài khoản, có dòng tổng |
| — | Đối trừ thủ công phụ thuộc kỷ luật của kế toán, ảnh hưởng báo cáo theo NV và tuổi nợ | Grid phân bổ trên form thu/chi gợi ý sẵn theo chứng từ đến hạn sớm nhất |

## 3. Chuyển sang BA / kế toán xác nhận

Đã đưa vào Open questions của [SUMMARY](SUMMARY.md):

- C10: phiếu tự động bị xóa khi Số tiền TT về 0 làm số phiếu thu/chi bị nhảy.
- C11: phiếu thu/chi tiền mặt và ngân hàng có cần đánh số riêng (Phiếu thu vs Giấy báo Có, Phiếu chi vs Ủy nhiệm chi) không.
- Công nợ theo nhân viên: người lập phiếu xuất có phải là sales không, hay cần `SalespersonId`.
- Thanh toán chéo chi nhánh.
- Tài khoản ngân hàng dùng chung nhiều chi nhánh.
- Role SALES có được xem công nợ khách (panel trên phiếu xuất) không.
