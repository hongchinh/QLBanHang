using ClosedXML.Excel;
using OrderMgmt.Application.CashDebt.Reports.Models;
using OrderMgmt.Application.Tt133.Interfaces;
using OrderMgmt.Application.Tt133.Models;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Infrastructure.Excel.Tt133;

/// Renders the TT133 forms with ClosedXML; each form is a class of this folder.
public class Tt133FormRenderer : ITt133FormRenderer
{
    public byte[] RenderCashBook(Tt133FormContext context, CashBookReportDto report, FundKind kind) =>
        Render(workbook => CashBookForm.Write(workbook, context, report, kind));

    public byte[] RenderDebtLedgers(Tt133FormContext context, IReadOnlyList<DebtLedgerReportDto> ledgers) =>
        Render(workbook => DebtLedgerForm.Write(workbook, context, ledgers));

    private static byte[] Render(Action<XLWorkbook> write)
    {
        using var workbook = new XLWorkbook();
        write(workbook);
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
