using ClosedXML.Excel;
using FluentAssertions;
using OrderMgmt.Application.Tt133.Models;
using OrderMgmt.Application.Tt133.Vouchers;
using OrderMgmt.Infrastructure.Excel.Tt133;
using OrderMgmt.Infrastructure.Excel.Tt133.Vouchers;
using Xunit;

namespace OrderMgmt.IntegrationTests.Tt133.Unit.Vouchers;

public class VoucherFormBuilderTests
{
    private static readonly DateOnly VoucherDate = new(2026, 10, 5);

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

    private static Tt133Column[] TenColumns() =>
        Enumerable.Range(1, 10).Select(i => new Tt133Column($"{i}", "", 9.5)).ToArray();

    private static (VoucherFormBuilder Builder, IXLWorksheet Sheet) NewBuilder(
        Tt133FormContext context, VoucherPaper paper = VoucherPaper.A5Landscape, IReadOnlyList<Tt133Column>? columns = null)
    {
        var sheet = new XLWorkbook().Worksheets.Add("V1");
        return (new VoucherFormBuilder(sheet, context, columns ?? TenColumns(), paper), sheet);
    }

    private static List<string> Texts(IXLWorksheet sheet) =>
        sheet.CellsUsed().Select(c => c.GetString()).ToList();

    private static IXLCell CellWith(IXLWorksheet sheet, string text) =>
        sheet.CellsUsed().First(c => c.GetString() == text);

    private static int RowOf(IXLWorksheet sheet, string text) => CellWith(sheet, text).Address.RowNumber;

    private static int ColumnOf(IXLWorksheet sheet, string text) => CellWith(sheet, text).Address.ColumnNumber;

    private static VoucherSigner[] FiveSigners(string? secondName = "Trần B") =>
    [
        new("Giám đốc", "(Ký, họ tên, đóng dấu)", "Nguyễn A"),
        new("Kế toán trưởng", "(Ký, họ tên)", secondName),
        new("Người nộp tiền", "(Ký, họ tên)", null),
        new("Người lập phiếu", "(Ký, họ tên)", "Lê C"),
        new("Thủ quỹ", "(Ký, họ tên)", null),
    ];

    [Fact]
    public void Header_has_unit_address_and_form_code_but_no_tax_code()
    {
        var (b, ws) = NewBuilder(Context());

        b.WriteHeaderBlock("01-TT");

        var texts = Texts(ws);
        texts.Should().Contain("Đơn vị: Công ty ABC – Chi nhánh HN");
        texts.Should().Contain("Địa chỉ: 12 Láng Hạ");
        texts.Should().Contain("Mẫu số 01-TT");
        texts.Should().Contain("(Ban hành theo Thông tư số 133/2016/TT-BTC");
        texts.Should().Contain("ngày 26/8/2016 của Bộ Tài chính)");
        texts.Should().NotContain(t => t.StartsWith("MST"));
        CellWith(ws, "Mẫu số 01-TT").Style.Font.Bold.Should().BeTrue();
        ColumnOf(ws, "Mẫu số 01-TT").Should().BeGreaterThan(ColumnOf(ws, "Đơn vị: Công ty ABC – Chi nhánh HN"));
    }

    [Fact]
    public void Header_without_company_profile()
    {
        var (b, ws) = NewBuilder(new Tt133FormContext { PrintDate = new DateOnly(2026, 10, 14) });

        b.WriteHeaderBlock("01-TT");

        var texts = Texts(ws);
        texts.Should().Contain("Đơn vị:");
        texts.Should().Contain("Địa chỉ:");
    }

    [Fact]
    public void Title_block_puts_the_right_lines_next_to_the_title()
    {
        var (b, ws) = NewBuilder(Context());

        b.WriteTitleBlock("PHIẾU THU", VoucherDate, false, [],
            [$"Quyển số: {VoucherPrintRules.Dots}", "Số: PT00001", $"Nợ: {VoucherPrintRules.Dots}", $"Có: {VoucherPrintRules.Dots}"]);

        var title = CellWith(ws, "PHIẾU THU");
        title.Style.Font.Bold.Should().BeTrue();
        title.Style.Font.FontSize.Should().Be(14);
        var titleRow = title.Address.RowNumber;

        var date = CellWith(ws, "Ngày 05 tháng 10 năm 2026");
        date.Address.RowNumber.Should().Be(titleRow + 1);
        date.Style.Font.Italic.Should().BeTrue();

        RowOf(ws, $"Quyển số: {VoucherPrintRules.Dots}").Should().Be(titleRow);
        RowOf(ws, "Số: PT00001").Should().Be(titleRow + 1);
        RowOf(ws, $"Nợ: {VoucherPrintRules.Dots}").Should().Be(titleRow + 2);
        RowOf(ws, $"Có: {VoucherPrintRules.Dots}").Should().Be(titleRow + 3);
        ColumnOf(ws, $"Quyển số: {VoucherPrintRules.Dots}").Should().BeGreaterThan(title.Address.ColumnNumber);
        ColumnOf(ws, "Số: PT00001").Should().BeGreaterThan(title.Address.ColumnNumber);

        Texts(ws).Should().NotContain("ĐÃ HỦY");
        b.Table.Row.Should().Be(titleRow + 3 + 2);
    }

