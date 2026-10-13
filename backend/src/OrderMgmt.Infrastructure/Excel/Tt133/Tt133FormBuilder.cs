using ClosedXML.Excel;
using OrderMgmt.Application.Inventory.Common;
using OrderMgmt.Application.Tt133.Models;

namespace OrderMgmt.Infrastructure.Excel.Tt133;

/// Shared layout of the TT133 forms (Phụ lục 4): header block, title, table, footer and page setup.
public sealed class Tt133FormBuilder
{
    private const string FontName = "Times New Roman";
    private const string MoneyFormat = "#,##0;(#,##0)";
    private const string QuantityFormat = "#,##0.###;(#,##0.###)";
    private const string DateFormat = "dd/MM/yyyy";
    private const int MaxSheetNameLength = 31;
    private static readonly char[] InvalidSheetChars = ['[', ']', ':', '*', '?', '/', '\\'];

    private readonly Tt133FormContext _context;
    private readonly IReadOnlyList<Tt133Column> _columns;
    private readonly bool _landscape;
    private readonly int _labelColumn;
    private bool _gapAfterTitle;

    /// labelColumn (1-based): where summary labels go; null = the first Text column that has no Group.
    public Tt133FormBuilder(IXLWorksheet sheet, Tt133FormContext context, IReadOnlyList<Tt133Column> columns, bool landscape, int? labelColumn = null)
    {
        Sheet = sheet;
        _context = context;
        _columns = columns;
        _landscape = landscape;
        _labelColumn = labelColumn ?? DefaultLabelColumn(columns);
        Row = 1;

        sheet.Style.Font.FontName = FontName;
        sheet.Style.Font.FontSize = 11;
    }

    public IXLWorksheet Sheet { get; }

    /// The first row of the table header, set by the latest WriteTableHeader.
    public int TableHeaderFirstRow { get; private set; }

    /// The symbol row of the table header, set by the latest WriteTableHeader.
    public int TableHeaderLastRow { get; private set; }

    /// The next free row (1-based).
    public int Row { get; private set; }

    public void Skip(int rows = 1)
    {
        Row += rows;
        _gapAfterTitle = false;
    }

    public void WriteHeaderBlock(string? formCode)
    {
        var count = _columns.Count;
        var rightWidth = Math.Max(1, count / 3);
        var rightStart = count - rightWidth + 1;
        var leftEnd = Math.Max(1, Math.Min((count + 1) / 2, rightStart - 1));

        var unit = string.IsNullOrWhiteSpace(_context.CompanyName)
            ? _context.BranchName
            : string.IsNullOrWhiteSpace(_context.BranchName)
                ? _context.CompanyName
                : $"{_context.CompanyName} – {_context.BranchName}";
        string[] left =
        [
            Labelled("Đơn vị:", unit),
            Labelled("Địa chỉ:", _context.Address),
            Labelled("MST:", _context.TaxCode),
        ];
        string[]? right = string.IsNullOrWhiteSpace(formCode)
            ? null
            :
            [
                $"Mẫu số {formCode}",
                "(Ban hành theo Thông tư số 133/2016/TT-BTC",
                "ngày 26/8/2016 của Bộ Tài chính)",
            ];

        for (var i = 0; i < left.Length; i++)
        {
            var cell = Merged(Row, 1, leftEnd);
            cell.Value = left[i];
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

            if (right != null)
            {
                var rightCell = Merged(Row, rightStart, count);
                rightCell.Value = right[i];
                rightCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                rightCell.Style.Font.Bold = i == 0;
            }
            Row++;
        }
        Skip();
    }

    public void WriteTitle(string title, string periodLine, params string[] parameterLines)
    {
        var titleCell = Merged(Row, 1, _columns.Count);
        titleCell.Value = title;
        titleCell.Style.Font.Bold = true;
        titleCell.Style.Font.FontSize = 14;
        titleCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        Row++;

        WriteText(periodLine, centered: true);
        foreach (var line in parameterLines)
            WriteText(line, centered: true);

        Skip();
        _gapAfterTitle = true;
    }

    /// One row merged over all columns.
    public void WriteText(string text, bool bold = false, bool italic = false, bool centered = false)
    {
        var cell = Merged(Row, 1, _columns.Count);
        cell.Value = text;
        cell.Style.Font.Bold = bold;
        cell.Style.Font.Italic = italic;
        cell.Style.Alignment.Horizontal = centered
            ? XLAlignmentHorizontalValues.Center
            : XLAlignmentHorizontalValues.Left;
        Row++;
        _gapAfterTitle = false;
    }

