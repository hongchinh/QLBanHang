using ClosedXML.Excel;
using OrderMgmt.Application.CashDebt.Common;
using OrderMgmt.Application.CashDebt.Reports.Models;
using OrderMgmt.Application.Tt133.Models;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Infrastructure.Excel.Tt133;

/// S12-DNN (Sổ chi tiết thanh toán với người mua / người bán): one sheet per ledger of the debt ledger report.
public static class DebtLedgerForm
{
    private static readonly Tt133Column[] Columns =
    [
        new("A", "Ngày, tháng ghi sổ", 12, Tt133CellKind.Date),
        new("B", "Số hiệu", 12, Tt133CellKind.Text, "Chứng từ"),
        new("C", "Ngày, tháng", 12, Tt133CellKind.Date, "Chứng từ"),
        new("D", "Diễn giải", 34),
        new("E", "Lý do", 20),
        new("1", "Hạn thanh toán", 12, Tt133CellKind.Date),
        new("2", "Nợ", 15, Tt133CellKind.Money, "Số phát sinh"),
        new("3", "Có", 15, Tt133CellKind.Money, "Số phát sinh"),
        new("4", "Nợ", 15, Tt133CellKind.Money, "Số dư"),
        new("5", "Có", 15, Tt133CellKind.Money, "Số dư"),
    ];

    private const int MovementDebit = 7;
    private const int MovementCredit = 8;
    private const int BalanceDebit = 9;
    private const int BalanceCredit = 10;

    public static void Write(XLWorkbook workbook, Tt133FormContext context, IReadOnlyList<DebtLedgerReportDto> ledgers)
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var ledger in ledgers)
        {
            var sheet = workbook.AddWorksheet(Tt133FormBuilder.SheetName(ledger.PartnerCode, used));
            WriteLedger(sheet, context, ledger);
        }
    }

    private static void WriteLedger(IXLWorksheet sheet, Tt133FormContext context, DebtLedgerReportDto ledger)
    {
        var side = ledger.Side;
        var builder = new Tt133FormBuilder(sheet, context, Columns, landscape: true);
        builder.WriteHeaderBlock("S12-DNN");
        builder.WriteTitle(
            side == DebtSide.Receivable ? "SỔ CHI TIẾT THANH TOÁN VỚI NGƯỜI MUA" : "SỔ CHI TIẾT THANH TOÁN VỚI NGƯỜI BÁN",
            Tt133FormBuilder.PeriodLine(ledger.From, ledger.To),
            side == DebtSide.Receivable ? "Loại công nợ: Phải thu khách hàng" : "Loại công nợ: Phải trả người bán",
            $"Đối tượng: {ledger.PartnerCode} – {ledger.PartnerName}",
            "Loại tiền: VNĐ");
        builder.WriteTableHeader();

        builder.WriteSummaryRow("Số dư đầu kỳ",
            Pair(side, BalanceDebit, ledger.OpeningDebit, ledger.OpeningCredit));
        foreach (var row in ledger.Rows)
        {
            var (debit, credit) = DebtBalanceSign.SplitMovement(side, row.Increase, row.Decrease);
            var (balanceDebit, balanceCredit) = DebtBalanceSign.Split(side, row.RunningBalance);
            builder.WriteRow(
                row.PostedAt,
                row.DocCode,
                row.DocDate,
                DescriptionOf(row),
                row.ReasonName,
                row.DueDate,
                NonZero(debit),
                NonZero(credit),
                NonZero(balanceDebit),
                NonZero(balanceCredit));
        }
        var (totalDebit, totalCredit) = DebtBalanceSign.SplitMovement(side, ledger.TotalIncrease, ledger.TotalDecrease);
        builder.WriteSummaryRow("Cộng số phát sinh", Pair(side, MovementDebit, totalDebit, totalCredit));
        builder.WriteSummaryRow("Số dư cuối kỳ", Pair(side, BalanceDebit, ledger.ClosingDebit, ledger.ClosingCredit));

        builder.WriteBookFooter();
        builder.Finish();
    }

    /// "Diễn giải" with the branch code in front when the ledger was read for the whole company (T21).
    internal static string DescriptionOf(DebtLedgerRowDto row) =>
        row.BranchCode is null ? row.Description ?? string.Empty : $"[{row.BranchCode}] {row.Description}";

    /// The two cells of a summary row. A zero is written as 0 in the debit column for a receivable and in the credit
    /// column for a payable (the natural direction) when both are zero, and left empty otherwise.
    private static (int Column, object? Value)[] Pair(DebtSide side, int debitColumn, decimal debit, decimal credit)
    {
        if (debit == 0m && credit == 0m)
        {
            return side == DebtSide.Receivable
                ? [(debitColumn, 0m), (debitColumn + 1, null)]
                : [(debitColumn, null), (debitColumn + 1, 0m)];
        }
        return [(debitColumn, NonZero(debit)), (debitColumn + 1, NonZero(credit))];
    }

    private static decimal? NonZero(decimal value) => value == 0m ? null : value;
}
