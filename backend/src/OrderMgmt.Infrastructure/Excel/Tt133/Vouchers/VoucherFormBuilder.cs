using ClosedXML.Excel;
using OrderMgmt.Application.Tt133.Models;
using OrderMgmt.Application.Tt133.Vouchers;

namespace OrderMgmt.Infrastructure.Excel.Tt133.Vouchers;

public enum VoucherPaper { A5Landscape, A4Portrait }

public sealed record VoucherSigner(string Title, string Hint, string? Name);

/// Layout of the TT133 vouchers (01-TT, 02-TT, 01-VT, 02-VT): writes the voucher blocks itself and leaves the table,
/// the plain text lines and the column widths to the wrapped Tt133FormBuilder.
public sealed class VoucherFormBuilder
{
    private readonly Tt133FormContext _context;
    private readonly IReadOnlyList<Tt133Column> _columns;
    private readonly VoucherPaper _paper;
    private readonly int _rightStart;
    private readonly int _leftEnd;

    public VoucherFormBuilder(IXLWorksheet sheet, Tt133FormContext context, IReadOnlyList<Tt133Column> columns,
        VoucherPaper paper, int? labelColumn = null)
    {
        Sheet = sheet;
        _context = context;
        _columns = columns;
        _paper = paper;
        Table = new Tt133FormBuilder(sheet, context, columns, landscape: paper == VoucherPaper.A5Landscape, labelColumn);

        var count = columns.Count;
        var rightWidth = Math.Max(2, count * 2 / 5);
        _rightStart = Math.Max(2, count - rightWidth + 1);
        _leftEnd = _rightStart - 1;
    }

    public Tt133FormBuilder Table { get; }

    public IXLWorksheet Sheet { get; }

    /// Unit and address on the left, the form code and its legal basis on the right; no MST line.
    public void WriteHeaderBlock(string formCode)
    {
        var unit = string.IsNullOrWhiteSpace(_context.CompanyName)
            ? _context.BranchName
            : string.IsNullOrWhiteSpace(_context.BranchName)
                ? _context.CompanyName
                : $"{_context.CompanyName} – {_context.BranchName}";
        string[] left = [Labelled("Đơn vị:", unit), Labelled("Địa chỉ:", _context.Address), string.Empty];
        string[] right =
        [
            $"Mẫu số {formCode}",
            "(Ban hành theo Thông tư số 133/2016/TT-BTC",
            "ngày 26/8/2016 của Bộ Tài chính)",
        ];

        for (var i = 0; i < left.Length; i++)
        {
            var row = Table.Row + i;
            var leftCell = Merged(row, 1, _leftEnd);
            leftCell.Value = left[i];
            leftCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

            var rightCell = Merged(row, _rightStart, _columns.Count);
            rightCell.Value = right[i];
            rightCell.Style.Font.Bold = i == 0;
            rightCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            var lines = Math.Max(
                Wrap(leftCell, left[i], 1, _leftEnd, bold: false),
                Wrap(rightCell, right[i], _rightStart, _columns.Count, bold: i == 0));
            SetRowLines(row, lines);
        }
        Table.Skip(left.Length + 1);
    }

    /// Title, "ĐÃ HỦY", date and centre lines on the left part; the right lines next to them, one per row.
    public void WriteTitleBlock(string title, DateOnly date, bool cancelled, IReadOnlyList<string> centreLines,
        IReadOnlyList<string> rightLines)
    {
        var first = Table.Row;
        var centre = 0;
        var rowLines = new Dictionary<int, int>();

        void Track(int row, int lines) =>
            rowLines[row] = Math.Max(lines, rowLines.GetValueOrDefault(row, 1));

        void PutCentre(string text, bool bold = false, bool italic = false, double? size = null)
        {
            var cell = Merged(first + centre, 1, _leftEnd);
            cell.Value = text;
            cell.Style.Font.Bold = bold;
            cell.Style.Font.Italic = italic;
            if (size is { } s) cell.Style.Font.FontSize = s;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            if (size is null) Track(first + centre, Wrap(cell, text, 1, _leftEnd, bold));
            centre++;
        }

        PutCentre(title, bold: true, size: 14);
        if (cancelled) PutCentre("ĐÃ HỦY", bold: true);
        PutCentre(DateLine(date), italic: true);
        foreach (var line in centreLines) PutCentre(line);

        for (var i = 0; i < rightLines.Count; i++)
        {
            var cell = Merged(first + i, _rightStart, _columns.Count);
            cell.Value = rightLines[i];
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
            Track(first + i, Wrap(cell, rightLines[i], _rightStart, _columns.Count, bold: false));
        }
        foreach (var (row, lines) in rowLines) SetRowLines(row, lines);

        Table.Skip(Math.Max(centre, rightLines.Count) + 1);
    }