    public void WriteNote(string note)
    {
        if (_gapAfterTitle)
        {
            // The note belongs to the title block: it takes the empty row and a new one is kept before the table.
            Row--;
            WriteText(note, italic: true, centered: true);
            Skip();
            _gapAfterTitle = true;
            return;
        }
        WriteText(note, italic: true, centered: true);
    }

    /// Two header tiers (a group and its sub-columns) plus the symbol row. May be called more than once.
    public void WriteTableHeader()
    {
        var first = Row;
        var count = _columns.Count;

        var i = 0;
        while (i < count)
        {
            var column = _columns[i];
            var col = i + 1;
            if (column.Group == null)
            {
                Sheet.Range(first, col, first + 1, col).Merge();
                Sheet.Cell(first, col).Value = column.Header;
                i++;
                continue;
            }

            var end = i;
            while (end + 1 < count && _columns[end + 1].Group == column.Group) end++;
            if (end > i) Sheet.Range(first, col, first, end + 1).Merge();
            Sheet.Cell(first, col).Value = column.Group;
            for (var j = i; j <= end; j++)
                Sheet.Cell(first + 1, j + 1).Value = _columns[j].Header;
            i = end + 1;
        }

        for (var j = 0; j < count; j++)
            Sheet.Cell(first + 2, j + 1).Value = _columns[j].Symbol;

        var header = Sheet.Range(first, 1, first + 2, count);
        header.Style.Font.Bold = true;
        header.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        header.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        header.Style.Alignment.WrapText = true;
        Bordered(header);

        var symbols = Sheet.Range(first + 2, 1, first + 2, count);
        symbols.Style.Font.Bold = false;
        symbols.Style.Font.Italic = true;

        TableHeaderFirstRow = first;
        TableHeaderLastRow = first + 2;
        Row = first + 3;
        _gapAfterTitle = false;
    }

    /// One value per column; null = empty.
    public void WriteRow(params object?[] cells)
    {
        for (var i = 0; i < _columns.Count; i++)
        {
            var cell = Sheet.Cell(Row, i + 1);
            var value = i < cells.Length ? cells[i] : null;
            if (value != null) SetValue(cell, value);
            FormatCell(cell, _columns[i].Kind);
        }
        Bordered(Sheet.Range(Row, 1, Row, _columns.Count));
        Row++;
        _gapAfterTitle = false;
    }

    /// Column is 1-based. The label goes in the label column; label and values are bold.
    public void WriteSummaryRow(string label, params (int Column, object? Value)[] values)
    {
        for (var i = 0; i < _columns.Count; i++)
            FormatCell(Sheet.Cell(Row, i + 1), _columns[i].Kind);

        var labelCell = Sheet.Cell(Row, _labelColumn);
        labelCell.Value = label;
        labelCell.Style.Font.Bold = true;

        foreach (var (column, value) in values)
        {
            var cell = Sheet.Cell(Row, column);
            if (value != null) SetValue(cell, value);
            cell.Style.Font.Bold = true;
        }
        Bordered(Sheet.Range(Row, 1, Row, _columns.Count));
        Row++;
        _gapAfterTitle = false;
    }

    /// "Sổ này có…" lines, the print date and the three signatures.
    public void WriteBookFooter()
    {
        var count = _columns.Count;
        Skip();
        WriteText("- Sổ này có … trang, đánh số từ trang 01 đến trang …");
        WriteText(_context.OpeningDate is { } opening
            ? $"- Ngày mở sổ: {opening:dd/MM/yyyy}"
            : "- Ngày mở sổ: …");

        var rightStart = count - Math.Max(1, count / 3) + 1;
        var dateCell = Merged(Row, rightStart, count);
        dateCell.Value = $"Ngày {_context.PrintDate:dd} tháng {_context.PrintDate:MM} năm {_context.PrintDate:yyyy}";
        dateCell.Style.Font.Italic = true;
        dateCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        Row++;

        string[] titles = ["Người ghi sổ", "Kế toán trưởng", "Người đại diện theo pháp luật"];
        string[] hints = ["(Ký, họ tên)", "(Ký, họ tên)", "(Ký, họ tên, đóng dấu)"];
        string?[] names = [_context.BookkeeperName, _context.ChiefAccountantName, _context.LegalRepresentativeName];

        WriteSignatureRow(titles, bold: true, italic: false);
        WriteSignatureRow(hints, bold: false, italic: true);
        Skip(4);
        WriteSignatureRow(names, bold: true, italic: false);

        if (!string.IsNullOrWhiteSpace(_context.AccountingServiceNote))
            WriteText(_context.AccountingServiceNote, italic: true);
    }

