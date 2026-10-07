# Tech Lead Review — Inventory Round 1 (Đợt 1 — Kho)

> Plan: `docs/plans/261006-2259-inventory-round1/` (SUMMARY + phase-00…11) · Reviewed: 2026-10-07 00:52:31 · Vai trò: Tech Lead / Team Lead
> Phương pháp: 4 reviewer tự động độc lập (spec-fidelity, internal-consistency, backend-grounding, frontend-grounding — 71 finding) + một lượt đọc toàn bộ plan của tech lead, đối chiếu trực tiếp với code (12 finding). Các finding quan trọng được tech lead kiểm chứng lại trên code thật. Xem §10 về giới hạn.

## 1. Kết luận

**Verdict ban đầu: REQUEST CHANGES** → **sau khi sửa plan (2026-10-07): APPROVED FOR EXECUTION.**

Plan được viết rất kỹ: decision log D1–D26 rõ ràng, công thức có bảng số kỳ vọng tính tay (tôi đã tính lại toàn bộ Phase 03 — đúng hết), engine được tách thành calculator thuần + service có test bất biến, mỗi task có TDD/commit. Tuy vậy có **2 lỗi chặn** khiến code không chạy được nếu làm đúng theo plan, và một nhóm lỗi Major về đồng thời (concurrency), định giá báo cáo, chi nhánh làm việc và rollout.

| Mức | Số lượng | Trạng thái |
|---|---|---|
| 🔴 Blocker | 2 | Đã sửa trong plan |
| 🟠 Major | 17 | Đã sửa trong plan |
| 🟡 Minor | 33 | Đã sửa trong plan (một số chuyển thành quyết định/ghi chú) |
| ⚪ Nit | 5 | Đã sửa trong plan |

**5 việc quan trọng nhất (đã xử lý):**
1. **B1** — mọi `DateTimeOffset` đi vào Npgsql phải là UTC (D27).
2. **B2** — không dùng `HasDefaultValue(true)` cho bool non-nullable (D28).
3. **M1/M2** — optimistic concurrency phải luôn kiểm tra được (header luôn UPDATE; form giữ `version`; 409 → reset form) (D29).
4. **M5/M6/M7/M8** — engine giá vốn: định giá báo cáo theo phạm vi tính giá (D32), khóa cổng chi nhánh thay cho hàng nghìn advisory lock (D30), kiểm âm chỉ khi thao tác làm xấu đi (D31), đổi cờ VAT chỉ tại cuối kỳ (D33).
5. **M10/M11/M12** — frontend: chờ khôi phục chi nhánh làm việc trước khi render, không cache API kho trong service worker, quy đổi giờ VN ↔ instant nhất quán.

## 2. Điểm mạnh của plan

- Decision log D1–D26 biến gần như mọi khoảng trống của brainstorm thành quyết định có ghi chép (quyền NCC, quy tắc PartnerType, token đánh số, múi giờ VN, mã lỗi, một ngày tồn đầu cho mỗi kho).
- Công thức §4 được chuyển thành bảng giá trị kỳ vọng dùng chung cho backend calculator (Task 3.3) và preview frontend (Task 9.2). Đã tính lại: Out example (Tổng 1.160.036), In example (Tổng 7.453.513; InboundValue 5.081.293/1.448.168; ledger InValue 5.573.858/1.560.473), BQ tháng 53.333,3333, chuyển kỳ quý 750.000 — tất cả khớp.
- Kiến trúc engine sạch: calculator thuần (không EF/DI/clock), ledger có RunningQty, cost period là output snapshot, StockBalance là cache; advisory lock theo thứ tự cố định + counter upsert `ON CONFLICT` atomic.
- Test bất biến "sửa/xóa/hủy lùi ngày = nhập lại từ đầu" là ý tưởng rất tốt để chốt chất lượng engine.
- Hiểu đúng mô hình lỗi của `api-client` (non-2xx là AxiosError) và refactor `PricingQuantity` giữ nguyên hành vi báo giá (không làm tròn ở hàm chung).

## 3. Blocker — phải sửa trước khi thực thi

