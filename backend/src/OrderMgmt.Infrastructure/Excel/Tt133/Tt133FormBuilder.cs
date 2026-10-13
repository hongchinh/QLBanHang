using ClosedXML.Excel;
using OrderMgmt.Application.Tt133.Models;

namespace OrderMgmt.Infrastructure.Excel.Tt133;

/// Shared layout of the TT133 forms (Phụ lục 4): header block, title, table, footer and page setup.
public sealed class Tt133FormBuilder
{
    private const string FontName = "Times New Roman";

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

    private IXLCell Merged(int row, int firstColumn, int lastColumn)
    {
        if (lastColumn > firstColumn)
            Sheet.Range(row, firstColumn, row, lastColumn).Merge();
        return Sheet.Cell(row, firstColumn);
    }
}
