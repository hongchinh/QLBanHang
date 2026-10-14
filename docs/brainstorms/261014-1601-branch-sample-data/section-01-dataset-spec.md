# Nội dung bộ dữ liệu mẫu

> Thuộc [brainstorm dữ liệu mẫu cho chi nhánh mới](SUMMARY.md). Số lượng và giá dưới đây là **gợi ý** cho generator. Agent sinh dữ liệu được chỉnh, miễn là đạt đủ các đích ở mục [Kết quả mong đợi](#kết-quả-mong-đợi-tại-ngày-tạo-t).

## Mốc thời gian và tính tất định

- T là ngày Việt Nam lúc tạo chi nhánh. Generator nhận T qua clock được inject, để test chạy cố định được.
- **Ngày đầu kỳ** là ngày 1 của tháng T − 3. **Chứng từ** trải từ ngày đầu kỳ đến T − 1, chỉ vào ngày làm việc (thứ 2 đến thứ 7), giờ ngẫu nhiên trong khoảng 08:00–17:30.
- Seed random tính từ mã chi nhánh. Cùng mã và cùng T thì ra cùng kế hoạch.

## Quy ước mã

| Loại | Mã | Ghi chú |
|---|---|---|
| Nhóm hàng | `DM`, tên "Dữ liệu mẫu" | Dùng chung. Bị xóa cùng danh mục mẫu khi không còn hàng nào thuộc nhóm |
| Hàng hóa | `DM-SP01` … `DM-SP30` | `IsSample = true`, thuế suất 10% như `seed-sample-catalog.sql` |
| Khách hàng | `DM-KH01` … `DM-KH20` | `IsCustomer` |
| Nhà cung cấp | `DM-NCC01` … `DM-NCC08` | `IsSupplier` |
| Vừa khách vừa NCC | `DM-DT01`, `DM-DT02` | Dùng cho phiếu bù trừ công nợ |
| Kho | `{mã CN}-K1` "Kho chính", `{mã CN}-K2` "Kho phụ" | Mã kho duy nhất toàn DB |
| Quỹ | `QTM` (có sẵn), `VCB`, `TCB` | Quỹ ngân hàng ghi tên ngân hàng, số tài khoản giả (`9999…`) |

Đơn vị tính dùng các unit có sẵn: Tấm, m², m³, Thùng, Chuyến.

## Hàng hóa (30)

Giá bán là giá chưa VAT. Giá mua bằng khoảng 78% giá bán, mỗi lần nhập dao động ±5%.

| Mã | Tên | ĐVT | Giá bán | Kho |
|---|---|---|---:|---|
| DM-SP01 | Tấm xốp EPS 1000×2000×20mm, tỷ trọng 10 | Tấm | 28.000 | ✓ |
| DM-SP02 | Tấm xốp EPS 1000×2000×30mm, tỷ trọng 10 | Tấm | 40.000 | ✓ |
| DM-SP03 | Tấm xốp EPS 1000×2000×50mm, tỷ trọng 10 | Tấm | 65.000 | ✓ |
| DM-SP04 | Tấm xốp EPS 1000×2000×50mm, tỷ trọng 12 | Tấm | 78.000 | ✓ |
| DM-SP05 | Tấm xốp EPS 1000×2000×100mm, tỷ trọng 12 | Tấm | 150.000 | ✓ |
| DM-SP06 | Xốp EPS khối, tỷ trọng 10 | m³ | 1.350.000 | ✓ |
| DM-SP07 | Tấm xốp XPS 600×1200×25mm | Tấm | 65.000 | ✓ |
| DM-SP08 | Tấm xốp XPS 600×1200×30mm | Tấm | 78.000 | ✓ |
| DM-SP09 | Tấm xốp XPS 600×1200×50mm | Tấm | 125.000 | ✓ |
| DM-SP10 | Tấm xốp XPS 600×1200×100mm | Tấm | 245.000 | ✓ |
| DM-SP11 | PE foam 1mm | m² | 4.500 | ✓ |
| DM-SP12 | PE foam 3mm | m² | 9.000 | ✓ |
| DM-SP13 | PE foam 5mm | m² | 14.000 | ✓ |
| DM-SP14 | PE foam 10mm | m² | 26.000 | ✓ |
| DM-SP15 | PE foam tráng bạc 5mm | m² | 22.000 | ✓ |
| DM-SP16 | Cao su non 1000×2000×10mm | Tấm | 120.000 | ✓ |
| DM-SP17 | Cao su non 1000×2000×20mm | Tấm | 230.000 | ✓ |
| DM-SP18 | Cao su non 1000×2000×30mm | Tấm | 340.000 | ✓ |
| DM-SP19 | Cao su non tráng bạc 10mm | Tấm | 150.000 | ✓ |
| DM-SP20 | Thùng xốp 5kg | Thùng | 18.000 | ✓ |
| DM-SP21 | Thùng xốp 10kg | Thùng | 28.000 | ✓ |
| DM-SP22 | Thùng xốp 20kg | Thùng | 45.000 | ✓ |
| DM-SP23 | Thùng xốp 30kg | Thùng | 62.000 | ✓ |
| DM-SP24 | Bông khoáng 40kg/m³ dày 50mm | m² | 42.000 | ✓ |
| DM-SP25 | Bông khoáng 60kg/m³ dày 50mm | m² | 55.000 | ✓ |
| DM-SP26 | Bông khoáng 100kg/m³ dày 50mm | m² | 85.000 | ✓ |
| DM-SP27 | Bông thủy tinh cuộn 12kg/m³ dày 50mm | m² | 22.000 | ✓ |
| DM-SP28 | Bông thủy tinh cuộn 24kg/m³ dày 50mm | m² | 32.000 | ✓ |
| DM-SP29 | Bông thủy tinh tấm 48kg/m³ dày 50mm | m² | 75.000 | ✓ |
| DM-SP30 | Vận chuyển nội thành | Chuyến | 350.000 | — (`TrackInventory = false`) |

Điền các trường `Length`, `Width`, `Thickness`, `Density` và `Specification` theo tên hàng. Hàng có giá bán theo m² hoặc m³ thì đặt `PricingMode` giống cách `seed-sample-catalog.sql` đang làm.

## Đối tượng

**Khách hàng (20)**

| Nhóm | Số lượng | Hạn nợ (`CreditDays`) | Thói quen trả tiền |
|---|---|---|---|
| Công ty (`Company`) | 6 | 30 | Phần lớn trả đúng hạn; 1 khách trả chậm 30–60 ngày |
| Đại lý (`Agent`) | 5 | 15–45 | 2 khách có **hạn mức tín dụng**; 1 khách trả chậm |
| Khách lẻ (`Retail`) | 5 | 0 | Trả ngay bằng tiền mặt; 1 khách có **khoản trả trước** |
| Dự án (`Project`) | 4 | 45 | Trả theo đợt; 1 khách **nợ khó đòi** (khoản đầu kỳ không bao giờ được trả) |

- Phân bổ doanh số: 5 khách lớn chiếm khoảng 40%, 10 khách vừa, 5 khách ít mua.
- Tên là tên giả, không trùng doanh nghiệp có thật. Hậu tố "(mẫu)" xem câu hỏi mở 1 trong SUMMARY.

**Nhà cung cấp (8):** hạn nợ 30 ngày (một NCC 15, một NCC 45). Mỗi NCC chuyên một vài nhóm hàng, ví dụ NCC xốp EPS/XPS, NCC PE foam/cao su non, NCC bông khoáng/bông thủy tinh, NCC thùng xốp.

**Vừa khách vừa NCC (2):** vừa mua thành phẩm vừa bán lại phế liệu hoặc gia công.

**Người phụ trách:** tối đa 3 user SALES đang active, chia đều theo khách. Không có user nào như vậy thì dùng người tạo chi nhánh.

## Đầu kỳ

| Mục | Nội dung |
|---|---|
| Số dư quỹ | QTM 30.000.000 · VCB 450.000.000 · TCB 150.000.000 |
| Tồn kho K1 | 26 mặt hàng. Số lượng bằng khoảng 3–6 tuần bán. Giá trị = số lượng × giá vốn đầu kỳ (khoảng 78% giá bán) |
| Tồn kho K2 | 10 mặt hàng, số lượng nhỏ hơn |
| Phải thu | 10 khoản cho 8 khách, tổng khoảng 350 triệu. `DocDate` từ 10 đến 120 ngày trước ngày đầu kỳ; `DueDate` = `DocDate` + hạn nợ. Chọn tuổi sao cho đến ngày T có khoản quá hạn ở **cả 4 nhóm** 1–30, 31–60, 61–90 và trên 90 ngày |
| Phải trả | 4 khoản cho 3 NCC, tổng khoảng 200 triệu |
| Trả trước | 1 khách trả trước 20 triệu (phải thu, loại Prepayment); 1 khoản trả trước cho NCC 30 triệu (phải trả, loại Prepayment) |

Mỗi khoản đầu kỳ gán `OwnerUserId` theo người phụ trách của khách.

## Phát sinh

Số lượng tính cho mỗi tháng. Generator mô phỏng tồn kho theo kho, số dư quỹ, công nợ mở và hạn mức trước khi sinh mỗi chứng từ.

| Chứng từ | Lý do | Số lượng | Quy tắc |
|---|---|---|---|
| Nhập mua | NMH | 10–12 | Nhập khi tồn mô phỏng xuống dưới ngưỡng. 3–8 dòng, từ NCC đúng nhóm hàng. Khoảng 80% vào K1. 50% trả ngay bằng CK, sinh phiếu chi tự động (CPN); còn lại ghi nợ |
| Xuất bán | XBH | 35–45 | 1–6 dòng; khoảng 20% có thêm dòng vận chuyển DM-SP30. Số lượng không vượt tồn mô phỏng. Đơn giá = giá bán trừ chiết khấu 0–5% theo nhóm khách. Khách lẻ trả ngay TM; khách khác 30% trả ngay (TM hoặc CK, sinh phiếu thu tự động TPX), còn lại ghi nợ. Đơn ghi nợ không làm vượt hạn mức |
| Hàng bán bị trả lại | NTL | 1–2 | Một phần của một phiếu xuất trong 30 ngày trước, cùng khách, cùng kho |
| Trả hàng NCC | XTL | 1 | Một phần của phiếu nhập gần nhất, cùng NCC |
| Xuất khác | XKH | 1 | Hàng hỏng, không có đối tượng, 1–2 dòng nhỏ |
| Thu tiền khách | TKH | theo thói quen trả tiền | Phân bổ vào chứng từ mở cũ nhất. 60% CK (VCB hoặc TCB), 40% TM. Khách nợ khó đòi không trả khoản đầu kỳ. Khách trả chậm trả sau hạn 30–60 ngày |
| Trả NCC | TNCC | theo hạn nợ | Trả gần đến hạn, phân bổ vào chứng từ cũ nhất, bằng CK |
| Chi khác | CK | 4–6 | Thuê kho 15 triệu đầu tháng (VCB), tiền điện 3–5 triệu (TM), xăng xe 2–4 lần (TM), lương 60 triệu cuối tháng (VCB) |
| Thu khác | TK | 1 | Bán phế liệu 1–3 triệu, TM |
| Chi hoàn tiền khách | CHKH | 1 lần trong cả kỳ | Hoàn một phần khoản trả trước của khách lẻ |
| Chuyển quỹ | — | hằng tuần + 1 | Hằng tuần nộp tiền mặt QTM → VCB, giữ QTM ở khoảng 20–40 triệu. Mỗi tháng chuyển VCB → TCB một lần |
| Bù trừ công nợ | — | 1 lần trong cả kỳ | Cho DM-DT01: bù trừ phải thu với phải trả |
| Phiếu bị hủy | — | 1 lần trong cả kỳ | Tạo một phiếu thu rồi hủy (`CancelAsync`), để có trạng thái "Đã hủy" khi tra cứu |
| Hạn mức tín dụng | — | 2, đặt trước chứng từ đầu tiên | Cho 2 đại lý |

Ước tính khoảng 3,5 tháng × 70 ≈ 250 chứng từ, cộng khoảng 40 phiếu thu/chi tự động.

## Kết quả mong đợi tại ngày tạo (T)

- Doanh thu bán khoảng 0,8–1,2 tỷ mỗi tháng.
- Mọi mặt hàng có tồn cuối kỳ ≥ 0. Có ít nhất 3 mặt hàng tồn thấp, để báo cáo tồn kho có gì đáng xem.
- Không quỹ nào âm ở bất kỳ thời điểm nào.
- Công nợ phải thu còn mở khoảng 400–600 triệu, có nợ quá hạn ở cả 4 nhóm tuổi nợ, và có một khoản trả trước chưa phân bổ.
- Mỗi NVKD có số dư công nợ trong báo cáo Theo NVKD.
- Có cả bán trả ngay (phiếu thu tự động) lẫn bán ghi nợ, và có phiếu thu phân bổ vào nhiều chứng từ.
- Sổ quỹ S04a (QTM) và S05 (VCB, TCB) đều có phát sinh mỗi tuần.

## Khối lượng và hiệu năng

Mục tiêu là seed xong dưới 60 giây trên máy dev. Nếu vượt, giảm theo thứ tự:

1. Phiếu xuất bán xuống 25 mỗi tháng.
2. Số dòng tối đa mỗi phiếu xuống 4.
3. Nhập mua xuống 8 mỗi tháng.
