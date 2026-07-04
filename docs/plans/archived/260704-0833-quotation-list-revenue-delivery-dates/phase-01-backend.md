# Phase 01 — Backend DTO, Service, Tests

**Status:** [ ] pending
**Complexity:** M

## Objective

Thêm `DeliveryDate` và `RevenueDate` vào `QuotationListItemDto`; thêm `RevenueDateFrom/To` và `DeliveryDateFrom/To` vào `QuotationListRequest`; cập nhật `QuotationService.ListAsync` để populate + filter theo hai trường mới; viết integration tests.

## Files

- `backend/src/OrderMgmt.Application/Sales/Quotations/Models/QuotationDto.cs`
- `backend/src/OrderMgmt.Application/Sales/Quotations/Services/QuotationService.cs`
- `backend/src/OrderMgmt.Application/Reports/Common/RevenueFilterHelper.cs`
- `backend/tests/OrderMgmt.IntegrationTests/Quotations/QuotationListFilterTests.cs`

## Tasks

### Task 1 — Thêm fields vào `QuotationListItemDto` và `QuotationListRequest`

1. **Viết failing test:** Trong `QuotationListFilterTests.cs`, thêm test:
   ```csharp
   [Fact]
   public async Task List_item_includes_delivery_date_and_revenue_date()
   {
       var req = BuildRequest();
       req.DeliveryDate = new DateOnly(2026, 6, 15);
       var res = await _client.PostAsJsonAsync("/api/quotations", req);
       res.EnsureSuccessStatusCode();
       var created = await res.Content.ReadFromJsonAsync<ApiResponse<QuotationDto>>(TestJson.Options);
       var id = created!.Data!.Id;

       var listRes = await _client.GetFromJsonAsync<ApiResponse<QuotationListResult>>(
           "/api/quotations?pageSize=100", TestJson.Options);

       var item = listRes!.Data!.Items.Single(x => x.Id == id);
       item.DeliveryDate.Should().Be(new DateOnly(2026, 6, 15));
       item.RevenueDate.Should().NotBeNull(); // QuotationDate mode = ngày báo giá
   }
   ```
   Chạy: `cd backend && dotnet test tests/OrderMgmt.IntegrationTests --filter "List_item_includes_delivery_date_and_revenue_date"`
   Expected: **FAIL** — compile error vì `DeliveryDate` và `RevenueDate` chưa tồn tại trên `QuotationListItemDto`.

2. **Run to verify fail:** Xác nhận lỗi là compile error / property-not-found.

3. **Implement:**

   Trong `QuotationDto.cs`, thêm vào class `QuotationListItemDto` (sau `CreatedAt`):
   ```csharp
   public DateOnly? DeliveryDate { get; set; }
   public DateOnly? RevenueDate { get; set; }
   ```

   Trong `QuotationDto.cs`, thêm vào class `QuotationListRequest` (sau `OwnerUserIds`):
   ```csharp
   public DateOnly? RevenueDateFrom { get; set; }
   public DateOnly? RevenueDateTo { get; set; }
   public DateOnly? DeliveryDateFrom { get; set; }
   public DateOnly? DeliveryDateTo { get; set; }
   ```

4. **Run to verify pass:** `dotnet test tests/OrderMgmt.IntegrationTests --filter "List_item_includes_delivery_date_and_revenue_date"`
   Expected: **FAIL** — test compile được, nhưng `item.DeliveryDate` sẽ là null vì service chưa populate.

5. **Commit:** Chưa commit — tiếp tục Task 2.

---

### Task 2 — Cập nhật `QuotationService.ListAsync` để populate `DeliveryDate` và `RevenueDate`

1. **Test đã có** từ Task 1 (đang fail tại assertion `item.DeliveryDate.Should().Be(...)`).

