using ClosedXML.Excel;
using ClosedXML.Excel.Drawings;
using OrderMgmt.Application.CashDebt.PaymentQr;
using OrderMgmt.Application.Tt133.Models;
using OrderMgmt.Application.Tt133.Vouchers;
using OrderMgmt.Infrastructure.Qr;

namespace OrderMgmt.Infrastructure.Excel.Tt133.Vouchers;

public enum VoucherPaper { A5Landscape, A4Portrait }

public sealed record VoucherSigner(string Title, string Hint, string? Name);

/// Layout of the TT133 vouchers (01-TT, 02-TT, 01-VT, 02-VT): writes the voucher blocks itself and leaves the table,
/// the plain text lines and the column widths to the wrapped Tt133FormBuilder.
public sealed class VoucherFormBuilder
{
    // Page geometry of the PDF that LibreOffice prints: A4 is 595.28 x 841.89 pt, A5 595.28 x 419.53 pt;
    // Tt133FormBuilder.Finish sets 0.59 inch margins and no header or footer is printed.
    private const double A4WidthPoints = 595.28;
    private const double A4HeightPoints = 841.89;
    private const double A5HeightPoints = 419.53;
    private const double MarginPoints = 0.59 * 72;
    // ClosedXML saves a column width with 0.710625 units of padding, and LibreOffice prints a width unit as about
    // 5.56 pt (the digit width of the default Calibri 11; measured: the 01-VT table of 112.69 saved units printed
    // 626 pt wide before scaling, and the 02-VT columns 13 and 15 printed 61.7 and 70.6 pt at 81 %). LibreOffice fits
    // the sheet with a zoom in whole percents.
    private const double SavedWidthPadding = 0.710625;
    private const double PointsPerWidthUnit = 5.56;
    // The estimate may be a few percent off: the block is kept whole for every page height within this share of it.
    private const double PageHeightTolerance = 0.03;
    // The sheet font size set by Tt133FormBuilder; Sheet.RowHeight is the height of a row of it.
    private const double DefaultFontSize = 11;
    // The transfer QR: its side on paper, in the last QrColumns columns.
    private const double QrPrintMillimetres = 25;
    // The A5 landscape receipt is fitted to one page by its height, so its zoom is known only once the whole sheet is
    // written (measured: 78 % with the short company profile, 73 % with the filled one, 71 % with a long reason, and 48 % with
    // maximum-length fields, while the width alone allows 89 %). EndQrBeside reserves the rows
    // for a picture at this zoom, and Finish draws it at the real one.
    private const double A5ReserveScale = 0.75;
    // A height shortfall below this is rounding noise of the summed row heights, not a reason to grow a row.
    private const double NegligiblePoints = 0.01;
    private const int QrColumns = 2;
    private const int QrTopOffsetPixels = 2;
    private const double QrCaptionFontSize = 8;
    private const double PointsPerPixel = 0.75;

    private readonly Tt133FormContext _context;
    private readonly IReadOnlyList<Tt133Column> _columns;
    private readonly VoucherPaper _paper;
    private readonly int _rightStart;
    private readonly int _leftEnd;
    private int _lastBodyRow;
    private int _signatureFirstRow;
    private int _signatureLastRow;
    private VoucherPaymentQrDto? _qr;
    private int _qrFirstRow;
    private int? _textLastColumn;
    private readonly List<QrBeside> _qrBlocks = [];

    /// A QR drawn beside the lines: the picture from the top of FirstRow, the captions from CaptionRow.
    private sealed record QrBeside(IXLPicture Picture, int FirstRow, int CaptionRow);

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

