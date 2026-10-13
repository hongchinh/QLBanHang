# Danh sách kiểm tra trước khi dùng báo cáo theo mẫu TT133

> Lập: 2026-10-14 04:03:39 · Dành cho: kế toán và PO · Thuộc [kế hoạch TT133](SUMMARY.md)

Phần mềm xuất được 8 biểu ra Excel và PDF theo bố cục Phụ lục 4 Thông tư 133/2016/TT-BTC: S04a, S05, S06, S07, S08, S12, Bảng tổng hợp công nợ và Biên bản đối chiếu công nợ. Số trên file luôn bằng số trên màn hình báo cáo tương ứng.

Trước khi dùng chính thức cần làm ba việc:

1. Trả lời 5 câu hỏi ở phần 1.
2. Duyệt bản in của từng biểu ở phần 2.
3. Thiết lập phần mềm theo phần 3.

Bố cục các biểu được dựng theo các trang tổng hợp văn bản, chưa đối chiếu với bản gốc Phụ lục 4. Việc đối chiếu đó là phần 2 của tài liệu này.

## 1. Câu hỏi cần trả lời

Phần mềm đang làm theo cột "Đang làm theo". Nếu câu trả lời khác thì phải sửa phần mềm trước khi dùng.

| # | Câu hỏi | Đang làm theo | Nếu đồng ý | Nếu không | Trả lời |
|---|---|---|---|---|---|
| 1 | Doanh nghiệp áp dụng Thông tư 133, hay sẽ chuyển sang Thông tư 99/2025 từ năm tài chính 2027? | Thông tư 133 | Không đổi gì. | Mọi biểu đang in "Mẫu số Sxx-DNN" và dòng "Ban hành theo Thông tư số 133/2016/TT-BTC". Theo Thông tư 99 thì mã mẫu và một số cột khác, phải làm thêm một bộ biểu riêng. Việc này chưa có trong phần mềm. | |
| 2a | Có chấp nhận thay cột "TK đối ứng" bằng cột "Lý do" không? Áp dụng cho S04a, S05, S06, S12. | Thay bằng "Lý do" (lý do thu/chi, lý do nhập/xuất; chuyển quỹ ghi "Chuyển quỹ"; bù trừ ghi "Bù trừ công nợ") | Không đổi gì. | Phần mềm không có hệ thống tài khoản kế toán, nên không in được số tài khoản. Muốn có cột này phải làm thêm phần gắn tài khoản, là một việc mới cần quyết định riêng. | |
| 2b | Có chấp nhận thay cột "Thời hạn được chiết khấu" của S12 bằng "Hạn thanh toán" không? | In hạn thanh toán của chứng từ làm tăng nợ; dòng giảm nợ để trống | Không đổi gì. | Phần mềm không lưu thời hạn chiết khấu. Cột này sẽ phải bỏ hoặc để trống cho kế toán ghi tay. | |
| 3 | Đơn vị có mẫu Biên bản đối chiếu công nợ riêng không (câu "Căn cứ…", điều khoản "phản hồi trong N ngày")? | Chưa có hai câu này | Gửi mẫu; phần mềm sẽ thêm đúng các câu của mẫu. | Biên bản giữ nguyên như bản in mẫu. | |
| 4 | Chi nhánh có mã số thuế riêng không? | Không. Cả công ty dùng một mã số thuế, nhập ở Thông tin đơn vị | Không đổi gì. Sổ in cho một chi nhánh ghi "Tên công ty – Tên chi nhánh", sổ toàn công ty ghi "Tên công ty". | Thông tin đơn vị phải nhập theo từng chi nhánh và phải bỏ lựa chọn "Toàn công ty" trên các báo cáo công nợ và Tổng hợp nhập xuất tồn. Đây là việc sửa lớn. | |
| 5 | Có đồng ý tính giá vốn bình quân theo từng kho không (Phạm vi tính giá = "Theo kho")? | Đổi sang "Theo kho" khi bắt đầu dùng | Làm theo mục 3.2. S06 của từng kho có đủ đơn giá và thành tiền. | Giữ "Theo chi nhánh": S06 của từng kho, và S07 khi lọc theo một kho, chỉ còn các cột số lượng, kèm dòng "Giá trị tính theo chi nhánh". | |

