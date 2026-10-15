using ClosedXML.Excel;
using ClosedXML.Excel.Drawings;
using FluentAssertions;
using OrderMgmt.Application.CashDebt.PaymentQr;
using OrderMgmt.Application.Payments.Services;
using OrderMgmt.Application.Tt133.Models;
using OrderMgmt.Application.Tt133.Vouchers;
using OrderMgmt.Domain.Enums;
using OrderMgmt.Infrastructure.Excel.Tt133.Vouchers;
using Xunit;

namespace OrderMgmt.IntegrationTests.Tt133.Unit.Vouchers;

public class CashVoucherFormTests
{
    private static string Dots => VoucherPrintRules.Dots;

    private static Tt133FormContext Context() => new()
    {
        CompanyName = "Công ty ABC",
        BranchName = "Chi nhánh HN",
        Address = "12 Láng Hạ",
        TaxCode = "0101234567",
        ChiefAccountantName = "Trần B",
        LegalRepresentativeName = "Nguyễn A",
        PrintDate = new DateOnly(2026, 10, 14),
    };

    private static CashVoucherPrintModel Receipt(string code = "PT00001") => new()
    {
        Type = CashVoucherType.Receipt,
        Code = code,
        Date = new DateOnly(2026, 10, 5),
        PersonName = "Nguyễn Văn A",
        Address = "Hà Nội",
        Reason = "Thu tiền hàng",
        Amount = 1_500_000m,
        AmountInWords = "Một triệu năm trăm nghìn đồng",
        AttachedDocsLine = "Kèm theo: 2 chứng từ gốc",
        PreparerName = "Quản trị hệ thống",
    };

    private static VoucherPaymentQrDto Qr() => new()
    {
        Payload = VietQrPayloadBuilder.Build("970436", "0123456789", "CONG TY ABC", 1_500_000m, "PT00001"),
        Amount = 1_500_000m,
        Total = 1_500_000m,
        BankName = "Ngân hàng TMCP Ngoại thương Việt Nam",
        BankShortName = "Vietcombank",
        AccountNumber = "0123456789",
        AccountHolderName = "Công ty ABC",
        Content = "PT00001",
        FundCode = "NH01",
        FundName = "Vietcombank",
    };

    private static CashVoucherPrintModel ReceiptWithQr()
    {
        var model = Receipt();
        model.Qr = Qr();
        return model;
    }

    private static readonly string[] SixLines =
    [
        "Họ và tên người nộp tiền: Nguyễn Văn A", "Địa chỉ: Hà Nội", "Lý do nộp: Thu tiền hàng",
        "Số tiền: 1.500.000 đồng", "(Viết bằng chữ): Một triệu năm trăm nghìn đồng", "Kèm theo: 2 chứng từ gốc",
    ];

    private const string ScanCaption = "Quét để chuyển khoản";
    private const string SignedDate = "Ngày 05 tháng 10 năm 2026";

    private static int LastColumnOf(IXLWorksheet sheet, string text) =>
        CellWith(sheet, text).MergedRange().RangeAddress.LastAddress.ColumnNumber;

    private static int SignatureDateRow(IXLWorksheet sheet) =>
        sheet.CellsUsed().Last(c => c.GetString() == SignedDate).Address.RowNumber;

    private static XLWorkbook Render(params CashVoucherPrintModel[] models)
    {
        var bytes = new VoucherFormRenderer().RenderCashVouchers(Context(), models);
        return new XLWorkbook(new MemoryStream(bytes));
    }

    /// Every text line of the used cells (the header's right part is one cell of several lines; a saved file has CRLF line ends).
    private static List<string> Texts(IXLWorksheet sheet) =>
        sheet.CellsUsed().SelectMany(c => c.GetString().Split('\n')).Select(t => t.TrimEnd('\r')).ToList();

    private static IXLCell CellWith(IXLWorksheet sheet, string text) =>
        sheet.CellsUsed().First(c => c.GetString() == text);

    private static int RowOf(IXLWorksheet sheet, string text) => CellWith(sheet, text).Address.RowNumber;

    private static int ColumnOf(IXLWorksheet sheet, string text) => CellWith(sheet, text).Address.ColumnNumber;