2. **Implement trong `QuotationService.cs`:**

   Đầu method `ListAsync`, sau khi khai báo `canViewCost`, đọc `dateMode` qua helper đã có sẵn:
   ```csharp
   var dateMode = await RevenueFilterHelper.GetDateModeAsync(_db, ct);
   ```
   > `RevenueFilterHelper.GetDateModeAsync` đã đọc `QuotationSystemSettings` với `AsNoTracking` và trả fallback `QuotationDate` — không cần viết lại.

   Trong Select projection (sau `CreatedAt = q.CreatedAt`), thêm:
   ```csharp
   DeliveryDate = q.DeliveryDate,
   RevenueDate = dateMode == RevenueDateField.ConfirmedAt
       ? (q.ConfirmedAt.HasValue ? (DateOnly?)DateOnly.FromDateTime(q.ConfirmedAt.Value) : null)
       : dateMode == RevenueDateField.AccountingConfirmedAt
           ? (q.AccountingConfirmedAt.HasValue ? (DateOnly?)DateOnly.FromDateTime(q.AccountingConfirmedAt.Value) : null)
           : (DateOnly?)q.QuotationDate,
   ```

   > **Note về EF Core:** `DateOnly.FromDateTime(DateTime)` không dịch sang SQL trong EF Core < 8. Nếu build/test lỗi, dùng phương án dự phòng: trả `DateTime?` raw cho `RevenueDate` (đổi type thành `DateTime?`) rồi convert sau khi `ToListAsync`. Xem chi tiết ở mục "Phương án dự phòng" bên dưới.

3. **Run to verify pass:** `dotnet test tests/OrderMgmt.IntegrationTests --filter "List_item_includes_delivery_date_and_revenue_date"`
   Expected: **PASS**.

4. **Commit:**
   ```
   git add backend/src/OrderMgmt.Application/Sales/Quotations/Models/QuotationDto.cs
   git add backend/src/OrderMgmt.Application/Sales/Quotations/Services/QuotationService.cs
   git add backend/tests/OrderMgmt.IntegrationTests/Quotations/QuotationListFilterTests.cs
   git commit -m "feat: add DeliveryDate and RevenueDate to QuotationListItemDto"
   ```

#### Phương án dự phòng nếu EF Core không dịch `DateOnly.FromDateTime`:

Thay vì dùng `DateOnly?` cho `RevenueDate` trong projection, dùng `DateTime?`:
1. Thêm helper field tạm `RevenueDateRaw = DateTime?` vào DTO (hoặc dùng anonymous type).
2. Sau `ToListAsync`, map bằng `.Select(dto => { dto.RevenueDate = dto.RevenueDateRaw.HasValue ? DateOnly.FromDateTime(dto.RevenueDateRaw.Value) : null; return dto; })`.
3. Hoặc đơn giản hơn: đổi kiểu `RevenueDate` trong DTO thành `DateTime?` và để frontend format.

---

### Task 3 — Thêm filter logic cho `RevenueDateFrom/To` và `DeliveryDateFrom/To`

