# Section 01 — Quyết định

> Chốt với user ngày 2026-10-07. Phần này trả lời 10 câu hỏi ở [section-04 cũ](../261006-2139-stock-voucher-clone/section-04-round2-open-questions.md) và bổ sung các quyết định phát sinh khi brainstorm.

## 1. Trả lời 10 câu hỏi của section-04

| # | Câu hỏi | Quyết định |
|---|---|---|
| 1 | THUKHAC/CHIKHAC có tính công nợ và sổ quỹ không? | Không còn 4 loại phiếu, chỉ còn Phiếu thu và Phiếu chi. "Thu khác" và "Chi khác" thành lý do có *Ảnh hưởng công nợ = Không*. **Mọi phiếu đều vào sổ quỹ** |
| 2 | Thay mã hard-code bằng cờ? | Có, theo MISA: cờ **DebtEffect** trên Lý do thu/chi (Không / Giảm phải thu / Tăng phải thu / Giảm phải trả / Tăng phải trả). Phiếu chi chỉ giảm phải trả NCC khi lý do có cờ đó |
| 3 | Trả nợ theo chứng từ hay theo đối tượng? | **Cả hai, như MISA.** Khi lập phiếu có thể chọn chứng từ còn nợ để phân bổ. Phần chưa phân bổ được ghép sau ở màn **Đối trừ chứng từ** (thủ công, không tự FIFO) |
| 4 | Phạm vi hạn mức; chặn hay cảnh báo? | **Khách × chi nhánh.** Chính sách cấu hình **Cho phép / Cảnh báo / Chặn** |
| 5 | Nợ đầu kỳ thế nào? | **Theo từng chứng từ cũ**: số CT, ngày CT, hạn TT, số tiền, loại Nợ hoặc Trả trước. Một đối tượng có thể có cả phải thu lẫn phải trả. Dùng ngày đầu kỳ chung của chi nhánh |
| 6 | Đối tượng ở header hay từng dòng? | **Header: đối tượng + lý do.** Dòng chỉ có diễn giải, số tiền, ghi chú |
| 7 | Quỹ, sổ quỹ, chặn chi âm quỹ? | **Danh mục quỹ** theo chi nhánh (tiền mặt + ngân hàng), số dư đầu, Sổ quỹ, chứng từ **Chuyển quỹ** *(thêm sau review)*. Âm quỹ dùng chính sách cấu hình như hạn mức |
| 8 | Đánh số thu/chi | Dùng chung `DocumentNumbering` theo loại × chi nhánh: Receipt, Payment, FundTransfer, DebtOffset. Reset theo cấu hình hiện có (không / tháng / năm) |
| 9 | Đối tượng thu/chi gồm ai? | **KH/NCC, do lý do quy định** (None / KH / NCC / Any). Lý do có ảnh hưởng công nợ thì bắt buộc chọn đối tượng; Thu/Chi khác được để trống và chỉ ghi tên người nộp/nhận. Không có đối tượng nhân viên |
| 10 | Bù trừ công nợ | **Theo chứng từ**: chọn phiếu xuất còn phải thu và phiếu nhập còn phải trả; số tiền hai bên phải bằng nhau. Sinh chứng từ Bù trừ có số |

## 2. Quyết định bổ sung