    /// Unit and address on the left, one row each; no MST line. The form code and its legal basis on the right are one
    /// wrapped cell over the three rows, so its lines stay together when a left line wraps; the rows are made tall
    /// enough for both sides. Top-aligned.
    public void WriteHeaderBlock(string formCode)
    {
        var unit = string.IsNullOrWhiteSpace(_context.CompanyName)
            ? _context.BranchName
            : string.IsNullOrWhiteSpace(_context.BranchName)
                ? _context.CompanyName
                : $"{_context.CompanyName} – {_context.BranchName}";
        string[] left = [Labelled("Đơn vị:", unit), Labelled("Địa chỉ:", _context.Address), string.Empty];
        var formLine = $"Mẫu số {formCode}";
        const string basis = "(Ban hành theo Thông tư số 133/2016/TT-BTC";
        const string basisDate = "ngày 26/8/2016 của Bộ Tài chính)";

        var count = _columns.Count;
        var first = Table.Row;
        var rightLines = Tt133FormBuilder.LineCount(formLine, Width(_rightStart, count), bold: true)
                         + Tt133FormBuilder.LineCount(basis, Width(_rightStart, count), bold: false)
                         + Tt133FormBuilder.LineCount(basisDate, Width(_rightStart, count), bold: false);

        var leftLines = 0;
        for (var i = 0; i < left.Length; i++)
        {
            var leftCell = Merged(first + i, 1, _leftEnd);
            leftCell.Value = left[i];
            leftCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
            leftCell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;

            var lines = Wrap(leftCell, left[i], 1, _leftEnd, bold: false);
            // The last row takes the lines the right part still needs.
            if (i == left.Length - 1) lines = Math.Max(lines, rightLines - leftLines);
            SetRowLines(first + i, lines);
            leftLines += lines;
        }

        Sheet.Range(first, _rightStart, first + left.Length - 1, count).Merge();
        var rightCell = Sheet.Cell(first, _rightStart);
        var text = rightCell.GetRichText();
        text.AddText(formLine).SetBold();
        text.AddText($"\n{basis}\n{basisDate}");
        rightCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        rightCell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
        rightCell.Style.Alignment.WrapText = true;

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
            if (size is { } s)
            {
                cell.Style.Font.FontSize = s;
                // An explicit height for the larger font: LibreOffice would grow the row itself, past the page estimate.
                Sheet.Row(first + centre).Height = Sheet.RowHeight * s / DefaultFontSize;
            }
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
        WriteText(string.IsNullOrWhiteSpace(value)
            ? $"{label} {VoucherPrintRules.Dots}"
            : $"{label} {value.Trim()}");

    /// One text line; while a QR block is open it covers the columns left of the QR only.
    public void WriteText(string text) => Table.WriteText(text, lastColumn: _textLastColumn);

    /// Opens a block of lines (WriteLine, WriteText) that stop two columns short of the right edge, to leave room for
    /// the transfer QR beside them. EndQrBeside closes it and draws the QR.
    public void BeginQrBeside(VoucherPaymentQrDto qr)
    {
        if (_columns.Count < 4)
            throw new InvalidOperationException("A QR beside the lines needs at least four columns.");
        if (_qr != null)
            throw new InvalidOperationException("A QR block is already open; call EndQrBeside first.");
        _qr = qr;
        _qrFirstRow = Table.Row;
        _textLastColumn = _columns.Count - QrColumns;
    }

    /// The QR picture in the last two columns from the first line of the block, with its two captions in the last two
    /// rows of the block. Blank rows are added after the lines until the rows above the captions hold the picture; on
    /// A5 Finish draws it again at the zoom of the finished sheet.
    public void EndQrBeside()
    {
        var qr = _qr ?? throw new InvalidOperationException("BeginQrBeside was not called.");
        var first = _qrFirstRow;
        // The block is closed first, so a failure below leaves no half-open state behind.
        _qr = null;
        _qrFirstRow = 0;
        _textLastColumn = null;
        var count = _columns.Count;
        var scale = InitialScale();
        var needed = QrSpanPoints(scale);

        var rows = Math.Max(3, Table.Row - first);
        if (Table.Row - first < rows) Table.Skip(rows - (Table.Row - first));
        while (HeightOfRows(first, first + rows - 3) < needed)
        {
            rows++;
            Table.Skip();
        }

        var firstColumn = count - QrColumns + 1;
        var picture = AddQrPicture(qr, first, scale);

        var last = first + rows - 1;
        PutQrCaption(last - 1, firstColumn, VoucherPrintRules.QrScanCaption, bold: true);
        PutQrCaption(last, firstColumn, VoucherPrintRules.QrBankCaption(qr), bold: false);
        _qrBlocks.Add(new QrBeside(picture, first, last - 1));
    }

    /// The payment block of the stock-out voucher (02-VT), after the last text line and before the signatures: the
    /// transfer details on the left, the QR in the last two columns from the first row, its
    /// caption in the last row, and a thin outside border around the whole block. The rows get explicit heights (the
    /// page-break estimate reads only those): the rows above the caption hold the picture, the caption row keeps its own.
    /// A4 only: its zoom is known when the block is written, while an A5 sheet is fitted by its finished height.
    public void WritePaymentBlock(VoucherPaymentQrDto qr)
    {
        if (_paper != VoucherPaper.A4Portrait)
            throw new InvalidOperationException("The payment block is drawn on the A4 portrait voucher only.");
        var count = _columns.Count;
        if (count < 4)
            throw new InvalidOperationException("A payment block needs at least four columns.");
        var textEnd = count - QrColumns;

        var lines = new List<(string Text, bool Bold)>
        {
            ("THÔNG TIN CHUYỂN KHOẢN", true),
            ($"Ngân hàng: {qr.BankName}", false),
            ($"Số tài khoản: {qr.AccountNumber}", false),
            ($"Chủ tài khoản: {qr.AccountHolderName}", false),
            (VoucherPrintRules.QrAmountLine(qr), true),
        };
        if (VoucherPrintRules.QrBreakdownLine(qr) is { } breakdown) lines.Add((breakdown, false));
        lines.Add(($"Nội dung chuyển khoản: {qr.Content}", false));

        var first = Table.Row;
        var heights = new double[lines.Count];
        for (var i = 0; i < lines.Count; i++)
        {
            var (text, bold) = lines[i];
            var cell = Merged(first + i, 1, textEnd);
            cell.Value = text;
            cell.Style.Font.Bold = bold;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
            heights[i] = Wrap(cell, text, 1, textEnd, bold) * Sheet.RowHeight;
        }

        // The rows above the caption hold the picture; the missing height is shared equally by them, and the caption
        // row keeps its own height.
        var scale = InitialScale();
        var above = lines.Count - 1;
        var extra = Math.Max(0, QrSpanPoints(scale) - heights.Take(above).Sum()) / above;
        for (var i = 0; i < lines.Count; i++) Sheet.Row(first + i).Height = heights[i] + (i < above ? extra : 0);

        var last = first + lines.Count - 1;
        Sheet.Range(first, 1, last, count).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        AddQrPicture(qr, first, scale);
        PutQrCaption(last, count - QrColumns + 1, VoucherPrintRules.QrScanCaption, bold: true);

        Table.Skip(lines.Count);
    }

    /// One table body row (see Tt133FormBuilder.WriteRow); the builder keeps the last ones with the signatures.
    public void WriteRow(params object?[] cells)
    {
        _lastBodyRow = Table.Row;
        Table.WriteRow(cells);
    }

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
        _signatureFirstRow = row;
        _signatureLastRow = row + 7;

        Table.Skip(9);
    }

