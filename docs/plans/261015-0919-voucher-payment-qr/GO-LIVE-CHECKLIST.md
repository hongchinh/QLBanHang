# Danh sách kiểm tra trước khi dùng QR chuyển khoản trên phiếu

> Lập: 2026-10-15 · Dành cho: kế toán và PO · Thuộc [kế hoạch QR chuyển khoản trên phiếu thu và phiếu xuất kho](SUMMARY.md) · Thiết kế: [brainstorm](../../brainstorms/261015-0656-voucher-payment-qr/SUMMARY.md)

Phần mềm in mã **VietQR** (có sẵn số tiền và nội dung chuyển khoản) trên hai mẫu in, đồng thời hiện mã đó trên màn phiếu để khách quét ngay tại quầy:

| Mẫu | Tên | Khổ giấy | QR nằm ở |
|---|---|---|---|
| 01-TT | Phiếu thu | A5 ngang | Bên phải các dòng nội dung |
| 02-VT | Phiếu xuất kho | A4 dọc | Khung "Thông tin chuyển khoản", bên phải, dưới cột Thành tiền |

Trên màn hình, nút **"QR chuyển khoản"** cạnh nút In của phiếu thu và phiếu xuất mở một hộp thoại (trên điện thoại là tấm trượt từ dưới lên) có mã QR, số tiền, ngân hàng, số tài khoản, chủ tài khoản, nội dung, nút sao chép và nút "Tải ảnh QR".

Phiếu nào có QR:

| Phiếu | Có QR khi | Số tiền trong QR |
|---|---|---|
| Phiếu thu | Chưa hủy, tổng tiền lớn hơn 0. Phiếu thu tự động sinh từ phiếu xuất chỉ có QR khi quỹ của nó là quỹ ngân hàng | Tổng tiền của phiếu |
| Phiếu xuất kho | Chưa hủy, lý do xuất là bán hàng (tăng phải thu), số tiền lớn hơn 0 | Tổng tiền trừ phần đã trả vào **quỹ tiền mặt**. Phiếu hình thức thanh toán chuyển khoản: QR đòi cả tổng tiền |
| Phiếu chi, phiếu nhập kho | Không bao giờ | |

Tiền luôn về một **quỹ ngân hàng** của chi nhánh: quỹ của phiếu nếu đó là quỹ ngân hàng, ngược lại là quỹ ngân hàng mặc định của chi nhánh. Quỹ phải khai đủ ngân hàng, số tài khoản và chủ tài khoản; thiếu một thứ thì phiếu vẫn in nhưng không có QR.

Trước khi dùng chính thức cần làm ba việc:

1. Trả lời 3 câu hỏi ở phần 1.
2. Thiết lập phần mềm theo phần 2.
3. Quét thử bằng giấy in thật và màn hình theo phần 3, và đọc phần 4.

## 1. Câu hỏi cần trả lời

Các câu hỏi này không chặn việc phát triển, nhưng cần có câu trả lời trước khi dùng thật. Phần mềm đang làm theo cột "Đang làm theo".

