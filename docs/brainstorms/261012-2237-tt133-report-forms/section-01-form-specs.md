# Section 01 — Đặc tả từng mẫu

> Thuộc [SUMMARY](SUMMARY.md). Bố cục dựa trên Phụ lục 4 TT133 qua các trang tổng hợp. **Phải đối chiếu bản gốc trước khi chốt.** Cột "TK đối ứng" luôn thay bằng **Lý do** (tên `CashReason` / `StockReason`; chuyển quỹ ghi "Chuyển quỹ").

## Khung chung (`Tt133FormBuilder`)

Khối đầu:
- Trái: **Đơn vị:** `{CompanyProfile.Name} – {Branch.Name}` (scope Branch) hoặc `{CompanyProfile.Name}` (scope Company, xem [review B1](section-02-review-decisions.md#b1--phạm-vi-sổ-chi-nhánh-hay-toàn-công-ty)); **Địa chỉ:** `Branch.Address` với scope Branch, nếu trống hoặc scope Company thì lấy `CompanyProfile.Address`; **MST:** `CompanyProfile.TaxCode`.
- Phải: **Mẫu số Sxx-DNN**, dòng dưới *(Ban hành theo Thông tư số 133/2016/TT-BTC ngày 26/8/2016 của Bộ Tài chính)*. Biểu không có mã mẫu thì bỏ khối này.

Tiêu đề: tên sổ in hoa, đậm, căn giữa. Dòng kỳ: "Từ ngày dd/MM/yyyy đến ngày dd/MM/yyyy", hoặc "Tháng MM năm yyyy" / "Năm yyyy" nếu kỳ đúng tháng/năm. Tiếp theo là các dòng tham số riêng của mẫu.

Bảng:
- Header 2–3 tầng có merge.
- Một dòng ký hiệu cột (A, B, C…, 1, 2, 3…), in nghiêng, căn giữa.
- Dòng **Số dư/tồn đầu kỳ**, các dòng phát sinh, dòng **Cộng số phát sinh trong kỳ**, dòng **Số dư/tồn cuối kỳ**. Các dòng tổng in đậm.

Định dạng:
- Số: `#,##0`, số âm trong ngoặc. Số lượng: `#,##0.###`.
- Ngày: `dd/MM/yyyy`.
- Font Times New Roman 11 (tiêu đề 14).

Cuối trang:
- "Sổ này có … trang, đánh số từ trang 01 đến trang …" và "Ngày mở sổ: `Branch.OpeningDate`".
- "Ngày … tháng … năm …" là ngày xuất file.
- 3 chữ ký:
  - **Người ghi sổ:** họ tên user đang xuất;
  - **Kế toán trưởng:** `ChiefAccountantName`;
  - **Người đại diện theo pháp luật:** `LegalRepresentativeName` (ký, họ tên, đóng dấu).
- Có `AccountingServiceNote` thì thêm dòng ghi chú dịch vụ kế toán.

Page setup:
- A4; S06, S07, S12 và Bảng tổng hợp in **ngang**, các mẫu khác in **dọc**.
- Lặp các dòng header ở mỗi trang, fit 1 trang theo chiều ngang, lề 1,5 cm.

Xuất nhiều đối tượng: mỗi đối tượng một sheet, tên sheet là mã đối tượng/mặt hàng (cắt 31 ký tự, chống trùng). PDF nối các sheet theo thứ tự.

## S04a-DNN — Sổ quỹ tiền mặt

- **Nguồn:** `cash-book` với `kind=Cash`. Mỗi `Fund` là một sheet.
- **Tham số in:** Loại quỹ = `Fund.Name`.

| Cột | Tên | Nguồn |
|---|---|---|
| A | Ngày, tháng ghi sổ | `at` |
| B | Ngày, tháng chứng từ | `at`: phiếu thu/chi không tách ngày ghi sổ với ngày chứng từ, nên A = B (áp dụng cả S05) |
| C | Số hiệu CT — Thu | số phiếu thu, hoặc số chứng từ chuyển quỹ khi quỹ nhận tiền |
| D | Số hiệu CT — Chi | số phiếu chi, hoặc số chứng từ chuyển quỹ khi quỹ chuyển đi |
| E | Diễn giải | diễn giải + đối tượng/người nộp-nhận |
| F | Lý do | `ReasonName` (trường mới của `CashBookRowDto`, review B3); chuyển quỹ ghi "Chuyển quỹ" |
| 1 | Số tiền — Thu | in |
| 2 | Số tiền — Chi | out |
| 3 | Số tiền — Tồn | running |
| G | Ghi chú | để trống |

Dòng đầu: "Số tồn đầu kỳ" (opening). Dòng cuối: "Cộng số phát sinh trong kỳ" (Σ thu, Σ chi) và "Số tồn cuối kỳ" (closing).

## S05-DNN — Sổ tiền gửi ngân hàng

- **Nguồn:** `cash-book` với `kind=Bank`. Mỗi `Fund` ngân hàng là một sheet.
- **Tham số in:** "Nơi mở tài khoản giao dịch: `Fund.BankName`" và "Số hiệu tài khoản tại nơi gửi: `Fund.AccountNumber`".

| Cột | Tên | Nguồn |
|---|---|---|
| A | Ngày, tháng ghi sổ | `at` |
| B | Chứng từ — Số hiệu | số chứng từ |
| C | Chứng từ — Ngày, tháng | ngày chứng từ |
| D | Diễn giải | diễn giải + đối tượng |
| E | Lý do | như S04a |
| 1 | Số tiền — Thu (gửi vào) | in |
| 2 | Số tiền — Chi (rút ra) | out |
| 3 | Số tiền — Còn lại | running |
| F | Ghi chú | để trống |

Các dòng tổng giống S04a, gọi là "Số dư đầu kỳ" / "Số dư cuối kỳ".

## S06-DNN — Sổ chi tiết vật liệu, dụng cụ, sản phẩm, hàng hóa

- **Nguồn:** `stock-card` theo (`productId`, `warehouseId`, `from`, `to`). Mỗi mặt hàng × kho là một sheet.
- **Tham số in:** Kho, Tên/quy cách (mã – tên), Đơn vị tính.
- **Quyền:** bắt buộc quyền giá vốn.
- **Phạm vi tính giá** ([review B2](section-02-review-decisions.md#b2--s06-theo-kho-không-có-giá-trị-khi-tính-giá-theo-chi-nhánh)): go-live đặt `CostingScope = Warehouse`. Nếu là `Branch` thì `stock-card` theo kho trả `ValuesAtScopeOnly`: bỏ cột 1, 3, 5, 7 và ghi "Giá trị tính theo chi nhánh" dưới tiêu đề.

| Cột | Tên | Nguồn |
|---|---|---|
| A | Chứng từ — Số hiệu | source code |
| B | Chứng từ — Ngày, tháng | postedAt |
| C | Diễn giải | đối tượng + loại chứng từ |
| D | Lý do | reason |
| 1 | Đơn giá | unitCost |
| 2, 3 | Nhập — SL, Thành tiền | qtyIn, inValue |
| 4, 5 | Xuất — SL, Thành tiền | qtyOut, costAmount |
| 6, 7 | Tồn — SL, Thành tiền | running qty, running value |
| 8 | Ghi chú | để trống |

Dòng "Số dư đầu kỳ" (cột 6, 7), dòng "Cộng phát sinh" (cột 2–5) và dòng "Số dư cuối kỳ".

Mục "Tất cả có phát sinh" gồm mọi mặt hàng × kho có tồn đầu kỳ ≠ 0 hoặc có phát sinh trong kỳ.

## S07-DNN — Bảng tổng hợp chi tiết vật liệu, dụng cụ, sản phẩm, hàng hóa

- **Nguồn:** truy vấn mới `GET api/reports/stock-summary`.
  - Tham số: `from`, `to`, `scope` (Branch | Company), `warehouseId?` (chỉ dùng với scope Branch), `productGroupId?`, `search?`.
  - Scope Company cộng mọi chi nhánh. Giá trị mỗi chi nhánh đã đúng theo phạm vi tính giá, nên cộng được.
  - Lọc kho khi `CostingScope = Branch` thì chỉ trả số lượng.
  - Mỗi dòng là một mặt hàng (gộp các kho nếu không lọc kho): mã, tên, ĐVT, nhóm; SL và TT cho đầu kỳ / nhập / xuất / cuối kỳ.
  - Các tổng: `IsProvisional`, `CanViewCost`.
  - Chỉ hàng `TrackInventory = true`. Bỏ dòng có cả 8 số bằng 0.
- **Có trang màn hình** `/inventory/stock-summary`, theo pattern trang Tồn kho.
- **Tham số in:** Kho (hoặc "Tất cả kho"), Nhóm hàng (nếu lọc).

| Cột | Tên |
|---|---|
| A | STT |
| B | Mã hàng |
| C | Tên, quy cách |
| D | ĐVT |
| 1, 2 | Tồn đầu kỳ — SL, TT |
| 3, 4 | Nhập trong kỳ — SL, TT |
| 5, 6 | Xuất trong kỳ — SL, TT |
| 7, 8 | Tồn cuối kỳ — SL, TT |

Dòng "Cộng" chỉ cộng các cột TT. Mẫu gốc chỉ có cột Số tiền; thêm SL vì TT133 cho phép. Không có quyền giá vốn thì bỏ các cột TT (màn hình và file).

**Bất biến cần test:**
- đầu kỳ + nhập − xuất = cuối kỳ, cả SL và TT;
- cuối kỳ khớp `stock-on-hand` tại ngày `to`;
- từng mặt hàng × kho khớp tổng của `stock-card`.

## S08-DNN — Thẻ kho (Sổ kho)

- **Nguồn:** `stock-card`, chỉ dùng số lượng. Không cần quyền giá vốn. Mỗi mặt hàng × kho là một sheet.
- **Tham số in:** Ngày lập thẻ (ngày xuất), Tờ số (số thứ tự sheet), Tên/nhãn hiệu/quy cách, ĐVT, Mã số, Kho.

| Cột | Tên | Nguồn |
|---|---|---|
| A | STT | |
| B | Ngày, tháng | postedAt |
| C | Số hiệu CT — Nhập | source code nếu là nhập |
| D | Số hiệu CT — Xuất | source code nếu là xuất |
| E | Diễn giải | đối tượng + lý do |
| F | Ngày nhập, xuất | postedAt (ngày) |
| 1 | Số lượng — Nhập | qtyIn |
| 2 | Số lượng — Xuất | qtyOut |
| 3 | Số lượng — Tồn | running qty |
| G | Ký xác nhận của kế toán | để trống |

Có dòng "Tồn đầu kỳ", "Cộng cuối kỳ" và "Tồn cuối kỳ".

## S12-DNN — Sổ chi tiết thanh toán với người mua (người bán)

- **Nguồn:** `debt-ledger` theo (`side`, `partnerId`, `from`, `to`, `scope`). Mỗi đối tượng × phía là một sheet.
- Scope Company trộn các dòng của mọi chi nhánh theo `postedAt` và tính lại running.
- **Tham số in:**
  - "Loại công nợ: Phải thu khách hàng" hoặc "Phải trả người bán" (thay cho "Tài khoản: 131/331");
  - "Đối tượng: mã – tên";
  - "Loại tiền: VNĐ".

| Cột | Tên | Nguồn |
|---|---|---|
| A | Ngày, tháng ghi sổ | postedAt |
| B | Chứng từ — Số hiệu | docCode |
| C | Chứng từ — Ngày, tháng | docDate |
| D | Diễn giải | description |
| E | Lý do | `ReasonName` (trường mới của `DebtLedgerRowDto`, review B3) |
| 1 | Hạn thanh toán | `DueDate` (trường mới) của dòng tăng, thay "Thời hạn được chiết khấu"; dòng giảm để trống |
| 2, 3 | Số phát sinh — Nợ, Có | theo quy tắc Nợ/Có |
| 4, 5 | Số dư — Nợ, Có | running theo quy tắc Nợ/Có |

**Quy tắc Nợ/Có:**
- Phải thu: tăng → Nợ, giảm → Có. Running ≥ 0 → Dư Nợ, < 0 → Dư Có (giá trị tuyệt đối).
- Phải trả: tăng → Có, giảm → Nợ. Running ≥ 0 → Dư Có, < 0 → Dư Nợ.

Có dòng "Số dư đầu kỳ", "Cộng số phát sinh" (Σ Nợ, Σ Có) và "Số dư cuối kỳ".

Mục "Tất cả có phát sinh" gồm mọi đối tượng có số dư đầu kỳ ≠ 0 hoặc có phát sinh trong kỳ ở phía đã chọn.

## Bảng tổng hợp chi tiết thanh toán với người mua / người bán

- **Nguồn:** `debt-balance` theo (`side`, `from`, `to`, `search?`, `scope`). Một bảng, in ngang. **Không có mã mẫu**, nên bỏ khối "Mẫu số".
- **Tiêu đề:** "BẢNG TỔNG HỢP CHI TIẾT THANH TOÁN VỚI NGƯỜI MUA" hoặc "… VỚI NGƯỜI BÁN".

| Cột | Tên |
|---|---|
| A | STT |
| B | Mã đối tượng |
| C | Tên đối tượng |
| 1, 2 | Số dư đầu kỳ — Nợ, Có |
| 3, 4 | Số phát sinh — Nợ, Có |
| 5, 6 | Số dư cuối kỳ — Nợ, Có |

Phát sinh Nợ/Có theo quy tắc ở S12; số dư lấy từ cột debit/credit đang có. Có dòng "Cộng".

**Bất biến cần test:** dòng Cộng khớp tổng của báo cáo Số dư công nợ; số dư cuối của từng đối tượng khớp dòng cuối kỳ S12.

## Biên bản đối chiếu công nợ

- **Nguồn:** `debt-reconciliation` theo (`partnerId`, `from`, `to`, `scope`). Một đối tượng mỗi file.
- Gửi khách thì nên dùng scope Company, để ra đủ số dư với công ty. Đây là chứng từ giao dịch, không phải sổ: không có "Mẫu số" và không có "Sổ này có … trang".
- **Bố cục:**
  1. Quốc hiệu, tiêu ngữ; tiêu đề "BIÊN BẢN ĐỐI CHIẾU CÔNG NỢ"; dòng "Từ ngày … đến ngày …".
  2. "Hôm nay, ngày …, chúng tôi gồm:"
     - **Bên A:** tên đơn vị, địa chỉ, MST, đại diện `LegalRepresentativeName`.
     - **Bên B:** tên đối tượng, địa chỉ (`CompanyAddress`), MST (`TaxCode`), đại diện (để trống).
  3. Mỗi phía có số liệu (phải thu, phải trả) là một bảng: STT · Ngày CT · Số CT · Diễn giải · Phát sinh tăng · Phát sinh giảm. Có dòng "Số dư đầu kỳ", "Cộng phát sinh" và "Số dư cuối kỳ".
  4. Có cả hai phía thì thêm dòng **"Bù trừ: Bên B còn nợ (hoặc Bên A còn nợ) … đồng"** theo `NetBalance`.
  5. Kết luận: "Tính đến ngày …, Bên B còn nợ Bên A số tiền: X đồng (Bằng chữ: `VndAmountInWords(X)`)". Đảo bên khi số dư ngược chiều; bằng 0 thì "hai bên không còn công nợ".
  6. Chữ ký: **Đại diện Bên A** và **Đại diện Bên B** (ký, họ tên, đóng dấu); dưới Bên A in thêm Kế toán trưởng.
- Câu "căn cứ" và điều khoản phản hồi: chờ mẫu của kế toán (xem Open questions).

## `VndAmountInWords`

Đọc số nguyên VND không âm. Âm thì đọc giá trị tuyệt đối, bên gọi tự ghi chiều nợ. Viết hoa chữ đầu, kết thúc bằng "đồng". Quy tắc:
- "linh" cho hàng chục bằng 0 (ví dụ 105 → "một trăm linh năm");
- "mốt" (21), "lăm" (15, 25), "mười" (10–19);
- nhóm nghìn / triệu / tỷ, có lặp "tỷ" cho số ≥ 10^18;
- nhóm ba chữ số bằng 0 ở giữa thì bỏ hẳn;
- nhóm không đứng đầu mà nhỏ hơn 100 thì đọc thêm "không trăm".

Test case bắt buộc:

| Số | Cách đọc |
|---|---|
| 0 | "Không đồng" |
| 105 | "Một trăm linh năm đồng" |
| 21 | "Hai mươi mốt đồng" |
| 15 | "Mười lăm đồng" |
| 1.000.005 | "Một triệu không trăm linh năm đồng" |
| 1.005.000 | "Một triệu không trăm linh năm nghìn đồng" |
| 2.000.000.000 | "Hai tỷ đồng" |
