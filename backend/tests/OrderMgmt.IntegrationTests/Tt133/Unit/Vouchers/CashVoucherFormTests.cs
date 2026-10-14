using ClosedXML.Excel;
using FluentAssertions;
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
}
