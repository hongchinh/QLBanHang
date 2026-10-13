using ClosedXML.Excel;
using OrderMgmt.Application.CashDebt.Reports.Models;
using OrderMgmt.Application.Tt133.Models;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Infrastructure.Excel.Tt133;

/// S04a-DNN (Sổ quỹ tiền mặt) and S05-DNN (Sổ tiền gửi ngân hàng): one sheet per fund of the cash book report.
public static class CashBookForm
{
    private static readonly Tt133Column[] CashColumns =
    [
        new("A", "Ngày, tháng ghi sổ", 12, Tt133CellKind.Date),
        new("B", "Ngày, tháng chứng từ", 12, Tt133CellKind.Date),
        new("C", "Thu", 11, Tt133CellKind.Text, "Số hiệu chứng từ"),
        new("D", "Chi", 11, Tt133CellKind.Text, "Số hiệu chứng từ"),
        new("E", "Diễn giải", 36),
        new("F", "Lý do", 20),
        new("1", "Thu", 15, Tt133CellKind.Money, "Số tiền"),
        new("2", "Chi", 15, Tt133CellKind.Money, "Số tiền"),
        new("3", "Tồn", 15, Tt133CellKind.Money, "Số tiền"),
        new("G", "Ghi chú", 14),
    ];

    private static readonly Tt133Column[] BankColumns =
    [
        new("A", "Ngày, tháng ghi sổ", 12, Tt133CellKind.Date),
        new("B", "Số hiệu", 12, Tt133CellKind.Text, "Chứng từ"),
        new("C", "Ngày, tháng", 12, Tt133CellKind.Date, "Chứng từ"),
        new("D", "Diễn giải", 36),
        new("E", "Lý do", 20),
        new("1", "Thu (gửi vào)", 15, Tt133CellKind.Money, "Số tiền"),
        new("2", "Chi (rút ra)", 15, Tt133CellKind.Money, "Số tiền"),
        new("3", "Còn lại", 15, Tt133CellKind.Money, "Số tiền"),
        new("F", "Ghi chú", 14),
    ];

    public static void Write(XLWorkbook workbook, Tt133FormContext context, CashBookReportDto report, FundKind kind)
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in report.Groups)
        {
            var sheet = workbook.AddWorksheet(Tt133FormBuilder.SheetName(group.FundCode, used));
            if (kind == FundKind.Bank) WriteBank(sheet, context, report, group);
            else WriteCash(sheet, context, report, group);
        }
    }

    private static void WriteCash(IXLWorksheet sheet, Tt133FormContext context, CashBookReportDto report, CashBookGroupDto group)
    {
        var builder = new Tt133FormBuilder(sheet, context, CashColumns, landscape: false);
        builder.WriteHeaderBlock("S04a-DNN");
        builder.WriteTitle("SỔ QUỸ TIỀN MẶT", Tt133FormBuilder.PeriodLine(report.From, report.To),
            $"Loại quỹ: {group.FundName}");
        builder.WriteTableHeader();

        builder.WriteSummaryRow("Số tồn đầu kỳ", (9, group.Opening));
        foreach (var row in group.Rows)
        {
            builder.WriteRow(
                row.At,
                row.At,
                row.ReceiptCode ?? (row.SourceType == CashBookSourceType.TransferIn ? row.TransferCode : null),
                row.PaymentCode ?? (row.SourceType == CashBookSourceType.TransferOut ? row.TransferCode : null),
                DescriptionOf(row),
                row.ReasonName,
                NonZero(row.In),
                NonZero(row.Out),
                row.Running,
                null);
        }
        builder.WriteSummaryRow("Cộng số phát sinh trong kỳ", (7, group.TotalIn), (8, group.TotalOut));
        builder.WriteSummaryRow("Số tồn cuối kỳ", (9, group.Closing));

        builder.WriteBookFooter();
        builder.Finish();
    }

    private static void WriteBank(IXLWorksheet sheet, Tt133FormContext context, CashBookReportDto report, CashBookGroupDto group)
    {
        var builder = new Tt133FormBuilder(sheet, context, BankColumns, landscape: false);
        builder.WriteHeaderBlock("S05-DNN");
        builder.WriteTitle("SỔ TIỀN GỬI NGÂN HÀNG", Tt133FormBuilder.PeriodLine(report.From, report.To),
            $"Nơi mở tài khoản giao dịch: {group.BankName}",
            $"Số hiệu tài khoản tại nơi gửi: {group.AccountNumber}");
        builder.WriteTableHeader();

        builder.WriteSummaryRow("Số dư đầu kỳ", (8, group.Opening));
        foreach (var row in group.Rows)
        {
            builder.WriteRow(
                row.At,
                row.ReceiptCode ?? row.PaymentCode ?? row.TransferCode,
                row.At,
                DescriptionOf(row),
                row.ReasonName,
                NonZero(row.In),
                NonZero(row.Out),
                row.Running,
                null);
        }
        builder.WriteSummaryRow("Cộng số phát sinh trong kỳ", (6, group.TotalIn), (7, group.TotalOut));
        builder.WriteSummaryRow("Số dư cuối kỳ", (8, group.Closing));

        builder.WriteBookFooter();
        builder.Finish();
    }

    /// Description, then " – {partner or person}". A description equal to the reason name is left out: the form has its
    /// own "Lý do" column (T9).
    internal static string DescriptionOf(CashBookRowDto row)
    {
        var description = row.Description?.Trim();
        if (string.IsNullOrEmpty(description)
            || string.Equals(description, row.ReasonName?.Trim(), StringComparison.OrdinalIgnoreCase))
            description = null;
        var person = row.PartnerOrPerson?.Trim();
        return string.Join(" – ", new[] { description, string.IsNullOrEmpty(person) ? null : person }
            .Where(part => !string.IsNullOrEmpty(part)));
    }

    private static decimal? NonZero(decimal value) => value == 0m ? null : value;
}
