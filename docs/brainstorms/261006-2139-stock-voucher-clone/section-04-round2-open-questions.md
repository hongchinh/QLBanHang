# Section 04 — Đợt 2 (Thu/Chi & Công nợ): phát hiện và câu hỏi mở

## Quyết định đã chốt

- Module Thu/Chi = **kết hợp**: data model & nghiệp vụ của `ChungTu.vb` (bảng CHUNGTU — loại đang dùng thật, nối công nợ) + UI nhiều dòng như `PhieuThuChi.vb`.
- UI list + form **giống Báo giá**.
- Phiếu thu/chi tự động từ phiếu kho khi HTTT `IsCash`: số tiền = **Số tiền TT** của phiếu kho; chọn khoản thu/chi ngay trên form phiếu kho; lưu trong cùng transaction; hủy/xóa phiếu kho thì hủy/xóa phiếu thu/chi đi kèm.
- Công nợ dùng **DebtLedger** (ghi khi lưu phiếu xuất/nhập/thu/chi) + số dư đầu kỳ.
- Hạn mức nợ chỉ áp phiếu xuất: `Số dư + Tổng TT − Số tiền TT > Hạn mức`.
- Báo cáo Số dư công nợ KH/NCC kèm chi tiết phát sinh.
- Khóa sổ theo chi nhánh áp cho cả phiếu thu/chi (khóa ngày bất kỳ như MISA).
- *(review)* Đối tượng dùng **danh mục chung** (Khách hàng có cờ Là KH / Là NCC) → cùng một đối tượng có cả phải thu và phải trả; thêm nghiệp vụ **Bù trừ công nợ** (Nợ 331 / Có 131) vào Đợt 2. Xem [section-05](section-05-review-decisions.md).

## Phát hiện từ code cũ

- `PhieuThuChi.vb` (bảng THUCHI/NOIDUNGTHUCHI, nhiều dòng) **không có đường vào UI** (lệnh `THUTMP` không được menu nào gọi), Xóa xóa nhầm bảng KIEMKE, In không chạy → dead feature.
- `ChungTu.vb` là form thật cho THUTM / CHITM / THUKHAC / CHIKHAC: chỉ header, 1 phiếu = 1 dòng CHUNGTU. Trường: số, ngày, đối tượng, khoản thu/chi (bắt buộc theo loại), diễn giải (bắt buộc), số tiền, loại quỹ (LOAITIEN ↔ DANHMUCLOAIQUY), ghi chú.
- Số dư đầu: `SODUCHITIETDOITUONG` (số dư nợ / số dư có theo đối tượng × đơn vị; không có cột năm — mỗi năm 1 CSDL), import Excel 5 cột.
- Số dư quỹ đầu: `SODUDAUKYTIENMAT` theo loại quỹ; sổ quỹ đọc CHUNGTU theo LOAITIEN.
- Hạn mức hiệu lực: `HANMUCNOKHACH` (khách × chi nhánh + cờ kiểm soát), chặn cứng; `DANHMUCTENDONVI.HANMUCDUNO` là UI chết.
- Công nợ phải thu (bản sao SP `SoPhaiThu`): số dư đầu + phiếu xuất − THUTM (trừ khoản thu '10') + CHITM khoản chi '09','04','08'. **THUKHAC/CHIKHAC không tính.** Mã khoản bị hard-code; danh mục khoản không có cờ ảnh hưởng công nợ.
- SP thật (`GetSoDuConNo`, `GetChiTietPhieuCongNo`, `SoTongHopCongNo`, `SoQuyTienMat`) không có trong repo.
- Bug đáng chú ý: F4 xóa phiếu không kiểm gì (có thể xóa nhầm phiếu cũ), Xóa không kiểm khóa sổ, đánh số lấy "dòng cuối" không phải MAX, QR lưu nhầm phiếu, THUKHAC/CHIKHAC không in được.

## Câu hỏi cần chốt trước khi plan Đợt 2

1. THUKHAC/CHIKHAC có tính vào công nợ và sổ quỹ không?
2. Thay mã hard-code ('10', '09', '04', '08') bằng cờ trên danh mục khoản thu/chi: "Ảnh hưởng công nợ: Phải thu / Phải trả / Không" + dấu (+/−)? CHITM ngoài các mã đó có giảm phải trả NCC không?
3. Phiếu thu/chi có cần chọn phiếu xuất/nhập cụ thể để thanh toán (trừ nợ theo chứng từ) không, hay chỉ trừ nợ theo đối tượng?
4. Hạn mức nợ theo khách × chi nhánh (như cũ) hay theo khách toàn hệ thống? Chặn cứng hay cảnh báo?
5. Số dư công nợ đầu kỳ: một mốc ngày bắt đầu theo chi nhánh hay theo kỳ/năm? Cho phép nhập đồng thời nợ và có cho một đối tượng?
6. Phiếu thu/chi nhiều dòng: mỗi dòng một đối tượng riêng (như PhieuThuChi) hay đối tượng ở header, dòng chỉ là khoản + số tiền?
7. Có cần Loại quỹ / Sổ quỹ / số dư quỹ đầu kỳ và chặn chi âm quỹ không?
8. Đánh số thu/chi: theo loại × chi nhánh, có reset theo tháng không (dùng chung `DocumentNumbering`)?
9. Đối tượng thu/chi: chỉ Khách hàng / Nhà cung cấp, hay thêm "đối tượng khác" (nhân viên, text tự do)? *(Danh mục chung đã chốt — nếu cần nhân viên thì thêm cờ Là NV như MISA.)*
10. Bù trừ công nợ: chọn đối trừ theo từng chứng từ hay theo tổng số dư của đối tượng?
