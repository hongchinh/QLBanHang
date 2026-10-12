# Section 02 — Thiết kế Đợt 2

> Phương án A: DebtLedger + DebtSettlement. Ký hiệu: **phía** = Phải thu (Receivable) hoặc Phải trả (Payable); **chiều** = Tăng (Increase) hoặc Giảm (Decrease). Đã cập nhật theo [review](section-04-review-decisions.md).

## 1. Danh mục & cấu hình

| Bảng | Trường chính | Ghi chú |
|---|---|---|
| `CashReason` (Lý do thu/chi) | Code, Name, Direction (Thu/Chi), PartnerType (None/Customer/Supplier/Any), DebtEffect, IsSystem, IsAuto | DebtEffect: None / ReceivableDecrease / ReceivableIncrease / PayableDecrease / PayableIncrease. Khi lý do đã được dùng thì khóa Direction, PartnerType và DebtEffect (giống `StockReason`). Lý do hệ thống không xóa được. `IsAuto` = chỉ dùng cho phiếu tự động, không hiện trong danh sách chọn |
| `Fund` (Quỹ) | BranchId, Code, Name, Kind (Cash/Bank), BankName?, AccountNumber?, IsDefault, OpeningAmount, IsActive | **Mỗi tài khoản ngân hàng là một quỹ**; một chi nhánh có bao nhiêu tài khoản (và quỹ tiền mặt) cũng được. Mỗi (chi nhánh, Kind) có đúng một quỹ mặc định, dùng cho phiếu tự động; trên phiếu kho và phiếu thu/chi người dùng chọn lại được. Số dư đầu tính tại ngày đầu kỳ của chi nhánh. Không xóa được quỹ đã có phát sinh |
| `PaymentMethod` | `IsCash` → `FundKind` (None/Cash/Bank) | Migration: IsCash=true → Cash, còn lại → None. HTTT chuyển khoản do người dùng đổi sang Bank |
| `StockReason` | + `DebtEffect` (cùng enum với `CashReason`) | Thay cho cờ bool `PostsDebt`, để hàng trả lại ghi đúng phía và chiều. Tổ hợp hợp lệ: **Xuất** → None / ReceivableIncrease / PayableDecrease; **Nhập** → None / PayableIncrease / ReceivableDecrease. Receivable* cần PartnerType Customer hoặc Any; Payable* cần Supplier hoặc Any. DebtEffect ≠ None thì bắt buộc chọn đối tượng. Khóa khi lý do đã được dùng |
| `Customer` | + `CreditDays` (int?, số ngày được nợ) | |
| `CreditLimit` | CustomerId, BranchId, Amount | Unique (CustomerId, BranchId) |
| `DebtSettings` (singleton, hoặc thêm cột vào settings hiện có) | CreditLimitPolicy, NegativeFundPolicy | Giá trị Allow/Warn/Block, dùng lại enum `NegativeStockPolicy` hoặc tạo enum chung. Mặc định Warn (chờ BA xác nhận) |
| Ngày đầu kỳ của chi nhánh | `Branch.OpeningDate` | Hiện nằm trên từng dòng `OpeningStock`; plan quyết cách migrate. **Chỉ sửa được khi chi nhánh chưa có chứng từ nào** (phiếu kho, thu/chi, chuyển quỹ, bù trừ). Dữ liệu đầu kỳ (tồn, nợ, quỹ) vẫn sửa được bình thường |

**Lý do thu/chi hệ thống được seed sẵn** (chờ kế toán duyệt):

| Mã | Tên | Hướng | Đối tượng | DebtEffect |
|---|---|---|---|---|
| TKH | Thu tiền khách hàng | Thu | Customer | ReceivableDecrease |
| THNCC | Thu hoàn tiền NCC | Thu | Supplier | PayableIncrease |
| TK | Thu khác | Thu | Any (không bắt buộc) | None |
| TNCC | Trả tiền nhà cung cấp | Chi | Supplier | PayableDecrease |
| CHKH | Chi hoàn tiền khách hàng | Chi | Customer | ReceivableIncrease |
| CK | Chi khác | Chi | Any (không bắt buộc) | None |
| TPX | Thu tiền theo phiếu xuất (tự động) | Thu | theo phiếu kho | lưu None, engine suy ra |
| CPN | Chi tiền theo phiếu nhập (tự động) | Chi | theo phiếu kho | lưu None, engine suy ra |