| # | Câu hỏi | Đang làm theo | Nếu trả lời như vậy, phần mềm đổi gì | Trả lời |
|---|---|---|---|---|
| 1 | Phiếu thu **ghi quỹ tiền mặt** nhưng khách chuyển khoản theo QR: kế toán muốn **sửa quỹ của phiếu** hay **lập phiếu chuyển quỹ**? | Phiếu thu ghi quỹ tiền mặt vẫn có QR. Tiền được gửi về **quỹ ngân hàng mặc định của chi nhánh**, và hộp thoại ghi rõ ở dòng **"Quỹ nhận"**. Sổ quỹ tiền mặt và sổ ngân hàng sẽ lệch cho tới khi kế toán chỉnh | **Sửa quỹ của phiếu**: không đổi gì, kế toán sửa phiếu sau khi nhận tiền. **Lập phiếu chuyển quỹ**: không đổi gì, kế toán lập phiếu chuyển từ quỹ tiền mặt sang quỹ ngân hàng. **Không muốn QR cho phiếu thu quỹ tiền mặt**: phải sửa phần mềm, bỏ việc tự chuyển sang quỹ ngân hàng mặc định, phiếu thu ghi quỹ tiền mặt sẽ báo "không có QR". Khi đó muốn thu bằng QR thì nhân viên phải chọn quỹ ngân hàng ngay lúc lập phiếu | |
| 2 | **Nội dung chuyển khoản** hiện chỉ có số phiếu. Có cần thêm **mã khách** (ví dụ `PX000318 KH0012`) không? | Chỉ số phiếu, ví dụ `PT000125`, `PX000318`. Riêng phiếu thu tự động dùng số của phiếu xuất gốc, để một khoản tiền chỉ mang một mã | **Không cần**: không đổi gì. **Cần thêm mã khách**: phải sửa phần mềm (bản in và màn hình cùng đổi). Nội dung tối đa **25 ký tự** kể cả dấu cách, nên phải chốt trước: mã khách dài quá thì cắt bớt hay bỏ. Số phiếu luôn được giữ nguyên | |
| 3 | Phiếu xuất hình thức thanh toán **chuyển khoản** mà ghi **một phần đã trả** (`Đã trả` nhỏ hơn `Tổng`): phần đó là tiền **đã nhận** (cọc) hay tiền **sẽ chuyển**? | QR đòi **cả tổng tiền**. Chỉ phần đã trả vào quỹ **tiền mặt** mới được trừ. Với phiếu thanh toán chuyển khoản mặc định (`Đã trả` bằng `Tổng`, khách chưa chuyển) thì QR đúng | **Sẽ chuyển**: không đổi gì. **Đã nhận (cọc)**: QR của phiếu có `0 < Đã trả < Tổng` đổi thành `Tổng − Đã trả`. Phiếu `Đã trả = Tổng` vẫn đòi `Tổng`, vì đó là trường hợp mặc định. Lưu ý: dữ liệu không phân biệt "đã nhận" với "sẽ chuyển", nên nếu hai loại cùng xảy ra thì phải thống nhất một cách nhập (ví dụ chỉ ghi tiền cọc ở ô đã trả) trước khi bật quy tắc này | |

Cột "Trả lời" ghi lựa chọn và ngày. Câu 1 và 3 có thể dẫn tới việc sửa phần mềm; câu 2 cũng vậy nếu cần thêm mã khách. Nếu cả ba câu trả lời là "không đổi gì" thì có thể dùng ngay sau khi quét thử.

## 2. Thiết lập trước khi dùng

### 2.1. Quỹ ngân hàng

- Quản trị viên vào màn **Quỹ**, mở từng quỹ có loại **Ngân hàng** và khai đủ ba thứ:
  - **Ngân hàng (cho QR)**: chọn từ danh sách, hiện tên viết tắt và mã BIN.
  - **Số tài khoản**: chỉ gồm chữ và số, **tối đa 19 ký tự** (giới hạn của VietQR). Không có dấu cách, dấu gạch.
  - **Chủ tài khoản**: tên đầy đủ. Mã QR dùng bản bỏ dấu và cắt tối đa 25 ký tự; hộp thoại và bản in vẫn hiện tên đầy đủ.
- Quỹ thiếu một trong ba thứ trên thì không có QR. Thông báo trên màn hình: "Quỹ … chưa khai ngân hàng, số tài khoản hoặc chủ tài khoản."
- Ô "Nơi mở tài khoản" (tên ngân hàng gõ tay) cũ vẫn giữ nguyên và vẫn in trên sổ S04. Phần mềm không tự ghép nó sang danh sách ngân hàng, nên các quỹ cũ phải khai lại.
- Quỹ khai xong rồi mà ngân hàng bị ngừng sử dụng thì xem rủi ro ở phần 4.

| Quỹ ngân hàng | Chi nhánh | Đã khai ngân hàng | Đã khai số tài khoản | Đã khai chủ tài khoản | Đã đối chiếu với giấy báo của ngân hàng |
|---|---|---|---|---|---|
| | | | | | |
| | | | | | |
| | | | | | |

### 2.2. Quỹ ngân hàng mặc định của từng chi nhánh

- Mỗi chi nhánh cần **một quỹ ngân hàng mặc định** (đang hoạt động, đủ thông tin QR). Phiếu xuất và phiếu thu ghi quỹ tiền mặt dùng quỹ này để nhận tiền.
- Chi nhánh chưa có quỹ ngân hàng mặc định đủ thông tin thì các phiếu đó báo "Chi nhánh chưa có quỹ ngân hàng mặc định đủ thông tin QR" và in không có QR.
- Phần mềm không chuyển sang quỹ ngân hàng khác nếu quỹ ghi trên phiếu (là quỹ ngân hàng) thiếu thông tin: sẽ không có QR, để tiền không về tài khoản khác với tài khoản ghi trên phiếu.