    [Fact]
    public void Cancelled_title_shows_ĐÃ_HỦY_under_the_title()
    {
        var (b, ws) = NewBuilder(Context());

        b.WriteTitleBlock("PHIẾU THU", VoucherDate, true, ["Số: PN00001"],
            [$"Quyển số: {VoucherPrintRules.Dots}", "Số: PT00001", $"Nợ: {VoucherPrintRules.Dots}", $"Có: {VoucherPrintRules.Dots}"]);

        var titleRow = RowOf(ws, "PHIẾU THU");
        var cancelled = CellWith(ws, "ĐÃ HỦY");
        cancelled.Address.RowNumber.Should().Be(titleRow + 1);
        cancelled.Style.Font.Bold.Should().BeTrue();
        RowOf(ws, "Ngày 05 tháng 10 năm 2026").Should().Be(titleRow + 2);
        RowOf(ws, "Số: PN00001").Should().Be(titleRow + 3);
    }

    [Fact]
    public void Line_prints_the_value_or_dots()
    {
        var (b, ws) = NewBuilder(Context());

        b.WriteLine("Địa chỉ:", " Hà Nội ");
        b.WriteLine("Lý do:", null);

        var texts = Texts(ws);
        texts.Should().Contain("Địa chỉ: Hà Nội");
        texts.Should().Contain($"Lý do: {VoucherPrintRules.Dots}");
    }

    [Fact]
    public void Signatures_in_order_with_hints_and_names()
    {
        var (b, ws) = NewBuilder(Context());

        b.WriteSignatures(VoucherDate, FiveSigners());

        var date = CellWith(ws, "Ngày 05 tháng 10 năm 2026");
        date.Style.Font.Italic.Should().BeTrue();

        var signers = FiveSigners();
        var titleRow = RowOf(ws, "Giám đốc");
        date.Address.RowNumber.Should().BeLessThan(titleRow);

        int[] expectedColumns = [1, 3, 5, 7, 9];
        for (var k = 0; k < signers.Length; k++)
        {
            var title = CellWith(ws, signers[k].Title);
            title.Address.RowNumber.Should().Be(titleRow);
            title.Address.ColumnNumber.Should().Be(expectedColumns[k]);
            title.Style.Font.Bold.Should().BeTrue();

            var hint = ws.Cell(titleRow + 1, expectedColumns[k]);
            hint.GetString().Should().Be(signers[k].Hint);
        }

        var namesRow = RowOf(ws, "Nguyễn A");
        namesRow.Should().BeGreaterThan(titleRow + 1);
        RowOf(ws, "Trần B").Should().Be(namesRow);
        RowOf(ws, "Lê C").Should().Be(namesRow);
        ColumnOf(ws, "Nguyễn A").Should().Be(1);
        ColumnOf(ws, "Trần B").Should().Be(3);
        ColumnOf(ws, "Lê C").Should().Be(7);
    }

    [Fact]
    public void Signature_blocks_follow_the_column_widths()
    {
        double[] widths = [5, 34, 12, 8, 10, 10, 13, 15];
        var columns = widths.Select((w, i) => new Tt133Column($"{i + 1}", "", w)).ToArray();
        var (b, ws) = NewBuilder(Context(), VoucherPaper.A4Portrait, columns);

        b.WriteSignatures(VoucherDate, FiveSigners("Nguyễn Văn Trường Giang"));

        var signers = FiveSigners();
        var titleRow = RowOf(ws, "Giám đốc");
        int[] expectedColumns = [1, 3, 4, 6, 8];
        for (var k = 0; k < signers.Length; k++)
        {
            var title = CellWith(ws, signers[k].Title);
            title.Address.RowNumber.Should().Be(titleRow);
            title.Address.ColumnNumber.Should().Be(expectedColumns[k]);

            var last = title.IsMerged() ? title.MergedRange().LastColumn().ColumnNumber() : title.Address.ColumnNumber;
            var blockWidth = widths[(title.Address.ColumnNumber - 1)..last].Sum();
            blockWidth.Should().BeGreaterThanOrEqualTo(12);
        }

        var name = CellWith(ws, "Nguyễn Văn Trường Giang");
        name.Address.ColumnNumber.Should().Be(3);
        ws.Row(name.Address.RowNumber).Height.Should().BeGreaterThan(ws.RowHeight);
        name.Style.Alignment.WrapText.Should().BeTrue();
    }

    [Fact]
    public void A5_landscape_is_one_page()
    {
        var (b, ws) = NewBuilder(Context(), VoucherPaper.A5Landscape);
        b.WriteLine("Lý do:", null);

        b.Finish();

        ws.PageSetup.PaperSize.Should().Be(XLPaperSize.A5Paper);
        ws.PageSetup.PageOrientation.Should().Be(XLPageOrientation.Landscape);
        ws.PageSetup.PagesWide.Should().Be(1);
        ws.PageSetup.PagesTall.Should().Be(1);
        ws.Column(1).Width.Should().BeApproximately(9.5, 0.01);
        ws.PageSetup.FirstRowToRepeatAtTop.Should().Be(0);
    }

    [Fact]
    public void A4_portrait_repeats_the_table_header()
    {
        Tt133Column[] columns =
        [
            new("A", "Tên", 30),
            new("B", "Mã số", 12),
            new("1", "Yêu cầu", 12, Tt133CellKind.Quantity, "Số lượng"),
            new("2", "Thực nhập", 12, Tt133CellKind.Quantity, "Số lượng"),
        ];
        var (b, ws) = NewBuilder(Context(), VoucherPaper.A4Portrait, columns);
        b.Table.WriteTableHeader();

        b.Finish();

        ws.PageSetup.PaperSize.Should().Be(XLPaperSize.A4Paper);
        ws.PageSetup.PageOrientation.Should().Be(XLPageOrientation.Portrait);
        ws.PageSetup.PagesWide.Should().Be(1);
        ws.PageSetup.PagesTall.Should().Be(0);
        ws.PageSetup.FirstRowToRepeatAtTop.Should().Be(b.Table.TableHeaderFirstRow);
    }
}