### B1 — `DateTimeOffset` +07:00 bị Npgsql từ chối khi ghi/so sánh với `timestamptz`
- **Vị trí:** SUMMARY D14; phase-03 Task 3.2 (`StartOfDay … // 00:00 at +07:00`); phase-04 Task 4.5 (`Vn()` parse +07:00), Task 4.6 (`PostedAt < VnTime.StartOfDay(...)`); phase-05 (VoucherAt, `StockAtRequest.At`); phase-06 Task 6.1 (`PostedAt = StartOfDay(OpeningDate)`), 6.4/6.5.
- **Bằng chứng:** Npgsql EF 9.0.4 (Npgsql 9.0.3), không bật `EnableLegacyTimestampBehavior`; Npgsql ≥ 6 ném "Cannot write DateTimeOffset with Offset=07:00:00 … only offset 0 (UTC) is supported" cho cả cột lẫn tham số. Code hiện tại luôn dùng `UtcNow` / `DateTimeKind.Utc`.
- **Tác động:** test Task 4.5 ném lỗi khi SaveChanges; mọi lần tính giá vốn ném lỗi ở tham số query; API nhận `VoucherAt` +07:00 trả 500; truy vấn `at`/stock-at khác nhau theo máy (VN dev box lỗi, Docker UTC chạy).
- **Đã sửa:** D27 (quy tắc UTC). `VnTime.StartOfDay/StartOfNextDay` trả instant UTC (test `Offset == TimeSpan.Zero`), `Vn()` trả `.ToUniversalTime()`, mọi input `DateTimeOffset` chuẩn hóa ở service, `IInventoryLedgerService` chuẩn hóa `PostedAt`, quy tắc ngày VN viết thành khoảng UTC trong query (EF không dịch được `ToVnDate`). Thêm test API gửi `VoucherAt` có offset +07:00.

### B2 — `HasDefaultValue(true)` trên bool non-nullable làm EF không bao giờ insert được `false`
- **Vị trí:** phase-02 Task 2.4 (`IsCustomer` `.HasDefaultValue(true)`), Task 2.5 (`TrackInventory` `HasDefaultValue`).
- **Bằng chứng:** EF Core coi `false` (CLR default) là sentinel → bỏ cột khỏi INSERT → DB lưu default `true` (cảnh báo `BoolWithDefaultWarning` của EF 9.0.15).
- **Tác động:** NCC thuần (`IsCustomer=false`) bị lưu thành khách hàng; hàng dịch vụ (`TrackInventory=false`) bị lưu thành theo dõi tồn → ghi ledger, kiểm âm, tính giá vốn sai, nhiều test fail không rõ nguyên nhân.
- **Đã sửa:** D28. Dùng `.HasDefaultValue(true).HasSentinel(true)` (hoặc chỉ đặt `defaultValue` trong migration cho dữ liệu cũ); test hồi quy tạo qua API với `isCustomer:false` / `trackInventory:false` → lưu `false`; convention cấm `HasDefaultValue(true)` cho bool non-nullable không kèm sentinel.

## 4. Major — sửa trước khi bắt đầu phase liên quan

### M1 — `xmin` không được kiểm tra khi chỉ sửa dòng (D16 bị vượt qua)
- **Vị trí:** phase-05 save rule 1, 6; Task 5.5. **Bằng chứng:** `AppDbContext.ApplyAudit` chỉ set `UpdatedAt` cho entry đã `Modified` (AppDbContext.cs:83-99); EF chỉ sinh `UPDATE … WHERE xmin=@orig` khi header thay đổi.
- **Tác động:** hai người sửa các dòng khác nhau (đổi kho dòng, ghi chú) đều lưu thành công, người sau ghi đè người trước, ledger bị đảo lại.
- **Đã sửa (D29):** update/cancel/restore/delete luôn đánh dấu header modified (set `UpdatedAt/UpdatedBy`) trước SaveChanges; thêm test "PUT chỉ đổi dòng, PUT thứ hai cùng version → 409".

