# Báo cáo theo mẫu biểu TT133 — Quỹ, Kho, Công nợ

> Brainstorm: 2026-10-12 22:37:02 · Tiếp nối [brainstorm Đợt 2 thu/chi & công nợ](../261007-2252-cash-debt-round2/SUMMARY.md) và [plan Round 2B](../../plans/archived/261012-1709-cash-debt-round2b/SUMMARY.md) (mục "Print/Excel để Đợt 3") · Chi tiết từng mẫu: [section-01-form-specs.md](section-01-form-specs.md) · Review BA/SA: 2026-10-12 23:03:28, duyệt có điều kiện, đã sửa theo [section-02](section-02-review-decisions.md)

## Problem framing

DNNVV tại Việt Nam chủ yếu áp dụng chế độ kế toán theo **Thông tư 133/2016/TT-BTC**. Từ 01/01/2026, **Thông tư 99/2025/TT-BTC** (thay TT200) có hiệu lực. DNNVV được chọn TT133 hoặc TT99, phải áp dụng nhất quán trong một năm tài chính, và nếu chuyển thì chuyển từ đầu năm và báo cơ quan thuế. TT99 không thay thế TT133. Thiết kế này theo **TT133**.

Phần mềm đã có 10 báo cáo kho, quỹ, công nợ trên màn hình (Tồn kho, Thẻ kho, Sổ quỹ, Số dư công nợ, Sổ chi tiết công nợ, Công nợ theo chứng từ, Tuổi nợ, Nợ quá hạn, Công nợ theo NV, Biên bản đối chiếu). **Chưa có báo cáo nào xuất được Excel/PDF hay in theo mẫu.** Phần mềm cũng thiếu ba thứ mà mẫu TT133 cần:

- **Tài khoản kế toán:** cột "TK đối ứng" có trong S04b, S05, S06, S12, nhưng hệ thống tài khoản là non-goal của dự án.
- **Thông tin đơn vị:** tên, địa chỉ, MST và người ký (kế toán trưởng, người đại diện theo pháp luật). Domain không có entity công ty.
- **Bảng tổng hợp nhập–xuất–tồn theo kỳ (S07):** hiện chỉ có Tồn kho tại một thời điểm.

TT133 (Phụ lục 4) quy định rõ: *"Số lượng, kết cấu các sổ, thẻ kế toán chi tiết không quy định bắt buộc"*. Vì vậy được phép bỏ hoặc điều chỉnh cột, miễn là giữ nội dung chính của mẫu.

### Mẫu TT133 liên quan (Phụ lục 4)

| Mẫu | Tên | Hiện trạng |
|---|---|---|
| S04a-DNN | Sổ quỹ tiền mặt | Có một phần: Sổ quỹ lọc Cash |
| S04b-DNN | Sổ kế toán chi tiết quỹ tiền mặt (S04a + TK đối ứng) | Gộp vào S04a (cột TK thay bằng Lý do) |
| S05-DNN | Sổ tiền gửi ngân hàng | Có một phần: Sổ quỹ lọc Bank, quỹ đã có BankName/AccountNumber |
| S06-DNN | Sổ chi tiết vật liệu, dụng cụ, sản phẩm, hàng hóa | Có một phần: Thẻ kho đã có giá trị |
| S07-DNN | Bảng tổng hợp chi tiết vật liệu, dụng cụ, sản phẩm, hàng hóa | **Chưa có** |
| S08-DNN | Thẻ kho (Sổ kho) | Có |
| S12-DNN | Sổ chi tiết thanh toán với người mua (người bán), TK 131/331 | Có một phần: Sổ chi tiết công nợ |
| — | Bảng tổng hợp chi tiết thanh toán (không có mẫu bắt buộc) | Có: Số dư công nợ |
| — | Biên bản đối chiếu công nợ (chứng từ giao dịch) | Có trên màn hình (Round 2B) |

Không làm: S13-DNN / S14-DNN (ngoại tệ, là non-goal) và Sổ nhật ký thu/chi tiền S03a1/S03a2 (thuộc hình thức Nhật ký chung, cần tài khoản).

## Goals

- Xuất **Excel và PDF đúng bố cục mẫu TT133** cho 8 biểu: S04a, S05, S06, S07, S08, S12, Bảng tổng hợp công nợ, Biên bản đối chiếu công nợ. Mỗi biểu có:
  - khối đầu: đơn vị, địa chỉ, mã mẫu, dòng "Ban hành theo Thông tư 133/2016/TT-BTC";
  - tiêu đề, dòng tham số, header nhiều tầng kèm dòng ký hiệu cột;
  - dòng đầu kỳ / cộng phát sinh / cuối kỳ;
  - khối "Sổ này có … trang", ngày mở sổ và chữ ký.