TPX/CPN có `IsAuto = true`, người dùng không chọn được. Cột `DebtEffect` của hai lý do này lưu None; phía và chiều của phiếu tự động do engine suy ra từ phiếu kho (§4).

**Lý do nhập/xuất: DebtEffect** (migration cho lý do đã có + đề xuất seed thêm, chờ kế toán duyệt):

| Mã | Tên | Hướng | Đối tượng | DebtEffect |
|---|---|---|---|---|
| XBH | Xuất bán hàng (đã có) | Xuất | Customer | ReceivableIncrease |
| NMH | Nhập mua hàng (đã có) | Nhập | Supplier | PayableIncrease |
| NKH / XKH | Nhập khác / Xuất khác (đã có) | | Any | None |
| NTL | Nhập hàng bán bị trả lại (mới) | Nhập | Customer | ReceivableDecrease |
| XTL | Xuất trả hàng NCC (mới) | Xuất | Supplier | PayableDecrease |

## 2. Chứng từ

| Bảng | Trường chính |
|---|---|
| `CashVoucher` | Type (Receipt/Payment), Code, VoucherAt (ngày + giờ), BranchId, FundId, ReasonId, PartnerId? + snapshot PartnerName/Address/TaxCode, PersonName (người nộp/nhận), Description (lý do nộp/chi), AttachedDocs (kèm theo), Note, Total, Status (Active/Cancelled), CancelledAt/By, OwnerUserId, Version (`xmin`), **SourceStockVoucherId?** |
| `CashVoucherLine` | CashVoucherId, SortOrder, Description, Amount (> 0), Note |
| `CashVoucherActivity` | Giống `StockVoucherActivity`: Created/Updated/Cancelled/Restored/Deleted, thêm SettlementRemoved |
| `FundTransfer` (Chuyển quỹ) | Code, VoucherAt, BranchId, FromFundId, ToFundId (khác nhau, cùng chi nhánh), Amount (> 0), Description, Note, Status, CancelledAt/By, OwnerUserId, Version + activity cùng kiểu. Dùng cho nộp tiền mặt vào ngân hàng, rút tiền về quỹ, chuyển giữa hai tài khoản. Không ảnh hưởng công nợ. Phí ngân hàng lập phiếu Chi khác riêng |
| `DebtOffsetVoucher` | Code, VoucherAt, BranchId, PartnerId, Amount, Note, Status, OwnerUserId, Version |
| `OpeningDebt` | BranchId, PartnerId, Side, Kind (Debt/Prepayment), DocNo, DocDate, DueDate?, Amount, OwnerUserId?, Note |
| `StockVoucher` (thêm) | DueDate?, FundId? |

`Total` của phiếu thu/chi = Σ dòng. Phiếu tự động có đúng 1 dòng; diễn giải sinh từ số phiếu kho.

`DocumentType` thêm: Receipt, Payment, FundTransfer, DebtOffset (đánh số theo loại × chi nhánh).

## 3. Ledger

**`DebtEntry`** (không kế thừa `BaseEntity`, không soft-delete):
- Trường: Id, BranchId, PartnerId, Side, Direction, Amount (> 0), PostedAt, DueDate?, OwnerUserId?, SourceType (Opening/StockIn/StockOut/Receipt/Payment/Offset), SourceId, DocCode, DocDate.
- `DueDate`: dòng Increase **luôn có giá trị** = hạn TT của chứng từ nguồn; nếu chứng từ không có hạn TT (đối tượng không có CreditDays, nợ đầu kỳ bỏ trống hạn) thì = DocDate, tức đến hạn ngay. Dòng Decrease để null.
- Unique `(SourceType, SourceId, Side)`: **mỗi chứng từ có tối đa một dòng cho mỗi phía.** Khi sửa thì **UPDATE tại chỗ**. Khi hủy hoặc xóa thì xóa dòng, sau khi đã bỏ hết settlement trỏ vào nó.
- Index: (BranchId, PartnerId, Side, PostedAt).