    public void Finish()
    {
        Table.Finish(repeatTableHeader: Table.TableHeaderFirstRow > 0);
        if (_paper == VoucherPaper.A4Portrait)
        {
            KeepSignaturesOnOnePage();
            return;
        }

        var setup = Sheet.PageSetup;
        setup.PaperSize = XLPaperSize.A5Paper;
        setup.PageOrientation = XLPageOrientation.Landscape;
        setup.FitToPages(1, 1);
        // Fitting to one page leaves the spare width on the right: centre the voucher instead.
        setup.CenterHorizontally = true;
        FitQrBesideToPage();
    }

    /// Draws the QR pictures beside the lines at the zoom LibreOffice fits the finished A5 sheet with. When the rows
    /// above the captions are too short for the larger picture, the last of them is made taller, which can lower the
    /// zoom again: repeated until it settles (it only goes down, in whole percents).
    private void FitQrBesideToPage()
    {
        if (_qrBlocks.Count == 0) return;
        while (true)
        {
            var scale = A5PrintScale();
            var needed = QrSpanPoints(scale);
            var grown = false;
            foreach (var block in _qrBlocks)
            {
                var missing = needed - HeightOfRows(block.FirstRow, block.CaptionRow - 1);
                if (missing <= NegligiblePoints) continue;
                Sheet.Row(block.CaptionRow - 1).Height += missing;
                grown = true;
            }
            if (grown) continue;

            foreach (var block in _qrBlocks) PlaceQrPicture(block.Picture, block.FirstRow, QrPixels(scale));
            return;
        }
    }