### M2 — Sau 409, form cũ có thể lưu với version mới và ghi đè thay đổi của người khác
- **Vị trí:** phase-09 Task 9.3 (`toUpsertPayload(type, values, version?)`), Task 9.9 (409 → toast + refetch). **Bằng chứng:** form báo giá chỉ reset khi `id` đổi (quotation-form-page.tsx:413-417).
- **Đã sửa (D29):** `version` nằm trong form values; 409 → refetch và `form.reset(toFormDefaults(fresh))`, báo người dùng thay đổi cục bộ bị bỏ; background refetch chỉ reset khi form không dirty; test tương ứng.

### M3 — Ma trận phân quyền ẩn module mới `inventory`
- **Vị trí:** phase-01 Task 1.1 (`InventoryModule = "inventory"`); không phase frontend nào sửa `features/admin-roles`. **Bằng chứng:** `PermissionModule = 'system' | 'catalog' | 'sales' | 'report'`, `MODULE_ORDER` cố định (role-matrix-table.tsx:15-22, 47-53).
- **Tác động:** admin không thấy/không cấp/không thu hồi được 17 quyền `stock_in.*`, `stock_out.*`, `inventory.*` — kể cả bước "thu hồi quyền trước go-live".
- **Đã sửa:** thêm Task 7.6 (module `inventory` = "Kho" trong types, MODULE_LABEL, MODULE_ORDER, role-create-dialog + test).

### M4 — Rollout: D23 tự cấp quyền mâu thuẫn "ẩn tính năng trước go-live"; phụ thuộc DbSeeder không được nêu; seeder không atomic
- **Vị trí:** SUMMARY D23, Rollback "Feature exposure"; phase-01 Task 1.1; phase-02 Task 2.7/2.8. **Bằng chứng:** `Program.cs:260-267` chỉ chạy seeder khi `Database:AutoMigrateAndSeed=true` (base config `false`, README Railway `true`); permission/KHO01/lý do/HTTT/đánh số chỉ được tạo trong DbSeeder; `SeedPermissionsAsync` commit trước `SeedRolesAsync` (DbSeeder.cs:104-109).
- **Đã sửa (D39):** mục "Deploy & go-live checklist" trong SUMMARY (seeder phải chạy một lần mỗi deploy; nếu deploy production trước go-live thì thu hồi quyền kho của WAREHOUSE/MANAGER ngay sau deploy qua ma trận quyền, cấp lại khi go-live); Task 1.1 bọc seed permission + role trong một transaction; ACCOUNTANT không có default (admin cấp qua ma trận — câu hỏi mở §7).

### M5 — Giá trị tồn theo kho sai khi phạm vi tính giá = Chi nhánh (mặc định)
- **Vị trí:** phase-06 Task 6.4 (`Value = Σ(InValue − CostAmount)` theo cặp hàng–kho), Task 6.5. **Ví dụ:** KHO01 nhập 10 × 100.000, KHO02 nhập 10 × 200.000, BQ chi nhánh 150.000, xuất 10 từ KHO01 → KHO01 SL 0, giá trị −500.000; KHO02 2.000.000; chỉ tổng 1.500.000 đúng.
- **Đã sửa (D32):** Warehouse scope giữ tổng theo cặp; Branch scope: giá trị dòng = phân bổ giá trị phạm vi theo tỷ lệ SL (dư làm tròn vào dòng cuối theo mã kho; SL phạm vi = 0 → giá trị dòng 0, `TotalValue` vẫn gồm giá trị phạm vi). Thẻ kho có lọc kho dưới Branch scope trả cột giá trị lũy kế `null` (UI ghi "Giá trị tính theo chi nhánh"). Thêm test kịch bản trên.