    [Fact]
    public void Receipt_is_01_TT()
    {
        using var wb = Render(Receipt());

        wb.Worksheets.Select(w => w.Name).Should().Equal("PT00001");
        var ws = wb.Worksheet(1);
        var texts = Texts(ws);
        texts.Should().Contain([
            "Mẫu số 01-TT", "PHIẾU THU", "Ngày 05 tháng 10 năm 2026",
            $"Quyển số: {Dots}", "Số: PT00001", $"Nợ: {Dots}", $"Có: {Dots}",
            "Họ và tên người nộp tiền: Nguyễn Văn A", "Địa chỉ: Hà Nội", "Lý do nộp: Thu tiền hàng",
            "Số tiền: 1.500.000 đồng", "(Viết bằng chữ): Một triệu năm trăm nghìn đồng",
            "Kèm theo: 2 chứng từ gốc",
        ]);
        texts.Should().NotContain("ĐÃ HỦY");

        var titleRow = RowOf(ws, "Giám đốc");
        string[] titles = ["Giám đốc", "Kế toán trưởng", "Người nộp tiền", "Người lập phiếu", "Thủ quỹ"];
        for (var k = 0; k < titles.Length; k++)
        {
            ws.Cell(titleRow, 1 + 2 * k).GetString().Should().Be(titles[k]);
        }
        ColumnOf(ws, "Nguyễn A").Should().Be(ColumnOf(ws, "Giám đốc"));
        ColumnOf(ws, "Trần B").Should().Be(ColumnOf(ws, "Kế toán trưởng"));
        ColumnOf(ws, "Quản trị hệ thống").Should().Be(ColumnOf(ws, "Người lập phiếu"));
        RowOf(ws, $"Đã nhận đủ số tiền (viết bằng chữ): {Dots}").Should().BeGreaterThan(RowOf(ws, "Nguyễn A"));

        ws.PageSetup.PaperSize.Should().Be(XLPaperSize.A5Paper);
        ws.PageSetup.PageOrientation.Should().Be(XLPageOrientation.Landscape);
        ws.PageSetup.PagesWide.Should().Be(1);
        ws.PageSetup.PagesTall.Should().Be(1);
    }

    [Fact]
    public void Payment_is_02_TT()
    {
        var model = Receipt("PC00001");
        model.Type = CashVoucherType.Payment;
        using var wb = Render(model);

        wb.Worksheets.Select(w => w.Name).Should().Equal("PC00001");
        var ws = wb.Worksheet(1);
        Texts(ws).Should().Contain([
            "Mẫu số 02-TT", "PHIẾU CHI", "Số: PC00001",
            "Họ và tên người nhận tiền: Nguyễn Văn A", "Lý do chi: Thu tiền hàng",
        ]);

        var titleRow = RowOf(ws, "Giám đốc");
        string[] titles = ["Giám đốc", "Kế toán trưởng", "Thủ quỹ", "Người lập phiếu", "Người nhận tiền"];
        for (var k = 0; k < titles.Length; k++)
        {
            ws.Cell(titleRow, 1 + 2 * k).GetString().Should().Be(titles[k]);
        }
    }

    [Fact]
    public void Blank_fields_print_dots()
    {
        var model = Receipt();
        model.PersonName = null;
        model.Address = null;
        model.Reason = null;
        using var wb = Render(model);

        Texts(wb.Worksheet(1)).Should().Contain([
            $"Họ và tên người nộp tiền: {Dots}", $"Địa chỉ: {Dots}", $"Lý do nộp: {Dots}",
        ]);
    }

    [Fact]
    public void Cancelled_voucher_shows_ĐÃ_HỦY()
    {
        var model = Receipt();
        model.IsCancelled = true;
        using var wb = Render(model);

        Texts(wb.Worksheet(1)).Should().Contain("ĐÃ HỦY");
    }

    [Fact]
    public void Cash_voucher_has_no_page_break()
    {
        var model = Receipt();
        model.Reason = string.Join(" ", Enumerable.Repeat("Thu tiền hàng theo hợp đồng", 20));
        using var wb = Render(model);

        wb.Worksheet(1).PageSetup.RowBreaks.Should().BeEmpty();
    }

    [Fact]
    public void One_sheet_per_voucher_in_the_given_order()
    {
        using var wb = Render(Receipt("PT00002"), Receipt("PT00001"), Receipt("PT00001"));

        wb.Worksheets.Select(w => w.Name).Should().Equal("PT00002", "PT00001", "PT00001 (2)");
    }

    [Fact]
    public void Receipt_with_qr_has_one_picture_beside_the_lines()
    {
        using var wb = Render(ReceiptWithQr());
        var ws = wb.Worksheet(1);

        ws.Pictures.Count.Should().Be(1);
        var picture = ws.Pictures.First();
        picture.TopLeftCell.Address.ColumnNumber.Should().Be(9);
        picture.TopLeftCell.Address.RowNumber.Should().Be(RowOf(ws, "Họ và tên người nộp tiền: Nguyễn Văn A"));
        picture.Width.Should().Be(picture.Height);
        picture.Width.Should().BeInRange(90, 130);
    }

    [Fact]
    public void Lines_beside_the_qr_stop_at_column_8()
    {
        using var withQr = Render(ReceiptWithQr());
        using var without = Render(Receipt());

        foreach (var line in SixLines)
        {
            LastColumnOf(withQr.Worksheet(1), line).Should().Be(8, line);
            LastColumnOf(without.Worksheet(1), line).Should().Be(10, line);
        }
    }

