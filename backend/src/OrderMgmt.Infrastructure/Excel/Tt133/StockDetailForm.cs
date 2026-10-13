using ClosedXML.Excel;
using OrderMgmt.Application.Inventory.Reports.Models;
using OrderMgmt.Application.Tt133.Models;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Infrastructure.Excel.Tt133;

/// S06-DNN, Sổ chi tiết vật liệu, dụng cụ, sản phẩm, hàng hóa: one landscape sheet per product × warehouse.
public static class StockDetailForm
{
    public static void Write(XLWorkbook workbook, Tt133FormContext context, IReadOnlyList<StockCardSheet> sheets)
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in sheets)
        {
            var sheet = workbook.AddWorksheet(Tt133FormBuilder.SheetName($"{source.Card.ProductCode}-{source.WarehouseCode}", used));
            WriteCard(sheet, context, source);
        }
    }

    private static void WriteCard(IXLWorksheet sheet, Tt133FormContext context, StockCardSheet source)
    {
        var card = source.Card;
        // A warehouse filter under Branch costing has no meaningful unit price or running value (review B2).
        var showValues = card.CanViewCost && !card.ValuesAtScopeOnly;

        var builder = new Tt133FormBuilder(sheet, context, BuildColumns(showValues), landscape: true);
        builder.WriteHeaderBlock("S06-DNN");
        builder.WriteTitle(
            "SỔ CHI TIẾT VẬT LIỆU, DỤNG CỤ, SẢN PHẨM, HÀNG HÓA",
            Tt133FormBuilder.PeriodLine(card.From, card.To),
            $"Kho: {source.WarehouseCode} – {source.WarehouseName}",
            $"Tên, quy cách: {card.ProductCode} – {card.ProductName}",
            $"Đơn vị tính: {card.UnitName}");

        if (card.ValuesAtScopeOnly) builder.WriteNote("Giá trị tính theo chi nhánh");
        else if (showValues && card.IsProvisional) builder.WriteNote("Giá vốn tạm tính");

        builder.WriteTableHeader();

        if (showValues)
        {
            builder.WriteSummaryRow("Số dư đầu kỳ", (10, card.OpeningQty), (11, card.OpeningValue));
            foreach (var row in card.Rows)
                builder.WriteRow(
                    row.SourceCode,
                    row.PostedAt,
                    DescriptionOf(row),
                    row.ReasonName,
                    row.UnitCost,
                    NonZero(row.QtyIn), row.QtyIn == 0m ? null : row.InValue,
                    NonZero(row.QtyOut), row.QtyOut == 0m ? null : row.CostAmount,
                    row.RunningQty, row.RunningValue);
            builder.WriteSummaryRow("Cộng phát sinh",
                (6, card.InQty), (7, card.InValue), (8, card.OutQty), (9, card.OutValue));
            builder.WriteSummaryRow("Số dư cuối kỳ", (10, card.ClosingQty), (11, card.ClosingValue));
        }
        else
        {
            builder.WriteSummaryRow("Số dư đầu kỳ", (7, card.OpeningQty));
            foreach (var row in card.Rows)
                builder.WriteRow(
                    row.SourceCode,
                    row.PostedAt,
                    DescriptionOf(row),
                    row.ReasonName,
                    NonZero(row.QtyIn),
                    NonZero(row.QtyOut),
                    row.RunningQty);
            builder.WriteSummaryRow("Cộng phát sinh", (5, card.InQty), (6, card.OutQty));
            builder.WriteSummaryRow("Số dư cuối kỳ", (7, card.ClosingQty));
        }

        builder.WriteBookFooter();
        builder.Finish();
    }

    private static Tt133Column[] BuildColumns(bool showValues)
    {
        var columns = new List<Tt133Column>
        {
            new("A", "Số hiệu", 12, Tt133CellKind.Text, "Chứng từ"),
            new("B", "Ngày, tháng", 12, Tt133CellKind.Date, "Chứng từ"),
            new("C", "Diễn giải", 30),
            new("D", "Lý do", 18),
        };
        if (showValues)
        {
            columns.Add(new("1", "Đơn giá", 13, Tt133CellKind.Quantity));
            columns.Add(new("2", "Số lượng", 11, Tt133CellKind.Quantity, "Nhập"));
            columns.Add(new("3", "Thành tiền", 15, Tt133CellKind.Money, "Nhập"));
            columns.Add(new("4", "Số lượng", 11, Tt133CellKind.Quantity, "Xuất"));
            columns.Add(new("5", "Thành tiền", 15, Tt133CellKind.Money, "Xuất"));
            columns.Add(new("6", "Số lượng", 11, Tt133CellKind.Quantity, "Tồn"));
            columns.Add(new("7", "Thành tiền", 15, Tt133CellKind.Money, "Tồn"));
            columns.Add(new("8", "Ghi chú", 14));
        }
        else
        {
            // Symbols stay 2, 4, 6, 8 so the form code keeps its meaning.
            columns.Add(new("2", "Số lượng", 11, Tt133CellKind.Quantity, "Nhập"));
            columns.Add(new("4", "Số lượng", 11, Tt133CellKind.Quantity, "Xuất"));
            columns.Add(new("6", "Số lượng", 11, Tt133CellKind.Quantity, "Tồn"));
            columns.Add(new("8", "Ghi chú", 14));
        }
        return columns.ToArray();
    }

    /// "{partner} – {document type}"; the part that is empty is left out (T9).
    internal static string DescriptionOf(StockCardRowDto row)
    {
        var type = row.SourceType switch
        {
            LedgerSourceType.Opening => "Tồn đầu kỳ",
            LedgerSourceType.StockIn => "Phiếu nhập kho",
            LedgerSourceType.StockOut => "Phiếu xuất kho",
            _ => null,
        };
        return string.Join(" – ", new[] { row.PartnerName, type }.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    private static decimal? NonZero(decimal value) => value == 0m ? null : value;
}