### M6 — Đổi cấu hình / tính lại giá vốn: hàng nghìn advisory lock và race với lần ghi đầu tiên
- **Vị trí:** phase-06 Task 6.2/6.3 (`AcquireLocksAsync(branch, all products with ledger rows)`). **Bằng chứng:** PostgreSQL local `max_locks_per_transaction=64`, `max_connections=100` → ~6.400 slot dùng chung; giả định "hàng nghìn mặt hàng"; sản phẩm chưa có ledger không bị khóa nên lần ghi đầu tiên chạy song song tính theo cấu hình cũ.
- **Đã sửa (D30):** cổng khóa chi nhánh `inv-branch:{id}`: posting/tồn đầu lấy **shared** trước khóa sản phẩm; đổi cấu hình/tính lại lấy **exclusive** theo chi nhánh (không khóa từng sản phẩm); đọc `InventorySettings` sau khi lấy khóa; thứ tự cố định gate → sản phẩm → counter. Thêm test chờ khóa.

### M7 — Kiểm âm đếm cả âm có sẵn → chặn/cảnh báo cả thao tác sửa sai
- **Vị trí:** phase-04 Task 4.5 (`MinRunningQty`), 4.7. **Ví dụ:** Warn mặc định: xuất 10 khi tồn 0 (đã xác nhận); nhập bổ sung 4 lùi ngày → lại 422 "Âm 6"; sau khi đổi sang Block thì không nhập/hủy được để sửa.
- **Đã sửa (D31):** cặp chỉ bị tính là thiếu khi `newMin < 0` **và** (`newMin < oldMin` hoặc thời điểm âm đầu tiên sớm hơn); `oldMin` đọc trước khi xóa bút toán cũ. Test: nhập bù/hủy phiếu xuất làm giảm thiếu hụt qua được dưới Block. ⚖️ Cần BA xác nhận cách hiểu rule 8.

### M8 — Đổi `PurchaseCostIncludesVat` khi kỳ bị khóa một phần làm một kỳ trộn hai chính sách VAT
- **Vị trí:** SUMMARY D11; phase-06 Task 6.3 bước 3 và 5.
- **Đã sửa (D33):** đổi cờ VAT bị từ chối (400) trừ khi `LockedUntil` của mọi chi nhánh có ledger trống hoặc là ngày cuối kỳ theo `CostingPeriod` hiện tại; khi hợp lệ thì tính lại InValue từ `LockedUntil + 1` (= đầu kỳ). Test khóa giữa kỳ → 400.

### M9 — NCC thuần lọt qua các truy vấn `Customers` trực tiếp
- **Vị trí:** phase-02 Task 2.4 (grep chỉ tìm `ICustomerService`). **Bằng chứng:** `SearchService.cs:35-42` (global search không lọc vai trò), `QuotationService.EnsureCustomerAsync` (QuotationService.cs:867-871); header search điều hướng `/customers/{id}` → 404.
- **Đã sửa (D38):** lọc `IsCustomer` trong global search + nhóm NCC theo `suppliers.view`; báo giá từ chối đối tượng không phải KH; grep `\.Customers\b`; header search điều hướng NCC sang `/suppliers/{id}` (Task 8.4); test.

### M10 — Khôi phục chi nhánh làm việc chạy đua với query đầu tiên; cache/draft không theo chi nhánh
- **Vị trí:** phase-07 Task 7.2/7.3; phase-09 Task 9.8. **Bằng chứng:** `app-layout.tsx` render `<Outlet/>` ngay; query key không chứa chi nhánh; refresh thất bại không xóa branch store.
- **Đã sửa:** `useBranchContext()` trả `ready`, AppLayout chỉ render nội dung khi ready; đổi chi nhánh dùng `resetQueries()` và về trang danh sách của module; xóa branch store khi refresh thất bại; key draft có branch id; test.

### M11 — Service worker cache API theo URL, trộn dữ liệu giữa chi nhánh và người dùng (kể cả giá vốn)
- **Vị trí:** `frontend/src/sw.ts:11-19` (NetworkFirst mọi GET `/api/*`, timeout 8s). Plan không nhắc tới sw.ts.
- **Đã sửa:** Task 7.7: route matcher tách module testable, `NetworkOnly`/không cache cho `/api/stock-vouchers`, `/api/inventory`, `/api/reports`, `/api/warehouses`, `/api/branches`, `/api/me/`, `/api/suppliers`; xóa `api-cache` khi logout và đổi chi nhánh; test matcher.

