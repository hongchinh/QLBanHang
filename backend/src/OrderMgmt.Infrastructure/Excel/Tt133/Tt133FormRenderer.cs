using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml.Linq;
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
    private static readonly XNamespace SpreadsheetMl = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly Regex RelativeRows = new(@"!(\d+):(\d+)$", RegexOptions.Compiled);

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

    public byte[] RenderStockDetail(Tt133FormContext context, IReadOnlyList<StockCardSheet> sheets) =>
        Render(workbook => StockDetailForm.Write(workbook, context, sheets));

    private static byte[] Render(Action<XLWorkbook> write)
    {
        using var workbook = new XLWorkbook();
        write(workbook);
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        MakePrintTitlesAbsolute(stream);
        return stream.ToArray();
    }

    /// ClosedXML writes the rows to repeat as "Sheet!9:11". Excel accepts that; LibreOffice (the PDF conversion) only
    /// repeats them when the reference is absolute ("Sheet!$9:$11"), so the defined names are rewritten after saving.
    private static void MakePrintTitlesAbsolute(MemoryStream stream)
    {
        using var zip = new ZipArchive(stream, ZipArchiveMode.Update, leaveOpen: true);
        var entry = zip.GetEntry("xl/workbook.xml");
        if (entry == null) return;

        XDocument document;
        using (var input = entry.Open())
            document = XDocument.Load(input);

        var changed = false;
        foreach (var name in document.Descendants(SpreadsheetMl + "definedName"))
        {
            if ((string?)name.Attribute("name") != "_xlnm.Print_Titles") continue;
            var absolute = RelativeRows.Replace(name.Value, "!$$$1:$$$2");
            if (absolute == name.Value) continue;
            name.Value = absolute;
            changed = true;
        }
        if (!changed) return;

        using var output = entry.Open();
        output.SetLength(0);
        document.Save(output);
    }
}