    /// A4 is fitted to one page wide only, so a long voucher flows onto more pages. The end of the voucher is kept on
    /// one page: the last two body rows, the totals and text lines and the signature block (date to names). When a
    /// page could end inside it, a page break before it moves it whole to a new page, where the repeated table header
    /// stands over real rows.
    private void KeepSignaturesOnOnePage()
    {
        if (_signatureFirstRow == 0) return;
        var keepFrom = _lastBodyRow > 0
            ? Math.Max(Table.TableHeaderLastRow + 1, _lastBodyRow - 1)
            : _signatureFirstRow;

        var pageHeight = (A4HeightPoints - 2 * MarginPoints) / A4PrintScale();

        // A break cannot help when the kept rows and the repeated header do not fit on one page.
        var kept = RepeatedHeaderHeight();
        for (var r = keepFrom; r <= _signatureLastRow; r++) kept += Sheet.Row(r).Height;
        if (kept > pageHeight * (1 - PageHeightTolerance)) return;

        var low = PagesOf(keepFrom, pageHeight * (1 - PageHeightTolerance));
        var high = PagesOf(keepFrom, pageHeight * (1 + PageHeightTolerance));
        if (low.First != low.Last || high.First != high.Last || low.First != high.First)
            Sheet.PageSetup.AddHorizontalPageBreak(keepFrom - 1);
    }

    /// The zoom (0-1) that LibreOffice prints an A4 portrait voucher at: the table width fitted to the page width.
    private double A4PrintScale() => WholePercent((A4WidthPoints - 2 * MarginPoints) / TableWidthPoints());

    /// The zoom (0-1) that LibreOffice prints an A5 landscape voucher at: the sheet, up to its last used row, fitted to
    /// one page both ways (A5 landscape is as wide as A4 portrait).
    private double A5PrintScale()
    {
        var height = HeightOfRows(1, Sheet.LastRowUsed()?.RowNumber() ?? 0);
        var fit = (A4WidthPoints - 2 * MarginPoints) / TableWidthPoints();
        if (height > 0) fit = Math.Min(fit, (A5HeightPoints - 2 * MarginPoints) / height);
        return WholePercent(fit);
    }

    /// The zoom the picture is first drawn for: A4 is fitted by its width, known from the start.
    private double InitialScale() => _paper == VoucherPaper.A5Landscape ? A5ReserveScale : A4PrintScale();

    /// LibreOffice zooms in whole percents, from 10 % to 100 %.
    private static double WholePercent(double fit) => Math.Clamp(Math.Floor(100 * fit) / 100, 0.1, 1);

    private double TableWidthPoints() => _columns.Sum(c => ColumnPoints(c.Width));