1. **Viết failing tests:** Thêm vào `QuotationListFilterTests.cs`:
   ```csharp
   [Fact]
   public async Task List_filter_by_delivery_date_range_returns_matching_quotations()
   {
       // Tạo 2 báo giá: 1 có DeliveryDate trong range, 1 ngoài range.
       var req1 = BuildRequest();
       req1.DeliveryDate = new DateOnly(2026, 6, 10);
       var r1 = await _client.PostAsJsonAsync("/api/quotations", req1);
       r1.EnsureSuccessStatusCode();
       var id1 = (await r1.Content.ReadFromJsonAsync<ApiResponse<QuotationDto>>(TestJson.Options))!.Data!.Id;

       var req2 = BuildRequest();
       req2.DeliveryDate = new DateOnly(2026, 8, 20);
       var r2 = await _client.PostAsJsonAsync("/api/quotations", req2);
       r2.EnsureSuccessStatusCode();
       var id2 = (await r2.Content.ReadFromJsonAsync<ApiResponse<QuotationDto>>(TestJson.Options))!.Data!.Id;

       var res = await _client.GetFromJsonAsync<ApiResponse<QuotationListResult>>(
           "/api/quotations?deliveryDateFrom=2026-06-01&deliveryDateTo=2026-06-30&pageSize=100",
           TestJson.Options);

       res!.Data!.Items.Should().Contain(x => x.Id == id1);
       res.Data.Items.Should().NotContain(x => x.Id == id2);
   }

   [Fact]
   public async Task List_filter_by_revenue_date_range_uses_system_date_mode()
   {
       // Default mode = QuotationDate → filter theo quotationDate.
       var req1 = BuildRequest();
       req1.QuotationDate = new DateOnly(2025, 3, 15);
       var r1 = await _client.PostAsJsonAsync("/api/quotations", req1);
       r1.EnsureSuccessStatusCode();
       var id1 = (await r1.Content.ReadFromJsonAsync<ApiResponse<QuotationDto>>(TestJson.Options))!.Data!.Id;

       var req2 = BuildRequest();
       req2.QuotationDate = new DateOnly(2025, 5, 10);
       var r2 = await _client.PostAsJsonAsync("/api/quotations", req2);
       r2.EnsureSuccessStatusCode();
       var id2 = (await r2.Content.ReadFromJsonAsync<ApiResponse<QuotationDto>>(TestJson.Options))!.Data!.Id;

       var res = await _client.GetFromJsonAsync<ApiResponse<QuotationListResult>>(
           "/api/quotations?revenueDateFrom=2025-03-01&revenueDateTo=2025-03-31&pageSize=100",
           TestJson.Options);

       res!.Data!.Items.Should().Contain(x => x.Id == id1);
       res.Data.Items.Should().NotContain(x => x.Id == id2);
   }

   [Fact]
   public async Task List_filter_by_revenue_date_excludes_cancelled_quotations()
   {
       // Báo giá có QuotationDate trong range nhưng đã bị hủy → phải bị loại khỏi kết quả.
       var req = BuildRequest();
       req.QuotationDate = new DateOnly(2025, 4, 10);
       var r = await _client.PostAsJsonAsync("/api/quotations", req);
       r.EnsureSuccessStatusCode();
       var id = (await r.Content.ReadFromJsonAsync<ApiResponse<QuotationDto>>(TestJson.Options))!.Data!.Id;

       // Hủy báo giá (dùng action name đúng từ QuotationAction enum — xem TransitionQuotationRequest).
       var cancelRes = await _client.PostAsJsonAsync(
           $"/api/quotations/{id}/transition",
           new { Action = "Cancel" });
       cancelRes.EnsureSuccessStatusCode();

       var res = await _client.GetFromJsonAsync<ApiResponse<QuotationListResult>>(
           "/api/quotations?revenueDateFrom=2025-04-01&revenueDateTo=2025-04-30&pageSize=100",
           TestJson.Options);

       res!.Data!.Items.Should().NotContain(x => x.Id == id);
   }
   ```
   Chạy: `dotnet test tests/OrderMgmt.IntegrationTests --filter "List_filter_by_delivery_date_range_returns_matching_quotations|List_filter_by_revenue_date_range_uses_system_date_mode|List_filter_by_revenue_date_excludes_cancelled_quotations"`
   Expected: **FAIL** — filter chưa được apply, cả các record đều xuất hiện.

2. **Run to verify fail.**