### M12 — Quy đổi giờ chưa được đặc tả (instant API ↔ `datetime-local` ↔ ngày)
- **Vị trí:** phase-09 Task 9.3/9.9; phase-10. **Bằng chứng:** helper hiện có dựa trên UTC (`toISOString().slice(0,10)` ở quotation-form-page.tsx:1153, sales-revenue-page.tsx:31).
- **Tác động:** mỗi lần lưu, VoucherAt lùi 7 giờ (có thể sang ngày/kỳ trước, vào kỳ khóa); tồn tại thời điểm lệch 7 giờ trên server UTC.
- **Đã sửa:** `lib/vn-datetime.ts` (`toDateTimeLocalValue`, `fromDateTimeLocalValue`, `todayYmd`, `firstDayOfMonthYmd`) bắt buộc dùng trong toFormDefaults, defaults/stock-at/report `at`, ngày tồn đầu, khoảng thẻ kho; test round-trip; cấm copy `toISOString().slice(...)`.

### M13 — Sửa phiếu làm `PaidAmount` thanh toán một phần bị reset về `Total`
- **Đã sửa:** `toFormDefaults(voucher)` đặt `paidAmountTouched = voucher.paidAmount !== voucher.total`; trên update luôn gửi `paidAmount`; contract Phase 05 ghi rõ `null → Total` chỉ khi tạo mới; test.

### M14 — Chọn hàng qua catalog dialog bỏ qua quy tắc autofill
- **Bằng chứng:** product-catalog-list.tsx:119-129 và product-catalog-detail.tsx:41-53 tự dựng suggestion 8 trường; `ProductListItemDto` thiếu VAT/kích thước.
- **Đã sửa:** Task 2.5 bổ sung trường vào `ProductListItemDto`; Task 8.5 thêm mapper `toProductSuggestion` dùng ở cả hai đường chọn, các trường mới là required; test chọn qua catalog ở Task 9.5 và 10.1.

### M15 — Phase 00: cách ly DB test có lỗ hổng
- **Bằng chứng:** `HandoverExportTests.cs:241-243`, `QuotationExportTests.cs:196-198` là subclass `WebAppFactory(string)` gọi bằng `_pg.ConnectionString` → đi đường template-builder trên DB gốc (4 test này đang fail ở baseline); app ưu tiên `ConnectionStrings:DefaultConnection` (DependencyInjection.cs:31-32) nhưng factory chỉ override `Default`; query kiểm tra rò rỉ khớp cả DB gốc.
- **Đã sửa:** chuyển hai subclass sang `(PostgresFixture pg) : base(pg)`; factory set cả `Default` và `DefaultConnection`; expose `ConnectionString`; query kiểm tra dùng `LIKE 'qldonhang\_integtest\_%'` trừ template; test assert DB của clone.

### M16 — `npm run typecheck` không kiểm tra file nào
- **Bằng chứng:** `tsconfig.json` có `"files": []` + references; `tsc --noEmit` không `-b`/`-p` → thoát 0 mà không check gì. Mọi cổng typecheck của Phase 07–11 vô tác dụng.
- **Đã sửa:** Task 7.1 đổi script thành `tsc -p tsconfig.app.json --noEmit`; Verification mỗi phase frontend chạy thêm `npm run build`.

### M17 — Rate limit đăng nhập 5 lần/phút làm hỏng test nhiều client
- **Bằng chứng:** Program.cs:122-136 (PermitLimit 5, QueueLimit 0, một partition cho TestServer); Task 5.8 cần 5 client admin + login gốc.
- **Đã sửa:** Task 2.1 thêm helper tái dùng token admin (`CloneAdminClient`), helper tạo client theo quyền đăng nhập qua token có sẵn khi có thể; factory cấu hình nâng `PermitLimit` cho môi trường test; ghi trong Executor conventions.

## 5. Minor

