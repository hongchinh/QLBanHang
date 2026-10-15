# Dữ liệu mẫu cho chi nhánh mới — chi nhánh demo để đào tạo

> Brainstorm: 2026-10-14 16:01:31 · Nội dung bộ dữ liệu: [section-01-dataset-spec.md](section-01-dataset-spec.md)

## Problem framing

Khách đã mua phần mềm muốn tập dùng ngay trên **DB thật**: tra cứu chứng từ, xem báo cáo kho, quỹ, công nợ. Nhưng một chi nhánh mới thì trống trơn, báo cáo không có số liệu gì để xem. Tự nhập tay đủ đầu kỳ và 3 tháng phát sinh thì mất hàng giờ.

Mục tiêu là tạo một chi nhánh demo kèm bộ dữ liệu mẫu đầy đủ. Bộ dữ liệu này không được làm bẩn số liệu của chi nhánh thật, và phải xóa sạch được khi đào tạo xong.

## Goals

- Trong form **Thêm chi nhánh** có ô **"Tạo kèm dữ liệu mẫu để đào tạo"**. Tick vào thì được một chi nhánh có cờ demo, kèm:
  - danh mục mẫu (hàng hóa, khách hàng, nhà cung cấp);
  - 2 kho và 3 quỹ;
  - đầu kỳ đầy đủ: số dư quỹ, tồn kho, công nợ phải thu và phải trả;
  - phát sinh 3 tháng đủ và tháng hiện tại đến hôm qua.
- Mọi báo cáo theo chi nhánh đều có số liệu hợp lý:
  - kho: Tồn kho, Thẻ kho S08, S06, Tổng hợp nhập–xuất–tồn S07;
  - quỹ: Sổ quỹ S04a, S05;
  - công nợ: Số dư, Sổ chi tiết S12, Theo chứng từ, Tuổi nợ, Quá hạn, Theo NVKD, Biên bản đối chiếu.
- Nút **"Xóa chi nhánh demo"** xóa cứng toàn bộ dữ liệu của chi nhánh demo. Danh mục mẫu cũng bị xóa khi không còn chi nhánh demo nào dùng.
- Báo cáo **toàn công ty** không cộng số liệu của chi nhánh demo.

## Non-goals

- **Báo giá mẫu.** Báo giá không có `BranchId`, nên Dashboard và doanh thu NVKD tính trên toàn hệ thống. Báo giá mẫu sẽ cộng vào doanh thu thật.
- Gắn báo giá với chi nhánh. Đây là thay đổi lớn ở module báo giá, nếu cần thì làm thành việc riêng.
- User mẫu, vai trò mẫu.
- Ẩn danh mục mẫu khỏi danh sách và tra cứu của chi nhánh thật. Danh mục mẫu vẫn hiện trong danh mục chung, nhận biết qua mã `DM-` và cờ `IsSample`.
- Script SQL hoặc dữ liệu mẫu cho môi trường dev. `backend/scripts/seed-sample-catalog.sql` vẫn giữ nguyên.
- Nạp dữ liệu mẫu vào chi nhánh đã có sẵn.

## Quyết định đã chốt

