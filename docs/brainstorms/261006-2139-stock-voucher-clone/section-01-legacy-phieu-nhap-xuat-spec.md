# Section 01 — Đặc tả nghiệp vụ form cũ PhieuNhapXuat.vb

Reverse-engineered từ `source/Forms/PhieuNhapXuat.vb` (designer nằm trong file, dòng 215–2310) và helper trong `source/Global/App.vb`. Số dòng trích dẫn để tra cứu khi implement. Đây là **hành vi cũ**; quyết định sửa đổi ở [section-02](section-02-legacy-bug-decisions.md).

## 1. Loại phiếu

- Một class cho **NHAP** và **XUAT**, chọn qua biến global `lPhieuChungTu` lúc Load (2694; menu App.vb 3433–3463).
- Khác biệt theo loại (2749–2766): tiêu đề, nhãn "Người bán/Người mua", "Lý do nhập/xuất", "Ngày nhập/xuất"; `NHAPXUAT.LOAI`, `PHIEU`.
- Trả hàng (NHAPTRA/XUATTRA) do form khác (`PhieuNhapXuatTraHang`) — ngoài phạm vi.

## 2. Header (bảng NHAPXUAT)

| Trường | Cột | Quy tắc |
|---|---|---|
| Số phiếu | SOCHUNGTU | Tự sinh (mục 5) |
| Ngày | NGAYCT | Mặc định hôm nay nếu `TUYCHON.NGAYCT=1`, ngược lại ngày phiếu đang xem; kiểm khóa sổ |
| Đơn vị (đối tượng công nợ) | MADOITUONG/TENDOITUONG/DIACHIDOITUONG | DANHMUCTENDONVI; hiển thị "Số dư công nợ" = `GetSoDuConNo` |
| Người bán/mua | MANX/TENNX/DIACHINX | Cũng tra DANHMUCTENDONVI |
| Kho | MAKHO/TENKHO | **Bắt buộc**; Thêm mới giữ kho phiếu trước |
| Lý do nhập/xuất | MALYDO/TENLYDO | **Bắt buộc**; DANHMUCLYDONHAPXUAT lọc N/X; giữ giá trị cũ khi Thêm |
| Hình thức TT | HINHTHUCTT | DANHMUCHINHTHUCTT; giữ giá trị cũ |
| Ghi chú | GHICHU | Enter → thêm dòng |
| Hủy | SELECTTED | Chỉ đổi qua nút Hủy/Khôi phục |
| Phí VC, CK cả đơn, Tỷ lệ VAT, Tiền VAT, Số tiền TT | PHIVANCHUYEN, CHIETKHAUALL, TYLEVAT, SOTIENVAT, SOTIENTT | Nhóm thanh toán |
| Đơn vị sử dụng | MADONVISUDUNG | Theo phiên đăng nhập; "ALL" chỉ xem |

## 3. Lưới dòng (NOIDUNGNHAPXUAT)

Cột: Mã kho (dòng), Mã hàng, Tên, ĐVT, SL tồn (`GetSoLuongTonTenVatTu`), SL, Đơn giá, Số tiền (SOTIENOK), %CK, Tiền CK, %VAT, Tiền VAT, Số còn lại (SOTIEN), Đơn giá xuất/Tiền xuất (giá vốn, ẩn).

**Chọn hàng** (`ApplyMaHangHoaChiTietToActiveRow` 3595–3696):
- NHAP: %CK = CK mua; ĐG = bảng giá mua theo nhóm (nếu bật) hoặc GIANHAP.
- XUAT: %CK = CK bán; ĐG ưu tiên chính sách giá KH → giá theo lý do → bảng giá bán → GIABANLE; giá vốn = `GetDonGiaTonVatTu`.

