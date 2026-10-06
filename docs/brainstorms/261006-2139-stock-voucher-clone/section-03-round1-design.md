# Section 03 — Thiết kế Đợt 1 (Kho)

> Cập nhật sau review 2026-10-06 22:40:45 — lý do từng thay đổi xem [section-05](section-05-review-decisions.md).

## 1. Phạm vi Đợt 1

- Danh mục: Chi nhánh, Kho, Đối tượng (mở rộng Khách hàng với cờ Là KH / Là NCC), Lý do nhập/xuất, Hình thức thanh toán; Hàng hóa thêm cờ theo dõi tồn kho, %CK mua/bán, cờ giá gồm VAT.
- Tồn đầu kỳ (màn riêng).
- Phiếu nhập kho / Phiếu xuất kho: list + form giống Báo giá.
- Tồn kho theo (hàng, kho); giá vốn **bình quân cuối kỳ** (kỳ và phạm vi cấu hình được), tự tính lại, nút "Tính lại giá vốn"; kiểm âm kho ở mọi thao tác; khóa sổ theo chi nhánh; đánh số.
- Báo cáo: Tồn kho (theo kho, tại thời điểm; SL + giá trị), Thẻ kho (1 mặt hàng).
- Ngoài Đợt 1: thu/chi, công nợ, hạn mức, bù trừ công nợ (Đợt 2); in/Excel, nhập Excel, sao chép (Đợt 3); FIFO, điều chuyển, kiểm kê, trả hàng, Báo giá → Phiếu bán hàng (backlog, [section-05 §5](section-05-review-decisions.md#5-backlog-ghi-vào-plan)). HTTT có cờ `IsCash` từ Đợt 1 để Đợt 2 dùng.

## 2. Kiến trúc

- `Domain/Entities/Inventory`: StockVoucher, StockVoucherLine, StockVoucherActivity, Warehouse, StockReason, PaymentMethod, OpeningStock, InventoryLedger, InventoryCostPeriod, StockBalance.
- `Domain/Entities/Organization`: Branch; `User.DefaultBranchId`.
- `Domain/Entities/Catalog`: field mới trên Product và Customer.
- Application:
  - `ICurrentBranch` — chi nhánh làm việc của request (frontend gửi header `X-Branch-Id`); user không có `branches.access_all` luôn bị ép về `DefaultBranchId`.
  - Port transaction mới (`IAppDbContext` hiện chỉ có `SaveChangesAsync`) và `IInventoryLock` (Infrastructure: `pg_advisory_xact_lock`).
  - `InventoryPostingService` — một transaction: validate → khóa sổ → lock → cấp số → ghi phiếu/tổng → ledger + SL lũy kế → kiểm âm → tính lại giá vốn → cập nhật giá nhập → activity log.
  - `InventoryCostingService` — bình quân cuối kỳ theo (hàng, phạm vi, kỳ).
- **Giá vốn chỉ nằm trong ledger và bảng kỳ giá vốn.** Dòng phiếu chỉ giữ dữ liệu người dùng nhập và các số tính từ chính phiếu đó; tính lại giá vốn không bao giờ sửa bản ghi phiếu.
- Activity log: hệ thống chỉ có `QuotationActivity` → thêm `StockVoucherActivity` theo cùng pattern.

## 3. Data model

**Danh mục**
- `Branch`: Code, Name, Address, `LockedUntil` (DateOnly?, ngày khóa sổ).
- `User.DefaultBranchId` (bắt buộc).
- `Warehouse`: Code, Name, BranchId, IsActive.
- `Customer` = **danh mục đối tượng chung**: thêm `IsCustomer` (mặc định true), `IsSupplier`. Màn "Nhà cung cấp" là danh sách lọc `IsSupplier`; Báo giá chỉ gợi ý `IsCustomer`. Giữ tên entity `Customer` để không phải refactor Báo giá. Không có bảng Supplier.
- `StockReason`: Code, Name, Direction (In/Out), PartnerType (Customer/Supplier/Any/None), IsSystem.
- `PaymentMethod`: Code, Name, IsCash.
- `Product` thêm: `TrackInventory`, `PurchaseDiscountRate`, `SalesDiscountRate`, `PriceIncludesVat`, `CostPriceUpdatedOn`. `CostPrice` = giá nhập gần nhất; `DefaultPrice` = giá bán; `DefaultTaxRate` = %VAT; Length/Width/Thickness = kích thước mặc định.
- **Đơn vị tồn** theo `PricingMode`: PerUnit → `Product.UnitId` (bắt buộc khi `TrackInventory`); PerLinearMeter → m; PerSquareMeter → m²; PerCubicMeter → m³. Không có ĐVT chuyển đổi.

**Chứng từ**
- `StockVoucher`: Type (In/Out), Code, **VoucherAt** (ngày + giờ, giờ VN), BranchId, WarehouseId (mặc định), PartnerId? (→ Customer), PartnerName/Address/TaxCode (snapshot), HandlerName (người giao/nhận), ReasonId, PaymentMethodId?, Note, Freight, OrderDiscount, totals (GoodsAmount, LineDiscountTotal, VatTotal, Total, PaidAmount), Status (Active/Cancelled), CancelledAt/By, OwnerUserId, audit, soft-delete, concurrency token (`xmin`). Không lưu TotalCost, TotalQuantity.
- `StockVoucherLine`: SortOrder, ProductId, ProductCode/Name (snapshot), WarehouseId, TrackInventory / PricingMode / UnitName / PriceIncludesVat (snapshot), SheetCount?, Length?, Width?, Thickness?, Quantity (đơn vị tồn), UnitPrice, Amount, DiscountRate, DiscountAmount, DiscountManual, OrderDiscountAllocated, FreightAllocated (phiếu nhập), VatRate, VatAmount, NetAmount (Số còn lại), InboundValue (giá trị nhập kho — phiếu nhập), Note. Không có UnitCost/CostAmount.
- `OpeningStock`: BranchId, WarehouseId, ProductId, OpeningDate, Quantity, Amount.

**Ledger & giá vốn** (dữ liệu dẫn xuất: không kế thừa `BaseEntity`, xóa cứng và ghi lại)
- `InventoryLedger`: PostedAt (= VoucherAt; tồn đầu = 00:00 ngày đầu), SortKey (PostedAt → Opening/In/Out → Code), BranchId, WarehouseId, ProductId, SourceType (Opening/StockIn/StockOut), SourceId, SourceLineId, QtyIn, QtyOut, InValue, **RunningQty** (lũy kế theo (hàng, kho)), UnitCost, CostAmount (dòng xuất, do tính giá ghi).
- `InventoryCostPeriod`: ProductId, ScopeKey (BranchId hoặc WarehouseId theo cấu hình), PeriodStart, PeriodEnd, OpeningQty/Value, InQty/Value, OutQty/Value, AvgCost, ClosingQty/Value. Đồng thời là bản chụp số dư cuối kỳ.
- `StockBalance`: (WarehouseId, ProductId) → Quantity hiện tại (cache tra cứu nhanh). Giá trị tồn đọc từ `InventoryCostPeriod`.

**Cấu hình**
- `InventorySettings`: CostingMethod (PeriodicAverage; Fifo = backlog), CostingPeriod (Month/Quarter/Year, mặc định Month), CostingScope (Branch/Warehouse, mặc định Branch), PurchaseCostIncludesVat (mặc định true), NegativeStockPolicy (Allow/Warn/Block), NetExcludesVat, DefaultDateMode (Now/PreviousVoucher).
  - Đổi CostingPeriod / CostingScope / PurchaseCostIncludesVat khi đã có phát sinh: chỉ áp từ đầu một kỳ chưa khóa, kéo theo tính lại toàn bộ từ kỳ đó.
- `DocumentNumbering`: DocType (StockIn/StockOut; mở rộng cho Đợt 2), BranchId, Prefix, Length, ResetPolicy (None/Monthly/Yearly), Pattern (KH/STT/THANG/NAM), counter. Khi lưu cấu hình: Monthly → mẫu phải có THANG và NAM; Yearly → phải có NAM. Unique `StockVoucher (Type, BranchId, Code)` lọc bản ghi chưa xóa.

**Permission mới**
- `stock_in.view/create/edit/delete/cancel/edit_all/export` và `stock_out.*` tương tự.
- `inventory.opening_stock`, `inventory.view_cost`, `inventory.catalogs.manage`, `inventory.settings`, `inventory.recalc_cost`, `reports.inventory`.
- `branches.manage`, `branches.access_all`, `period_lock.manage`.

## 4. Công thức

**Số lượng (đơn vị tồn)** — dùng chung với `EffectiveQuantity` của Báo giá, kích thước tính bằng mm:
- PerUnit: Quantity = SL nhập
- PerLinearMeter: số tấm × dài / 1 000
- PerSquareMeter: số tấm × dài × rộng / 1 000 000
- PerCubicMeter: số tấm × dài × rộng × dày / 1 000 000 000

**Dòng** (tiền làm tròn 0 số lẻ)
- Số tiền = Quantity × ĐG
- Tiền CK = Số tiền × %CK / 100 (giữ giá trị gõ tay nếu `DiscountManual`)
- Net = Số tiền − CK
- CK cả đơn phân bổ = CK cả đơn × Net / ΣNet (mọi dòng, theo giá trị; phần lẻ dồn vào dòng cuối)
- Net′ = Net − CK cả đơn phân bổ
- VAT: phiếu xuất + `PriceIncludesVat` → `Net′ − Net′/(1 + %/100)`; còn lại `Net′ × %/100`. %VAT mặc định `DefaultTaxRate`, sửa được từng dòng
- Số còn lại: phiếu xuất + giá gồm VAT → `Net′`; còn lại → `Net′ + VAT`, hoặc `Net′` nếu `NetExcludesVat`

**Tổng**
- Tiền hàng = ΣSố tiền; Tổng CK = ΣCK + CK cả đơn; Tổng VAT = ΣVAT
- **Tổng TT = ΣSố còn lại + Phí VC** (CK cả đơn đã nằm trong Số còn lại)
- Phí VC phiếu xuất: không chịu VAT, không vào giá vốn; cần VAT vận chuyển thì dùng dòng hàng dịch vụ "Vận chuyển"
- Số tiền TT mặc định = Tổng TT, sửa được
- Backend tính lại tất cả khi lưu; không tin số từ client
- Ô %VAT header (chỉ phiếu nhập): gán % xuống mọi dòng → tính lại bằng công thức dòng

**Giá trị nhập kho** (phiếu nhập, chỉ dòng `TrackInventory`)
- Phí VC phân bổ = Phí VC × Net′ / ΣNet′ (chỉ các dòng theo dõi tồn; phần lẻ dồn vào dòng cuối)
- InboundValue = Net′ + Phí VC phân bổ + (VAT dòng nếu `PurchaseCostIncludesVat`)

**Chọn hàng (một quy tắc duy nhất)**
- Phiếu nhập: ĐG = CostPrice, %CK = CK mua. Phiếu xuất: ĐG = DefaultPrice, %CK = CK bán
- %VAT = DefaultTaxRate; kích thước = kích thước của hàng; kho dòng = kho header
- SL tồn = tồn tại **VoucherAt** của phiếu theo kho dòng, không tính bút toán của chính phiếu

## 5. Quy tắc lưu (1 transaction)

1. Quyền theo loại phiếu (`stock_in.*` / `stock_out.*`); phiếu thuộc chi nhánh làm việc; sửa/xóa/hủy phiếu người khác cần `edit_all`; phiếu Cancelled chỉ xem; concurrency token lệch → 409.
2. Khóa sổ: ngày mới **và** ngày cũ (khi sửa) phải > `Branch.LockedUntil`.
3. Validate: thời điểm, kho header, lý do bắt buộc; đối tượng theo PartnerType của lý do; ≥ 1 dòng; mỗi dòng có hàng, đủ kích thước theo PricingMode, Quantity > 0, ĐG ≥ 0; kho dòng thuộc chi nhánh của phiếu.
4. `pg_advisory_xact_lock` theo (ProductId, BranchId) của các hàng thuộc cũ ∪ mới, thứ tự tăng dần (tránh deadlock; bao trùm cả cặp (hàng, kho) lẫn phạm vi tính giá).
5. Phiếu mới: cấp số từ counter.
6. Ghi phiếu, dòng, tổng.
7. Ledger: xóa bút toán cũ của phiếu, ghi mới (chỉ dòng `TrackInventory`); cập nhật RunningQty từ min(thời điểm cũ, mới) cho các cặp (hàng, kho) thuộc cũ ∪ mới; cập nhật StockBalance.
8. **Kiểm âm kho** (policy ≠ Allow) cho mọi cặp bị ảnh hưởng: MIN(RunningQty) từ thời điểm sớm nhất bị ảnh hưởng trở về sau ≥ 0. Warn → client xác nhận rồi gửi lại với `acknowledgeNegativeStock`; Block → 422 kèm danh sách dòng thiếu.
9. Tính lại giá vốn cho các (hàng, phạm vi) bị ảnh hưởng, từ kỳ chứa min(thời điểm cũ, mới).
10. Phiếu nhập: `Product.CostPrice` = ĐG (trước CK/VAT) nếu VoucherAt ≥ `CostPriceUpdatedOn`.
11. Activity log.

**Hủy / Khôi phục / Xóa** — đều qua bước 1, 2, 4, 7, 8, 9
- Hủy: quyền `cancel` → Status = Cancelled, xóa bút toán ledger. Phiếu hủy chỉ xem.
- Khôi phục: ghi lại ledger.
- Xóa: soft-delete, hiệu ứng ledger như Hủy.

**Giá vốn — bình quân cuối kỳ**
- Tính theo (hàng, phạm vi, kỳ): phạm vi = chi nhánh (mặc định) hoặc kho; kỳ = tháng (mặc định) / quý / năm.
- AvgCost = (OpeningValue + InValue) / (OpeningQty + InQty) khi mẫu số > 0; ngược lại lấy AvgCost kỳ trước, chưa có thì `Product.CostPrice`.
- Dòng xuất: CostAmount = round(QtyOut × AvgCost). ClosingValue = OpeningValue + InValue − OutValue; **ClosingQty = 0 → ClosingValue = 0**, chênh lệch làm tròn dồn vào dòng xuất cuối kỳ. Opening kỳ sau = Closing kỳ trước.
- Tự tính lại sau mỗi lần ghi: chỉ tính lại bảng kỳ từ kỳ bị ảnh hưởng đến kỳ hiện tại (chi phí theo số kỳ, không replay từng giao dịch), rồi ghi lại CostAmount các dòng xuất trong các kỳ đó.
- Kỳ chưa kết thúc: giá vốn là **tạm tính** (UI ghi nhãn).
- **Khóa sổ như MISA:** đặt ngày bất kỳ; chặn chứng từ có ngày ≤ ngày khóa. Giá vốn của kỳ chưa kết thúc vẫn được tính lại tới hết kỳ, kể cả phiếu xuất trong phần đã khóa; kỳ có PeriodEnd ≤ `LockedUntil` thì cố định, không bao giờ ghi lại.
- "Tính lại giá vốn" (`inventory.recalc_cost`): chọn từ kỳ (chưa khóa hết), lọc kho/hàng tùy chọn; dùng sau khi đổi cấu hình.
- Hàng `TrackInventory = false`: không ledger, không giá vốn, không kiểm âm.

**Tồn đầu kỳ**: màn lưới theo kho, quyền `inventory.opening_stock`; mỗi dòng → bút toán Opening; sửa tồn đầu cũng kiểm âm và tính lại; kiểm khóa sổ.

**Ràng buộc danh mục**: hàng đã có phát sinh ledger → không đổi PricingMode, ĐVT, `TrackInventory`, `PriceIncludesVat`, không xóa; kho có phát sinh → không xóa, không đổi chi nhánh; đối tượng có chứng từ → không xóa.

## 6. UI

- Header app: chọn **Chi nhánh làm việc** (chỉ hiện với `branches.access_all`).
- Sidebar nhóm **Kho**: Phiếu nhập kho, Phiếu xuất kho (2 route, dùng chung component theo `type`), Tồn đầu kỳ, Tồn kho, Thẻ kho.
- Nhóm **Chức năng**: thêm Nhà cung cấp (danh sách đối tượng lọc NCC), Kho, Lý do nhập xuất, HTTT. Màn Khách hàng thêm cờ Là KH / Là NCC.
- **Cấu hình hệ thống**: Chi nhánh, Khóa sổ, Đánh số chứng từ, Cấu hình kho, Tính lại giá vốn.
- List phiếu: như `quotation-list-page` — mọi phiếu trong chi nhánh làm việc; lọc thời gian/kho/đối tượng/lý do/trạng thái/người tạo, lưu filter, cột resize, footer tổng.
- Form phiếu: như `quotation-form-page` — toolbar sticky (Lưu tạm / Lưu và thoát / Cập nhật / Hủy phiếu / Khôi phục / Xóa), card "Thông tin chung" (thời điểm ngày + giờ), grid dòng (typeahead hàng; cột số tấm / dài / rộng / dày bật theo PricingMode; SL đơn vị tồn tự tính; kho dòng; SL tồn tại thời điểm phiếu; %CK, %VAT sửa được), panel tổng bên phải (Tiền hàng, CK, VAT, Phí VC, CK cả đơn, Tổng TT, Số tiền TT), nháp chưa lưu, Ctrl+S.
- Cột giá vốn / giá trị tồn chỉ hiện với `inventory.view_cost`; giá vốn kỳ chưa kết thúc ghi "tạm tính".
- Hàng hóa: cờ Theo dõi tồn kho, %CK mua/bán, cờ giá gồm VAT; khóa các trường bị ràng buộc khi đã phát sinh.

## 7. Kiểm thử

**Backend integration (PostgreSQL thật)**
- Công thức dòng/tổng theo bảng giá trị kỳ vọng: CK gõ tay, phân bổ CK cả đơn (dồn lẻ), giá gồm VAT, NetExcludesVat, phí VC.
- SL theo PricingMode khớp `EffectiveQuantity` của Báo giá.
- Giá trị nhập kho: phân bổ phí VC và CK cả đơn; `PurchaseCostIncludesVat` bật/tắt; dòng dịch vụ không nhận phân bổ phí VC.
- BQ cuối kỳ với kịch bản tính tay theo tháng/quý/năm, phạm vi chi nhánh/kho; tồn về 0 → giá trị 0; **bất biến: sửa/xóa/hủy lùi ngày ra cùng kết quả với nhập lại từ đầu**; kỳ đã khóa hết không đổi; kỳ khóa một phần vẫn tính lại.
- Kiểm âm: cộng dồn nhiều dòng xuất; sửa giảm / hủy / xóa / dời ngày phiếu nhập; sửa tồn đầu; 3 mức policy; thứ tự theo giờ phiếu.
- Khóa sổ chặn tạo/sửa (ngày cũ & mới)/xóa/hủy/khôi phục/tồn đầu.
- Đánh số: reset tháng/năm, kiểm mẫu số, 2 lần lưu đồng thời không trùng.
- Phân quyền: chi nhánh làm việc, `access_all`, `edit_all`, `view_cost`, tách nhập/xuất.
- Hai người cùng sửa một phiếu → 409.
- Lỗi giữa chừng → rollback, không lệch ledger/StockBalance/bảng kỳ.
- Sau mỗi kịch bản: StockBalance = SUM(ledger); RunningQty khớp; Closing kỳ = Opening kỳ sau.

**Frontend (Vitest)**
- Util `compute-stock-line` (dùng chung logic SL với `compute-line`).
- Grid: chọn hàng tự điền giá/CK/VAT/kích thước/SL tồn; cột kích thước theo PricingMode.
- Form: cảnh báo xuất âm, phiếu hủy chỉ đọc.

**Đối chiếu thủ công**: nhập lại chứng từ 1 tháng của vài mặt hàng từ DB cũ, so thẻ kho và giá vốn.

## 8. Rollout

- EF migrations: bảng mới + cột mới Product/User/Customer.
- Seed: chi nhánh mặc định (gán `DefaultBranchId` cho mọi user hiện có), kho mặc định, lý do nhập/xuất mặc định (kèm PartnerType), HTTT mặc định ("Tiền mặt" IsCash), đánh số PN/PX 5 chữ số; Customer hiện có `IsCustomer = true`; Product hiện có `TrackInventory = true` trừ nhóm Vận chuyển (rà lại với danh mục thật).
- Permission: ADMIN tự có đủ; `DbSeeder` không gán cho role đã có assignment → thêm bước seed gán permission kho mới cho WAREHOUSE.
- Menu "Kho" hiện khi có `stock_in.view` hoặc `stock_out.view`.
- Cập nhật docs: `product-goals.md` (đưa kho/tồn/công nợ vào scope), `system-architecture.md` (ledger, posting flow, chi nhánh làm việc), `directory-structure.md`, `conventions.md` nếu có pattern mới.

## 9. Rủi ro

- Công thức hàm SQL cũ không có trong repo → số liệu có thể lệch phần mềm cũ; cần đối chiếu (nên lấy định nghĩa hàm từ DB production).
- Thay đổi `Product.CostPrice` từ phiếu nhập ảnh hưởng giá vốn mặc định Báo giá.
- Tồn chỉ theo tổng m / m² / m³, không theo số tấm từng cỡ.
- `PurchaseCostIncludesVat` mặc định true chỉ đúng khi VAT đầu vào không được khấu trừ.
- Giá vốn tạm tính trong kỳ thay đổi theo từng phiếu nhập; đổi cấu hình kỳ/phạm vi khi đã có dữ liệu kéo theo tính lại lớn.
