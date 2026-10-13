# Phase 01 — Backend: `/customers/search` + unaccent

**Status:** [ ] pending
**Complexity:** M

## Objective

Tạo endpoint `GET /api/customers/search` tối ưu cho typeahead: tìm trên 5 cột (`Code | Name | TaxCode | CompanyAddress | PhoneNumber`), hỗ trợ không dấu qua extension `unaccent`, mặc định chỉ trả về KH `Active`, response rút gọn.

## Files

- (new) `backend/src/OrderMgmt.Infrastructure/Persistence/Migrations/<timestamp>_EnableUnaccent.cs`
- `backend/src/OrderMgmt.Application/Catalog/Customers/Interfaces/ICustomerService.cs` (hoặc tên hiện hành)
- `backend/src/OrderMgmt.Application/Catalog/Customers/Services/CustomerService.cs`
- `backend/src/OrderMgmt.Application/Catalog/Customers/Models/CustomerDto.cs`
- `backend/src/OrderMgmt.WebApi/Controllers/CustomersController.cs`
- (new) `backend/tests/OrderMgmt.IntegrationTests/CustomerSearchTests.cs`

## Tasks

1. Tìm Migrations folder thực tế trong project (Glob `**/Migrations/*.cs` để confirm đường dẫn). Tạo migration **EnableUnaccent**:
   - `Up`: `migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS unaccent;");`
   - `Down`: không drop (extension có thể có dependency khác).
2. Trong `CustomerDto.cs`, thêm DTO:
   ```csharp
   public class CustomerSearchItemDto {
       public Guid Id { get; set; }
       public string Code { get; set; } = default!;
       public string Name { get; set; } = default!;
       public string? TaxCode { get; set; }
       public string? CompanyAddress { get; set; }
       public string? DefaultShippingAddress { get; set; }
       public string? ContactPerson { get; set; }
       public string? PhoneNumber { get; set; }
       public CustomerStatus Status { get; set; }
   }
   public class CustomerSearchRequest {
       public string Keyword { get; set; } = string.Empty;
       public bool ActiveOnly { get; set; } = true;
       public int Limit { get; set; } = 20;
   }
   ```
3. Trong `ICustomerService` thêm: `Task<List<CustomerSearchItemDto>> SearchAsync(CustomerSearchRequest request, CancellationToken ct = default);`
4. Implement `SearchAsync` trong `CustomerService`:
   - Trim keyword; nếu rỗng → return `[]`.
   - Clamp `Limit` vào `[1, 50]` (default 20).
   - Build `var pattern = $"%{EscapeLike(keyword)}%";`
   - Query base: `_db.Customers.AsNoTracking().Where(c => !c.IsDeleted)`.
   - Nếu `ActiveOnly`: `.Where(c => c.Status == CustomerStatus.Active)`.
   - Where clause dùng `EF.Functions.ILike` + `EF.Functions.Unaccent`:
     ```csharp
     query = query.Where(c =>
         EF.Functions.ILike(EF.Functions.Unaccent(c.Code), EF.Functions.Unaccent(pattern))
         || EF.Functions.ILike(EF.Functions.Unaccent(c.Name), EF.Functions.Unaccent(pattern))
         || (c.TaxCode != null && EF.Functions.ILike(EF.Functions.Unaccent(c.TaxCode), EF.Functions.Unaccent(pattern)))
         || (c.CompanyAddress != null && EF.Functions.ILike(EF.Functions.Unaccent(c.CompanyAddress), EF.Functions.Unaccent(pattern)))
         || (c.PhoneNumber != null && EF.Functions.ILike(EF.Functions.Unaccent(c.PhoneNumber), EF.Functions.Unaccent(pattern))));
     ```
   - **Lưu ý**: `EF.Functions.Unaccent` cần Npgsql 8+; nếu chưa có method này, tạo SQL function call: `EF.Functions.ILike(c.Name, pattern).ToLower()` rồi dùng raw SQL `FromSqlInterpolated` — verify khi implement.
   - Order: `.OrderBy(c => c.Code).ThenBy(c => c.Name)`.
   - Take `limit`, project sang `CustomerSearchItemDto`.
5. Thêm action trong `CustomersController.cs`:
   ```csharp
   [HttpGet("search")]
   public async Task<ActionResult<List<CustomerSearchItemDto>>> Search(
       [FromQuery] string keyword = "",
       [FromQuery] bool activeOnly = true,
       [FromQuery] int limit = 20,
       CancellationToken ct = default)
       => Ok(await _service.SearchAsync(new CustomerSearchRequest { Keyword = keyword, ActiveOnly = activeOnly, Limit = limit }, ct));
   ```
   Verify route prefix khớp `/api/customers/search`.
6. Viết `CustomerSearchTests.cs` (integration, copy pattern từ `CustomerCrudTests.cs`):
   - Test 1: keyword rỗng → empty list.
   - Test 2: search theo mã chính xác → trả về đúng KH.
   - Test 3: search theo tên có dấu "Công" → trả về KH chứa "Công" và "Cong" (sau khi seed).
   - Test 4: search không dấu "cong" → trả về KH có "Công" (xác nhận unaccent).
   - Test 5: `activeOnly=true` (default) ẩn KH Inactive.
   - Test 6: `activeOnly=false` trả về cả Inactive.
   - Test 7: `limit=2` clamp đúng.

## Verification

```powershell
# Apply migration (giả định project pattern dotnet ef migrations)
dotnet ef database update --project d:\Projects\QLBanHang\backend\src\OrderMgmt.Infrastructure --startup-project d:\Projects\QLBanHang\backend\src\OrderMgmt.WebApi

# Build & test
dotnet build d:\Projects\QLBanHang\backend\src\OrderMgmt.Application
dotnet build d:\Projects\QLBanHang\backend\src\OrderMgmt.Infrastructure
dotnet build d:\Projects\QLBanHang\backend\src\OrderMgmt.WebApi
dotnet test d:\Projects\QLBanHang\backend\tests\OrderMgmt.IntegrationTests\OrderMgmt.IntegrationTests.csproj --filter "FullyQualifiedName~CustomerSearch"
```

**Manual** (nếu WebApi đang chạy, không cần restart — chỉ rebuild lib đã đổi theo [[build-skip-when-app-running]]):
- Swagger UI: `GET /api/customers/search?keyword=cong&activeOnly=true&limit=10` → 200, array với items đúng schema.
- `GET /api/customers/search?keyword=&limit=10` → 200, array rỗng.

## Exit Criteria

- 7 integration tests pass.
- Manual Swagger trả về kết quả đúng cho 4 keyword: rỗng / có dấu / không dấu / không khớp.
- Migration apply không lỗi, idempotent (apply 2 lần OK).
