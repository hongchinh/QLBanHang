# Phase 01 — Backend: Domain, Migration, DTO, Service

**Status:** [ ] pending
**Complexity:** M

## Objective
Thêm `AdvancePayment` vào domain entity, tạo EF migration, cập nhật EF configuration, DTO, validator và tất cả các điểm trong `QuotationService` cần persist/đọc field mới. Thêm integration tests kiểm tra behavior.

## Files
- `backend/src/OrderMgmt.Domain/Entities/Sales/Quotation.cs`
- `backend/src/OrderMgmt.Infrastructure/Persistence/Configurations/SalesConfiguration.cs`
- `backend/src/OrderMgmt.Infrastructure/Persistence/Migrations/20260521XXXXXX_AddQuotationAdvancePayment.cs` *(mới — tạo bằng EF CLI)*
- `backend/src/OrderMgmt.Application/Sales/Quotations/Models/QuotationDto.cs`
- `backend/src/OrderMgmt.Application/Sales/Quotations/Validators/QuotationValidators.cs`
- `backend/src/OrderMgmt.Application/Sales/Quotations/Services/QuotationService.cs`
- `backend/tests/OrderMgmt.IntegrationTests/Quotations/QuotationCrudTests.cs`

## Tasks

### 1. Domain entity
Mở `Quotation.cs`. Sau dòng `public decimal Total { get; set; }` (dòng 37), thêm:
```csharp
public decimal AdvancePayment { get; set; }
```

### 2. EF Configuration — column type
Mở `SalesConfiguration.cs`. Trong `QuotationConfiguration.Configure()`, sau dòng `b.Property(x => x.Total).HasColumnType("numeric(18,2)");` (dòng 31), thêm:
```csharp
b.Property(x => x.AdvancePayment).HasColumnType("numeric(18,2)");
```
**Thực hiện bước này TRƯỚC khi chạy `dotnet ef migrations add`** để migration gen ra đúng type `numeric(18,2)`, nhất quán với tất cả các monetary fields khác.

### 3. EF Core migration
Chạy lệnh tạo migration (từ thư mục `backend/`):
```powershell
dotnet ef migrations add AddQuotationAdvancePayment `
  --project src/OrderMgmt.Infrastructure `
  --startup-project src/OrderMgmt.WebApi `
  --output-dir Persistence/Migrations
```

Sau khi tạo xong, mở file migration mới và **luôn luôn** kiểm tra `Up()` có dạng:
```csharp
migrationBuilder.AddColumn<decimal>(
    name: "advance_payment",
    table: "quotations",
    type: "numeric(18,2)",
    nullable: false,
    defaultValue: 0m);
```
EF PostgreSQL provider **không tự thêm `defaultValue`** cho non-nullable decimal. **Luôn thêm tay `defaultValue: 0m`** — nếu thiếu, migration sẽ fail trên DB đang có data vì PostgreSQL không biết fill gì cho rows cũ.

### 4. QuotationDto — thêm field đọc
Mở `QuotationDto.cs`. Sau dòng `public decimal Total { get; set; }`, thêm:
```csharp
public decimal AdvancePayment { get; set; }
```

### 5. UpsertQuotationRequest — thêm field ghi
Trong cùng file `QuotationDto.cs`, class `UpsertQuotationRequest`. Sau dòng `public decimal Freight { get; set; }`, thêm:
```csharp
public decimal AdvancePayment { get; set; }
```

### 6. Validator — thêm rule
Mở `QuotationValidators.cs`. Trong `UpsertQuotationRequestValidator`, sau dòng `RuleFor(x => x.Freight).GreaterThanOrEqualTo(0);` (dòng 61), thêm:
```csharp
RuleFor(x => x.AdvancePayment).GreaterThanOrEqualTo(0);
```

### 7. QuotationService.CreateAsync — persist
Trong `QuotationService.cs`, method `CreateAsync` (~dòng 425–450), object initializer `new Quotation { ... }`.
Sau dòng `Freight = request.Freight,`, thêm:
```csharp
AdvancePayment = request.AdvancePayment,
```

### 8. QuotationService.UpdateAsync — persist
Trong `UpdateAsync` (~dòng 480–505), sau dòng `quotation.Freight = request.Freight;`, thêm:
```csharp
quotation.AdvancePayment = request.AdvancePayment;
```

### 9. QuotationService.MapToDto — đọc
Trong `MapToDto` (~dòng 994), sau dòng `Total = q.Total,`, thêm:
```csharp
AdvancePayment = q.AdvancePayment,
```

### 10. Clone — xác nhận không copy
Trong `CloneAsync` (~dòng 625–647), object initializer `new Quotation { ... }` **không** có dòng `AdvancePayment`. C# default = 0 nên clone sẽ tự động có `AdvancePayment = 0`. Không cần thêm gì.

### 11. Integration tests
Mở `QuotationCrudTests.cs`. Thêm 3 test methods vào **cuối class**:

```csharp
[Fact]
public async Task Create_WithAdvancePayment_PersistsValue()
{
    var request = BuildRequest();
    request.AdvancePayment = 50_000m;

    var create = await _client.PostAsJsonAsync("/api/quotations", request);
    create.StatusCode.Should().Be(HttpStatusCode.OK);
    var created = await create.Content.ReadFromJsonAsync<ApiResponse<QuotationDto>>(TestJson.Options);

    var get = await _client.GetFromJsonAsync<ApiResponse<QuotationDto>>(
        $"/api/quotations/{created!.Data!.Id}", TestJson.Options);
    get!.Data!.AdvancePayment.Should().Be(50_000m);
}

[Fact]
public async Task Update_ChangesAdvancePayment()
{
    var create = await _client.PostAsJsonAsync("/api/quotations", BuildRequest());
    var created = await create.Content.ReadFromJsonAsync<ApiResponse<QuotationDto>>(TestJson.Options);
    var id = created!.Data!.Id;

    var update = BuildRequest();
    update.AdvancePayment = 30_000m;
    var put = await _client.PutAsJsonAsync($"/api/quotations/{id}", update);
    put.StatusCode.Should().Be(HttpStatusCode.OK);

    var get = await _client.GetFromJsonAsync<ApiResponse<QuotationDto>>(
        $"/api/quotations/{id}", TestJson.Options);
    get!.Data!.AdvancePayment.Should().Be(30_000m);
}

[Fact]
public async Task Clone_DoesNotCopyAdvancePayment()
{
    var request = BuildRequest();
    request.AdvancePayment = 100_000m;
    var create = await _client.PostAsJsonAsync("/api/quotations", request);
    var created = await create.Content.ReadFromJsonAsync<ApiResponse<QuotationDto>>(TestJson.Options);
    var id = created!.Data!.Id;

    var clone = await _client.PostAsJsonAsync($"/api/quotations/{id}/clone", new { });
    clone.StatusCode.Should().Be(HttpStatusCode.OK);
    var cloned = await clone.Content.ReadFromJsonAsync<ApiResponse<QuotationDto>>(TestJson.Options);
    cloned!.Data!.AdvancePayment.Should().Be(0m);
}
```

## Verification
```powershell
# Build toàn bộ backend
cd d:\Projects\QLBanHang\backend
dotnet build src/OrderMgmt.WebApi

# Chạy integration tests (đảm bảo TEST_DB_CONNECTION ≠ dev DB)
dotnet test tests/OrderMgmt.IntegrationTests --filter "QuotationCrudTests"
```

## Exit Criteria
- `dotnet build` không có error
- 3 test mới pass
- Không có test regression trong `QuotationCrudTests`