- **Thông tin đơn vị** (`CompanyProfile`) có màn cài đặt. Đây là nguồn cho khối đầu và chữ ký.
- **Báo cáo mới S07**: Tổng hợp nhập–xuất–tồn theo kỳ, có cả trang màn hình và file xuất.
- S06, S08, S12 xuất được **một, nhiều, hoặc tất cả đối tượng/mặt hàng có phát sinh**, mỗi cái một sheet Excel / một trang PDF.

## Non-goals

- Hệ thống tài khoản, TK đối ứng, sổ cái, bút toán. Cột "TK đối ứng" thay bằng **Lý do**.
- Hình thức Nhật ký chung / Chứng từ ghi sổ, báo cáo tài chính (B01–B09-DNN), sổ thuế GTGT.
- Theo dõi ngoại tệ (S13, S14).
- Template Excel do admin tự sửa bố cục.
- Trang in HTML trên frontend (Ctrl+P).
- Mẫu in phiếu thu/chi, phiếu nhập/xuất: không thuộc phạm vi này.
- Hỗ trợ TT99: nếu khách chọn TT99 thì làm bộ mapper riêng sau. Builder thiết kế để thêm được.

## Constraints & assumptions

- Không đổi công thức số liệu. Mỗi biểu là một **cách trình bày** của báo cáo đang có, để số trên file luôn khớp số trên màn hình. Truy vấn mới duy nhất là S07.
- **Có mở rộng DTO** *(review B3)*:
  - `CashBookRowDto` thêm `ReasonName`;
  - `DebtLedgerRowDto` thêm `ReasonName` và `DueDate`;
  - màn Sổ quỹ và Sổ chi tiết công nợ cũng hiện các cột này.
- Clean Architecture và các pattern hiện có:
  - ClosedXML 0.104 cho Excel;
  - `LibreOfficeSpreadsheetPdfConverter` cho PDF (LibreOffice đã có trong Dockerfile);
  - phạm vi chi nhánh qua `ICurrentBranch` / `X-Branch-Id`.
- Quyền dùng đúng quyền của báo cáo gốc. Giá trị kho cần quyền xem giá vốn (`CanViewCost`).
- Kỳ báo cáo là Từ ngày–Đến ngày, có chọn nhanh tháng/quý/năm. Mẫu ghi "Năm/Tháng" nên tiêu đề in theo kỳ đã chọn.
- Đơn vị kế toán là công ty; chi nhánh không phải đơn vị kế toán độc lập. *(Review B1)*
  - S07, S12, Bảng tổng hợp công nợ và Biên bản đối chiếu có tham số **`scope = Branch | Company`**, mặc định là chi nhánh đang làm việc.
  - `Company` cộng mọi chi nhánh và cần quyền `branches.access_all`. Đối trừ vẫn chỉ trong cùng chi nhánh.
  - S04a/S05 theo từng quỹ; S06/S08 theo chi nhánh đang làm việc.
  - Dòng "Đơn vị" in `{Tên công ty}` (scope Company) hoặc `{Tên công ty} – {Tên chi nhánh}` (scope Branch).
- **`CostingScope = Warehouse` lúc go-live** *(review B2)*, để S06 theo kho có giá trị. Thiết lập có sẵn, đổi thì tự chạy lại giá vốn. Nếu để `Branch` thì S06/S07 lọc theo kho chỉ có số lượng.
- Làm **sau khi Round 2B xong phase 04–06**. Thiết kế này thay cho mục "Print/Excel" của Đợt 3 trong plan 2B.
- Phụ lục mẫu lấy từ các trang tổng hợp (Thư viện pháp luật, Hoatieu, Kế toán Lê Ánh, Easybooks). **Phải đối chiếu bản gốc Phụ lục 4 TT133** trước khi chốt bố cục in.

## Approaches considered

### Cột "TK đối ứng"

| Phương án | Ưu | Nhược | Quyết định |
|---|---|---|---|
| Gắn TK mặc định lên Lý do thu/chi, Lý do nhập/xuất; 111/112 suy từ loại quỹ, 131/331 từ phía công nợ | Gần mẫu gốc nhất | Thêm schema và cấu hình. Phiếu bán có VAT phải tách 511 + 3331. Đi dần về phần mềm kế toán. | Không chọn |
| **Bỏ cột, thay bằng Lý do** | Đơn giản, không đổi schema; TT133 cho phép điều chỉnh sổ chi tiết | Không đúng 100% mẫu gốc | **Chọn** |
| Giữ cột, để trống cho kế toán ghi tay | Giữ hình thức | Cột vô nghĩa trên file điện tử | Không chọn |