    /// The printed width of a column at 100 %, as LibreOffice draws it.
    private static double ColumnPoints(double width) => (width + SavedWidthPadding) * PointsPerWidthUnit;

    /// Side of the QR picture in sheet pixels. The sheet is shrunk to the page, so the picture is drawn larger in the
    /// sheet to come out at QrPrintMillimetres on paper; never wider than its columns, though (an A5 receipt shrunk
    /// below about 62 % prints it smaller instead of past the last column, which would widen the print range).
    private int QrPixels(double scale) =>
        Math.Min((int)Math.Round(QrPrintMillimetres * 72 / 25.4 / scale / PointsPerPixel), (int)QrColumnsPixels());

    /// Height in points the picture takes from the top of its first row: the picture and its offset from the top.
    private double QrSpanPoints(double scale) => (QrPixels(scale) + QrTopOffsetPixels) * PointsPerPixel;

    /// The last QrColumns columns as LibreOffice prints them, in sheet pixels.
    private double QrColumnsPixels()
    {
        var pixels = 0d;
        for (var i = _columns.Count - QrColumns + 1; i <= _columns.Count; i++)
            pixels += ColumnPoints(_columns[i - 1].Width) / PointsPerPixel;
        return pixels;
    }

    private double HeightOfRows(int firstRow, int lastRow)
    {
        var height = 0d;
        for (var r = firstRow; r <= lastRow; r++) height += Sheet.Row(r).Height;
        return height;
    }

    /// The QR picture for the given zoom, centred over the last QrColumns columns from the top of the given row.
    private IXLPicture AddQrPicture(VoucherPaymentQrDto qr, int row, double scale)
    {
        using var png = new MemoryStream(QrPngRenderer.Render(qr.Payload));
        var picture = Sheet.AddPicture(png, XLPictureFormat.Png, "payment-qr");
        PlaceQrPicture(picture, row, QrPixels(scale));
        return picture;
    }

    /// Sizes the picture and centres it over the last QrColumns columns as LibreOffice prints them (the Excel model of
    /// 7 pixels per width unit put it about 1 mm left of the centre in the PDF).
    private void PlaceQrPicture(IXLPicture picture, int row, int pixels)
    {
        var firstColumn = _columns.Count - QrColumns + 1;
        var xOffset = Math.Max(0, (int)Math.Round((QrColumnsPixels() - pixels) / 2));

        picture.MoveTo(Sheet.Cell(row, firstColumn), xOffset, QrTopOffsetPixels).WithSize(pixels, pixels);
    }

    private void PutQrCaption(int row, int firstColumn, string text, bool bold)
    {
        var cell = Merged(row, firstColumn, _columns.Count);
        cell.Value = text;
        cell.Style.Font.FontSize = QrCaptionFontSize;
        cell.Style.Font.Bold = bold;
        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        cell.Style.Alignment.ShrinkToFit = true;
    }

    /// The pages (1-based) of the row `from` and of the names row, for a page of the given height in sheet points;
    /// whole rows only, and the repeated table header rows take their height again on every later page.
    private (int First, int Last) PagesOf(int from, double pageHeight)
    {
        var headerLast = Table.TableHeaderLastRow;
        var repeated = RepeatedHeaderHeight();
        var page = 1;
        var used = 0d;
        var firstPage = 0;
        for (var r = 1; r <= _signatureLastRow; r++)
        {
            var height = Sheet.Row(r).Height;
            if (used > 0 && used + height > pageHeight)
            {
                page++;
                used = Table.TableHeaderFirstRow > 0 && r > headerLast ? repeated : 0;
            }
            used += height;
            if (r == from) firstPage = page;
        }
        return (firstPage, page);
    }

    private double RepeatedHeaderHeight()
    {
        var height = 0d;
        if (Table.TableHeaderFirstRow > 0)
            for (var r = Table.TableHeaderFirstRow; r <= Table.TableHeaderLastRow; r++) height += Sheet.Row(r).Height;
        return height;
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
