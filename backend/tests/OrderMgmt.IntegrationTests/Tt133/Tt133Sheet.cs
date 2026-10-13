using ClosedXML.Excel;

namespace OrderMgmt.IntegrationTests.Tt133;

/// Workbook assertions of the TT133 export tests: find a row by its label, not by a fixed row number.
internal static class Tt133Sheet
{
    /// The row of the first used cell whose string equals `text`.
    public static int RowOf(IXLWorksheet ws, string text)
    {
        var cell = ws.CellsUsed().FirstOrDefault(c => c.GetString() == text);
        if (cell is null) throw new InvalidOperationException($"No cell with the text '{text}' in sheet '{ws.Name}'.");
        return cell.Address.RowNumber;
    }

    /// The column of the first cell of `headerRow` whose string equals `text`.
    public static int ColumnOf(IXLWorksheet ws, int headerRow, string text)
    {
        var cell = ws.Row(headerRow).CellsUsed().FirstOrDefault(c => c.GetString() == text);
        if (cell is null) throw new InvalidOperationException($"No cell with the text '{text}' in row {headerRow} of sheet '{ws.Name}'.");
        return cell.Address.ColumnNumber;
    }

    public static bool Contains(IXLWorksheet ws, string text) => ws.CellsUsed().Any(c => c.GetString() == text);

    public static decimal Number(IXLWorksheet ws, int row, int column) => ws.Cell(row, column).GetValue<decimal>();

    /// The workbook of a response body.
    public static async Task<XLWorkbook> OpenAsync(HttpResponseMessage response)
    {
        var bytes = await response.Content.ReadAsByteArrayAsync();
        return new XLWorkbook(new MemoryStream(bytes));
    }
}