| ID | Phase | Vấn đề | Xử lý |
|---|---|---|---|
| m1 | 05 | Thêm entity con qua navigation với Id có sẵn → EF sinh UPDATE → 409 "CONCURRENCY" giả | Bắt buộc `DbSet.Add`/`EntityState.Added` cho dòng, activity, OpeningStock; message 409 chung |
| m2 | 06 | DTO viết dạng public field (STJ bỏ qua) | Viết lại bằng auto-property |
| m3 | 06 | Reset DB trong test bất biến bị query filter giới hạn | `IgnoreQueryFilters()` + thứ tự xóa |
| m4 | 05/07–10 | Key `details` lệch hoa/thường; message `VALIDATION` chung chung tiếng Anh; `getApiError` đến muộn, không đọc ProblemDetails | camelCase PropertyNameResolver; `ValidationDomainException(errors, message)` tiếng Việt; chuyển `getApiError` + `formatApiErrorDetails` sang Task 7.2 |
| m5 | 09 | `excludeVoucherId: 'new'`; defaults async có thể xóa/unmount form mới | `isEdit ? id : undefined`; chỉ gate lần tải đầu, đổi ngày chỉ cập nhật "Số phiếu dự kiến" |
| m6 | 05/09 | `StockVoucherListResult` thiếu trường phân trang | Kế thừa `PagedResult<T>` |
| m7 | 02/05 | Thiếu DTO response (StockReason, PaymentMethod, InventorySettings, DocumentNumbering, Activity) | Định nghĩa rõ |
| m8 | 09 | Lịch sử hoạt động có API nhưng không hiển thị | Hook + panel lịch sử |
| m9 | 05 | Task 5.1 làm rule 1–9 → RED của 5.2 không thể fail | 5.1 chỉ happy path; 5.2 thêm khóa sổ/validation/quyền |
| m10 | 03–06 | Ký hiệu "@" 3 nghĩa; Task 6.3 trộn đơn giá/giá trị dòng | Quy ước `@` = giá trị dòng (engine), `×` = đơn giá (API) |
| m11 | 09 | Exit criteria không có test (409, VALIDATION, Ctrl+S, version/ack cho hủy/xóa/khôi phục) | Thêm test + nhãn nút theo hành động |
| m12 | 05 | D19: `CostPriceUpdatedOn` không bao giờ sửa lại được | D35: tính lại từ dòng nhập Active mới nhất khi nhập/sửa/hủy/khôi phục/xóa |
| m13 | 03 | D8 giữ giá trị khi SL 0 (không có dòng xuất) | Giữ D8, đưa vào go-live gate cần kế toán ký |
| m14 | 06/10 | Tồn đầu kỳ lộ giá trị với người không có `view_cost` | D36: quyền tồn đầu kỳ bao gồm xem giá trị tồn đầu; chỉnh smoke step 7 |
| m15 | 05 | Kiểm "active" chặn sửa phiếu cũ; chưa rõ bước nào cho hủy/khôi phục/xóa | Chỉ kiểm active cho dòng/kho mới hoặc đổi; bảng bước theo thao tác |
| m16 | SUMMARY | Go-live gates không được theo dõi | Mục "Go-live gates" |
| m17 | 04–06 | Thiếu test: bất biến sau mọi kịch bản, kỳ Năm, xóa/dời phiếu nhập, reset theo năm | Bổ sung |
| m18 | 00/11 | product-goals.md chỉ là thay đổi chưa commit; nguy cơ commit nhầm `source/` | Task 0.0 commit tài liệu nền; stage file tường minh |
| m19 | SUMMARY | "brainstorm thắng trừ D1–D24"; sai lệch cấu trúc chưa khai báo | D1–D39; D34 refinements; task text thắng về chi tiết hiện thực |
| m20 | 08 | Form NCC dùng lại form KH vẫn giữ tiêu đề/back link/tab báo giá | `role` điều khiển nhãn, back link; không có tab báo giá |
| m21 | 08 | Cờ vai trò bắt buộc làm vỡ schema test, lộ vào quick-add báo giá | `.default(true/false)`; prop ẩn ở quick-add |
| m22 | 09 | Bộ lọc trạng thái lưu không biểu diễn được "Tất cả" | Sentinel `all` → không gửi status |
| m23 | 09/10 | Mutation không invalidate báo cáo/tồn đầu/đối tượng | Root key `['inventory']`; invalidate chéo KH/NCC |
| m24 | 09/05 | `round0` ≠ AwayFromZero; `DiscountAmount` tay không giới hạn | `roundAwayFromZero`; validate 0 ≤ DiscountAmount ≤ Amount |
| m25 | 09/10 | Ô chọn hàng cần `products.view` | Ghi rõ tiền điều kiện quyền |
| m26 | 07 | Form user mặc định chi nhánh đầu → "Invalid uuid" | `defaultBranchId` optional (backend → chi nhánh chính) |
| m27 | 09/10 | Định dạng SL 2 số lẻ ẩn số m³ nhỏ | Formatter SL tồn tối đa 6 số lẻ |
| m28 | 08 | UI chưa phản ánh D2 cho đối tượng hai vai trò | Thông báo chỉ đọc + khóa nút lưu |
| m29 | 03 | AvgCost có thể âm sau giai đoạn âm kho | D37: tử số < 0 → dùng fallback |
| m30 | 04 | Files thiếu `InventoryTestBase.cs`; Task 4.1 cần helper đến sau | Bổ sung Files; Task 4.1 dùng `_productId` + truy vấn trực tiếp |
| m31 | 01 | `SingleAsync` trên user đã xóa mềm → 500 | `SingleOrDefaultAsync` → 401 |
| m32 | 04 | Độ chính xác D9 chưa áp cho mọi bảng | Chỉ định numeric cho OpeningStock/StockBalance/CostPeriod |
| m33 | 05–10 | Chi tiết contract nhỏ (nhãn tồn đầu, ScopeCount, `Version` action nullable, hook trùng tên) | Chốt từng điểm |