| Chủ đề | Quyết định |
|---|---|
| Mức "giống MISA" | Theo mô hình nghiệp vụ của MISA, **không** có hệ thống tài khoản và TK Nợ/Có |
| Phiếu kho nào ghi nợ | **`DebtEffect`** trên Lý do nhập xuất, cùng enum với Lý do thu/chi *(sửa sau review, thay cờ `PostsDebt`)*: Xuất bán = tăng phải thu, Nhập mua = tăng phải trả, Nhập hàng bán bị trả lại = giảm phải thu, Xuất trả hàng NCC = giảm phải trả. Lý do có ảnh hưởng thì bắt buộc chọn đối tượng; bị khóa khi lý do đã được dùng |
| Phiếu tự động | Sinh khi Số tiền TT > 0. Thuộc phiếu kho, **chỉ đọc** trên màn thu/chi. Sửa, hủy, khôi phục, xóa đều đi theo phiếu kho. Tự đối trừ với chính phiếu kho đó (cùng phía, ngược chiều). Dùng lý do hệ thống "Thu tiền theo phiếu xuất" / "Chi tiền theo phiếu nhập", nên **không còn** chọn khoản thu/chi trên form phiếu kho như section-04 cũ |
| Số tiền TT *(review)* | Số tiền TT > 0 thì bắt buộc HTTT có loại quỹ, để mọi khoản đã trả đều có phiếu thu/chi thật. Được trả dư Tổng TT; phần dư là khoản trả trước chưa đối trừ |
| Chuyển quỹ *(review)* | Chứng từ `FundTransfer` (quỹ đi → quỹ đến, cùng chi nhánh), không ảnh hưởng công nợ |
| HTTT | `IsCash` → `FundKind` (None / Tiền mặt / Ngân hàng). Phiếu kho dùng quỹ mặc định của loại đó trong chi nhánh và sửa được |
| Hạn thanh toán | Thêm `Customer.CreditDays` và `StockVoucher.DueDate` (mặc định = ngày phiếu + CreditDays, sửa được). Không có hạn TT thì coi như đến hạn ngay ngày chứng từ *(review)* |
| Báo cáo | Số dư công nợ + chi tiết, Công nợ theo chứng từ, Tuổi nợ, Nợ quá hạn + cảnh báo, Công nợ theo nhân viên, Biên bản đối chiếu (xem trên màn hình; in ở Đợt 3), Sổ quỹ |
| Lưu công nợ | Phương án **A**: DebtLedger + DebtSettlement (xem [section-02](section-02-design.md)) |
| Công nợ theo nhân viên | Theo `OwnerUserId` của phiếu xuất. Review đề nghị BA xác nhận lại người lập phiếu xuất có phải sales không (có thể cần `SalespersonId`) |
| Ngày đầu kỳ | Nợ đầu kỳ và số dư quỹ đầu kỳ dùng chung ngày đầu kỳ của chi nhánh với tồn đầu kỳ. Ngày này chỉ sửa được khi chi nhánh chưa có chứng từ *(review)* |
| Quyền NCC | Đã đóng: Đợt 1 đã tách `suppliers.*` (mục còn mở ở section-05 §6 cũ) |

## 3. Đối chiếu MISA

- **Lý do thu/chi:** MISA cho người dùng thêm lý do thu/chi, mỗi lý do có bút toán ngầm định ([helpact](https://helpact.misa.vn/kb/toi-muon-them-cac-loai-ly-do-tren-phieu-thu-phieu-chi-va-hach-toan-tu-dong-theo-tung-loai/)). Ở đây bút toán được thay bằng cờ DebtEffect.
- **Thu tiền khách hàng không theo hóa đơn / Thu khác:** hai lý do riêng; "Thu khác" dùng cho khoản không liên quan công nợ ([helpact](https://helpact.misa.vn/?p=503), [helpact](https://helpact.misa.vn/?p=515)).
- **Thu tiền theo hóa đơn:** người dùng chọn các hóa đơn còn nợ và nhập số thu từng hóa đơn; trả một phần thì nhập số thu nhỏ hơn ([helpact](https://helpact.misa.vn/kb/thu_tien_tra_no_cua_nhieu_khach_hang_bang_tien_mat/)). MISA cho một phiếu thu nợ nhiều khách; ở đây mỗi phiếu chỉ một đối tượng (Q6).
- **Báo cáo công nợ MISA:** tổng hợp, chi tiết, theo hóa đơn, tuổi nợ, quá hạn, theo nhân viên, biên bản đối chiếu, theo mặt hàng ([sme.misa.vn](https://sme.misa.vn/216987/phan-mem-quan-ly-cong-no-nhanh-chong-chinh-xac/)). Đợt 2 lấy tất cả trừ báo cáo theo mặt hàng.
- **Khác MISA có chủ đích:**
  - Không có TK, không có trạng thái "Chưa ghi sổ".
  - Mỗi phiếu thu/chi chỉ một đối tượng.
  - Phiếu tự động từ phiếu kho được đồng bộ hai chiều thay vì sinh một lần.
  - Mockup [ui_form_them_moi_phieu_thu_html.html](../../bd/ui_form_them_moi_phieu_thu_html.html) có cột TK Nợ/Có và đối tượng từng dòng; khi làm UI thì bỏ hai thứ này.