### Thông tin đơn vị

| Phương án | Quyết định |
|---|---|
| **Cài đặt đơn vị (một dòng) + chi nhánh** | **Chọn**: một nguồn, sửa trên màn hình |
| Chỉ dùng thông tin chi nhánh (thêm MST/người ký vào Branch) | Không: chi nhánh không phải đơn vị kế toán |
| Ghi cứng trong file template | Không: đi kèm phương án template |
| Chỉ in đơn vị, chữ ký để trống | Không: thiếu tên kế toán trưởng / người đại diện |

### Cách dựng file Excel/PDF

| Tiêu chí | **A. ClosedXML dựng bằng code** | B. Template .xlsx từng mẫu | C. In HTML + Excel backend |
|---|---|---|---|
| Đúng mẫu khi in | Tốt, page setup làm một lần trong builder | Tốt nhất | PDF phụ thuộc trình duyệt, chữ ký cuối trang khó cố định |
| Số dòng thay đổi, nhiều sheet | Tự nhiên | Khó: chèn dòng làm lệch merge/công thức | Dễ trên HTML nhưng vẫn phải làm Excel |
| Khối lượng | Builder khoảng 300–400 dòng, mỗi mẫu khoảng 50–100 dòng | 8 template + 8 renderer theo tọa độ ô | Gấp đôi |
| Nhất quán giữa các mẫu | Cao | Thấp | Thấp |
| Admin tự sửa bố cục | Không | Có | Không |
| Kiểm thử | Dễ: đọc lại workbook | Trung bình | Khó |

**Chọn A.** Các mẫu TT133 có cùng một khung, nên builder dùng chung tận dụng được. Xuất hàng loạt mỗi đối tượng một trang là chỗ B và C yếu nhất. TT133 không bắt buộc kết cấu sổ chi tiết, nên nhu cầu admin sửa bố cục thấp.

### Xuất nhiều đối tượng

**Chọn: một, nhiều hoặc tất cả có phát sinh/số dư, mỗi cái một sheet/trang**, dùng để in sổ cuối kỳ. Không chọn "chỉ từng cái một".

## Recommended approach (đã chốt)

**Backend:**
1. **`CompanyProfile`** (entity một dòng, migration mới) gồm:
   - `Name`, `Address`, `TaxCode`;
   - `ChiefAccountantName`, `LegalRepresentativeName`;
   - `AccountingServiceNote` (tùy chọn, cho dòng ghi chú dịch vụ kế toán thuê ngoài).

   API `GET/PUT api/settings/company`, quyền `settings.company` (ADMIN).
2. **`Tt133FormBuilder`** (Infrastructure/Excel) dựng các phần chung:
   - khối đầu;
   - tiêu đề và dòng tham số;
   - header cột 2–3 tầng kèm dòng ký hiệu A, B, 1, 2;
   - thân bảng;
   - dòng đầu kỳ / cộng / cuối kỳ;
   - khối "Sổ này có … trang", ngày mở sổ (`Branch.OpeningDate`), ngày lập và chữ ký;
   - page setup (A4, hướng giấy theo mẫu, lặp header, fit chiều ngang) và nhiều sheet.
3. **8 form mapper**, mỗi mẫu một class: DTO báo cáo vào, khai báo cột, gọi builder.
4. **`VndAmountInWords`**: đọc số tiền VND thành chữ, cho biên bản đối chiếu.
5. **Truy vấn `stock-summary` (S07)** từ stock ledger: đầu kỳ / nhập / xuất / cuối kỳ, có SL và giá trị, theo mặt hàng (lọc kho, nhóm hàng, `scope`).
6. **Tham số `scope = Branch | Company`** *(review B1)* cho `debt-balance`, `debt-ledger`, `debt-reconciliation` và `stock-summary`. Company cần quyền `branches.access_all`.
7. **Endpoint** `GET api/reports/{report}/export?format=xlsx|pdf&…`:
   - tham số giống báo cáo màn hình;
   - thêm `partnerIds` / `productIds` / `all=true` cho S06, S08, S12;
   - PDF qua LibreOffice; đổi tên interface converter khỏi chữ "Quotation" vì giờ dùng chung.

