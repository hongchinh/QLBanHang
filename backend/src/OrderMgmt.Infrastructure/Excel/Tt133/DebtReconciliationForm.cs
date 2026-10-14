using System.Globalization;
using ClosedXML.Excel;
using OrderMgmt.Application.CashDebt.Reports.Models;
using OrderMgmt.Application.Common;
using OrderMgmt.Application.Tt133.Models;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Infrastructure.Excel.Tt133;

/// Biên bản đối chiếu công nợ: one portrait sheet, no form code, no book footer.
public static class DebtReconciliationForm
{
    private static readonly Tt133Column[] Columns =
    [
        new("A", "STT", 6, Tt133CellKind.Quantity),
        new("B", "Ngày CT", 12, Tt133CellKind.Date),
        new("C", "Số CT", 12),
        new("D", "Diễn giải", 36),
        new("1", "Phát sinh tăng", 16, Tt133CellKind.Money),
        new("2", "Phát sinh giảm", 16, Tt133CellKind.Money),
    ];

    private const int Increase = 5;
    private const int Decrease = 6;
    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");

    public static void Write(XLWorkbook workbook, Tt133FormContext context, DebtReconciliationReportDto report)
    {
        var sheet = workbook.AddWorksheet("DoiChieu");
        var builder = new Tt133FormBuilder(sheet, context, Columns, landscape: false, labelColumn: 4);

        builder.WriteText("CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM", bold: true, centered: true);
        builder.WriteText("Độc lập – Tự do – Hạnh phúc", bold: true, centered: true);
        builder.Skip();
        builder.WriteTitle("BIÊN BẢN ĐỐI CHIẾU CÔNG NỢ",
            $"Từ ngày {report.From:dd/MM/yyyy} đến ngày {report.To:dd/MM/yyyy}");

        var print = context.PrintDate;
        builder.WriteText($"Hôm nay, ngày {print:dd} tháng {print:MM} năm {print:yyyy}, chúng tôi gồm:");
        builder.WriteText($"BÊN A: {context.CompanyName}", bold: true);
        builder.WriteText(Labelled("Địa chỉ:", context.Address));
        builder.WriteText(Labelled("Mã số thuế:", context.TaxCode));
        builder.WriteText(Labelled("Đại diện:", context.LegalRepresentativeName));
        builder.WriteText($"BÊN B: {report.PartnerName}", bold: true);
        builder.WriteText(Labelled("Địa chỉ:", report.Address));
        builder.WriteText(Labelled("Mã số thuế:", report.TaxCode));
        builder.WriteText("Đại diện:");
        builder.WriteText("Hai bên cùng đối chiếu công nợ như sau:");

        foreach (var side in report.Sides)
            WriteSide(builder, side);

        if (report.NetBalance is { } net)
        {
            builder.WriteText(net switch
            {
                > 0 => $"Bù trừ: Bên B còn nợ Bên A {Money(net)} đồng",
                < 0 => $"Bù trừ: Bên A còn nợ Bên B {Money(-net)} đồng",
                _ => "Bù trừ: hai bên không còn công nợ",
            });
        }

        builder.WriteText(Conclusion(report));
        builder.Skip();
        WriteSignatures(builder, context);
        builder.Finish(repeatTableHeader: false);
    }

    private static void WriteSide(Tt133FormBuilder builder, DebtLedgerReportDto side)
    {
        builder.WriteText(
            side.Side == DebtSide.Receivable ? "Công nợ phải thu (Bên B nợ Bên A)" : "Công nợ phải trả (Bên A nợ Bên B)",
            bold: true);
        builder.WriteTableHeader();

        builder.WriteSummaryRow("Số dư đầu kỳ", Balance(side.OpeningBalance));
        var number = 0;
        foreach (var row in side.Rows)
        {
            builder.WriteRow(
                ++number,
                row.DocDate,
                row.DocCode,
                DebtLedgerForm.DescriptionOf(row),
                NonZero(row.Increase),
                NonZero(row.Decrease));
        }
        builder.WriteSummaryRow("Cộng phát sinh", (Increase, side.TotalIncrease), (Decrease, side.TotalDecrease));
        builder.WriteSummaryRow("Số dư cuối kỳ", Balance(side.ClosingBalance));
        builder.Skip();
    }

    /// A balance goes to "tăng" when it is zero or positive, otherwise its absolute value goes to "giảm".
    private static (int Column, object? Value) Balance(decimal balance) =>
        balance >= 0m ? (Increase, balance) : (Decrease, -balance);

    private static string Conclusion(DebtReconciliationReportDto report)
    {
        var receivable = report.Sides.FirstOrDefault(s => s.Side == DebtSide.Receivable);
        var payable = report.Sides.FirstOrDefault(s => s.Side == DebtSide.Payable);
        var n = report.NetBalance
            ?? (receivable is not null ? receivable.ClosingBalance : payable is not null ? -payable.ClosingBalance : 0m);

        var date = $"{report.To:dd/MM/yyyy}";
        if (n > 0)
            return $"Tính đến ngày {date}, Bên B còn nợ Bên A số tiền: {Money(n)} đồng (Bằng chữ: {VndAmountInWords.Read(n)}).";
        if (n < 0)
            return $"Tính đến ngày {date}, Bên A còn nợ Bên B số tiền: {Money(-n)} đồng (Bằng chữ: {VndAmountInWords.Read(-n)}).";
        return $"Tính đến ngày {date}, hai bên không còn công nợ.";
    }

    /// Two signature columns (left and right half of the table), written from the builder's current row.
    private static void WriteSignatures(Tt133FormBuilder builder, Tt133FormContext context)
    {
        var sheet = builder.Sheet;
        var row = builder.Row;

        void Put(int firstColumn, int lastColumn, int atRow, string? text, bool bold = false, bool italic = false)
        {
            var range = sheet.Range(atRow, firstColumn, atRow, lastColumn);
            range.Merge();
            var cell = sheet.Cell(atRow, firstColumn);
            cell.Value = text ?? string.Empty;
            cell.Style.Font.Bold = bold;
            cell.Style.Font.Italic = italic;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        Put(1, 3, row, "ĐẠI DIỆN BÊN A", bold: true);
        Put(4, 6, row, "ĐẠI DIỆN BÊN B", bold: true);
        Put(1, 3, row + 1, "(Ký, họ tên, đóng dấu)", italic: true);
        Put(4, 6, row + 1, "(Ký, họ tên, đóng dấu)", italic: true);
        // Four empty rows for the signature and seal, then the name of the legal representative under Bên A.
        Put(1, 3, row + 6, context.LegalRepresentativeName, bold: true);
        Put(1, 3, row + 8, "Kế toán trưởng", bold: true);
        Put(1, 3, row + 12, context.ChiefAccountantName, bold: true);

        builder.Skip(13);
    }

    private static string Money(decimal amount) => amount.ToString("#,##0", Vietnamese);

    private static string Labelled(string label, string? value) =>
        string.IsNullOrWhiteSpace(value) ? label : $"{label} {value.Trim()}";

    private static decimal? NonZero(decimal value) => value == 0m ? null : value;
}