| Chi nhánh | Quỹ ngân hàng mặc định | Đủ thông tin QR |
|---|---|---|
| | | |
| | | |

### 2.3. Ai thấy nút "QR chuyển khoản"

| Việc | Quyền cần có |
|---|---|
| Xem QR của phiếu thu (và in phiếu thu kèm QR) | Xem phiếu thu (`receipts.view`) |
| Xem QR của phiếu xuất kho (và in phiếu xuất kèm QR) | Xem phiếu xuất kho (`stock_out.view`) |
| Thấy nút "Mở màn Quỹ" trong thông báo lỗi cấu hình quỹ | Quản lý danh mục quỹ (`cash.catalogs.manage`) |

- Không cần thêm quyền riêng cho QR: ai xem được phiếu thì xem được QR.
- Chỉ xem được QR của phiếu thuộc chi nhánh đang làm việc.
- Nút "QR chuyển khoản" chỉ bật khi phiếu đã lưu và không có thay đổi chưa lưu. Phiếu đã hủy báo "Phiếu đã hủy."

### 2.4. Ô "Kèm QR chuyển khoản" khi in

- Phiếu thu: mục **"Kèm QR chuyển khoản"** trong menu In. Tick xong menu vẫn mở để chọn tiếp PDF hoặc Excel.
- Phiếu xuất: ô **"Kèm QR chuyển khoản"** trong hộp thoại chọn giá. Ô này chỉ dùng được ở chế độ **"Giá trên phiếu"**; chế độ "Giá vốn" và "Không in tiền" không có QR.
- Mặc định **bật**. Lựa chọn được nhớ **theo từng người dùng** trên từng trình duyệt.
- In nhiều phiếu một lần: mỗi phiếu được xét riêng, phiếu không đủ điều kiện vẫn in, chỉ không có QR.
- Nên tắt ô này khi in phiếu mà khách đã trả xong (xem rủi ro ở phần 4).

## 3. Quét thử

### Cách quét thử

- Dùng **dữ liệu thật** của một quỹ ngân hàng đã khai ở phần 2 và một phiếu có số tiền nhỏ (ví dụ 10.000 đồng) để có thể chuyển thật nếu cần.
- Quét bằng **ít nhất 3 app ngân hàng khác nhau** cho từng dòng của bảng dưới đây. Ghi tên các app.
- Với mỗi lần quét, **trước khi bấm xác nhận chuyển** kiểm tra app hiện đúng: **số tiền**, **số tài khoản**, **tên chủ tài khoản** và **nội dung**. Không cần chuyển thật.
- PDF của 01-TT phải đi qua **LibreOffice** (nút "In (PDF)" của phần mềm đã làm sẵn); in ra giấy thật rồi mới quét, không quét từ màn hình hiện file PDF.
- Ghi "Đạt" vào ô, hoặc ghi lỗi (app nào, thấy gì).

### Bảng quét

| # | Nguồn QR | Việc làm | App 1 | App 2 | App 3 | Số tiền, số TK, chủ TK, nội dung đúng |
|---|---|---|---|---|---|---|
| 1 | Giấy in **01-TT A5** (PDF qua LibreOffice) | In một phiếu thu có QR ra giấy A5 ngang và quét | | | | |
| 2 | Giấy in **02-VT A4**, phiếu bán hàng thanh toán chuyển khoản | In một phiếu xuất ra giấy A4 và quét | | | | |
| 3 | Giấy in **02-VT A4**, phiếu bán hàng trả một phần bằng tiền mặt | Số tiền trong QR phải bằng "Cần chuyển khoản" in trong khung | | | | |
| 4 | **Màn hình** phiếu thu | Mở hộp thoại "QR chuyển khoản" trên máy tính và quét bằng điện thoại | | | | |
| 5 | **Màn hình** phiếu xuất | Như trên | | | | |
| 6 | **Màn hình điện thoại** (tấm trượt từ dưới lên) | Mở trên điện thoại này, chụp ảnh màn hình, quét từ ảnh bằng một điện thoại khác | | | | |
| 7 | Phiếu thu có tên chủ tài khoản **có dấu hoặc dài hơn 25 ký tự** | Quét giấy in: app hiện tên không dấu đã cắt, giấy in và hộp thoại hiện tên đầy đủ | | | | |
| 8 | In **hàng loạt** (3 phiếu, ít nhất 1 phiếu không đủ điều kiện) | Phiếu đủ điều kiện quét được, phiếu kia in không có QR | | | | |

