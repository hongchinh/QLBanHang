using ClosedXML.Excel;
using OrderMgmt.Application.Inventory.Reports.Models;
using OrderMgmt.Application.Tt133.Models;

namespace OrderMgmt.Infrastructure.Excel.Tt133;

/// S08-DNN, Thẻ kho (Sổ kho): one portrait sheet per product × warehouse. Quantities only, so no cost permission is needed.
public static class StockCardForm
{
    private static readonly Tt133Column[] Columns =
    [
        new("A", "STT", 6, Tt133CellKind.Quantity),
        new("B", "Ngày, tháng", 12, Tt133CellKind.Date),
        new("C", "Nhập", 12, Tt133CellKind.Text, "Số hiệu chứng từ"),
        new("D", "Xuất", 12, Tt133CellKind.Text, "Số hiệu chứng từ"),
        new("E", "Diễn giải", 28),
        new("F", "Ngày nhập, xuất", 12, Tt133CellKind.Date),
        new("1", "Nhập", 11, Tt133CellKind.Quantity, "Số lượng"),
        new("2", "Xuất", 11, Tt133CellKind.Quantity, "Số lượng"),
        new("3", "Tồn", 11, Tt133CellKind.Quantity, "Số lượng"),
        new("G", "Ký xác nhận của kế toán", 14),
    ];

    private const int InColumn = 7;
    private const int OutColumn = 8;
    private const int BalanceColumn = 9;

    public static void Write(XLWorkbook workbook, Tt133FormContext context, IReadOnlyList<StockCardSheet> sheets)
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < sheets.Count; i++)
        {
            var card = sheets[i].Card;
            var sheet = workbook.AddWorksheet(Tt133FormBuilder.SheetName($"{card.ProductCode}-{sheets[i].WarehouseCode}", used));
            WriteCard(sheet, context, sheets[i], i + 1);
        }
    }

    private static void WriteCard(IXLWorksheet sheet, Tt133FormContext context, StockCardSheet source, int sheetNumber)
    {
        var card = source.Card;
        var builder = new Tt133FormBuilder(sheet, context, Columns, landscape: false);
        builder.WriteHeaderBlock("S08-DNN");
        builder.WriteTitle(
            "THẺ KHO (SỔ KHO)",
            Tt133FormBuilder.PeriodLine(card.From, card.To),
            $"Ngày lập thẻ: {context.PrintDate:dd/MM/yyyy}",
            $"Tờ số: {sheetNumber}",
            $"Tên, nhãn hiệu, quy cách vật tư: {card.ProductName}",
            $"Đơn vị tính: {card.UnitName}",
            $"Mã số: {card.ProductCode}",
            $"Kho: {source.WarehouseCode} – {source.WarehouseName}");
        builder.WriteTableHeader();

        builder.WriteSummaryRow("Tồn đầu kỳ", (BalanceColumn, card.OpeningQty));
        var number = 0;
        foreach (var row in card.Rows)
        {
            var isOut = row.QtyOut > 0m;
            builder.WriteRow(
                ++number,
                row.PostedAt,
                isOut ? null : row.SourceCode,
                isOut ? row.SourceCode : null,
                DescriptionOf(row),
                row.PostedAt,
                NonZero(row.QtyIn),
                NonZero(row.QtyOut),
                row.RunningQty);
        }
        builder.WriteSummaryRow("Cộng cuối kỳ", (InColumn, card.InQty), (OutColumn, card.OutQty));
        builder.WriteSummaryRow("Tồn cuối kỳ", (BalanceColumn, card.ClosingQty));

        builder.WriteBookFooter();
        builder.Finish();
    }

    /// "{partner} – {reason}"; the part that is empty is left out (T9).
    internal static string DescriptionOf(StockCardRowDto row) =>
        string.Join(" – ", new[] { row.PartnerName, row.ReasonName }.Where(part => !string.IsNullOrWhiteSpace(part)));

    private static decimal? NonZero(decimal value) => value == 0m ? null : value;
}