## 2. Duyệt bản in từng biểu

### Cách duyệt

- Bản in mẫu nằm trên máy của nhóm phát triển, thư mục `C:\Users\admin\AppData\Local\Temp\tt133-samples\`. Mỗi biểu có một file Excel (`.xlsx`) và một file PDF cùng tên. Nhóm phát triển gửi cả thư mục cho kế toán.
- In file PDF ra giấy A4 rồi **đối chiếu với Phụ lục 4 TT133 bản gốc**: tên sổ, tên và thứ tự cột, các dòng đầu kỳ / cộng / cuối kỳ, khối chữ ký.
- Số liệu trong bản mẫu là dữ liệu thử, không phải số của doanh nghiệp. Chỉ duyệt bố cục.
- Ghi "Đạt" hoặc nội dung cần sửa vào cột "Duyệt".

### Điểm khác mẫu gốc áp dụng cho mọi biểu

- Dòng "Sổ này có … trang, đánh số từ trang 01 đến trang …" để dấu chấm, kế toán ghi tay sau khi in.
- "Ngày mở sổ" là ngày mở sổ của chi nhánh. Sổ toàn công ty lấy ngày sớm nhất trong các chi nhánh. Chưa khai báo thì in dấu chấm.
- Dòng "Ngày … tháng … năm …" ở cuối là ngày xuất file.
- "Người ghi sổ" là họ tên người đang đăng nhập và bấm xuất. "Kế toán trưởng" và "Người đại diện theo pháp luật" lấy từ Thông tin đơn vị.
- Dòng kỳ báo cáo in "Tháng MM năm yyyy" khi chọn trọn một tháng, "Năm yyyy" khi chọn trọn một năm, còn lại in "Từ ngày … đến ngày …".
- Tồn kho đầu kỳ được ghi sổ vào ngày mở sổ của chi nhánh. Vì vậy ở **kỳ đầu tiên**, S06 và S08 có một dòng "Tồn đầu kỳ" nằm trong phần phát sinh, và S07 cộng số này vào cột "Nhập trong kỳ" chứ không vào "Tồn đầu kỳ". Từ kỳ thứ hai trở đi thì bình thường.
- Trong file PDF, số ở các ô của bảng đang in dấu phẩy ngăn hàng nghìn và dấu chấm thập phân (`1,250,750,000`, `350.5`). Mở file Excel trên máy Windows tiếng Việt thì ra dấu chấm ngăn hàng nghìn. Kế toán cho biết có chấp nhận dấu phẩy trên PDF không.

### Từng biểu

| Biểu | File mẫu | Việc cần làm | Điểm khác mẫu gốc đã biết | Duyệt (ký, ngày) |
|---|---|---|---|---|
| **S04a-DNN** Sổ quỹ tiền mặt | `S04a-DNN` (2 trang) | Đối chiếu với Phụ lục 4 TT133 bản gốc | Không có mẫu S04b riêng; cột "TK đối ứng" thành "Lý do" ngay trong S04a. Cột A (ngày ghi sổ) và cột B (ngày chứng từ) luôn cùng một ngày, vì phiếu thu/chi chỉ có một ngày. "Diễn giải" in diễn giải của phiếu rồi đến người nộp/nhận; nếu diễn giải trùng với lý do thì chỉ in ở cột "Lý do". Chuyển quỹ in số chứng từ ở cột Thu hoặc Chi và lý do "Chuyển quỹ". Cột "Ghi chú" để trống. Biểu có 10 cột in trên giấy dọc nên chữ nhỏ; cần xem có đọc được không. | |
| **S05-DNN** Sổ tiền gửi ngân hàng | `S05-DNN` (1 trang) | Như trên | Cột "TK đối ứng" thành "Lý do". Ngày ghi sổ và ngày chứng từ luôn cùng một ngày. Hai dòng "Nơi mở tài khoản giao dịch" và "Số hiệu tài khoản tại nơi gửi" lấy từ khai báo của quỹ ngân hàng. Cột "Ghi chú" để trống. | |
| **S06-DNN** Sổ chi tiết vật liệu, dụng cụ, sản phẩm, hàng hóa | `S06-DNN` (2 sổ, mỗi sổ 1 trang) | Như trên | Cột "TK đối ứng" thành "Lý do". "Diễn giải" in tên đối tượng và loại chứng từ ("Phiếu nhập kho", "Phiếu xuất kho", "Tồn đầu kỳ"). Đơn giá in tới 3 chữ số thập phân (ví dụ `15,064,058.355`). Mỗi mặt hàng ở mỗi kho là một sổ. Có thể có dòng "Giá vốn tạm tính" dưới tiêu đề khi giá vốn chưa chốt. Tồn đầu kỳ ở kỳ đầu tiên: xem phần trên. Cột "Ghi chú" để trống. | |
| **S07-DNN** Bảng tổng hợp chi tiết vật liệu, dụng cụ, sản phẩm, hàng hóa | `S07-DNN` (1 trang) | Như trên | Thêm cột "Số lượng" bên cạnh "Thành tiền" ở cả bốn nhóm. Dòng "Cộng" chỉ cộng các cột thành tiền. Cuối biểu dùng ba chữ ký chung (Người ghi sổ, Kế toán trưởng, Người đại diện theo pháp luật). Tồn đầu kỳ ở kỳ đầu tiên nằm trong "Nhập trong kỳ". Có dòng "Kho: …", thêm "Nhóm hàng: …" khi lọc nhóm, và "Điều kiện lọc: mã hoặc tên chứa …" khi xuất lúc đang tìm kiếm. Người không có quyền xem giá vốn chỉ xuất được các cột số lượng. | |
| **S08-DNN** Thẻ kho (Sổ kho) | `S08-DNN` (2 thẻ, mỗi thẻ 1 trang) | Như trên | "Ngày lập thẻ" là ngày xuất file. "Tờ số" là số thứ tự của thẻ trong file. Cột "Ngày nhập, xuất" trùng với cột "Ngày, tháng". "Diễn giải" in tên đối tượng và lý do nhập/xuất. Cột "Ký xác nhận của kế toán" để trống. Cuối thẻ dùng ba chữ ký chung. Tồn đầu kỳ ở kỳ đầu tiên: xem phần trên. Dòng "Đơn vị" ở đầu thẻ đang cao hơn cần thiết một dòng. | |
| **S12-DNN** Sổ chi tiết thanh toán với người mua (người bán) | `S12-DNN_Receivable` (phải thu, 4 trang), `S12-DNN_Payable` (phải trả, 3 trang), `S12-DNN_Receivable_Company` (toàn công ty, 5 trang) | Như trên | Cột "TK đối ứng" thành "Lý do". Cột "Thời hạn được chiết khấu" thành "Hạn thanh toán". Dòng "Tài khoản: 131/331" thành "Loại công nợ: Phải thu khách hàng / Phải trả người bán". Mỗi đối tượng là một sổ, bắt đầu ở trang mới. Khi xuất **toàn công ty**, "Diễn giải" có `[mã chi nhánh]` đứng trước, vì số chứng từ của các chi nhánh có thể trùng nhau. Khách trả trước thì số dư nằm ở cột "Có" của sổ người mua (và ngược lại với người bán). | |
| **Bảng tổng hợp chi tiết thanh toán** với người mua / người bán | `TongHopCongNo_Receivable`, `TongHopCongNo_Payable`, `TongHopCongNo_Receivable_Search` (mỗi file 1 trang) | TT133 không có mẫu bắt buộc cho bảng này; duyệt theo nhu cầu của đơn vị | Không có "Mẫu số" ở góc phải. Cuối bảng dùng ba chữ ký chung. Khi xuất lúc đang tìm kiếm, bảng chỉ gồm các đối tượng tìm được và in thêm dòng "Điều kiện lọc: mã hoặc tên chứa …" (xem file `…_Search`). | |
| **Biên bản đối chiếu công nợ** | `DoiChieuCongNo_Customer` (khách hàng, 2 trang), `DoiChieuCongNo_Both` (đối tượng vừa mua vừa bán, 1 trang) | Đối chiếu với mẫu đơn vị đang dùng (câu hỏi 3) | Là chứng từ giao dịch, không phải sổ: không có "Mẫu số" và không có "Sổ này có … trang". Chưa có câu "Căn cứ…" và điều khoản phản hồi. "Đại diện" của Bên B để trống. Khi xuất toàn công ty, "Diễn giải" có `[mã chi nhánh]` đứng trước. Biên bản dài sang trang 2 thì tiêu đề cột không lặp lại. Đối tượng có cả phải thu và phải trả thì có hai bảng và dòng "Bù trừ". Số tiền trong câu kết luận in dấu chấm ngăn hàng nghìn (`368.775.000 đồng`), còn số trong bảng của file PDF in dấu phẩy. | |

## 3. Thiết lập trước khi dùng

### 3.1. Thông tin đơn vị

- Vào **Cài đặt → Thông tin đơn vị**, nhập: Tên đơn vị, Địa chỉ, Mã số thuế, Kế toán trưởng, Người đại diện theo pháp luật. "Ghi chú dịch vụ kế toán" chỉ nhập khi thuê dịch vụ kế toán; có nhập thì in thành một dòng dưới khối chữ ký.
- Chưa nhập Tên đơn vị thì nút xuất hiện dòng nhắc "Chưa có thông tin đơn vị". Vẫn xuất được, nhưng phần đầu biểu để trống.
- Dòng "Địa chỉ" trên sổ của một chi nhánh lấy địa chỉ chi nhánh; chi nhánh chưa có địa chỉ, hoặc sổ toàn công ty, thì lấy địa chỉ ở Thông tin đơn vị.
- Người được sửa Thông tin đơn vị: người có quyền **"Thông tin đơn vị"** (`settings.company`). Mặc định là ba vai trò Quản trị hệ thống (ADMIN), Quản lý (MANAGER) và Kế toán (ACCOUNTANT). Mọi người đã đăng nhập đều xem được.

### 3.2. Phạm vi tính giá = "Theo kho"

Làm sau khi câu hỏi 5 được đồng ý.

- Vào **Cài đặt → Cấu hình kho**, ô **Phạm vi tính giá** (`CostingScope`), chọn **"Theo kho"** (`Warehouse`) rồi lưu.
- Sau khi lưu, phần mềm tự tính lại giá vốn.
- Tác động: giá vốn bình quân tính riêng cho từng kho, không còn bình quân chung cả chi nhánh. Cùng một mặt hàng có thể có giá vốn khác nhau ở hai kho. Giá vốn hàng xuất và giá trị tồn kho đã có trên các báo cáo có thể thay đổi sau lần tính lại.
- Nên đổi trước khi in sổ của kỳ đầu tiên, và xem lại báo cáo Tồn kho sau khi đổi.

### 3.3. Quyền của người in sổ

| Việc | Quyền cần có | Mặc định ai có |
|---|---|---|
| Xuất S04a, S05 | "Sổ quỹ" | Quản trị, Quản lý, Kế toán |
| Xuất S12, Bảng tổng hợp công nợ, Biên bản đối chiếu | "Báo cáo công nợ" | Quản trị, Quản lý, Kế toán |
| Xuất S07, S08 | "Báo cáo tồn kho, thẻ kho" | Quản trị, Quản lý, Kho / giao hàng. **Vai trò Kế toán mặc định chưa có.** |
| Xuất S06, và các cột thành tiền của S07 | thêm "Xem giá vốn, giá trị tồn kho" | Quản trị, Quản lý. **Vai trò Kế toán mặc định chưa có.** |
| In sổ **toàn công ty** (S07, S12, Bảng tổng hợp công nợ, Biên bản đối chiếu) | "Làm việc ở mọi chi nhánh" (`branches.access_all`) | Quản trị, Quản lý. **Vai trò Kế toán mặc định chưa có.** |

- Người không có quyền "Làm việc ở mọi chi nhánh" không thấy ô chọn "Toàn công ty" và chỉ in được sổ của chi nhánh đang làm việc.
- S04a, S05 in theo từng quỹ; S06, S08 in theo chi nhánh đang làm việc. Bốn biểu này không có lựa chọn "Toàn công ty".
- Biên bản đối chiếu gửi cho khách nên in "Toàn công ty" để ra đủ số dư của khách với cả công ty.
- Nếu kế toán là người in sổ kho và sổ toàn công ty, quản trị viên cấp thêm ba quyền in đậm ở trên cho vai trò Kế toán.

### 3.4. Giới hạn khi xuất nhiều sổ

S06, S08 và S12 xuất được một, nhiều hoặc "Tất cả có phát sinh trong kỳ"; mỗi đối tượng hoặc mỗi mặt hàng × kho là một sổ trong cùng một file.

- **Tối đa 300 sổ mỗi lần xuất.** Quá 300 thì phần mềm báo "Số sổ cần xuất vượt giới hạn" và không xuất; khi đó thu hẹp điều kiện (chọn một kho, chọn ít đối tượng hơn) rồi xuất nhiều lần.
- Thời gian đo được với 300 sổ, mỗi sổ 20 dòng:

| Biểu | Excel | Chuyển sang PDF | Tổng khi xuất PDF | Số trang PDF |
|---|---|---|---|---|
| S12 | 2,6 giây | 28,5 giây | 31,0 giây | 300 |
| S08 | 3,4 giây | 19,5 giây | 22,9 giây | 300 |
| S06 | 3,1 giây | 61,5 giây | 64,6 giây | 600 |

- Với 100 sổ, chuyển sang PDF mất 9,8 giây (S12), 6,8 giây (S08) và 20,7 giây (S06).
- Trình duyệt chờ tối đa 120 giây cho một lần xuất. Phần mềm cho phép chuyển PDF tối đa 130 giây.
- Các con số trên đo bằng dữ liệu thử trên máy của nhóm phát triển, chưa đo trên máy chủ thật. Sổ dài hơn 20 dòng hoặc máy chủ yếu hơn thì chậm hơn. Xuất hàng loạt bản PDF của S06 là chậm nhất; nếu chờ lâu thì xuất Excel, hoặc chia theo từng kho.
- Khi PDF lỗi, phần mềm báo "Xuất PDF thất bại, hãy dùng Excel." File Excel có cùng nội dung và in được từ Excel.
- **Lưu ý khi nhiều người xuất PDF cùng lúc:** trong lần thử chạy ba lần chuyển PDF cùng một lúc, 7 trên 27 lần bị lỗi (không lần nào bị treo). Lần thử này chạy thẳng bộ chuyển PDF, chưa chạy qua phần mềm. Cách xử lý đang chờ quyết định của PO. Cho đến khi sửa xong: gặp thông báo lỗi PDF thì bấm xuất lại, hoặc dùng Excel.

## 4. Xác nhận

| Vai trò | Họ tên | Ngày | Chữ ký |
|---|---|---|---|
| Kế toán trưởng | | | |
| PO | | | |