    /// "{label} {value}", or "{label} {Dots}" when the value is blank.
    public void WriteLine(string label, string? value) =>
        Table.WriteText(string.IsNullOrWhiteSpace(value)
            ? $"{label} {VoucherPrintRules.Dots}"
            : $"{label} {value.Trim()}");

    /// The date, then one block of columns per signer (split by column width): title, hint, four empty rows, name.
    public void WriteSignatures(DateOnly date, IReadOnlyList<VoucherSigner> signers)
    {
        var count = _columns.Count;
        if (signers.Count > count)
            throw new ArgumentOutOfRangeException(nameof(signers), signers.Count,
                $"A voucher with {count} columns cannot hold {signers.Count} signature blocks.");
        var row = Table.Row + 1;

        var dateCell = Merged(row, _rightStart, count);
        dateCell.Value = DateLine(date);
        dateCell.Style.Font.Italic = true;
        dateCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

        var bounds = BlockBounds(signers.Count);
        WriteSignatureRow(row + 1, bounds, signers.Select(s => (string?)s.Title).ToList(), bold: true, italic: false);
        WriteSignatureRow(row + 2, bounds, signers.Select(s => (string?)s.Hint).ToList(), bold: false, italic: true);
        WriteSignatureRow(row + 7, bounds, signers.Select(s => s.Name).ToList(), bold: true, italic: false);

        Table.Skip(9);
    }

    public void Finish()
    {
        Table.Finish(repeatTableHeader: Table.TableHeaderFirstRow > 0);
        if (_paper != VoucherPaper.A5Landscape) return;

        var setup = Sheet.PageSetup;
        setup.PaperSize = XLPaperSize.A5Paper;
        setup.PageOrientation = XLPageOrientation.Landscape;
        setup.FitToPages(1, 1);
    }

    private void WriteSignatureRow(int row, int[] bounds, IReadOnlyList<string?> texts, bool bold, bool italic)
    {
        var lines = 1;
        for (var k = 0; k < texts.Count; k++)
        {
            var first = bounds[k] + 1;
            var last = bounds[k + 1];
            var text = texts[k] ?? string.Empty;
            var cell = Merged(row, first, last);
            cell.Value = text;
            cell.Style.Font.Bold = bold;
            cell.Style.Font.Italic = italic;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
            cell.Style.Alignment.WrapText = true;
            lines = Math.Max(lines, Tt133FormBuilder.LineCount(text, Width(first, last), bold));
        }
        Sheet.Row(row).Height = lines * Sheet.RowHeight;
    }

    /// b[0] = 0, b[n] = count; b[k] is the first column where the summed width reaches k/n of the total, kept so
    /// that every block has at least one column.
    private int[] BlockBounds(int n)
    {
        var count = _columns.Count;
        var cumulative = new double[count + 1];
        for (var j = 1; j <= count; j++) cumulative[j] = cumulative[j - 1] + _columns[j - 1].Width;
        var total = cumulative[count];

        var bounds = new int[n + 1];
        bounds[n] = count;
        for (var k = 1; k < n; k++)
        {
            var target = k * total / n - 0.001;
            var j = 0;
            while (j < count && cumulative[j] < target) j++;
            bounds[k] = Math.Clamp(j, bounds[k - 1] + 1, count - (n - k));
        }
        return bounds;
    }

    /// Wraps a text longer than its merged columns; returns its line count.
    private int Wrap(IXLCell cell, string text, int firstColumn, int lastColumn, bool bold)
    {
        var lines = Tt133FormBuilder.LineCount(text, Width(firstColumn, lastColumn), bold);
        if (lines > 1) cell.Style.Alignment.WrapText = true;
        return lines;
    }

    private void SetRowLines(int row, int lines)
    {
        if (lines > 1) Sheet.Row(row).Height = lines * Sheet.RowHeight;
    }

    private double Width(int firstColumn, int lastColumn)
    {
        var width = 0d;
        for (var i = firstColumn; i <= lastColumn; i++) width += _columns[i - 1].Width;
        return width;
    }

    private static string DateLine(DateOnly date) => $"Ngày {date:dd} tháng {date:MM} năm {date:yyyy}";

    private static string Labelled(string label, string? value) =>
        string.IsNullOrWhiteSpace(value) ? label : $"{label} {value.Trim()}";

    private IXLCell Merged(int row, int firstColumn, int lastColumn)
    {
        if (lastColumn > firstColumn)
            Sheet.Range(row, firstColumn, row, lastColumn).Merge();
        return Sheet.Cell(row, firstColumn);
    }
}