**Công thức dòng** (3701–3835):
- `SOTIENOK = SL × ĐG`
- `CK = %CK × SOTIENOK / 100` (giữ nguyên nếu user gõ tay tiền CK)
- `Net = SOTIENOK − CK`
- VAT: XUAT + hàng loại thuế "2." (giá đã gồm VAT) → `Net − Net/(1+%)`; còn lại `Net × %`
- SOTIEN: `VAT_SOTIEN=1` → Net; XUAT + "2." → Net − VAT; còn lại Net + VAT
- `SOTIENXUAT = SL × DONGIAXUAT`

## 4. Tổng

- Tổng tiền hàng = ΣSOTIENOK; Tổng CK = ΣCK + CKALL; Tổng VAT = ΣVAT; Tổng SL = ΣSL.
- Tổng thanh toán: **3 công thức khác nhau** (4902/4904, 3040/3409, 5038) — xem section-02.
- Các tổng **không lưu** vào NHAPXUAT.

## 5. Đánh số

- DANHMUCSOCHUNGTU (KYHIEUCHUNGTU, DODAI) theo LOAI.
- Không theo tháng: MAX số của bản ghi MAX(ID) + 1 (3210–3244).
- Theo tháng (`SOCHUNGTUTHEOTHANG=1`): trong tháng/năm/đơn vị; mẫu `KYHIEUSOCHUNGTU` thay STT/KH/THANG/NAM (3245–3309).
- Tính lại khi Lưu phiếu mới (ghi đè số sửa tay); không chống trùng.

## 6. Thao tác

- **Thêm**: chặn ALL, quyền Them, giữ kho/lý do/HTTT.
- **Sửa**: chặn ALL, khóa sổ, quyền Sua; không có SUAPHIEU → chỉ sửa phiếu mình.
- **Lưu** (2837–3073): khóa sổ → KiemTra → kiểm tồn (XUAT) → số phiếu → header → phiếu thu/chi tự động → nhật ký → lưu dòng → NOIDUNGQUYDOI → cập nhật giá nhập (NHAP) / giá vốn FIFO (XUAT) → STT → QR.
- **Xóa**: quyền, khóa sổ, xác nhận; xóa header + dòng.
- **Hủy/Khôi phục**: `SELECTTED=1/0`.
- **In**: `PhieuNhapKho.rpt` / `PhieuXuatKho.rpt`.
- **Sao chép**: chép dòng phiếu nguồn, giá lấy lại theo danh mục.
- **Nhập Excel**: 7 cột (mã, tên, ĐVT, SL, ĐG, số tiền, ghi chú).
- **Chuyển/Nhận phiếu FTP** qua bảng ZZZ.

## 7. Ảnh hưởng dữ liệu

- **Tồn kho**: tính động qua hàm SQL trên NOIDUNGNHAPXUAT; không có bảng tồn.
- **Giá vốn**: BQ qua `GetDonGiaTonVatTu`; FIFO (`TUYCHON.PPTONKHO2=1`) qua SP `GetDongGiaXuatFIFO` sau khi lưu.
- **Giá nhập danh mục** (NHAP, 3074–3103): `GIANHAP = ĐG`, `NGAYCAPNHATGIA = NGAYCT` nếu ngày phiếu ≥ ngày cập nhật cũ.
- **Quy đổi ĐVT**: mã hàng có `QUYDOI=1` ghi NOIDUNGQUYDOI về mã gốc × hệ số.
- **Phiếu thu/chi tự động** khi HTTT tiền mặt và bật registry THUTM/CHITM → CHUNGTU (IDTHUCHI = LOAIPHIEU).
- **Công nợ**: không ghi sổ; số dư tính bằng `GetSoDuConNo`.

## 8. Kiểm tra

- `KiemTra()` (4747–4800): ngày hợp lệ, kho, lý do, ≥ 1 dòng, mọi dòng có mã hàng; hạn mức nợ (`CANHBAODUNO`).
- Xuất âm (`HANGAM=1`): so từng dòng với tồn kho header; chặn khi `CHOPHEPXXUAT=1`.
- Khóa sổ (`KHOASO`): áp cho Sửa/Lưu/Xóa, không áp cho Hủy.
- Không kiểm SL > 0, đơn giá, đối tượng bắt buộc.
