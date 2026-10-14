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
    private const string DateText = "Ngày 05 tháng 10 năm 2026";

    private static readonly string[] CashRightLines =
        [$"Quyển số: {VoucherPrintRules.Dots}", "Số: PT00001", $"Nợ: {VoucherPrintRules.Dots}", $"Có: {VoucherPrintRules.Dots}"];

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

    /// First and last column of the merged range of the cell holding the text (the cell itself when not merged).
    private static (int First, int Last) SpanOf(IXLWorksheet sheet, string text)
    {
        var cell = CellWith(sheet, text);
        if (!cell.IsMerged()) return (cell.Address.ColumnNumber, cell.Address.ColumnNumber);
        var range = cell.MergedRange();
        return (range.FirstColumn().ColumnNumber(), range.LastColumn().ColumnNumber());
    }

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
        texts.Should().NotContain(t => t.StartsWith("MST"));

        RowOf(ws, "Đơn vị: Công ty ABC – Chi nhánh HN").Should().Be(1);
        RowOf(ws, "Địa chỉ: 12 Láng Hạ").Should().Be(2);
        RowOf(ws, "Mẫu số 01-TT").Should().Be(1);
        RowOf(ws, "(Ban hành theo Thông tư số 133/2016/TT-BTC").Should().Be(2);
        RowOf(ws, "ngày 26/8/2016 của Bộ Tài chính)").Should().Be(3);

        ColumnOf(ws, "Mẫu số 01-TT").Should().Be(7);
        SpanOf(ws, "Đơn vị: Công ty ABC – Chi nhánh HN").Should().Be((1, 6));
        SpanOf(ws, "Địa chỉ: 12 Láng Hạ").Should().Be((1, 6));
        foreach (var right in new[] { "Mẫu số 01-TT", "(Ban hành theo Thông tư số 133/2016/TT-BTC", "ngày 26/8/2016 của Bộ Tài chính)" })
        {
            SpanOf(ws, right).Should().Be((7, 10));
            CellWith(ws, right).Style.Alignment.Horizontal.Should().Be(XLAlignmentHorizontalValues.Center);
        }
        CellWith(ws, "Mẫu số 01-TT").Style.Font.Bold.Should().BeTrue();
        CellWith(ws, "(Ban hành theo Thông tư số 133/2016/TT-BTC").Style.Font.Bold.Should().BeFalse();

        b.Table.Row.Should().Be(5); // three rows and one empty row
    }

    [Fact]
    public void Header_cells_are_top_aligned()
    {
        var (b, ws) = NewBuilder(Context());

        b.WriteHeaderBlock("01-TT");

        foreach (var text in new[]
                 {
                     "Đơn vị: Công ty ABC – Chi nhánh HN", "Địa chỉ: 12 Láng Hạ", "Mẫu số 01-TT",
                     "(Ban hành theo Thông tư số 133/2016/TT-BTC", "ngày 26/8/2016 của Bộ Tài chính)",
                 })
            CellWith(ws, text).Style.Alignment.Vertical.Should().Be(XLAlignmentVerticalValues.Top, text);
        ws.Cell(3, 1).Style.Alignment.Vertical.Should().Be(XLAlignmentVerticalValues.Top);
    }

    [Fact]
    public void Header_without_company_profile()
    {
        var (b, ws) = NewBuilder(new Tt133FormContext { PrintDate = new DateOnly(2026, 10, 14) });

        b.WriteHeaderBlock("01-TT");

        RowOf(ws, "Đơn vị:").Should().Be(1);
        RowOf(ws, "Địa chỉ:").Should().Be(2);
    }

    [Fact]
    public void Header_with_company_only_or_branch_only()
    {
        var company = Context();
        company.BranchName = null;
        var (b1, ws1) = NewBuilder(company);
        b1.WriteHeaderBlock("01-TT");
        RowOf(ws1, "Đơn vị: Công ty ABC").Should().Be(1);

        var branch = Context();
        branch.CompanyName = " ";
        var (b2, ws2) = NewBuilder(branch);
        b2.WriteHeaderBlock("01-TT");
        RowOf(ws2, "Đơn vị: Chi nhánh HN").Should().Be(1);
    }

    [Fact]
    public void Header_long_address_wraps_and_its_row_is_two_lines_high()
    {
        var context = Context();
        context.Address = "số 12 ngõ 34 đường láng hạ phường thành công quận ba đình thành phố hà nội việt nam";
        var text = $"Địa chỉ: {context.Address}";
        Tt133FormBuilder.LineCount(text, 6 * 9.5, bold: false).Should().Be(2); // precondition of the case
        var (b, ws) = NewBuilder(context);

        b.WriteHeaderBlock("01-TT");

        var cell = CellWith(ws, text);
        cell.Address.RowNumber.Should().Be(2);
        cell.Style.Alignment.WrapText.Should().BeTrue();
        ws.Row(2).Height.Should().BeApproximately(2 * ws.RowHeight, 0.01);
        b.Table.Row.Should().Be(5);
    }

    [Fact]
    public void Title_block_puts_the_right_lines_next_to_the_title()
    {
        var (b, ws) = NewBuilder(Context());

        b.WriteTitleBlock("PHIẾU THU", VoucherDate, false, [], CashRightLines);

        var title = CellWith(ws, "PHIẾU THU");
        title.Style.Font.Bold.Should().BeTrue();
        title.Style.Font.FontSize.Should().Be(14);
        title.Style.Alignment.Horizontal.Should().Be(XLAlignmentHorizontalValues.Center);
        SpanOf(ws, "PHIẾU THU").Should().Be((1, 6));
        var titleRow = title.Address.RowNumber;

        var date = CellWith(ws, DateText);
        date.Address.RowNumber.Should().Be(titleRow + 1);
        date.Style.Font.Italic.Should().BeTrue();

        for (var i = 0; i < CashRightLines.Length; i++)
        {
            var cell = CellWith(ws, CashRightLines[i]);
            cell.Address.RowNumber.Should().Be(titleRow + i);
            cell.Address.ColumnNumber.Should().Be(7);
            cell.Style.Alignment.Horizontal.Should().Be(XLAlignmentHorizontalValues.Left);
        }

        Texts(ws).Should().NotContain("ĐÃ HỦY");
        b.Table.Row.Should().Be(titleRow + 3 + 2);
    }

    [Fact]
    public void Cancelled_title_shows_ĐÃ_HỦY_under_the_title()
    {
        var (b, ws) = NewBuilder(Context());

        b.WriteTitleBlock("PHIẾU THU", VoucherDate, true, ["Số: PN00001"], CashRightLines);

        var titleRow = RowOf(ws, "PHIẾU THU");
        var cancelled = CellWith(ws, "ĐÃ HỦY");
        cancelled.Address.RowNumber.Should().Be(titleRow + 1);
        cancelled.Style.Font.Bold.Should().BeTrue();
        RowOf(ws, DateText).Should().Be(titleRow + 2);
        RowOf(ws, "Số: PN00001").Should().Be(titleRow + 3);
        b.Table.Row.Should().Be(titleRow + 5);
    }

    [Fact]
    public void Title_block_with_more_centre_lines_than_right_lines_skips_past_the_centre()
    {
        var (b, ws) = NewBuilder(Context());

        b.WriteTitleBlock("PHIẾU NHẬP KHO", VoucherDate, true, ["Số: PN00001", "Kho: Kho chính"], ["Nợ: 156", "Có: 331"]);

        var titleRow = RowOf(ws, "PHIẾU NHẬP KHO");
        RowOf(ws, "Kho: Kho chính").Should().Be(titleRow + 4);
        RowOf(ws, "Có: 331").Should().Be(titleRow + 1);
        b.Table.Row.Should().Be(titleRow + 6);
    }

    [Fact]
    public void Title_block_wraps_long_lines_and_sizes_their_rows()
    {
        var right = "Số: " + new string('1', 50);
        var centre = "Kho: " + new string('a', 80);
        Tt133FormBuilder.LineCount(right, 4 * 9.5, bold: false).Should().Be(2); // preconditions of the case
        Tt133FormBuilder.LineCount(centre, 6 * 9.5, bold: false).Should().Be(2);
        var (b, ws) = NewBuilder(Context());

        b.WriteTitleBlock("PHIẾU THU", VoucherDate, false, [centre], ["Quyển số: 1", right]);

        var titleRow = RowOf(ws, "PHIẾU THU");
        var rightCell = CellWith(ws, right);
        rightCell.Address.RowNumber.Should().Be(titleRow + 1);
        rightCell.Style.Alignment.WrapText.Should().BeTrue();
        ws.Row(titleRow + 1).Height.Should().BeApproximately(2 * ws.RowHeight, 0.01);

        var centreCell = CellWith(ws, centre);
        centreCell.Address.RowNumber.Should().Be(titleRow + 2);
        centreCell.Style.Alignment.WrapText.Should().BeTrue();
        ws.Row(titleRow + 2).Height.Should().BeApproximately(2 * ws.RowHeight, 0.01);
    }

    [Fact]
    public void Line_prints_the_value_or_dots()
    {
        var (b, ws) = NewBuilder(Context());

        b.WriteLine("Địa chỉ:", " Hà Nội ");
        b.WriteLine("Lý do:", null);

        RowOf(ws, "Địa chỉ: Hà Nội").Should().Be(1);
        RowOf(ws, $"Lý do: {VoucherPrintRules.Dots}").Should().Be(2);
    }

    [Fact]
    public void Signatures_in_order_with_hints_and_names()
    {
        var (b, ws) = NewBuilder(Context());

        b.WriteSignatures(VoucherDate, FiveSigners());

        var signers = FiveSigners();
        var titleRow = RowOf(ws, "Giám đốc");

        var date = CellWith(ws, DateText);
        date.Style.Font.Italic.Should().BeTrue();
        date.Address.RowNumber.Should().Be(titleRow - 1);
        date.Style.Alignment.Horizontal.Should().Be(XLAlignmentHorizontalValues.Right);
        SpanOf(ws, DateText).Should().Be((7, 10));
        ws.Row(titleRow - 2).CellsUsed().Should().BeEmpty();

        int[] expectedColumns = [1, 3, 5, 7, 9];
        for (var k = 0; k < signers.Length; k++)
        {
            var title = CellWith(ws, signers[k].Title);
            title.Address.RowNumber.Should().Be(titleRow);
            title.Address.ColumnNumber.Should().Be(expectedColumns[k]);
            title.Style.Font.Bold.Should().BeTrue();

            var hint = ws.Cell(titleRow + 1, expectedColumns[k]);
            hint.GetString().Should().Be(signers[k].Hint);
            hint.Style.Font.Italic.Should().BeTrue();
        }

        var namesRow = RowOf(ws, "Nguyễn A");
        namesRow.Should().Be(titleRow + 6);
        RowOf(ws, "Trần B").Should().Be(namesRow);
        RowOf(ws, "Lê C").Should().Be(namesRow);
        ColumnOf(ws, "Nguyễn A").Should().Be(1);
        ColumnOf(ws, "Trần B").Should().Be(3);
        ColumnOf(ws, "Lê C").Should().Be(7);
        foreach (var name in new[] { "Nguyễn A", "Trần B", "Lê C" })
            CellWith(ws, name).Style.Font.Bold.Should().BeTrue();

        ws.Row(titleRow).Height.Should().BeApproximately(ws.RowHeight, 0.01);
        b.Table.Row.Should().Be(namesRow + 1);
    }

    [Fact]
    public void Signature_cells_are_top_aligned()
    {
        var (b, ws) = NewBuilder(Context());

        b.WriteSignatures(VoucherDate, FiveSigners());

        foreach (var text in new[] { "Giám đốc", "(Ký, họ tên, đóng dấu)", "Nguyễn A", "Lê C" })
            CellWith(ws, text).Style.Alignment.Vertical.Should().Be(XLAlignmentVerticalValues.Top, text);
    }

    [Fact]
    public void More_signers_than_columns_is_refused()
    {
        var columns = Enumerable.Range(1, 4).Select(i => new Tt133Column($"{i}", "", 9.5)).ToArray();
        var (b, _) = NewBuilder(Context(), VoucherPaper.A4Portrait, columns);

        var act = () => b.WriteSignatures(VoucherDate, FiveSigners());

        act.Should().Throw<ArgumentOutOfRangeException>();
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

            var (first, last) = SpanOf(ws, signers[k].Title);
            widths[(first - 1)..last].Sum().Should().BeGreaterThanOrEqualTo(12);
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