| Câu hỏi | Quyết định |
|---|---|
| Dùng cho ai, ở đâu | Đào tạo khách đã mua, trên DB thật. Sau đó phải xóa sạch được |
| Kích hoạt | Ô tick trong dialog Thêm chi nhánh |
| Danh mục | Danh mục mẫu riêng: mã tiền tố `DM-`, cờ `IsSample`, hàng hóa nằm trong nhóm `DM` "Dữ liệu mẫu", dùng chung cho mọi chi nhánh demo. Không đụng giá vốn của hàng thật |
| Báo giá | Không tạo |
| Dọn dẹp | Nút "Xóa chi nhánh demo", chỉ dùng cho chi nhánh có cờ demo, phải gõ mã chi nhánh để xác nhận |
| Cách làm | Seeder C# trong backend gọi các service nghiệp vụ theo thứ tự thời gian (cách A) |
| Thực thi | Song song bằng nhiều agent (xem [Chia việc](#chia-việc-cho-nhiều-agent)) |

## Constraints & assumptions

Các điểm dưới đây lấy từ code tại thời điểm brainstorm.

**Dữ liệu theo chi nhánh và dữ liệu dùng chung**

- **Theo chi nhánh:**
  - `Warehouse`, `Fund`, `DocumentNumbering`, `DocumentCounter`;
  - `StockVoucher` (+ dòng, activity), `OpeningStock`, `InventoryLedgerEntry`, `InventoryCostPeriod`, `StockBalance`;
  - `CashVoucher` (+ dòng, activity), `FundTransfer`, `DebtOffsetVoucher`;
  - `DebtEntry`, `DebtSettlement`, `OpeningDebt`, `CreditLimit`;
  - `Branch.LockedUntil`, `Branch.OpeningDate`.
- **Dùng chung toàn hệ thống:**
  - `Product` (mã duy nhất toàn DB; `CostPrice` bị phiếu nhập ở bất kỳ chi nhánh nào ghi đè), `ProductGroup`, `Unit`;
  - `Customer` (cả khách lẫn NCC, phân vai bằng `IsCustomer`/`IsSupplier`; mã duy nhất toàn DB);
  - `User`, `StockReason`, `CashReason`, `PaymentMethod`;
  - các singleton cấu hình (`InventorySettings`, `CashDebtSettings`…);
  - `Quotation`.

**Phải đi qua service.** Sổ kho, giá vốn, sổ công nợ, phiếu thu/chi tự động và cấn trừ tự động đều được ghi ngay trong transaction lưu chứng từ:

- `StockVoucherService.CreateAsync` gọi `InventoryPostingService`, sau đó `InventoryCostingService`, `DebtLedgerService` và `AutoCashVoucherService`.
- `CashVoucherService.CreateAsync` ghi sổ công nợ và các phân bổ.

Không có chức năng nào dựng lại sổ kho, sổ công nợ hay phiếu tự động từ chứng từ. Insert thẳng sẽ làm lệch báo cáo.

**Danh tính gắn với HTTP.** `ICurrentUser` (claims) và `ICurrentBranch` (header `X-Branch-Id`, có cache trong scope) đọc từ HttpContext. Các service dùng chúng để kiểm tra quyền, lấy `OwnerUserId` = user hiện tại và xác định chi nhánh làm việc. Vì vậy seeder chạy nền cần một "danh tính tạm".

**Thứ tự bắt buộc**

1. Đặt `Branch.OpeningDate`.
2. Số dư đầu quỹ, tồn đầu kỳ (`OpeningStockService.SaveAsync`, mỗi lần thay cả lưới của một kho), công nợ đầu kỳ (`OpeningDebtService.CreateAsync`, `DocDate` ≤ ngày đầu kỳ, nhận `OwnerUserId`).
3. Chứng từ. Mọi chứng từ đều qua `OpeningDateGuard`, nên phải có ngày ≥ ngày đầu kỳ.

**Các chính sách** âm kho, vượt hạn mức, âm quỹ mặc định là cảnh báo (Warn). Khi ở chế độ Warn, gửi cờ `Acknowledge*` là qua. Khi ở chế độ chặn (Block) thì không vượt được, nên bộ sinh dữ liệu phải tự đảm bảo không bao giờ vi phạm.

**Số chứng từ** được cấp theo thứ tự insert. Mẫu `{THANG}`/`{NAM}` lấy theo ngày chứng từ. Insert theo thứ tự thời gian thì số liền mạch.

**`CreatedAt`** luôn bằng thời điểm hiện tại. Ngày chứng từ (`VoucherAt`) được lùi về quá khứ bình thường.

**Báo cáo toàn công ty** (`ReportScope.Company`) hiện đọc mọi chi nhánh. Có nhiều chỗ truy vấn riêng: `CashDebtReportService`, `InventoryReportService` (S07), `Tt133ExportService`, `DebtPositionReportService`. Plan phải liệt kê đủ các chỗ này.

**Không có bảng gán user–chi nhánh.**
- User có `branches.access_all` (ADMIN, MANAGER) chuyển sang chi nhánh demo được ngay.
- User khác phải được admin đổi `DefaultBranchId`.
- Chi nhánh đang là chi nhánh mặc định của một user thì không xóa được bằng lệnh xóa thường.

**Bộ dữ liệu mẫu có sẵn trong test:** `backend/tests/OrderMgmt.IntegrationTests/CashDebt/Reports/ReportDataset.cs` đã dựng một bộ dữ liệu báo cáo hoàn chỉnh qua API. Có thể dùng làm mẫu thứ tự các bước. Các hàm kiểm tra tính nhất quán dùng lại được: `InventoryTestBase.AssertInvariantsAsync` và `CashDebtTestBase.AssertCashDebtInvariantsAsync`.

**Ngành hàng** lấy theo seed: xốp EPS/XPS, PE foam, cao su non, thùng xốp, bông khoáng, bông thủy tinh, vận chuyển.

## Approaches considered

| | Cách | Ưu | Nhược | Độ phức tạp |
|---|---|---|---|---|
| **A** | **Seeder C# gọi các service nghiệp vụ** trong scope DI có danh tính tạm, theo thứ tự thời gian | Dữ liệu phái sinh luôn khớp vì tính bằng chính code đang chạy thật. Nghiệp vụ đổi thì seeder tự đúng theo | Chậm hơn insert thẳng, vì mỗi phiếu kho tính lại giá vốn đến kỳ hiện tại | Vừa |
| B | Script gọi REST API như `ReportDataset` | Không phải sửa service | Chạy ngoài app nên không gắn được vào ô tick; phải đăng nhập; không chia được NVKD | Thấp, nhưng không đáp ứng yêu cầu |
| C | Insert thẳng bằng SQL/EF, kể cả các bảng phái sinh | Rất nhanh | Phải tự tính sổ kho, giá vốn bình quân, số dư chạy, cấn trừ. Dễ lệch, và phải sửa theo mỗi lần đổi nghiệp vụ | Cao, rủi ro cao |

**Chọn A.**

## Recommended approach — thiết kế

### Luồng

1. Dialog **Thêm chi nhánh** có thêm ô tick, kèm dòng giải thích: tạo danh mục mẫu, đầu kỳ và khoảng 3 tháng phát sinh; chi nhánh được đánh dấu Demo; xóa bằng nút "Xóa chi nhánh demo".
2. `POST /api/branches` với `withSampleData: true`:
   - tạo chi nhánh như hiện nay (số chứng từ, quỹ QTM), đặt `IsDemo = true`, `SampleDataStatus = Running`;
   - đưa việc seed vào hàng đợi rồi trả về ngay.
3. Hosted service lấy việc khỏi hàng đợi và chạy seeder. **Mỗi bước là một transaction riêng**, giống một request thật, nên không giữ khóa advisory lâu, kể cả khóa trên lý do kho/quỹ dùng chung với chi nhánh thật. Seeder cập nhật `SampleDataProgress` sau mỗi nhóm chứng từ.
4. Trang Chi nhánh:
   - hiện nhãn **Demo · Đang tạo dữ liệu (x%)**, rồi **Demo** khi xong, hoặc **Demo · Lỗi** kèm thông báo;
   - tự hỏi lại server (khoảng 2 giây một lần) khi còn chi nhánh đang Running.
5. **Xóa chi nhánh demo:**
   - hộp xác nhận nói rõ chứng từ do học viên tự nhập cũng bị xóa, và báo trước số user sẽ được chuyển về Chi nhánh chính;
   - phải gõ đúng mã chi nhánh mới xóa được.

**Mốc thời gian** (tính theo ngày Việt Nam lúc tạo, ký hiệu T):
- Ngày đầu kỳ là ngày 1 của tháng T − 3. Ví dụ T = 14/10/2026 thì đầu kỳ là 01/07/2026.
- Chứng từ trải từ ngày đầu kỳ đến hôm qua, trong ngày làm việc (thứ 2 đến thứ 7), giờ hành chính.

### Model (1 migration)

| Entity | Thêm |
|---|---|
| `Branch` | `IsDemo` (bool), `SampleDataStatus` (enum None/Running/Ready/Failed, mặc định None), `SampleDataProgress` (int 0–100), `SampleDataError` (string?) |
| `Product`, `Customer` | `IsSample` (bool, mặc định false). Chỉ dùng để dọn dẹp, không lọc ở màn hình nào |

### API

- `POST /api/branches`: `CreateBranchRequest` thêm `WithSampleData` (bool, mặc định false). Dùng quyền `branches.manage` như hiện nay.
- `BranchDto` thêm `isDemo`, `sampleDataStatus`, `sampleDataProgress`, `sampleDataError`.
- `GET /api/branches/{id}/demo-delete-preview` trả số chứng từ và số user sẽ bị chuyển chi nhánh, để hiện trong hộp xác nhận. Endpoint này có cần hay không thì chốt trong plan.
- `DELETE /api/branches/{id}/demo` (quyền `branches.manage`):
  - chi nhánh không phải demo → 409;
  - chi nhánh đang `Running` → 409;
  - chi nhánh chính → 409, vì chi nhánh chính không bao giờ là demo.

### Seeder

**Danh tính tạm**
- `CurrentUser` và `CurrentBranch` được sửa để đọc một holder scoped (`IActingIdentity`) trước. Nếu holder được đặt thì dùng user, quyền và chi nhánh trong đó, bỏ qua HttpContext. Request HTTP thường không bị ảnh hưởng.
- Seeder đặt lại danh tính cho từng chứng từ, để `OwnerUserId` chia đều cho tối đa 3 user active có vai trò SALES. Không có ai thì dùng người tạo chi nhánh.

**Bộ sinh dữ liệu (generator)**
- Logic thuần, không đụng DB. Đầu vào: ngày đầu kỳ, ngày kết thúc, seed random (từ mã chi nhánh), danh mục mẫu, danh sách NVKD.
- Đầu ra: một **kế hoạch có thứ tự thời gian** gồm danh mục, kho/quỹ, đầu kỳ và danh sách chứng từ.
- Generator tự mô phỏng tồn kho theo kho, số dư quỹ, công nợ mở và hạn mức. Nhờ vậy không có phiếu nào làm âm kho, âm quỹ hay vượt hạn mức, và seeder chạy đúng cả khi chính sách đang ở chế độ Block. Seeder vẫn gửi `Acknowledge*` = true để không bị cảnh báo chặn lại.

**Bộ chạy (runner)**
- Lần lượt thực thi kế hoạch qua service: `WarehouseService`, `FundService`, `BranchService.SetOpeningDateAsync`, `OpeningStockService`, `OpeningDebtService`, `StockVoucherService`, `CashVoucherService`, `FundTransferService`, `DebtOffsetService`, `CreditLimit`…
- Phân bổ phiếu thu/chi cần id của dòng công nợ (`DebtEntry`) do chứng từ trước sinh ra. Runner tra id đó sau khi lưu chứng từ trước, hoặc kế hoạch tham chiếu bằng khóa logic.

**Danh mục mẫu**
- Tạo nếu chưa có, so theo mã. Chạy dưới một khóa advisory riêng để hai chi nhánh demo tạo cùng lúc không đụng nhau.
- Nếu mã `DM-…` đã bị một bản ghi không phải mẫu chiếm thì seed thất bại, báo lỗi rõ ràng.

**Lỗi và gián đoạn**
- Có exception thì status chuyển `Failed`, ghi `SampleDataError`. Dữ liệu đã tạo được giữ nguyên, người dùng xóa chi nhánh demo rồi tạo lại.
- Khi app khởi động, mọi chi nhánh còn `Running` chuyển thành `Failed` ("Bị gián đoạn do khởi động lại").

### Xóa chi nhánh demo

- Một transaction, xóa cứng bằng `IgnoreQueryFilters` + `ExecuteDelete` theo thứ tự khóa ngoại:
  1. activity, dòng và chứng từ (kho, thu chi, chuyển quỹ, bù trừ);
  2. `DebtSettlement`, `DebtEntry`, `OpeningDebt`, `CreditLimit`;
  3. `InventoryLedgerEntry`, `StockBalance`, `InventoryCostPeriod` (theo `ScopeKey` là chi nhánh hoặc các kho của nó), `OpeningStock`;
  4. `Fund`, `Warehouse`, `DocumentCounter`, `DocumentNumbering`;
  5. cuối cùng là `Branch`.
- User có `DefaultBranchId` là chi nhánh này được chuyển về Chi nhánh chính.
- Bỏ qua khóa sổ (`LockedUntil`).
- **Danh mục mẫu:** khi không còn chi nhánh demo nào, xóa các `Product`/`Customer` có `IsSample` mà không còn bị tham chiếu (chứng từ ở chi nhánh khác, báo giá, công nợ). Nhóm hàng `DM` cũng bị xóa khi không còn hàng nào thuộc nhóm. Mục nào còn bị tham chiếu thì giữ lại, kết quả trả về số mục được giữ.
- Notification hoặc push liên quan đến chứng từ của chi nhánh này (nếu có): plan cần kiểm tra.

### Báo cáo toàn công ty

Mọi truy vấn `ReportScope.Company` chỉ lấy các chi nhánh `!IsDemo`. Báo cáo theo chi nhánh, khi đang đứng ở chi nhánh demo, vẫn hiện đầy đủ.

### Frontend

- `branch-form-dialog.tsx`: ô tick, chỉ hiện ở chế độ tạo mới, kèm dòng giải thích.
- `branches-page.tsx`:
  - nhãn Demo kèm trạng thái và tiến độ;
  - polling khi có chi nhánh Running;
  - với chi nhánh demo: thay nút Xóa thường bằng "Xóa chi nhánh demo".
- Hộp xác nhận xóa demo: bắt gõ mã chi nhánh; hiện số chứng từ và số user bị chuyển.
- Nếu đang đứng ở chi nhánh demo mà xóa nó thì chuyển về chi nhánh khác. Dùng lại logic `leaveDeletedWorkingBranch` có sẵn.

### Edge cases

| Tình huống | Xử lý |
|---|---|
| Chính sách âm kho, hạn mức, âm quỹ ở chế độ Block | Generator không bao giờ vi phạm; có test riêng |
| Mẫu đánh số có `{THANG}`/`{NAM}`, reset theo tháng/năm | Chứng từ insert theo thứ tự thời gian nên số liền mạch theo kỳ |
| Hai chi nhánh demo tạo cùng lúc | Tạo danh mục dưới khóa advisory, so theo mã |
| Mã kho trùng toàn DB (`KHO01` của chi nhánh chính) | Mã kho demo là `{mã CN}-K1`, `{mã CN}-K2`; plan kiểm tra độ dài tối đa |
| Học viên tự nhập chứng từ vào chi nhánh demo | Bị xóa cùng chi nhánh, có cảnh báo trong hộp xác nhận |
| Học viên dùng hàng/khách mẫu ở chi nhánh thật | Mục đó được giữ lại khi dọn danh mục |
| Xóa khi đang Running | 409 |
| App khởi động lại giữa chừng | Status `Failed`, xóa rồi tạo lại |
| Seed chậm | Mục tiêu dưới 60 giây; nếu chậm hơn thì giảm khối lượng (xem section-01) |

## Kiểm thử

**Unit (generator)**
- Cùng seed thì ra cùng kế hoạch.
- Mọi ngày nằm trong [đầu kỳ, hôm qua] và theo thứ tự.
- Tồn kho, quỹ, hạn mức mô phỏng không bao giờ âm hay vượt.
- Có đủ loại chứng từ, và có nợ quá hạn ở mọi nhóm tuổi nợ.

**Integration** (Postgres `qldonhang_integtest`)
- Tạo với `withSampleData`, chờ `Ready`.
- `AssertInvariantsAsync` và `AssertCashDebtInvariantsAsync` đều qua.
- Báo cáo có số: Tồn kho, S07, S08, Sổ quỹ (S04a/S05), S12, Tuổi nợ, Quá hạn, Theo NVKD.
- Chạy được với chính sách ở chế độ Block.
- Chi nhánh demo thứ hai dùng lại danh mục mẫu.
- Báo cáo toàn công ty không tính chi nhánh demo.

**Integration (xóa chi nhánh demo)**
- Không còn dòng nào có `BranchId` của chi nhánh demo.
- Dữ liệu chi nhánh thật không đổi (so snapshot số dòng và tổng tiền trước và sau).
- User được chuyển về Chi nhánh chính.
- Danh mục mẫu chỉ bị xóa khi chi nhánh demo cuối cùng bị xóa; mục còn bị tham chiếu thì được giữ.
- Bị từ chối khi chi nhánh đang Running, hoặc không phải demo.

**Frontend**
- Ô tick gửi đúng `withSampleData`.
- Nhãn trạng thái đúng và polling dừng khi xong.
- Hộp xóa demo bắt gõ mã chi nhánh.

**Trình duyệt**
- Tạo chi nhánh demo, mở các báo cáo, xóa chi nhánh.
- Đo thời gian seed.

## Chia việc cho nhiều agent

| Lượt | Agent | Việc | File chạm tới |
|---|---|---|---|
| 1 (một agent, chạy trước) | Nền tảng | Migration và các field mới; `IActingIdentity` cho `CurrentUser`/`CurrentBranch`; DTO; **contract** của kế hoạch seed (record/interface) để các agent sau làm song song | Domain, Infrastructure (migration, configuration), WebApi Services, Branch DTO |
| 2 (song song) | A — Generator | Danh mục mẫu và kế hoạch chứng từ theo [section-01](section-01-dataset-spec.md), kèm unit test | `Application/Organization/SampleData/Generation/**` |
| | B — Runner | Hàng đợi, hosted service, thực thi kế hoạch qua service, status/tiến độ, xử lý khởi động lại, nối vào `BranchService.CreateAsync`; integration test | `Application/Organization/SampleData/Runner/**`, `Infrastructure/...HostedService`, `BranchService` |
| | C — Xóa demo và phạm vi | `DELETE /api/branches/{id}/demo`, dọn danh mục, chuyển user; lọc `!IsDemo` ở mọi truy vấn `ReportScope.Company`; integration test | `Application/Organization/SampleData/Cleanup/**`, các report service, `BranchesController` |
| | D — Frontend | Ô tick, nhãn và polling, hộp xóa demo; test | `frontend/src/pages/settings/**`, `frontend/src/features/branches/**` |
| 3 | Ghép | Gộp, chạy toàn bộ test, review, kiểm tra trên trình duyệt, cập nhật docs | — |

Runner (B) chỉ cần contract của kế hoạch từ lượt 1. Không phải chờ generator (A) xong mới bắt đầu, vì test của B dùng một kế hoạch nhỏ tự dựng.

## Open questions

1. Tên khách và NCC mẫu có thêm hậu tố **"(mẫu)"** không? Mặc định đề xuất là **có**: trên DB thật, nhân viên ở chi nhánh thật tìm khách sẽ không chọn nhầm. Đổi lại thì khi đào tạo trông bớt thật.
2. Có hiện badge "Mẫu" cạnh hàng/khách `IsSample` trong danh mục không? Mặc định đề xuất: chưa làm.
3. Hàng mẫu nằm chung một nhóm `DM`, nên báo cáo theo nhóm hàng ở chi nhánh demo chỉ có một nhóm. Cách khác là dùng các nhóm thật (EPS, XPS…): báo cáo theo nhóm trông thật hơn, nhưng hàng mẫu sẽ lẫn vào nhóm thật. Mặc định đề xuất: giữ nhóm `DM`, như phương án đã chọn.
4. Thời gian seed thực tế: đo trong plan. Nếu vượt 60 giây trên máy dev thì giảm số phiếu bán mỗi tháng.
5. **Luồng sự kiện chứng từ** trong brainstorm [mobile manager hub](../261014-0618-mobile-manager-hub/SUMMARY.md) (đợt 3: push, Telegram, Google Chat, Zalo) khi được làm phải **bỏ qua chi nhánh demo**. Nếu không, seeder sẽ bắn hàng trăm thông báo.
6. Báo cáo theo NVKD: user SALES thật sẽ đứng tên chứng từ mẫu trong chi nhánh demo. Báo cáo này chỉ tính theo chi nhánh nên không lẫn sang chi nhánh thật. Cần xác nhận lại trong plan.

## Next steps

1. `write-plan` từ brainstorm này. Plan chia phase theo bảng [Chia việc](#chia-việc-cho-nhiều-agent) và liệt kê đủ các truy vấn `ReportScope.Company`.
2. Thực thi bằng nhiều agent song song ở lượt 2.

Plan: [261014-1616-branch-sample-data](../../plans/archived/261014-1616-branch-sample-data/SUMMARY.md)
