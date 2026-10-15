using ClosedXML.Excel;
using OrderMgmt.Application.Tt133.Models;
using OrderMgmt.Application.Tt133.Vouchers;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Infrastructure.Excel.Tt133.Vouchers;

/// 01-VT (Phiếu nhập kho) and 02-VT (Phiếu xuất kho): one A4 portrait sheet per voucher.
public static class StockVoucherForm
{
    private const string Hint = "(Ký, họ tên)";
    private const string QuantityGroup = "Số lượng";
    private const string NameHeader = "Tên, nhãn hiệu, quy cách, phẩm chất vật tư, dụng cụ, sản phẩm, hàng hóa";

    public static void Write(XLWorkbook workbook, Tt133FormContext context, IReadOnlyList<StockVoucherPrintModel> vouchers)
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var voucher in vouchers)
        {
            var sheet = workbook.AddWorksheet(Tt133FormBuilder.SheetName(voucher.Code, used));
            WriteVoucher(sheet, context, voucher);
        }
    }

    private static Tt133Column[] BuildColumns(StockVoucherPrintModel m)
    {
        var stockIn = m.Type == StockDirection.In;
        var columns = new List<Tt133Column>
        {
            new("A", "STT", 5, Tt133CellKind.Quantity),
            // The Kho column takes 3 of the name column's units, so the table keeps its width.
            new("B", NameHeader, m.ShowWarehouseColumn ? 31 : 34),
            new("C", "Mã số", 12),
            new("D", "Đơn vị tính", 8),
        };
        if (m.ShowWarehouseColumn) columns.Add(new Tt133Column("E", "Kho", 11));
        columns.Add(new Tt133Column("1", stockIn ? "Theo chứng từ" : "Yêu cầu", 10, Tt133CellKind.Quantity, QuantityGroup));
        columns.Add(new Tt133Column("2", stockIn ? "Thực nhập" : "Thực xuất", 10, Tt133CellKind.Quantity, QuantityGroup));
        columns.Add(new Tt133Column("3", "Đơn giá", 13, Tt133CellKind.Money));
        columns.Add(new Tt133Column("4", "Thành tiền", 15, Tt133CellKind.Money));
        return columns.ToArray();
    }

    private static void WriteVoucher(IXLWorksheet sheet, Tt133FormContext context, StockVoucherPrintModel m)
    {
        var stockIn = m.Type == StockDirection.In;
        var columns = BuildColumns(m);
        var builder = new VoucherFormBuilder(sheet, context, columns, VoucherPaper.A4Portrait, labelColumn: 2);

        builder.WriteHeaderBlock(stockIn ? "01-VT" : "02-VT");
        builder.WriteTitleBlock(stockIn ? "PHIẾU NHẬP KHO" : "PHIẾU XUẤT KHO", m.Date, m.IsCancelled,
            [$"Số: {m.Code}"], [$"Nợ: {VoucherPrintRules.Dots}", $"Có: {VoucherPrintRules.Dots}"]);
        foreach (var note in m.Notes) builder.Table.WriteNote(note);

        if (stockIn)
        {
            builder.WriteLine("- Họ và tên người giao:", m.HandlerName);
            builder.Table.WriteText(
                $"- Theo {VoucherPrintRules.Dots} số {VoucherPrintRules.Dots} ngày {VoucherPrintRules.Dots} của {m.PartnerName ?? VoucherPrintRules.Dots}");
            builder.Table.WriteText($"- Nhập tại kho: {m.WarehouseNames} Địa điểm: {VoucherPrintRules.Dots}");
        }
        else
        {
            builder.WriteLine("- Họ và tên người nhận hàng:", m.HandlerName);
            builder.WriteLine("Địa chỉ (bộ phận):", m.PartnerAddress);
            builder.WriteLine("- Lý do xuất kho:", m.ReasonName);
            builder.Table.WriteText($"- Xuất tại kho (ngăn lô): {m.WarehouseNames} Địa điểm: {VoucherPrintRules.Dots}");
        }

        builder.Table.WriteTableHeader();
        // WriteTableHeader wraps but sets no height, and LibreOffice keeps the default one: the sub-header row gets its lines.
        var subHeaderLines = columns.Where(c => c.Group != null)
            .Max(c => Tt133FormBuilder.LineCount(c.Header, c.Width, bold: true));
        sheet.Row(builder.Table.TableHeaderFirstRow + 1).Height = subHeaderLines * sheet.RowHeight;

        foreach (var line in m.Lines)
        {
            var cells = new List<object?> { line.Number, line.Name, line.Code, line.UnitName };
            if (m.ShowWarehouseColumn) cells.Add(line.WarehouseCode);
            cells.AddRange([line.Quantity, line.Quantity, line.UnitPrice, line.Amount]);
            builder.WriteRow(cells.ToArray());
        }

        var amountColumn = columns.Length;
        foreach (var total in m.Totals)
            builder.Table.WriteSummaryRow(total.Label, (amountColumn, total.Amount));
        if (!string.IsNullOrWhiteSpace(m.VatIncludedLine))
            builder.Table.WriteText(m.VatIncludedLine, italic: true);

        builder.WriteLine("- Tổng số tiền (viết bằng chữ):", m.AmountInWords);
        builder.WriteLine("- Số chứng từ gốc kèm theo:", null);
        if (!stockIn && m.Qr is { } qr) builder.WritePaymentBlock(qr);

        var preparer = new VoucherSigner("Người lập phiếu", Hint, m.PreparerName);
        var treasurer = new VoucherSigner("Thủ kho", Hint, null);
        var accountant = new VoucherSigner("Kế toán trưởng", Hint, context.ChiefAccountantName);
        builder.WriteSignatures(m.Date, stockIn
            ? [preparer, new VoucherSigner("Người giao hàng", Hint, null), treasurer, accountant]
            :
            [
                preparer, new VoucherSigner("Người nhận hàng", Hint, null), treasurer, accountant,
                new VoucherSigner("Giám đốc", Hint, context.LegalRepresentativeName),
            ]);
        builder.Finish();
    }
}
