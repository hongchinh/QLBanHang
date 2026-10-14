# Danh sách kiểm tra trước khi dùng mẫu in chứng từ TT133

> Lập: 2026-10-15 · Dành cho: kế toán và PO · Thuộc [kế hoạch mẫu in chứng từ TT133](SUMMARY.md) · Thiết kế: [brainstorm](../../../brainstorms/261013-2042-tt133-voucher-print-forms/SUMMARY.md)

Phần mềm in được 4 mẫu chứng từ ra Excel và PDF theo bố cục Phụ lục 3 Thông tư 133/2016/TT-BTC, từ màn phiếu và từ màn danh sách (một phiếu, hoặc nhiều phiếu đã chọn, mỗi phiếu một trang):

| Mẫu | Tên | Khổ giấy |
|---|---|---|
| 01-TT | Phiếu thu | A5 ngang |
| 02-TT | Phiếu chi | A5 ngang |
| 01-VT | Phiếu nhập kho | A4 dọc |
| 02-VT | Phiếu xuất kho | A4 dọc |

Số trên bản in luôn là số của phiếu trên màn hình (riêng chế độ "Giá vốn" lấy số từ sổ kho, khớp thẻ kho).

Trước khi dùng chính thức cần làm ba việc:

1. Trả lời 6 câu hỏi ở phần 1.
2. Duyệt bản in của từng mẫu ở phần 2.
3. Thiết lập phần mềm theo phần 3.

Bố cục các mẫu được dựng theo các trang tổng hợp văn bản, chưa đối chiếu với bản gốc Phụ lục 3. Việc đối chiếu đó là phần 2 của tài liệu này.

## 1. Câu hỏi cần trả lời

Phần mềm đang làm theo cột "Đang làm theo". Nếu câu trả lời khác thì phải sửa phần mềm trước khi dùng.

| # | Câu hỏi | Đang làm theo | Nếu đồng ý | Nếu không | Trả lời |
|---|---|---|---|---|---|
| 1 | Phiếu thu/chi của **quỹ ngân hàng** có in theo 01-TT/02-TT không, hay kế toán chỉ dùng giấy báo Có / ủy nhiệm chi của ngân hàng? | In được phiếu của cả quỹ tiền mặt lẫn quỹ ngân hàng | Không đổi gì. | Phiếu thu/chi của quỹ ngân hàng phải bị chặn in (nút in ẩn hoặc báo lỗi). Việc này chưa có trong phần mềm, cần làm thêm. Cho đến lúc đó, kế toán chỉ in phiếu của quỹ tiền mặt. | |
| 2 | Có chấp nhận để trống ô **Nợ, Có và Quyển số** (dòng chấm) cho kế toán ghi tay không? | Cả ba in dòng chấm. Phần mềm không có hệ thống tài khoản nên không có số tài khoản để in | Không đổi gì. | Muốn in Nợ/Có thì phải làm thêm phần gắn tài khoản cho từng loại lý do thu/chi, nhập/xuất, là một việc mới cần quyết định riêng. Quyển số cần thêm một ô nhập trên phiếu hoặc một cài đặt cho đơn vị. | |
| 3 | Cột **"Số lượng theo chứng từ" (01-VT) / "Yêu cầu" (02-VT)** in bằng số lượng thực nhập/xuất có đúng thực tế không, hay phải để trống cho thủ kho ghi? | In bằng đúng số lượng thực nhập/xuất của dòng phiếu | Không đổi gì. | Để trống cột này (hoặc để dòng chấm) cho thủ kho ghi tay; sửa nhỏ ở mẫu in, không đổi dữ liệu. | |
| 4 | Chế độ "Giá trên phiếu" thêm các dòng **Chiết khấu, Thuế GTGT, Cước vận chuyển** dưới bảng (ngoài mẫu gốc), và với phiếu xuất có giá đã gồm VAT thì thêm dòng chữ nghiêng "Trong đó thuế GTGT đã gồm trong giá". Có chấp nhận không? | Có các dòng này khi giá trị khác 0; phép cộng luôn ra đúng Tổng cộng của phiếu. Phiếu không có chiết khấu, VAT cộng thêm và cước chỉ in một dòng "Cộng" | Không đổi gì. | Bỏ các dòng phụ thì tổng in ra không còn bằng Tổng cộng của phiếu trên màn hình (khi phiếu có chiết khấu, VAT hoặc cước). Có thể dùng chế độ "Không in tiền" hoặc "Giá vốn" cho bản giao hàng, hoặc làm thêm mẫu in rút gọn. | |
| 5 | Hàng tính theo m, m², m³ in **quy cách** (kích thước, số tấm) trong cột Tên, ví dụ `Kính cường lực (1200×2400×5 mm, 10 tấm)`. Cách ghi này có đúng nhu cầu của thủ kho không? | In quy cách trong ngoặc sau tên; đơn vị tính là m, m² hoặc m³ và số lượng là số đã quy đổi | Không đổi gì. | Chỉ in tên hàng (bỏ phần trong ngoặc), hoặc thêm một cột quy cách riêng. Cần cho biết thủ kho muốn ghi thế nào. | |
| 6 | Phiếu kho có dòng ở **nhiều kho**: in tên các kho ở dòng "Nhập/Xuất tại kho" và thêm cột **Kho** có được không, hay phải tách mỗi kho một phiếu in? | Một kho: in tên kho đó. Nhiều kho: tên các kho nối bằng dấu phẩy và bảng thêm cột "Kho" (mã kho) sau Đơn vị tính | Không đổi gì. | Mỗi kho một phiếu in: phần mềm phải tách phiếu theo kho khi in (số phiếu trên bản in sẽ không còn một-một với phiếu trên màn hình). Việc này chưa có trong phần mềm. | |

