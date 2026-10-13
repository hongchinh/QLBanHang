# Phase 02 — Backend: Cho phép override `CustomerName` trên báo giá

**Status:** [ ] pending
**Complexity:** S

## Objective

Cho client gửi `customerName` tùy chọn trong `UpsertQuotationRequest`. Backend dùng nó làm snapshot trên chứng từ; nếu không gửi thì fallback `customer.Name` (giữ hành vi cũ — backward-compatible).

PDF không cần đổi: `QuotationPdfRenderer` đã đọc từ `q.CustomerName` ([QuotationPdfRenderer.cs:80](../../../backend/src/OrderMgmt.Infrastructure/Pdf/QuotationPdfRenderer.cs#L80)).

## Files

- `backend/src/OrderMgmt.Application/Sales/Quotations/Models/QuotationDto.cs`
- `backend/src/OrderMgmt.Application/Sales/Quotations/Services/QuotationService.cs`
- `backend/src/OrderMgmt.Application/Sales/Quotations/Validators/QuotationValidators.cs`
- `backend/tests/OrderMgmt.IntegrationTests/Quotations/QuotationCrudTests.cs` (mở rộng)

## Tasks

1. Trong `QuotationDto.cs`, thêm vào `UpsertQuotationRequest`:
   ```csharp
   public string? CustomerName { get; set; }
   ```
   Giữ vị trí ngay sau `CustomerId` và trước `QuotationDate`.

2. Trong `QuotationService.CreateAsync` ([QuotationService.cs:131-151](../../../backend/src/OrderMgmt.Application/Sales/Quotations/Services/QuotationService.cs#L131)), đổi dòng:
   ```csharp
   CustomerName = customer.Name,
   ```
   thành:
   ```csharp
   CustomerName = string.IsNullOrWhiteSpace(request.CustomerName)
       ? customer.Name
       : request.CustomerName.Trim(),
   ```

3. Tương tự trong `UpdateAsync` ([QuotationService.cs:187](../../../backend/src/OrderMgmt.Application/Sales/Quotations/Services/QuotationService.cs#L187)):
   ```csharp
   quotation.CustomerName = string.IsNullOrWhiteSpace(request.CustomerName)
       ? customer.Name
       : request.CustomerName.Trim();
   ```
   Giữ nguyên các snapshot khác (`CustomerTaxCode`, `CustomerAddress`, ...) refresh từ master như cũ — chỉ cho override tên.

4. Trong `QuotationValidators.cs`, thêm rule cho `UpsertQuotationRequest`:
   ```csharp
   RuleFor(x => x.CustomerName)
       .MaximumLength(255).WithMessage("Tên khách hàng tối đa 255 ký tự.")
       .When(x => !string.IsNullOrWhiteSpace(x.CustomerName));
   ```
   (Cấu trúc xác nhận khi đọc file thực tế — có thể là `FluentValidation` hoặc validator pattern khác.)

5. Mở rộng `QuotationCrudTests.cs`:
   - Test `Create_WithCustomerNameOverride_PersistsOverride`: pass `customerName = "ABC Display"`, kiểm tra GET trả về `CustomerName == "ABC Display"`.
   - Test `Create_WithoutCustomerName_FallsBackToMaster`: không pass `customerName`, kiểm tra `CustomerName == customer.Name`.
   - Test `Update_ChangesCustomerNameOverride`: update với tên khác, verify persist.
   - Test `Update_WithEmptyCustomerName_FallsBackToMaster`: pass `customerName = "   "` → fallback master (vì `IsNullOrWhiteSpace`).

## Verification

```powershell
dotnet build d:\Projects\QLBanHang\backend\src\OrderMgmt.Application
dotnet test d:\Projects\QLBanHang\backend\tests\OrderMgmt.IntegrationTests\OrderMgmt.IntegrationTests.csproj --filter "FullyQualifiedName~QuotationCrud"
```

**Manual** (nếu WebApi đang chạy — chỉ rebuild Application/Infrastructure lib):
- Swagger `POST /api/quotations` với body có `"customerName": "Bên A"` → response trả về `customerName: "Bên A"`.
- Tải PDF của báo giá vừa tạo → in ra "Đơn vị: Bên A".

## Exit Criteria

- 4 integration tests mới pass + toàn bộ test cũ vẫn pass.
- Manual: tạo báo giá với `customerName` override, in PDF, verify mắt thường thấy tên override.
- Không thay đổi schema DB (chỉ dùng cột `CustomerName` đã có).
