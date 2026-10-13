# Phase 04 — Excel Export: Template + Renderer

**Status:** [ ] pending
**Complexity:** M

## Objective
Thêm 2 dòng "Tạm ứng" và "Còn lại" vào template Excel, cập nhật renderer để fill giá trị, và thêm automated test kiểm tra cell values.

## Files
- `backend/src/OrderMgmt.WebApi/templates/template_baogia.xlsx`
- `backend/src/OrderMgmt.Infrastructure/Excel/QuotationExcelRenderer.cs`
- `backend/tests/OrderMgmt.IntegrationTests/Quotations/QuotationExportTests.cs`

## Tasks

### 1. Backup template
```powershell
Copy-Item `
  "d:\Projects\QLBanHang\backend\src\OrderMgmt.WebApi\templates\template_baogia.xlsx" `
  "d:\Projects\QLBanHang\backend\src\OrderMgmt.WebApi\templates\template_baogia.xlsx.bak"
```

### 2. Mở template và xác định cấu trúc
Mở `template_baogia.xlsx` bằng Excel. Xác định:
- Item rows bắt đầu từ row 15 (2 sample rows: 15 và 16)
- `summaryRow` với 2 items = row 17
- **Đếm offset từ row 17 đến row "Tổng cộng"** — gọi là `TotalRowOffset`

Ví dụ nếu layout là:
```
Row 17: (summary — SUM formulas)
Row 18: Chiết khấu
Row 19: Vận chuyển
Row 20: Thuế
Row 21: Tổng cộng  ← offset = 4
```

Ghi lại giá trị `TotalRowOffset` để dùng ở Task 3.

### 3. Chèn 2 dòng mới vào template
Trong Excel, chèn 2 dòng **sau** dòng "Tổng cộng":
- **Dòng mới 1:** Label "Tạm ứng" (cột label); để trống cột G (renderer fill)
- **Dòng mới 2:** Label "Còn lại" (cột label); để trống cột G (renderer fill)
- Áp dụng cùng border/font style với dòng "Tổng cộng"
- **Không** dùng công thức Excel — renderer sẽ fill giá trị tính sẵn từ server

Sau khi chèn (ví dụ với `TotalRowOffset = 4`):
```
Row 21: Tổng cộng    ← offset từ summaryRow = 4
Row 22: Tạm ứng      ← AdvancePaymentRowOffset = 5
Row 23: Còn lại      ← RemainingBalanceRowOffset = 6
```
**Ghi lại offset thực tế** — sẽ dùng làm giá trị constants ở Task 4.

Lưu file và đóng Excel.

### 4. QuotationExcelRenderer.cs — thêm constants
Mở `QuotationExcelRenderer.cs`. Sau constant `SampleRowCount` (~dòng 12), thêm:
```csharp
// Offset từ summaryRow đến các dòng tổng trong template_baogia.xlsx.
// Nếu template thay đổi cấu trúc, cập nhật cả 2 constants này.
private const int AdvancePaymentRowOffset = 5;    // THAY bằng offset đo được ở Task 3
private const int RemainingBalanceRowOffset = 6;   // THAY bằng offset đo được ở Task 3
```

### 5. QuotationExcelRenderer.cs — thêm method FillSummaryTotals
Thêm method mới sau method `FillItemRows`:
```csharp
private static void FillSummaryTotals(IXLWorksheet ws, int summaryRow, QuotationDto q)
{
    ws.Cell(summaryRow + AdvancePaymentRowOffset, 7).SetValue((double)q.AdvancePayment);
    ws.Cell(summaryRow + RemainingBalanceRowOffset, 7).SetValue((double)(q.Total - q.AdvancePayment));
}
```

### 6. QuotationExcelRenderer.cs — gọi FillSummaryTotals
Trong method `RenderAsync(QuotationDto quotation, string templatePath, ...)`, sau dòng `FillItemRows(ws, quotation);`, thêm:
```csharp
FillSummaryTotals(ws, FirstSampleRow + quotation.Lines.Count, quotation);
```
`summaryRow = FirstSampleRow + lines.Count` — dùng `quotation.Lines.Count` ở đây là đúng vì `FillItemRows` không filter bỏ line nào.

### 7. QuotationExportTests.cs — thêm test kiểm tra cell values
Mở `QuotationExportTests.cs`. Thêm using `using ClosedXML.Excel;` nếu chưa có. Thêm test mới vào cuối class:

```csharp
[Fact]
public async Task Excel_AdvancePayment_WrittenToCorrectCells()
{
    var request = BuildRequest();
    request.AdvancePayment = 50_000m;
    var create = await _client.PostAsJsonAsync("/api/quotations", request);
    var created = await create.Content.ReadFromJsonAsync<ApiResponse<QuotationDto>>(TestJson.Options);
    var id = created!.Data!.Id;

    var response = await _client.GetAsync($"/api/quotations/{id}/excel");
    response.StatusCode.Should().Be(HttpStatusCode.OK);
    var bytes = await response.Content.ReadAsByteArrayAsync();

    using var wb = new XLWorkbook(new MemoryStream(bytes));
    var ws = wb.Worksheet(1);

    // summaryRow với 1 item line = FirstSampleRow + 1 = 16
    // AdvancePaymentRowOffset và RemainingBalanceRowOffset phải khớp constants trong renderer
    const int summaryRow = 16; // FirstSampleRow(15) + 1 line
    var advanceCell = ws.Cell(summaryRow + QuotationExcelRenderer.AdvancePaymentRowOffset, 7);
    var remainingCell = ws.Cell(summaryRow + QuotationExcelRenderer.RemainingBalanceRowOffset, 7);

    advanceCell.GetDouble().Should().Be(50_000d);
    // Total = 5 * 12000 = 60000, AdvancePayment = 50000, Remaining = 10000
    remainingCell.GetDouble().Should().Be(10_000d);
}
```

**Lưu ý:** Để test truy cập được constants, đổi `private const` thành `internal const` trong `QuotationExcelRenderer`:
```csharp
internal const int AdvancePaymentRowOffset = 5;
internal const int RemainingBalanceRowOffset = 6;
```

## Verification
```powershell
# Build để kiểm tra lỗi compile
cd d:\Projects\QLBanHang\backend
dotnet build src/OrderMgmt.Infrastructure
dotnet build src/OrderMgmt.WebApi

# Chạy export tests
dotnet test tests/OrderMgmt.IntegrationTests --filter "QuotationExportTests"
```

## Exit Criteria
- `QuotationExportTests` pass gồm test mới `Excel_AdvancePayment_WrittenToCorrectCells`
- Test dùng ClosedXML assert đúng giá trị cell — không chỉ dựa vào manual check
- Khi `AdvancePayment = 0`: cell tạm ứng = 0, cell còn lại = Total
- Không có regression trong `QuotationExportTests` cũ