## 6. Nit

| ID | Vấn đề | Xử lý |
|---|---|---|
| n1 | Câu kiểm tra `xmin` trong migration dễ gây sửa sai | Diễn đạt lại (Npgsql không sinh DDL cho system column) |
| n2 | Quy ước thư mục trong SUMMARY lệch với đường dẫn phẳng trong phase | Cập nhật quy ước |
| n3 | Giả định "rebuild DB mỗi test" đã cũ; exit criterion Phase 02 về D4 | Sửa câu chữ |
| n4 | Mô tả toast/mock/try-catch/footer lệch với code | Sửa theo code thật |
| n5 | SQL rollback `LIKE 'stock_%'` (dấu `_` là wildcard); lệnh `&&` trên PowerShell 5.1 | Escape; ghi rõ dùng Git Bash |

## 7. Câu hỏi cần chốt (đã áp dụng phương án khuyến nghị, chờ xác nhận)

1. **D31 — Kiểm âm "chỉ khi làm xấu đi"** (M7): BA xác nhận cách hiểu rule 8 §5 brainstorm.
2. **D35 — Giá nhập gần nhất được tính lại khi sửa/hủy/xóa phiếu nhập** (m12): thay cho "không bao giờ hoàn lại" của D19.
3. **D36 — Người có quyền Tồn đầu kỳ được xem giá trị tồn đầu** (m14): hoặc chuyển quyền nhập tồn đầu cho kế toán.
4. **D8 — Giá trị dư khi SL về 0 mà kỳ không có phiếu xuất** (m13): kế toán ký xác nhận trước go-live.
5. **ACCOUNTANT** có quyền mặc định nào cho kho không (hiện: không — admin cấp qua ma trận quyền)?
6. **Deploy Round 1 lên production trước go-live?** Nếu có: thu hồi quyền kho của WAREHOUSE/MANAGER ngay sau deploy (D39).

## 8. Khuyến nghị tổ chức thực thi

