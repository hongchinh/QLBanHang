using ClosedXML.Excel;
using OrderMgmt.Application.CashDebt.Common;
using OrderMgmt.Application.CashDebt.Reports.Models;
using OrderMgmt.Application.Tt133.Models;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Infrastructure.Excel.Tt133;

/// Bảng tổng hợp chi tiết thanh toán với người mua / người bán: one sheet, no form code.
public static class DebtSummaryForm
{
    private static readonly Tt133Column[] Columns =
    [
        new("A", "STT", 6, Tt133CellKind.Quantity),
        new("B", "Mã đối tượng", 16),
        new("C", "Tên đối tượng", 34),
        new("1", "Nợ", 16, Tt133CellKind.Money, "Số dư đầu kỳ"),
        new("2", "Có", 16, Tt133CellKind.Money, "Số dư đầu kỳ"),
        new("3", "Nợ", 16, Tt133CellKind.Money, "Số phát sinh"),
        new("4", "Có", 16, Tt133CellKind.Money, "Số phát sinh"),
        new("5", "Nợ", 16, Tt133CellKind.Money, "Số dư cuối kỳ"),
        new("6", "Có", 16, Tt133CellKind.Money, "Số dư cuối kỳ"),
    ];

    public static void Write(XLWorkbook workbook, Tt133FormContext context, DebtBalanceReportDto report, string? search)
    {
        var sheet = workbook.AddWorksheet("TongHop");
        var builder = new Tt133FormBuilder(sheet, context, Columns, landscape: true);
        builder.WriteHeaderBlock(null);

        var parameterLines = search is null
            ? Array.Empty<string>()
            : [$"Điều kiện lọc: mã hoặc tên chứa \"{search}\""];
        builder.WriteTitle(
            report.Side == DebtSide.Receivable
                ? "BẢNG TỔNG HỢP CHI TIẾT THANH TOÁN VỚI NGƯỜI MUA"
                : "BẢNG TỔNG HỢP CHI TIẾT THANH TOÁN VỚI NGƯỜI BÁN",
            Tt133FormBuilder.PeriodLine(report.From, report.To),
            parameterLines);
        builder.WriteTableHeader();

        var number = 0;
        foreach (var row in report.Rows)
        {
            var (debit, credit) = DebtBalanceSign.SplitMovement(report.Side, row.Increase, row.Decrease);
            builder.WriteRow(
                ++number,
                row.PartnerCode,
                row.PartnerName,
                NonZero(row.OpeningDebit),
                NonZero(row.OpeningCredit),
                NonZero(debit),
                NonZero(credit),
                NonZero(row.ClosingDebit),
                NonZero(row.ClosingCredit));
        }

        var totals = report.Totals;
        var (totalDebit, totalCredit) = DebtBalanceSign.SplitMovement(report.Side, totals.Increase, totals.Decrease);
        builder.WriteSummaryRow("Cộng",
            (4, totals.OpeningDebit),
            (5, totals.OpeningCredit),
            (6, totalDebit),
            (7, totalCredit),
            (8, totals.ClosingDebit),
            (9, totals.ClosingCredit));

        builder.WriteBookFooter();
        builder.Finish();
    }

    private static decimal? NonZero(decimal value) => value == 0m ? null : value;
}
