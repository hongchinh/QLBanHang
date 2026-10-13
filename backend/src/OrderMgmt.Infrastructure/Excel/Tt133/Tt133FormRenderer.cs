using ClosedXML.Excel;
using OrderMgmt.Application.CashDebt.Reports.Models;
using OrderMgmt.Application.Inventory.Reports.Models;
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

    public byte[] RenderDebtSummary(Tt133FormContext context, DebtBalanceReportDto report, string? search) =>
        Render(workbook => DebtSummaryForm.Write(workbook, context, report, search));

    public byte[] RenderDebtReconciliation(Tt133FormContext context, DebtReconciliationReportDto report) =>
        Render(workbook => DebtReconciliationForm.Write(workbook, context, report));

    public byte[] RenderStockSummary(Tt133FormContext context, StockSummaryReportDto report, string warehouseLabel, string? productGroupName, string? search) =>
        Render(workbook => StockSummaryForm.Write(workbook, context, report, warehouseLabel, productGroupName, search));

    public byte[] RenderStockCards(Tt133FormContext context, IReadOnlyList<StockCardSheet> sheets) =>
        Render(workbook => StockCardForm.Write(workbook, context, sheets));

    private static byte[] Render(Action<XLWorkbook> write)
    {
        using var workbook = new XLWorkbook();
        write(workbook);
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