**`DebtSettlement`**:
- Trường: Id, IncreaseEntryId, DecreaseEntryId, Amount (> 0), Origin (AtVoucher/Manual/Offset/Auto), BatchId, EffectiveAt, CreatedAt, CreatedBy.
- Ràng buộc:
  - Hai dòng cùng BranchId, PartnerId và Side; một dòng Increase, một dòng Decrease.
  - Σ settlement của một dòng ≤ Amount của dòng đó.
- `EffectiveAt` = max(PostedAt của hai dòng). Đây là ngày dùng cho khóa sổ và cho báo cáo "tại ngày". Cột được lưu để đánh index, nên **mỗi khi PostedAt của một dòng đổi (sửa ngày chứng từ) thì tính lại EffectiveAt của mọi settlement trỏ vào dòng đó**, trong cùng transaction.

**Số liệu suy ra:**
- Số dư (phía, đối tượng, chi nhánh) tại ngày D = Σ Increase − Σ Decrease với PostedAt ≤ D. Số dư dương nghĩa là đối tượng còn nợ (phía Phải thu) hoặc mình còn nợ (phía Phải trả); số âm nghĩa là trả trước.
- Còn mở của một dòng tại ngày D = Amount − Σ settlement của nó có EffectiveAt ≤ D.

## 4. Quy tắc ghi sổ

| Nguồn | Điều kiện | Phía / Chiều | Số tiền | Hạn TT | Owner |
|---|---|---|---|---|---|
| Phiếu nhập/xuất | Lý do có DebtEffect ≠ None | theo DebtEffect của lý do | Total | DueDate (dòng Increase) | Owner phiếu |
| Phiếu thu/chi tay | Lý do có DebtEffect ≠ None | theo DebtEffect của lý do | Total | DocDate (dòng Increase) | Owner phiếu |
| Phiếu tự động | Phiếu kho có DebtEffect ≠ None | **cùng phía, ngược chiều** với dòng của phiếu kho, kèm settlement Auto nối với dòng đó | PaidAmount | — | Owner phiếu kho |
| Bù trừ | — | Receivable / Decrease **và** Payable / Decrease | Amount | — | Owner phiếu |
| Nợ đầu kỳ | Kind = Debt / Prepayment | Side / Increase hoặc Decrease, tại 00:00 VN của ngày đầu kỳ | Amount | DueDate ?? DocDate | OwnerUserId |

Ví dụ phía/chiều: Xuất bán → Phải thu tăng, phiếu thu tự động làm Phải thu giảm. Nhập hàng bán bị trả lại → Phải thu giảm, phiếu chi tự động (hoàn tiền khách) làm Phải thu tăng. Xuất trả hàng NCC → Phải trả giảm, phiếu thu tự động (NCC hoàn tiền) làm Phải trả tăng.

**Số tiền TT trên phiếu kho:**
- `PaidAmount > 0` thì **bắt buộc** chọn HTTT có `FundKind ≠ None` (lỗi validation). Nhờ vậy mọi khoản đã trả trên phiếu kho đều có phiếu thu/chi thật, nên công nợ và hạn mức chỉ cần đọc ledger.
- `PaidAmount > Total` được phép (khách trả dư, trả tròn số).

**Phiếu tự động** (khi Số tiền TT > 0; HTTT luôn có quỹ theo quy tắc trên):
- Phiếu xuất sinh Phiếu thu; phiếu nhập sinh Phiếu chi. Quy tắc này đúng cả với hàng trả lại.
- Quỹ = `StockVoucher.FundId`, mặc định là quỹ mặc định của loại quỹ đó trong chi nhánh.
- Ngày, đối tượng và chi nhánh lấy theo phiếu kho. Số phiếu lấy từ counter Receipt/Payment (xem open question về counter riêng cho ngân hàng).
- Settlement Auto = min(PaidAmount, Amount của dòng phiếu kho − Σ settlement khác của dòng đó). Phần dư của phiếu tự động là khoản trả trước chưa đối trừ, được đối trừ sau như phiếu thu/chi thường.
- Khi phiếu kho được lưu lại:
  - Số tiền TT về 0 → xóa phiếu tự động (xem open question về số phiếu bị nhảy).
  - Các trường khác thay đổi → cập nhật phiếu tự động và tính lại settlement Auto.