- **Task 0.0** commit tài liệu nền (brainstorm, plan, product-goals, docs/SUMMARY) trước khi tạo nhánh; không bao giờ `git add -A` (thư mục `source/` là mã legacy chưa track).
- Làm trên nhánh `feat/inventory-round1`; tính năng ẩn sau quyền nên có thể merge về `main` theo mốc (sau Phase 02, sau Phase 06, sau Phase 10) để giảm xung đột với nhánh chính đang phát triển báo giá.
- Baseline trước khi làm (2026-10-07): backend 19 fail / 201 pass (danh sách trong `EXECUTION-REPORT.md` khi thực thi; 4 test PDF fail do lỗi M15 sẽ được Phase 00 sửa), frontend 3 fail / 229 pass. Các lỗi có sẵn khác nằm ngoài phạm vi plan.
- Cổng kiểm tra frontend dùng `npm run typecheck` (đã sửa script) **và** `npm run build`.

## 9. Checklist sửa plan theo file

- [x] **SUMMARY.md** — D27–D39, Assumptions (D1–D39, clone template), Executor conventions (UTC, bool default, `@`/`×`, rate limit, Git Bash, stage file tường minh, typecheck/build), Go-live gates, Deploy & go-live checklist, Rollback SQL escape, smoke test step 7.
- [x] **phase-00** — Task 0.0 commit tài liệu nền; hai subclass factory; `DefaultConnection`; expose `ConnectionString`; query kiểm tra rò rỉ.
- [x] **phase-01** — seed trong transaction; `SingleOrDefaultAsync`.
- [x] **phase-02** — sentinel bool; global search + báo giá lọc vai trò; DTO response; `ProductListItemDto` đủ trường; helper client không đăng nhập lại; PartnerType exit criterion.
- [x] **phase-03** — VnTime UTC; AvgCost guard; quy ước `@`.
- [x] **phase-04** — gate lock; `oldMin`/kiểm âm D31; chuẩn hóa `PostedAt`; Files; độ chính xác numeric; câu chữ xmin; Task 4.1 dùng `_productId`.
- [x] **phase-05** — header luôn modified; per-operation steps; active check; camelCase keys; DTO list kế thừa PagedResult; `Version` action nullable; Activity DTO; Add tường minh; 5.1/5.2 tách rõ; D35 cost price; test mới; UTC input.
- [x] **phase-06** — D32 định giá; D30 gate; D33 VAT; DTO auto-property; IgnoreQueryFilters; `×` notation; test mới.
- [x] **phase-07** — typecheck script; `getApiError` sớm; ready gate; resetQueries; Task 7.6 ma trận quyền; Task 7.7 service worker; user form optional branch.
- [x] **phase-08** — mapper suggestion; form NCC theo role; schema default; header search NCC; D2 UI; hiển thị message validation.
- [x] **phase-09** — vn-datetime; version trong form + reset khi 409; paidAmount; stock-at khi tạo mới; activity panel; filter `all`; root key; roundAwayFromZero; formatter SL; test bổ sung; nhãn nút theo hành động.
- [x] **phase-10** — vn-datetime; mapper; giá trị lũy kế null dưới Branch scope + lọc kho; formatter SL.
- [x] **phase-11** — sửa product-goals đúng dòng; docs mới (UTC, gate lock, D29–D33); build.

## 10. Phạm vi review & giới hạn

- **Đã chạy đầy đủ:** 4/12 góc review tự động (spec-fidelity, internal-consistency, backend-grounding, frontend-grounding) và một lượt đọc toàn bộ 12 phase + SUMMARY của tech lead, đối chiếu code (EF/Npgsql version, middleware, AppDbContext audit, DbSeeder, Program.cs, sw.ts, tsconfig, test fixtures, role matrix, rate limit).
- **Không chạy được:** 8 góc còn lại (concurrency, security, executability, rollout, UX, kiến trúc, công thức, ledger) và bước kiểm chứng phản biện tự động — tài khoản chạm giới hạn phiên/tuần của subagent lúc review. Tech lead đã tự rà các mảng này (công thức Phase 03 tính lại toàn bộ; khóa/đồng thời; định giá; rollout/seed; service worker), và tự kiểm chứng trên code mọi finding Blocker/Major ở trên.
- Môi trường kiểm tra: PostgreSQL 18.4 local, .NET SDK 10.0.401 (target net9.0), dotnet-ef 9.0.15, Node 24.18.
