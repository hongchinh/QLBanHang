using ClosedXML.Excel;
using ClosedXML.Excel.Drawings;

namespace OrderMgmt.IntegrationTests.Tt133.Unit.Vouchers;

/// How LibreOffice prints a voucher sheet, measured on the real PDFs of the voucher QR samples (Task 3.6): a column of
/// width w is (w + 0.710625) * 5.56 pt wide, a picture pixel is 0.75 pt, and the margins are 0.59 inch. An A5 landscape
/// sheet is fitted to one page at the largest whole percent that fits both ways: the receipt samples with the filled company
/// profile printed at 73 % and 71 % (a long reason), and 78 % with the short profile, all limited by the height, while
/// the width alone allows 89 %.
internal static class LibreOfficePrint
{
    private const double MarginPoints = 0.59 * 72;
    private const double A5LongSide = 595.28;
    private const double A5ShortSide = 419.53;

    public static double ColumnPoints(double width) => (width + 0.710625) * 5.56;

    /// The zoom (0-1) of an A5 landscape sheet; its columns are the first `columns` ones.
    public static double A5Zoom(IXLWorksheet sheet, int columns)
    {
        var width = Enumerable.Range(1, columns).Sum(c => ColumnPoints(sheet.Column(c).Width));
        var height = Enumerable.Range(1, sheet.LastRowUsed()!.RowNumber()).Sum(r => sheet.Row(r).Height);
        var fit = Math.Min((A5LongSide - 2 * MarginPoints) / width, (A5ShortSide - 2 * MarginPoints) / height);
        return Math.Min(1, Math.Floor(100 * fit) / 100);
    }

    public static double PrintedMillimetres(IXLPicture picture, double zoom) => picture.Width * 0.75 * zoom * 25.4 / 72;

    /// The side in sheet pixels that prints 25 mm at the given zoom.
    public static int QrPixels(double zoom) => (int)Math.Round(25 * 72 / 25.4 / zoom / 0.75);

    /// The given columns as LibreOffice prints them, in sheet pixels.
    public static double ColumnsPixels(params double[] columnWidths) => columnWidths.Sum(ColumnPoints) / 0.75;

    /// The x offset (pixels) that centres the picture over the given columns as LibreOffice draws them.
    public static double CentredOffset(IXLPicture picture, params double[] columnWidths) =>
        (ColumnsPixels(columnWidths) - picture.Width) / 2;
}