- Hủy, khôi phục, xóa phiếu kho thì phiếu tự động đi theo.
- Mọi việc trên chạy trong cùng transaction với phiếu kho.
- API thu/chi từ chối sửa, hủy, xóa phiếu có `SourceStockVoucherId` (409 `MANAGED_BY_STOCK_VOUCHER`).

## 5. Sửa, hủy, xóa và đối trừ

| Thao tác | Quy tắc |
|---|---|
| Sửa làm Amount của dòng công nợ < Σ settlement (ví dụ giảm giá sau khi khách đã trả một phần) | Lần đầu trả 422 `DEBT_SETTLEMENTS_WILL_BE_REMOVED` kèm danh sách. Người dùng xác nhận (`confirmRemoveSettlements`) thì bỏ mọi settlement của dòng đó, ghi activity ở mọi chứng từ bị ảnh hưởng rồi mới lưu. Settlement Auto với phiếu tự động của chính phiếu kho không cần xác nhận, được tính lại theo §4 |
| Đổi đối tượng, đổi phía (lý do), hoặc DebtEffect thành None khi dòng đã có settlement | **Chặn**, phải bỏ đối trừ trước |
| Hủy hoặc xóa chứng từ có settlement | Lần đầu trả 422 `DEBT_SETTLEMENTS_WILL_BE_REMOVED` kèm danh sách. Người dùng xác nhận (cờ `confirmRemoveSettlements`) thì bỏ hết settlement liên quan, ghi activity ở mọi chứng từ bị ảnh hưởng rồi mới hủy/xóa |
| Khôi phục | Ghi lại dòng công nợ; **không** khôi phục settlement. Ngoại lệ: settlement Auto giữa phiếu kho và phiếu tự động của nó được tạo lại theo §4 |
| Đối trừ lúc lập phiếu thu/chi | Request kèm `allocations[{entryId, amount}]`. Σ ≤ Total, từng dòng ≤ số còn mở, cùng đối tượng, cùng phía, cùng chi nhánh. Lưu với Origin = AtVoucher. Grid trên form **gợi ý sẵn** phân bổ theo chứng từ đến hạn sớm nhất (DueDate, rồi DocDate), người dùng sửa được |
| Sửa phiếu thu/chi đã phân bổ | Danh sách `allocations` gửi lên **chỉ thay các settlement Origin = AtVoucher**. Settlement Manual/Offset trỏ vào phiếu được giữ nguyên. Σ AtVoucher mới + Σ settlement còn lại phải ≤ Total, nếu không thì 422 `ALLOCATION_EXCEEDS_TOTAL` |
| Đối trừ thủ công | Màn Đối trừ: chọn đối tượng + phía; bên trái là các dòng Decrease còn mở, bên phải là các dòng Increase còn mở; nhập số tiền rồi lưu một batch (Origin = Manual). Có nút gợi ý "phân bổ theo thứ tự cũ nhất", người dùng vẫn sửa được |
| Bỏ đối trừ | Theo batch hoặc theo từng settlement |
| Bù trừ công nợ | Chọn đối tượng; liệt kê dòng phải thu Increase còn mở và dòng phải trả Increase còn mở; nhập số tiền mỗi dòng sao cho hai bên bằng nhau. Chứng từ sinh 2 dòng Decrease cùng các settlement Origin = Offset. Hủy chứng từ thì bỏ các settlement đó và xóa 2 dòng |

**Khóa sổ** (`Branch.LockedUntil`):
- Chứng từ: giống Đợt 1, gồm cả chuyển quỹ, nợ đầu kỳ và số dư quỹ đầu kỳ.
- Settlement: không tạo và không bỏ được nếu `EffectiveAt` ≤ ngày khóa.
- Vì `EffectiveAt` ≥ PostedAt của cả hai dòng, hủy/xóa/sửa một chứng từ chưa khóa không bao giờ kéo theo settlement đã khóa, nên không cần quy tắc riêng cho trường hợp này.

## 6. Hạn mức nợ & nợ quá hạn