## 2. Duyệt bản in từng mẫu

### Cách duyệt

- Bản in mẫu nằm trên máy của nhóm phát triển, thư mục `%TEMP%\voucher-print-samples\`. Mỗi mẫu có một file Excel (`.xlsx`) và một file PDF cùng tên. Nhóm phát triển gửi cả thư mục cho kế toán.
- In file PDF ra giấy (A5 ngang cho 01-TT/02-TT, A4 dọc cho 01-VT/02-VT) rồi **đối chiếu với Phụ lục 3 TT133 bản gốc**: tên phiếu, mã mẫu, các dòng thông tin, tên và thứ tự cột của bảng, dòng cộng, khối chữ ký.
- Số liệu trong bản mẫu là dữ liệu thử, không phải số của doanh nghiệp. Chỉ duyệt bố cục.
- Ghi "Đạt" hoặc nội dung cần sửa vào cột "Duyệt".

### Điểm khác mẫu gốc áp dụng cho mọi mẫu

- Ô **Quyển số**, **Nợ**, **Có** in dòng chấm, kế toán ghi tay (câu hỏi 2).
- Không có các dòng ngoại tệ (tỷ giá, số tiền quy đổi) của mẫu gốc.
- Dòng "Ngày … tháng … năm …" phía trên khối chữ ký là **ngày lập phiếu** (không phải ngày in).
- "Giám đốc" và "Kế toán trưởng" lấy từ Thông tin đơn vị; "Người lập phiếu" là họ tên người tạo phiếu. Các chức danh còn lại (Thủ quỹ, Thủ kho, Người nộp/nhận tiền, Người giao/nhận hàng) chỉ in chức danh và "(Ký, họ tên)", để trống tên.
- Phiếu đã hủy vẫn in được, có chữ **"ĐÃ HỦY"** dưới tiêu đề.
- Chưa nhập Thông tin đơn vị thì vẫn in, các ô đơn vị để trống.
- Trong file PDF, số in dấu chấm ngăn hàng nghìn và dấu phẩy thập phân, giống file Excel mở trên máy Windows tiếng Việt.

### Từng mẫu

| Mẫu và chế độ | File mẫu | Việc cần làm | Điểm khác mẫu gốc đã biết | Duyệt (ký, ngày) |
|---|---|---|---|---|
| **01-TT** Phiếu thu | `01-TT` (một phiếu có đối tác, người nộp, hai dòng, "Kèm theo 2 chứng từ gốc"); `01-TT-cancelled` (phiếu đã hủy); `01-TT-batch` (ba phiếu, ba trang) | Đối chiếu với Phụ lục 3 TT133 bản gốc | Nợ, Có, Quyển số là dòng chấm. "Họ và tên người nộp tiền" lấy người nộp, trống thì lấy tên đối tác. "Lý do nộp" lấy diễn giải phiếu, trống thì diễn giải các dòng, rồi tên lý do thu. "Kèm theo": số nguyên in "n chứng từ gốc", chữ khác in nguyên văn, trống in dòng chấm. "Đã nhận đủ số tiền (viết bằng chữ)" là dòng chấm. Khối chữ ký: Giám đốc, Kế toán trưởng, Người nộp tiền, Người lập phiếu, Thủ quỹ. | |
| **02-TT** Phiếu chi | `02-TT-long` (lý do dài khoảng 400 ký tự, kiểm tra xuống dòng trên khổ A5) | Như trên | Như 01-TT, người nhận tiền thay người nộp tiền. Khối chữ ký: Giám đốc, Kế toán trưởng, Thủ quỹ, Người lập phiếu, Người nhận tiền. Lý do dài xuống dòng trong khung, phiếu vẫn nằm trên một trang A5. | |
| **01-VT** Phiếu nhập kho, **Giá trên phiếu** | `01-VT-voucher` (có chiết khấu dòng, chiết khấu đơn, VAT, cước); `01-VT-40-lines` (40 dòng, sang trang, lặp tiêu đề bảng); `01-VT-spec-warehouses` (dòng m² có quy cách, hai kho) | Như trên | Có các dòng phụ Cộng tiền hàng, Chiết khấu, Thuế GTGT, Cước vận chuyển, Tổng cộng khi phiếu có giá trị đó (câu hỏi 4). Quy cách hàng m, m², m³ nằm trong cột Tên (câu hỏi 5). Phiếu nhiều kho có thêm cột "Kho" (câu hỏi 6). "Số lượng theo chứng từ" in bằng số lượng thực nhập (câu hỏi 3). "Theo … số … ngày … của …" và "Số chứng từ gốc kèm theo" là dòng chấm, "của" điền tên đối tác. Khối chữ ký: Người lập phiếu, Người giao hàng, Thủ kho, Kế toán trưởng. | |
| **01-VT** Phiếu nhập kho, **Giá vốn** | `01-VT-cost` | Như trên; so đơn giá và thành tiền với thẻ kho của mặt hàng | Chỉ in dòng hàng theo dõi tồn kho. Đơn giá và thành tiền lấy từ sổ kho (khớp thẻ kho, kể cả khi giá nhập đã gồm VAT), một dòng "Cộng". Phiếu đã hủy không còn giá vốn: in như "Không in tiền" kèm ghi chú "Phiếu đã hủy, không có giá vốn." | |
| **01-VT** Phiếu nhập kho, **Không in tiền** | Chưa có file riêng; xem `02-VT-none` (cùng bố cục, khác tiêu đề, chữ ký và nhãn) | Như trên | Chỉ in dòng hàng theo dõi tồn kho; cột Đơn giá, Thành tiền để trống; dòng "Cộng" và bằng chữ để dòng chấm. Phiếu không có dòng hàng theo dõi tồn kho in bảng rỗng kèm ghi chú "Phiếu không có hàng theo dõi tồn kho." | |
| **02-VT** Phiếu xuất kho, **Giá trên phiếu** | `02-VT-vat-inside` (dòng có giá đã gồm VAT) | Như trên | Phiếu có dòng giá đã gồm VAT thêm dòng chữ nghiêng "Trong đó thuế GTGT đã gồm trong giá: …" dưới Tổng cộng; dòng này không tham gia phép cộng. "Số lượng yêu cầu" in bằng số lượng thực xuất (câu hỏi 3). "Lý do xuất kho" lấy lý do xuất của phiếu; "Địa chỉ (bộ phận)" lấy địa chỉ đối tác. "(Hoặc bộ phận có nhu cầu nhập)" dưới Kế toán trưởng không in. Khối chữ ký: Người lập phiếu, Người nhận hàng, Thủ kho, Kế toán trưởng, Giám đốc. Kiểm tra tên người lập phiếu dài không bị cắt. | |
| **02-VT** Phiếu xuất kho, **Giá vốn** | Chưa có file riêng; xem `01-VT-cost` (cùng cách tính giá vốn) | Như trên; so với thẻ kho | Như 01-VT Giá vốn. Phiếu xuất có thêm ghi chú "Giá vốn tạm tính (kỳ tính giá vốn chưa kết thúc)." khi kỳ tính giá vốn của ngày phiếu chưa kết thúc. | |
| **02-VT** Phiếu xuất kho, **Không in tiền** | `02-VT-none` (có tên người lập phiếu dài, kiểm tra khối chữ ký) | Như trên | Như 01-VT Không in tiền. | |

## 3. Thiết lập trước khi dùng

### 3.1. Thông tin đơn vị

- Vào **Cài đặt → Thông tin đơn vị**, nhập: Tên đơn vị, Địa chỉ, Giám đốc (Người đại diện theo pháp luật), Kế toán trưởng. Phiếu không in mã số thuế.
- Chưa nhập Tên đơn vị thì nút in hiện dòng nhắc "Chưa có thông tin đơn vị". Vẫn in được, nhưng các ô đơn vị, tên Giám đốc và Kế toán trưởng để trống.
- Dòng "Đơn vị" của phiếu ghi "Tên công ty – Tên chi nhánh" của chi nhánh lập phiếu; dòng "Địa chỉ" lấy địa chỉ chi nhánh, chi nhánh chưa có thì lấy địa chỉ ở Thông tin đơn vị.
- Người được sửa Thông tin đơn vị: người có quyền **"Thông tin đơn vị"** (`settings.company`). Mặc định là ba vai trò Quản trị hệ thống (ADMIN), Quản lý (MANAGER) và Kế toán (ACCOUNTANT).

### 3.2. Quyền của người in

| Việc | Quyền cần có |
|---|---|
| In phiếu thu | Xem phiếu thu (`receipts.view`) |
| In phiếu chi | Xem phiếu chi (`payments.view`) |
| In phiếu nhập kho | Xem phiếu nhập kho (`stock_in.view`) |
| In phiếu xuất kho | Xem phiếu xuất kho (`stock_out.view`) |
| In phiếu kho ở chế độ **Giá vốn** | thêm "Xem giá vốn, giá trị tồn kho" (`inventory.view_cost`) |

- Người không có quyền xem giá vốn không thấy mục "Giá vốn" trong hộp thoại chọn giá in; gọi trực tiếp API sẽ nhận lỗi 403. Quản trị viên cấp quyền này cho những ai được phép biết giá vốn (mặc định Quản trị và Quản lý, vai trò Kế toán chưa có).
- Chỉ in được phiếu của chi nhánh đang làm việc; phiếu của chi nhánh khác báo "Phiếu thuộc chi nhánh khác."
- Chế độ giá chọn lần trước được nhớ theo từng người dùng trên từng trình duyệt; người không còn quyền giá vốn tự quay về "Giá trên phiếu".

### 3.3. Số bản in và giới hạn

- Mỗi phiếu in **một liên**. Muốn in nhiều liên, đặt số bản trong hộp thoại in của trình duyệt hoặc máy in.
- In nhiều phiếu: chọn các dòng trên màn danh sách (chỉ trong trang đang hiển thị; đổi trang hoặc đổi bộ lọc thì bỏ chọn) rồi bấm "In" ở thanh "Đã chọn N phiếu". Mỗi phiếu một sheet Excel / một trang PDF.
- **Tối đa 300 phiếu mỗi file** (cấu hình `Tt133Export:MaxSheets`, dùng chung với các sổ). Quá 300 thì phần mềm báo "Số phiếu cần in (…) vượt giới hạn 300. Hãy chọn ít phiếu hơn." và không in.
- Khi PDF lỗi, phần mềm báo "Xuất PDF thất bại, hãy dùng Excel." File Excel có cùng nội dung và in được từ Excel.
- Nút in chỉ bật khi phiếu đã lưu và không có thay đổi chưa lưu ("Lưu thay đổi trước khi in").

## 4. Xác nhận

| Vai trò | Họ tên | Ngày | Chữ ký |
|---|---|---|---|
| Kế toán trưởng | | | |
| PO | | | |