### Tải ảnh, sao chép, chia sẻ

| # | Việc làm | Kết quả mong đợi | Đạt | Ghi chú (thiết bị, trình duyệt) |
|---|---|---|---|---|
| 9 | **iPhone** chạy phần mềm ở dạng **app cài đặt** (thêm vào màn hình chính): bấm "Tải ảnh QR" | Hiện bảng chia sẻ. Chọn "Lưu ảnh" thì ảnh vào Ảnh; chọn gửi Zalo thì ảnh gửi được. Ảnh tên `QR_{số phiếu}.png`, có QR, số tiền, nội dung, dòng ngân hàng | | |
| 10 | **Máy tính**: bấm "Tải ảnh QR" | Tải được file `QR_{số phiếu}.png`; mở ra quét được | | |
| 11 | Trên máy tính dùng **Chrome, Edge và Safari** (nếu có): bấm "Tải ảnh QR" | Ghi lại những gì thấy. Các trình duyệt này có thể mở **bảng chia sẻ của hệ điều hành** thay vì tải file (vì chúng hỗ trợ chia sẻ file). PO quyết định máy tính có nên **luôn tải file** không | | |
| 12 | Bấm **sao chép** số tài khoản và nội dung, rồi **sao chép thông tin** (qua **HTTPS**, tức địa chỉ thật chứ không phải `http://` nội bộ) | Dán ra được đúng chữ. Nếu trình duyệt không cho sao chép thì có thông báo lỗi, không im lặng | | |
| 13 | Điện thoại Android, mở dạng app cài đặt hoặc trình duyệt: "Tải ảnh QR" | Lưu được ảnh hoặc hiện bảng chia sẻ | | |

Quyết định cho dòng 11: ☐ giữ nguyên (máy tính có thể hiện bảng chia sẻ) · ☐ máy tính luôn tải file. Người quyết định, ngày: ______

### Chữ nhỏ trên giấy

QR in rộng khoảng **25 mm**, mỗi ô vuông của QR chỉ khoảng 0,5 mm (kết quả đo ở phần 5). Kiểm thử tự động chỉ chứng minh được file ảnh QR đúng; việc quét được từ **giấy in thật** (dòng 1 đến 3) mới chứng minh được cỡ này đủ lớn với máy in của đơn vị và camera điện thoại. Nếu một app không quét được:

- Ghi lại: app nào, khoảng cách quét, máy in nào, giấy in sắc nét hay mờ.
- Thử in lại ở chất lượng cao hơn. Nếu vẫn không quét được thì báo cho nhóm phát triển, vì lúc đó phải chọn: tăng cỡ QR (tốn thêm chỗ trên giấy) hoặc đổi cách vẽ QR. Không tự sửa.

## 4. Rủi ro cần biết