- Kiểm tra khi lưu, sửa, khôi phục phiếu kho có lý do **ReceivableIncrease**, nếu khách có `CreditLimit` tại chi nhánh.
- Số dư sau = số dư phải thu của khách tại chi nhánh (đọc từ ledger, mọi ngày) sau khi áp phiếu đang lưu **và phiếu tự động của nó**. Số dư trước = cùng cách tính nhưng với trạng thái trước thao tác.
- Vi phạm khi `Số dư sau > Hạn mức` **và** `Số dư sau > Số dư trước`. Sửa giảm hoặc thao tác không làm nợ tăng thì không cảnh báo (giống âm kho D31).
- Xử lý theo chính sách:
  - Allow: bỏ qua.
  - Warn: 422 `CREDIT_LIMIT_EXCEEDED` kèm số liệu, người dùng xác nhận (`confirmCreditLimit`) thì lưu.
  - Block: 422 không cho xác nhận.
- Form phiếu xuất có panel thông tin khách: số dư phải thu, hạn mức, còn được nợ, **nợ quá hạn** (tổng còn mở có DueDate < hôm nay; DueDate mặc định theo §3). Chỉ hiển thị, không chặn.

## 7. Âm quỹ

- Số dư quỹ tại thời điểm t = OpeningAmount + Σ Thu − Σ Chi + Σ Chuyển đến − Σ Chuyển đi (chứng từ Active, VoucherAt ≤ t).
- Kiểm tra mọi thao tác làm giảm quỹ: lưu hoặc sửa phiếu chi; sửa giảm, hủy, xóa phiếu thu; lưu/sửa chuyển quỹ (quỹ đi), hủy/xóa chuyển quỹ (quỹ đến); đổi quỹ hoặc đổi ngày; phiếu tự động; sửa số dư đầu.
- Cách kiểm giống âm kho D31: tìm điểm thấp nhất của số dư lũy kế từ ngày bị ảnh hưởng trở đi, chỉ báo khi thao tác làm điểm đó tệ hơn. Dùng `NegativeFundPolicy`, 422 `NEGATIVE_FUND` kèm cờ xác nhận.

## 8. Khóa & transaction

- Thứ tự advisory lock: branch gate → product keys (kho) → **partner keys (công nợ)** → **fund keys** (chuyển quỹ khóa cả hai quỹ) → counter row.
- Gom khóa vào một câu lệnh như cách sửa của Đợt 1 (1 lock cho mỗi key, thứ tự `COLLATE "C"`).
- Kiểm tra tham chiếu (settlement, đã khóa sổ) **sau khi** đã lấy khóa, như commit `1797dcc` của Đợt 1.

## 9. Quyền

| Quyền | Ghi chú |
|---|---|
| `receipts.{view,create,edit,delete,cancel,edit_all}` | Giống `stock_in.*` |
| `payments.{view,create,edit,delete,cancel,edit_all}` | |
| `fund_transfers.{view,create,edit,delete,cancel,edit_all}` | Chuyển quỹ |
| `debt_offset.{view,create,cancel,delete}` | |
| `debt.matching` | Đối trừ và bỏ đối trừ thủ công |
| `debt.opening_balance` | Nợ đầu kỳ |
| `cash.opening_balance` | Số dư quỹ đầu kỳ |
| `cash.catalogs.manage` | Lý do thu/chi, quỹ |
| `credit_limit.manage` | Hạn mức nợ |
| `cash.settings` | Chính sách hạn mức và âm quỹ (hoặc dùng chung `inventory.settings`; plan quyết) |
| `reports.debt` (đã có) | 7 báo cáo công nợ (§11) |
| `reports.cash` | Sổ quỹ |

Phiếu tự động đi theo quyền của phiếu kho. Seed mặc định theo role (ACCOUNTANT, MANAGER, WAREHOUSE, SALES) do plan quyết, làm một lần như D23. Việc SALES có được xem panel công nợ trên phiếu xuất hay không: xem open question.

## 10. API (đều đi qua `X-Branch-Id`)