3. **Extend `RevenueFilterHelper.cs`** — thêm method mới `ApplyRevenueDateRangeFilter` (hỗ trợ optional bounds và loại bỏ báo giá đã hủy — khác với `ApplyRevenueFilter` hiện tại vốn dùng cho dashboard với from/to bắt buộc):

   ```csharp
   public static IQueryable<Quotation> ApplyRevenueDateRangeFilter(
       IQueryable<Quotation> q, string dateMode, DateOnly? from, DateOnly? to)
   {
       if (!from.HasValue && !to.HasValue) return q;

       switch (dateMode)
       {
           case RevenueDateField.ConfirmedAt:
           {
               var fromDt = from?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
               var toDt   = to?.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
               return q.Where(x =>
                   x.ConfirmedAt != null && x.CancelledAt == null
                   && (!fromDt.HasValue || x.ConfirmedAt >= fromDt.Value)
                   && (!toDt.HasValue   || x.ConfirmedAt < toDt.Value));
           }
           case RevenueDateField.AccountingConfirmedAt:
           {
               var fromDt = from?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
               var toDt   = to?.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
               return q.Where(x =>
                   x.AccountingConfirmedAt != null && x.CancelledAt == null
                   && (!fromDt.HasValue || x.AccountingConfirmedAt >= fromDt.Value)
                   && (!toDt.HasValue   || x.AccountingConfirmedAt < toDt.Value));
           }
           default: // QuotationDate — luôn có giá trị; loại bỏ đã hủy để nhất quán với dashboard
               return q.Where(x =>
                   x.CancelledAt == null
                   && (!from.HasValue || x.QuotationDate >= from.Value)
                   && (!to.HasValue   || x.QuotationDate <= to.Value));
       }
   }
   ```

   > **Tại sao dùng `< toDt+1day` thay vì `<= TimeOnly.MaxValue`:** pattern exclusive midnight là chuẩn EF Core và nhất quán với `ApplyRevenueFilter` hiện có. `TimeOnly.MaxValue` (23:59:59.9999999) có thể miss sub-second timestamps.
   >
   > **Tại sao thêm `CancelledAt == null`:** báo giá bị hủy sau khi xác nhận vẫn có `ConfirmedAt` set — filter phải loại chúng ra (nhất quán với dashboard). Xem assumption trong SUMMARY.

4. **Implement trong `QuotationService.ListAsync`** (sau block `OwnerUserIds` filter, trước aggregate query):

   ```csharp
   // Revenue date filter — delegate sang RevenueFilterHelper để tập trung logic revenue semantics.
   query = RevenueFilterHelper.ApplyRevenueDateRangeFilter(
       query, dateMode, request.RevenueDateFrom, request.RevenueDateTo);

   // Delivery date filter.
   if (request.DeliveryDateFrom.HasValue)
       query = query.Where(q => q.DeliveryDate >= request.DeliveryDateFrom.Value);
   if (request.DeliveryDateTo.HasValue)
       query = query.Where(q => q.DeliveryDate <= request.DeliveryDateTo.Value);
   ```

5. **Run to verify pass:** `dotnet test tests/OrderMgmt.IntegrationTests --filter "List_filter_by_delivery_date_range_returns_matching_quotations|List_filter_by_revenue_date_range_uses_system_date_mode|List_filter_by_revenue_date_excludes_cancelled_quotations"`
   Expected: **PASS**.

6. **Run full filter test suite:**
   ```bash
   cd backend && dotnet test tests/OrderMgmt.IntegrationTests --filter "ClassName~QuotationListFilter"
   ```
   Expected: tất cả tests pass (không có regression).

7. **Commit:**
   ```
   git add backend/src/OrderMgmt.Application/Reports/Common/RevenueFilterHelper.cs
   git add backend/src/OrderMgmt.Application/Sales/Quotations/Services/QuotationService.cs
   git add backend/tests/OrderMgmt.IntegrationTests/Quotations/QuotationListFilterTests.cs
   git commit -m "feat: add revenue date and delivery date filters to quotation list"
   ```

## Verification

```bash
cd backend && dotnet test tests/OrderMgmt.IntegrationTests --filter "ClassName~QuotationListFilter"
```

Expected: tất cả tests pass.

## Exit Criteria

- `QuotationListItemDto` có fields `DeliveryDate` và `RevenueDate`.
- `QuotationListRequest` có 4 param mới: `RevenueDateFrom`, `RevenueDateTo`, `DeliveryDateFrom`, `DeliveryDateTo`.
- `QuotationService.ListAsync` populate `DeliveryDate` và `RevenueDate` đúng dựa vào `dateMode`.
- Filter `revenueDateFrom/To` và `deliveryDateFrom/To` hoạt động đúng.
- Tất cả integration tests trong `QuotationListFilterTests` pass.