    /// Column widths and print setup. The table header rows repeat on every page unless repeatTableHeader is false.
    public void Finish(bool repeatTableHeader = true)
    {
        for (var i = 0; i < _columns.Count; i++)
            Sheet.Column(i + 1).Width = _columns[i].Width;

        var setup = Sheet.PageSetup;
        setup.PaperSize = XLPaperSize.A4Paper;
        setup.PageOrientation = _landscape ? XLPageOrientation.Landscape : XLPageOrientation.Portrait;
        setup.FitToPages(1, 0);
        setup.Margins.Left = setup.Margins.Right = setup.Margins.Top = setup.Margins.Bottom = 0.59;
        if (repeatTableHeader && TableHeaderFirstRow > 0)
            setup.SetRowsToRepeatAtTop(TableHeaderFirstRow, TableHeaderLastRow);
    }

    /// Replaces [ ] : * ? / \ with _, cuts to 31 characters and makes the name unique (case-insensitive); adds the result to used.
    public static string SheetName(string code, ISet<string> used)
    {
        var chars = (code ?? string.Empty).Trim().ToCharArray();
        for (var i = 0; i < chars.Length; i++)
            if (InvalidSheetChars.Contains(chars[i])) chars[i] = '_';
        var baseName = new string(chars).Trim();
        if (baseName.Length > MaxSheetNameLength) baseName = baseName[..MaxSheetNameLength].TrimEnd();
        if (baseName.Length == 0) baseName = "Sheet";

        var name = baseName;
        for (var n = 2; used.Any(u => string.Equals(u, name, StringComparison.OrdinalIgnoreCase)); n++)
        {
            var suffix = $" ({n})";
            name = baseName[..Math.Min(baseName.Length, MaxSheetNameLength - suffix.Length)] + suffix;
        }
        used.Add(name);
        return name;
    }

    private void WriteSignatureRow(string?[] texts, bool bold, bool italic)
    {
        var count = _columns.Count;
        for (var k = 0; k < 3; k++)
        {
            var first = Math.Clamp(k * count / 3 + 1, 1, count);
            var last = Math.Clamp((k + 1) * count / 3, first, count);
            var cell = Merged(Row, first, last);
            cell.Value = texts[k] ?? string.Empty;
            cell.Style.Font.Bold = bold;
            cell.Style.Font.Italic = italic;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }
        Row++;
        _gapAfterTitle = false;
    }

    public static string PeriodLine(DateOnly from, DateOnly to)
    {
        if (from.Year == to.Year && from.Month == to.Month && from.Day == 1
            && to.Day == DateTime.DaysInMonth(to.Year, to.Month))
            return $"Tháng {from:MM} năm {from:yyyy}";
        if (from.Year == to.Year && from is { Month: 1, Day: 1 } && to is { Month: 12, Day: 31 })
            return $"Năm {from:yyyy}";
        return $"Từ ngày {from:dd/MM/yyyy} đến ngày {to:dd/MM/yyyy}";
    }

    private static string Labelled(string label, string? value) =>
        string.IsNullOrWhiteSpace(value) ? label : $"{label} {value.Trim()}";

    private static int DefaultLabelColumn(IReadOnlyList<Tt133Column> columns)
    {
        for (var i = 0; i < columns.Count; i++)
            if (columns[i].Kind == Tt133CellKind.Text && columns[i].Group == null)
                return i + 1;
        return 1;
    }

    private static void Bordered(IXLRange range)
    {
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
    }

    private static void SetValue(IXLCell cell, object value)
    {
        switch (value)
        {
            case DateOnly d: cell.Value = d.ToDateTime(TimeOnly.MinValue); break;
            case DateTimeOffset at: cell.Value = VnTime.ToVnDate(at).ToDateTime(TimeOnly.MinValue); break;
            case DateTime dt: cell.Value = dt; break;
            case decimal m: cell.Value = m; break;
            case int n: cell.Value = n; break;
            case long l: cell.Value = l; break;
            case double x: cell.Value = x; break;
            case string text: cell.Value = text; break;
            default: cell.Value = value.ToString(); break;
        }
    }

    private static void FormatCell(IXLCell cell, Tt133CellKind kind)
    {
        switch (kind)
        {
            case Tt133CellKind.Money:
                cell.Style.NumberFormat.Format = MoneyFormat;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                break;
            case Tt133CellKind.Quantity:
                cell.Style.NumberFormat.Format = QuantityFormat;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                break;
            case Tt133CellKind.Date:
                cell.Style.DateFormat.Format = DateFormat;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                break;
            default:
                cell.Style.Alignment.WrapText = true;
                break;
        }
    }

    private IXLCell Merged(int row, int firstColumn, int lastColumn)
    {
        if (lastColumn > firstColumn)
            Sheet.Range(row, firstColumn, row, lastColumn).Merge();
        return Sheet.Cell(row, firstColumn);
    }
}