- `GET/POST/PUT/DELETE /api/cash-vouchers` (`?type=receipt|payment`), `POST /{id}/cancel`, `POST /{id}/restore`, `GET /defaults`
- `/api/fund-transfers` (CRUD + cancel/restore)
- `/api/cash-reasons`, `/api/funds`, `/api/credit-limits`, `/api/opening-debts`, `/api/funds/{id}/opening-balance`
- `GET /api/debt/open-items?partnerId&side` (dùng cho phân bổ trên form, màn đối trừ, bù trừ)
- `GET /api/debt/partner-summary?partnerId` (panel khách trên phiếu xuất)
- `POST /api/debt/settlements` (một batch), `DELETE /api/debt/settlements/{id}`, `DELETE /api/debt/settlement-batches/{batchId}`
- `/api/debt-offsets` (CRUD + cancel)
- Báo cáo: `/api/reports/debt-balance`, `debt-ledger`, `debt-documents`, `debt-aging`, `debt-overdue`, `debt-by-salesperson`, `debt-reconciliation`, `cash-book`

## 11. Báo cáo

Tất cả báo cáo lọc theo chi nhánh làm việc, như báo cáo Đợt 1. Tham số chung: phía (Phải thu / Phải trả), đối tượng (tùy chọn). 7 báo cáo công nợ (1–7) + Sổ quỹ (8).

| # | Báo cáo | Tham số | Cột / nội dung |
|---|---|---|---|
| 1 | **Số dư công nợ** | Từ ngày – đến ngày | Mã, tên, đầu kỳ (Nợ / Có), phát sinh tăng, phát sinh giảm, cuối kỳ (Nợ / Có). Bấm một dòng để mở sổ chi tiết |
| 2 | **Sổ chi tiết công nợ** | Đối tượng, từ – đến | Đầu kỳ, rồi từng `DebtEntry`: ngày, số CT, loại CT, diễn giải, tăng, giảm, số dư lũy kế; cuối kỳ |
| 3 | **Công nợ theo chứng từ** | Tại ngày | Mỗi dòng Increase còn mở: số CT, ngày, hạn TT, số tiền, đã đối trừ, còn lại, số ngày nợ, số ngày quá hạn. Phần riêng: các khoản Decrease chưa đối trừ (trả trước). Còn lại − chưa đối trừ = số dư |
| 4 | **Tuổi nợ** | Tại ngày, các nhóm tuổi | Theo đối tượng: còn mở chia nhóm theo DocDate, thêm cột "chưa đối trừ"; tổng khớp số dư. Nhóm mặc định 0–30 / 31–60 / 61–90 / >90; người dùng đổi các mốc ngày ngay trên tham số báo cáo (không lưu cấu hình) |
| 5 | **Nợ quá hạn** | Tại ngày | Các dòng còn mở có DueDate < ngày (DueDate mặc định theo §3), số ngày quá hạn, chia nhóm 1–30 / 31–60 / 61–90 / >90; tổng theo đối tượng |
| 6 | **Công nợ theo nhân viên** | Từ – đến | Theo OwnerUserId của dòng Increase (đang chờ xác nhận lại, xem open question về `SalespersonId`): phát sinh nợ trong kỳ, đã thu trong kỳ (settlement có EffectiveAt trong kỳ, gắn với chứng từ của NV), còn nợ cuối kỳ, trong đó quá hạn. Thêm một dòng "Chưa phân bổ" cho khoản trả chưa đối trừ |
| 7 | **Biên bản đối chiếu** | Đối tượng, từ – đến | Đầu kỳ, danh sách phát sinh (giống sổ chi tiết), cuối kỳ. Đối tượng vừa là KH vừa là NCC thì hiện cả hai phía và số bù trừ ròng. Mẫu in ở Đợt 3 |
| 8 | **Sổ quỹ** | Loại quỹ (Tiền mặt / Ngân hàng / Tất cả), quỹ (một hoặc tất cả quỹ thuộc loại đã chọn), từ – đến | Chọn Ngân hàng + tất cả quỹ là **sổ tiền gửi ngân hàng** của mọi tài khoản trong chi nhánh, nhóm theo từng tài khoản và có tồn đầu/cuối riêng cùng dòng tổng. Mỗi nhóm quỹ: tồn đầu, rồi từng chứng từ: ngày, số phiếu thu, số phiếu chi, đối tượng / người nộp-nhận, diễn giải, thu, chi, tồn lũy kế; tồn cuối. Chuyển quỹ hiện là dòng thu ở quỹ đến và dòng chi ở quỹ đi, loại CT "Chuyển quỹ". Khi xem tất cả quỹ thì bỏ các dòng chuyển quỹ nội bộ, vì chúng triệt tiêu nhau và không phải thu/chi thật |