| # | Rủi ro | Chuyện gì xảy ra | Cách giảm | Đã nắm (tick) |
|---|---|---|---|---|
| 1 | Phiếu xuất **trả đủ bằng chuyển khoản** và **phiếu thu tự động** của nó | Hai tờ giấy có **hai QR giống hệt nhau** (cùng tài khoản, số tiền, nội dung `PX…`). Khách có thể quét **hai lần** và chuyển hai lần | Khi in phiếu thu tự động, **tắt "Kèm QR chuyển khoản"**. Nhắc nhân viên chỉ đưa khách một QR | |
| 2 | Phiếu thu tự động **ghi quỹ tiền mặt** (phiếu xuất trả tiền mặt) | **Không có QR**, vì khách đã đưa tiền mặt, QR sẽ đòi lại khoản đã thu. Hộp thoại báo "Phiếu thu tự động thu tiền mặt, không có QR chuyển khoản." Phần còn nợ của phiếu xuất trả một phần vẫn được đòi bằng QR của chính phiếu xuất | Không cần làm gì; nhân viên cần biết để không đi tìm QR | |
| 3 | **Phiếu thu ghi quỹ tiền mặt** nhưng vẫn có QR | Khách chuyển khoản thì tiền về ngân hàng, còn phiếu ghi tiền mặt (câu hỏi 1) | Nhân viên xem dòng **"Quỹ nhận"** trong hộp thoại trước khi đưa QR; tắt "Kèm QR" khi không muốn dùng QR | |
| 4 | **Sửa phiếu sau khi in** | QR trên bản in cũ **không đổi theo**. Phiếu sửa số tiền hoặc quỹ thì tờ giấy cũ sai | Hủy tờ cũ, in lại tờ mới. Hộp thoại trên màn hình luôn hỏi lại máy chủ mỗi lần mở nên luôn đúng với phiếu hiện tại | |
| 5 | **Phiếu đã hủy** | Không có QR: bản in không vẽ QR, hộp thoại báo "Phiếu đã hủy." | Không cần làm gì | |
| 6 | Ngân hàng bị **ngừng sử dụng** sau khi đã gắn vào quỹ | Quỹ đó **không sửa được** (kể cả đổi tên hay đổi số tài khoản) cho tới khi chọn lại một ngân hàng khác đang hoạt động, vì phần mềm từ chối lưu quỹ gắn với ngân hàng đã ngừng. Phiếu của quỹ đó không có QR trong lúc chờ. **Cần PO xác nhận đây là cách muốn làm việc**; nếu muốn cho sửa quỹ khi ngân hàng đã ngừng thì phải sửa phần mềm | Khi một ngân hàng ngừng sử dụng, kiểm tra các quỹ đang gắn với nó | |
| 7 | Cỡ QR 25 mm trên giấy | Có máy in hoặc camera quét không được (xem "Chữ nhỏ trên giấy" ở phần 3) | Quét thử bằng giấy in thật, ít nhất 3 app | |
| 8 | Chưa có quỹ ngân hàng đủ thông tin | Phiếu in được nhưng **không có QR**; nút QR trên màn hình báo lỗi kèm câu giải thích | Làm đủ phần 2 trước khi dùng | |
| 9 | Chưa xác nhận thanh toán | Phần mềm **chỉ tạo QR**, không biết khách đã chuyển chưa và không tự khớp giao dịch ngân hàng với phiếu. Việc xác nhận vẫn làm tay | Dùng nội dung chuyển khoản (số phiếu) để đối chiếu sao kê. Việc khớp tự động là phần riêng, sau này | |

## 5. Kết quả đo thực tế

Phần này do người thực hiện kế hoạch điền sau khi in thử mẫu thật (Task 3.6), không phải kế toán.

| Mục | Kết quả |
|---|---|
| Bề rộng QR in trên **01-TT A5** | **24,9 đến 25,07 mm** (đo trên PDF do LibreOffice dựng), đúng mục tiêu 25 mm |
| Bề rộng QR in trên **02-VT A4** | **24,9 đến 25,07 mm** |
| Khổ trang PDF của **01-TT** (kể cả khi có QR) | A5 ngang, **595,3 × 419,6 pt**. Có QR vẫn là một trang |
| Khổ trang PDF của **02-VT** (có và không có cột Kho) | A4 dọc, **595,3 × 841,9 pt**. Chưa ghi riêng hai trường hợp có và không có cột Kho. Phiếu 40 dòng hàng in ra 2 trang, khối "Thông tin chuyển khoản" và chữ ký cùng nằm ở trang 2 |
| Tỉ lệ co thật của A5 ngang sau khi thêm dòng cho QR | **71 đến 73 %**. Phiếu thu **không** có QR co khoảng 81 %, nên phiếu có QR in chữ nhỏ hơn khoảng 8 đến 10 điểm phần trăm. **PO quyết định**: (a) chấp nhận; (b) thu QR còn khoảng 22 mm để tờ bớt cao; (c) rút ngắn các dòng chú thích dưới QR. Phần mềm đang làm theo (a) |
| Ảnh QR nhúng trong PDF (kích thước điểm ảnh, có bị nén mờ không) | PNG **không nén mất dữ liệu**, 295 × 295 điểm ảnh, khoảng 6 điểm ảnh mỗi module (QR phiên bản 6), không bị nén mờ. OpenCV giải mã được cả **8 trang mẫu** và nội dung khớp từng ký tự với payload. Đây là đo bằng máy, chưa thay thế bảng quét bằng giấy in thật và app ngân hàng ở phần 3 |
| Bản in 01-TT có "Lý do nộp" dài vẫn nằm trên **một trang** A5 | Chưa đo riêng với lý do dài. Theo thiết kế tờ A5 luôn được co để vừa một trang (`FitToPages(1, 1)`), nên lý do dài làm tỉ lệ co thấp hơn 71 % chứ không sinh trang thứ hai; chữ nhỏ hơn nữa là điều cần nhìn khi in thử |

## 6. Xác nhận

| Vai trò | Họ tên | Ngày | Chữ ký |
|---|---|---|---|
| Kế toán trưởng | | | |
| PO | | | |
