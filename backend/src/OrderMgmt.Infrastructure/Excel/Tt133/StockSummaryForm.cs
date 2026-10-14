using ClosedXML.Excel;
using OrderMgmt.Application.Inventory.Reports.Models;
using OrderMgmt.Application.Tt133.Models;

namespace OrderMgmt.Infrastructure.Excel.Tt133;

/// S07-DNN, Bảng tổng hợp chi tiết vật liệu, dụng cụ, sản phẩm, hàng hóa: one landscape sheet.
public static class StockSummaryForm
{
    private static readonly string[] Groups = ["Tồn đầu kỳ", "Nhập trong kỳ", "Xuất trong kỳ", "Tồn cuối kỳ"];

    public static void Write(XLWorkbook workbook, Tt133FormContext context, StockSummaryReportDto report,
        string warehouseLabel, string? productGroupName, string? search)
    {
        // Without cost permission, or for one warehouse under Branch costing, the screen shows quantities only.
        var showValues = report.CanViewCost && !report.ValuesAtScopeOnly;

        var sheet = workbook.AddWorksheet("S07");
        var builder = new Tt133FormBuilder(sheet, context, BuildColumns(showValues), landscape: true);
        builder.WriteHeaderBlock("S07-DNN");

        var parameterLines = new List<string> { $"Kho: {warehouseLabel}" };
        if (!string.IsNullOrWhiteSpace(productGroupName)) parameterLines.Add($"Nhóm hàng: {productGroupName}");
        if (search is not null) parameterLines.Add($"Điều kiện lọc: mã hoặc tên chứa \"{search}\"");
        builder.WriteTitle(
            "BẢNG TỔNG HỢP CHI TIẾT VẬT LIỆU, DỤNG CỤ, SẢN PHẨM, HÀNG HÓA",
            Tt133FormBuilder.PeriodLine(report.From, report.To),
            parameterLines.ToArray());

        if (report.ValuesAtScopeOnly) builder.WriteNote("Giá trị tính theo chi nhánh");
        else if (showValues && report.IsProvisional) builder.WriteNote("Giá vốn tạm tính");

        builder.WriteTableHeader();

        var number = 0;
        foreach (var row in report.Rows)
        {
            if (showValues)
                builder.WriteRow(++number, row.ProductCode, row.ProductName, row.UnitName,
                    row.OpeningQty, row.OpeningValue,
                    row.InQty, row.InValue,
                    row.OutQty, row.OutValue,
                    row.ClosingQty, row.ClosingValue);
            else
                builder.WriteRow(++number, row.ProductCode, row.ProductName, row.UnitName,
                    row.OpeningQty, row.InQty, row.OutQty, row.ClosingQty);
        }

        if (showValues)
            builder.WriteSummaryRow("Cộng",
                (6, report.TotalOpeningValue),
                (8, report.TotalInValue),
                (10, report.TotalOutValue),
                (12, report.TotalClosingValue));

        builder.WriteBookFooter();
        builder.Finish();
    }

    private static Tt133Column[] BuildColumns(bool showValues)
    {
        var columns = new List<Tt133Column>
        {
            new("A", "STT", 6, Tt133CellKind.Quantity),
            new("B", "Mã hàng", 16),
            new("C", "Tên, quy cách", 34),
            new("D", "ĐVT", 9),
        };
        var symbol = 1;
        foreach (var group in Groups)
        {
            columns.Add(new(symbol++.ToString(), "Số lượng", 12, Tt133CellKind.Quantity, group));
            if (showValues)
                columns.Add(new(symbol++.ToString(), "Thành tiền", 16, Tt133CellKind.Money, group));
            else
                symbol++; // symbols stay 1, 3, 5, 7 so the form code keeps its meaning
        }
        return columns.ToArray();
    }
}