**Frontend:**
- Nút **"Xuất theo mẫu TT133 ▾ (Excel / PDF)"** trên các trang báo cáo.
- Hộp chọn đối tượng/mặt hàng (một / nhiều / tất cả có phát sinh).
- Trang mới **Tổng hợp nhập–xuất–tồn**.
- Màn **Thông tin đơn vị**.

**Quy tắc Nợ/Có** (S12, Bảng tổng hợp):
- Phải thu: tăng = Nợ, giảm = Có.
- Phải trả: tăng = Có, giảm = Nợ.
- Số dư âm so với phía thì sang cột đối diện. Ví dụ: khách trả trước thì ghi vào Dư Có.

**Quyền và lỗi:**
- Quyền giống báo cáo gốc.
- S06 cần quyền giá vốn: không có quyền thì nút bị ẩn và API trả 403. S07 không có quyền giá vốn thì chỉ có cột SL.
- Chưa có thông tin đơn vị: vẫn xuất, khối đầu để trống, frontend cảnh báo kèm link sang Cài đặt.
- Xuất hàng loạt không có đối tượng nào: trả 422.
- Vượt ngưỡng sheet (mặc định 300, cấu hình qua options): trả 422.
- Giá vốn tạm tính: thêm ghi chú dưới tiêu đề.
- PDF lỗi: trả 503 "Xuất PDF thất bại, hãy dùng Excel".

**Kiểm thử:**
- Builder và mapper: đọc lại workbook để kiểm ô, merge, tổng, chữ ký, page setup, số sheet.
- Quy tắc Nợ/Có: cả hai phía, kể cả dư âm.
- S07 integration trên `qldonhang_integtest`: đầu + nhập − xuất = cuối; cuối kỳ khớp `stock-on-hand`; từng dòng khớp `stock-card`.
- Endpoint: quyền, content-type, 422.
- `VndAmountInWords`: linh/lẻ, mốt, lăm, triệu, tỷ.
- Frontend: test nút xuất, hộp chọn, trang S07, form đơn vị.
- **Go-live gate:** kế toán duyệt bản in mẫu của từng biểu.

**Chia phase:**

| Phase | Nội dung |
|---|---|
| P1 | `CompanyProfile` + migration + màn Thông tin đơn vị · `VndAmountInWords` · đổi tên interface PDF converter |
| P2 | `Tt133FormBuilder` + hạ tầng endpoint export · `CashBookRowDto.ReasonName` · S04a, S05, kèm nút xuất trên Sổ quỹ |
| P3 | `scope` cho các báo cáo công nợ + DTO thêm `ReasonName`/`DueDate` · S12, Bảng tổng hợp công nợ, Biên bản đối chiếu, kèm nút xuất và hộp chọn đối tượng |
| P4 | Truy vấn + trang S07 (có `scope`) · S06, S08, kèm nút xuất trên Thẻ kho |
| P5 | Docs (SUMMARY, conventions, product-goals, directory-structure) và kiểm tra cuối |

## Open questions

**Kế toán / PO:**
- Doanh nghiệp đang áp dụng TT133 hay sẽ chuyển sang TT99 từ năm tài chính 2027? Nếu là TT99 thì mã mẫu và một số cột khác, cần thêm bộ mapper.
- Có chấp nhận thay "TK đối ứng" bằng "Lý do", và "Thời hạn được chiết khấu" (S12) bằng "Hạn thanh toán" không?
- Biên bản đối chiếu: đơn vị có mẫu riêng đang dùng không (câu "căn cứ", điều khoản phản hồi trong N ngày)? Nếu có thì gửi mẫu để làm theo.
- Xác nhận chi nhánh **không** có MST riêng, theo giả định của review B1. Nếu có thì `CompanyProfile` phải theo chi nhánh và bỏ scope Company.
- Xác nhận đổi `CostingScope` sang **Warehouse** (review B2): giá vốn bình quân theo từng kho.

**Kỹ thuật (đã đo ở Phase 05 của [plan](../../plans/archived/261013-1926-tt133-report-forms/SUMMARY.md), 2026-10-14):**
- Ngưỡng sheet giữ **300** (`Tt133Export:MaxSheets`); timeout chuyển PDF tăng từ 60 lên **130 giây** (`QuotationExport:ConversionTimeoutSeconds`).
- Số đo với 300 sheet, mỗi sheet 20 dòng. Đây là dữ liệu sinh ra để đo, không phải dữ liệu thật; chứng từ nguồn không tồn tại nên bước tra "Lý do" có chạy nhưng không tìm thấy gì.

  | Mẫu | Xuất Excel | Chuyển PDF (image Docker, LibreOffice 7.4) | Tổng |
  |---|---|---|---|
  | S12 | 2,57 giây | 28,47 giây | 31,0 giây |
  | S08 | 3,37 giây | 19,48 giây | 22,9 giây |
  | S06 | 3,13 giây | 61,49 giây | 64,6 giây |

  Giới hạn đặt ra: Excel dưới 20 giây, Excel + PDF dưới 90 giây (client chờ 120 giây). S06 chậm nhất; timeout 130 giây lớn hơn hai lần thời gian chuyển PDF của nó (123 giây).