    [Fact]
    public void Qr_captions_are_in_columns_9_to_10()
    {
        using var wb = Render(ReceiptWithQr());
        var ws = wb.Worksheet(1);
        var pictureRow = ws.Pictures.First().TopLeftCell.Address.RowNumber;

        foreach (var caption in new[] { ScanCaption, "Vietcombank · 0123456789" })
        {
            ColumnOf(ws, caption).Should().Be(9, caption);
            RowOf(ws, caption).Should().BeGreaterThan(pictureRow, caption);
            LastColumnOf(ws, caption).Should().Be(10, caption);
            var style = CellWith(ws, caption).Style;
            style.Font.FontSize.Should().Be(8, caption);
            style.Alignment.Horizontal.Should().Be(XLAlignmentHorizontalValues.Center, caption);
            style.Alignment.ShrinkToFit.Should().BeTrue(caption);
        }
        CellWith(ws, ScanCaption).Style.Font.Bold.Should().BeTrue();
        CellWith(ws, "Vietcombank · 0123456789").Style.Font.Bold.Should().BeFalse();
        RowOf(ws, "Vietcombank · 0123456789").Should().Be(RowOf(ws, ScanCaption) + 1);
        // The captions are the last two rows of the block: the signature date follows after one blank row.
        RowOf(ws, "Vietcombank · 0123456789").Should().Be(SignatureDateRow(ws) - 2);
    }

    [Fact]
    public void Qr_is_centred_in_columns_9_to_10_two_pixels_below_the_top()
    {
        using var wb = Render(ReceiptWithQr());
        var picture = wb.Worksheet(1).Pictures.First();

        var offset = picture.GetOffset(XLMarkerPosition.TopLeft);
        offset.Y.Should().Be(2);
        // Two columns of width 9.5 as LibreOffice prints them (the Excel 7w + 5 pixels put it 0.8 mm left of centre).
        offset.X.Should().BeInRange((int)LibreOfficePrint.CentredOffset(picture, 9.5, 9.5) - 1,
            (int)LibreOfficePrint.CentredOffset(picture, 9.5, 9.5) + 1);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void Qr_prints_25_mm_at_the_zoom_that_fits_the_receipt_to_the_page(bool longReason, bool cancelled)
    {
        var model = ReceiptWithQr();
        if (longReason) model.Reason = string.Join(" ", Enumerable.Repeat("Thu tiền hàng theo hợp đồng", 20))[..400];
        model.IsCancelled = cancelled;
        using var wb = Render(model);
        var ws = wb.Worksheet(1);

        // The sheet is fitted to one A5 page by its height, which the QR rows and the reason change; a fixed zoom
        // printed it 21.1 mm and 19.7 mm wide.
        var picture = ws.Pictures.First();
        LibreOfficePrint.PrintedMillimetres(picture, LibreOfficePrint.A5Zoom(ws, 10)).Should().BeApproximately(25, 0.5);
        AssertRowsCoverPicture(ws);
    }

    [Fact]
    public void Rows_beside_the_qr_cover_the_picture()
    {
        using var wb = Render(ReceiptWithQr());
        var ws = wb.Worksheet(1);

        AssertRowsCoverPicture(ws);
        RowOf(ws, ScanCaption).Should().BeLessThan(SignatureDateRow(ws));
    }

    [Fact]
    public void Long_reason_still_fits()
    {
        var model = ReceiptWithQr();
        model.Reason = string.Join(" ", Enumerable.Repeat("Thu tiền hàng theo hợp đồng", 11))[..300].Trim();
        using var wb = Render(model);
        using var shortWb = Render(ReceiptWithQr());
        var ws = wb.Worksheet(1);

        AssertRowsCoverPicture(ws);
        // The wrapped reason makes the rows taller, so no more blank rows than for the short one are needed.
        SignatureDateRow(ws).Should().BeLessThanOrEqualTo(SignatureDateRow(shortWb.Worksheet(1)));
        LastColumnOf(ws, $"Lý do nộp: {model.Reason}").Should().Be(8);
        ws.Row(RowOf(ws, $"Lý do nộp: {model.Reason}")).Height.Should().BeGreaterThan(ws.RowHeight);
    }

    [Fact]
    public void Without_qr_nothing_changes()
    {
        using var wb = Render(Receipt());
        var ws = wb.Worksheet(1);

        ws.Pictures.Count.Should().Be(0);
        foreach (var line in SixLines) LastColumnOf(ws, line).Should().Be(10, line);
        SignatureDateRow(ws).Should().Be(RowOf(ws, "Kèm theo: 2 chứng từ gốc") + 2);
    }

    [Fact]
    public void Qr_is_on_01_TT_only()
    {
        var model = ReceiptWithQr();
        model.Type = CashVoucherType.Payment;
        using var wb = Render(model);
        var ws = wb.Worksheet(1);

        ws.Pictures.Count.Should().Be(0);
        Texts(ws).Should().NotContain(ScanCaption);
        LastColumnOf(ws, "Họ và tên người nhận tiền: Nguyễn Văn A").Should().Be(10);
    }

    /// The rows from the picture to the one before the first caption hold the picture.
    private static void AssertRowsCoverPicture(IXLWorksheet ws)
    {
        var picture = ws.Pictures.First();
        var first = picture.TopLeftCell.Address.RowNumber;
        var captionRow = RowOf(ws, ScanCaption);
        var height = 0d;
        for (var r = first; r < captionRow; r++) height += ws.Row(r).Height;
        height.Should().BeGreaterThanOrEqualTo(picture.Height * 0.75);
    }
}