- R2 đã xử lý (quyết định của người dùng ngày 14/10/2026): chạy 3 lần chuyển PDF cùng lúc bằng `soffice` trực tiếp trong image thì 7/27 lần lỗi, không lần nào treo. Converter nay cấp một thư mục profile riêng cho mỗi lần chuyển (`-env:UserInstallation`); chạy 9 lần chuyển song song qua converter với LibreOffice thật thì 9/9 thành công. Không dùng semaphore.

## Rủi ro

Ghi nhận từ review; không đổi thiết kế. Chi tiết ở [section-02](section-02-review-decisions.md#rủi-ro-ghi-nhận).

- **R1:** xuất hàng loạt gọi truy vấn một lần cho mỗi đối tượng (N+1), cộng PDF đồng bộ, nên có thể vượt timeout của proxy. Plan phải đo trên dữ liệu thật.
- **R2:** nhiều lần chuyển PDF bằng LibreOffice chạy cùng lúc dùng chung user profile, có thể lỗi hoặc treo. Plan cần test chạy song song trên Docker.

## Supporting files

- [section-01-form-specs.md](section-01-form-specs.md): khung chung, cột ↔ nguồn dữ liệu của từng mẫu, quy tắc Nợ/Có, bất biến của S07, test case đọc số thành chữ
- [section-02-review-decisions.md](section-02-review-decisions.md): kết quả review BA/SA, các quyết định B1–B3, đề xuất không chấp nhận, rủi ro

## Next steps

1. ~~Hoàn tất Round 2B (phase 04–06).~~ Xong.
2. Gửi kế toán các câu hỏi Kế toán/PO ở trên và bản mô tả cột trong [section-01](section-01-form-specs.md). Đối chiếu bố cục với bản gốc Phụ lục 4 TT133.
3. ~~`write-plan` cho 5 phase ở trên.~~ Xong: [plan TT133 report forms](../../plans/archived/261013-1926-tt133-report-forms/SUMMARY.md) (Phase 00–05).
4. ~~Cập nhật `docs/project-pdr/product-goals.md` khi plan được duyệt.~~ Xong trong Phase 05 của [plan](../../plans/archived/261013-1926-tt133-report-forms/SUMMARY.md).

## Nguồn tham khảo

- [Từ 01/01/2026 có thể áp dụng chế độ kế toán doanh nghiệp cho DNNVV — LuatVietnam](https://luatvietnam.vn/tin-van-ban-moi/tu-01-01-2026-co-the-ap-dung-che-do-ke-toan-doanh-nghiep-cho-doanh-nghiep-vua-va-nho-186-105084-article.html)
- [So sánh chế độ kế toán TT133 và TT99 — Gonnapass](https://gonnapass.com/sanh-che-ke-toan-thong-tu-133-va-thong-tu-99/)
- [Hệ thống danh mục các mẫu sổ kế toán theo TT133 — Kế toán Việt Hưng](https://ketoanviethung.vn/he-thong-danh-muc-cac-mau-so-ke-toan-theo-tt-133.html)
- [Sổ quỹ tiền mặt theo TT133 — Easybooks](https://easybooks.vn/so-quy-tien-mat-theo-thong-tu-133/)
- [Mẫu S07-DNN Bảng tổng hợp chi tiết VL, DC, SP, HH — Hoatieu](https://hoatieu.vn/bieu-mau/mau-so-s08-dnn-bang-tong-hop-chi-tiet-vat-lieu-dung-cu-san-pham-hang-hoa-66445)
- [Mẫu S12-DNN Sổ chi tiết thanh toán với người mua (người bán) — Hoatieu](https://hoatieu.vn/bieu-mau/mau-so-s13-dnn-so-chi-tiet-thanh-toan-voi-nguoi-mua-nguoi-ban-66449)
- [Cách ghi mẫu S12-DNN — Thư viện pháp luật](https://thuvienphapluat.vn/hoi-dap-phap-luat/83A15D4-hd-cach-ghi-mau-s12dnn-so-chi-tiet-thanh-toan-voi-nguoi-mua-nguoi-ban-theo-thong-tu-133.html)
