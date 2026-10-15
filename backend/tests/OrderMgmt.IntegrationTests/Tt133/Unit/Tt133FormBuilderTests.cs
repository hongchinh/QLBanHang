using ClosedXML.Excel;
using FluentAssertions;
using OrderMgmt.Application.Tt133.Models;
using OrderMgmt.Infrastructure.Excel.Tt133;
using Xunit;

namespace OrderMgmt.IntegrationTests.Tt133.Unit;

public class Tt133FormBuilderTests
{
    private static Tt133FormContext Context() => new()
    {
        CompanyName = "Công ty ABC",
        BranchName = "Chi nhánh HN",
        Address = "12 Lê Lợi",
        TaxCode = "0101234567",
        BookkeeperName = "Lê C",
        ChiefAccountantName = "Trần B",
        LegalRepresentativeName = "Nguyễn A",
        OpeningDate = new DateOnly(2026, 10, 1),
        PrintDate = new DateOnly(2026, 10, 13),
    };

    private static (Tt133FormBuilder Builder, IXLWorksheet Sheet) NewBuilder(Tt133FormContext context, bool landscape = false)
    {
        var wb = new XLWorkbook();
        var sheet = wb.Worksheets.Add("S1");
        var columns = new[]
        {
            new Tt133Column("A", "Ngày", 12, Tt133CellKind.Date),
            new Tt133Column("B", "Diễn giải", 30, Tt133CellKind.Text),
            new Tt133Column("1", "Thu", 14, Tt133CellKind.Money, "Số tiền"),
            new Tt133Column("2", "Chi", 14, Tt133CellKind.Money, "Số tiền"),
        };
        return (new Tt133FormBuilder(sheet, context, columns, landscape), sheet);
    }

    private static List<string> Texts(IXLWorksheet sheet) =>
        sheet.CellsUsed().Select(c => c.GetString()).ToList();

    private static IXLCell CellWith(IXLWorksheet sheet, string text) =>
        sheet.CellsUsed().First(c => c.GetString() == text);

    [Fact]
    public void Header_block_prints_unit_address_tax_code_and_form_code()
    {
        var (b, ws) = NewBuilder(Context());

        b.WriteHeaderBlock("S04a-DNN");

        var texts = Texts(ws);
        texts.Should().Contain("Đơn vị: Công ty ABC – Chi nhánh HN");
        texts.Should().Contain("Địa chỉ: 12 Lê Lợi");
        texts.Should().Contain("MST: 0101234567");
        texts.Should().Contain("Mẫu số S04a-DNN");
        texts.Should().Contain("(Ban hành theo Thông tư số 133/2016/TT-BTC");
        CellWith(ws, "Mẫu số S04a-DNN").Style.Font.Bold.Should().BeTrue();
        b.Row.Should().Be(5); // three rows and one empty row
    }

    [Fact]
    public void Header_block_for_company_scope_prints_the_company_only()
    {
        var context = Context();
        context.BranchName = null;
        var (b, ws) = NewBuilder(context);

        b.WriteHeaderBlock("S04a-DNN");

        Texts(ws).Should().Contain("Đơn vị: Công ty ABC");
    }

    [Fact]
    public void Header_block_without_form_code_has_no_form_lines()
    {
        var (b, ws) = NewBuilder(Context());

        b.WriteHeaderBlock(null);

        Texts(ws).Should().NotContain(t => t.Contains("Mẫu số") || t.Contains("Thông tư"));
    }

    [Fact]
    public void Header_block_with_an_empty_profile_keeps_the_labels()
    {
        var context = Context();
        context.CompanyName = null;
        context.Address = null;
        context.TaxCode = null;
        var (b, ws) = NewBuilder(context);

        b.WriteHeaderBlock("S04a-DNN");

        var texts = Texts(ws);
        texts.Should().Contain("Đơn vị: Chi nhánh HN");
        texts.Should().Contain("Địa chỉ:");
        texts.Should().Contain("MST:");
    }

    [Fact]
    public void Title_block()
    {
        var (b, ws) = NewBuilder(Context());

        b.WriteTitle("SỔ QUỸ TIỀN MẶT", "Tháng 10 năm 2026", "Loại quỹ: Quỹ tiền mặt");

        var title = CellWith(ws, "SỔ QUỸ TIỀN MẶT");
        title.Style.Font.Bold.Should().BeTrue();
        title.Style.Font.FontSize.Should().Be(14);
        title.Style.Alignment.Horizontal.Should().Be(XLAlignmentHorizontalValues.Center);
        title.IsMerged().Should().BeTrue();
        title.MergedRange().RangeAddress.FirstAddress.ColumnNumber.Should().Be(1);
        title.MergedRange().RangeAddress.LastAddress.ColumnNumber.Should().Be(4);

        var row = title.Address.RowNumber;
        ws.Cell(row + 1, 1).GetString().Should().Be("Tháng 10 năm 2026");
        ws.Cell(row + 1, 1).Style.Alignment.Horizontal.Should().Be(XLAlignmentHorizontalValues.Center);
        ws.Cell(row + 2, 1).GetString().Should().Be("Loại quỹ: Quỹ tiền mặt");
        b.Row.Should().Be(row + 4); // one empty row before the table

        b.WriteNote("Giá vốn tạm tính");

        var note = CellWith(ws, "Giá vốn tạm tính");
        note.Style.Font.Italic.Should().BeTrue();
        note.Style.Alignment.Horizontal.Should().Be(XLAlignmentHorizontalValues.Center);
        b.Row.Should().Be(row + 5); // the empty row before the table is kept
    }

    [Fact]
    public void Text_line_is_merged_over_all_columns()
    {
        var (b, ws) = NewBuilder(Context());
        var before = b.Row;

        b.WriteText("Hôm nay, chúng tôi gồm:", bold: true);

        var cell = CellWith(ws, "Hôm nay, chúng tôi gồm:");
        cell.Style.Font.Bold.Should().BeTrue();
        cell.Style.Alignment.Horizontal.Should().Be(XLAlignmentHorizontalValues.Left);
        cell.MergedRange().RangeAddress.FirstAddress.ColumnNumber.Should().Be(1);
        cell.MergedRange().RangeAddress.LastAddress.ColumnNumber.Should().Be(4);
        b.Row.Should().Be(before + 1);

        b.Skip(2);

        b.Row.Should().Be(before + 3);
    }

    [Theory]
    [InlineData("2026-10-01", "2026-10-31", "Tháng 10 năm 2026")]
    [InlineData("2026-02-01", "2026-02-28", "Tháng 02 năm 2026")]
    [InlineData("2026-01-01", "2026-12-31", "Năm 2026")]
    [InlineData("2026-10-05", "2026-10-31", "Từ ngày 05/10/2026 đến ngày 31/10/2026")]
    [InlineData("2026-10-01", "2026-11-30", "Từ ngày 01/10/2026 đến ngày 30/11/2026")]
    public void Period_line(string from, string to, string expected)
    {
        Tt133FormBuilder.PeriodLine(DateOnly.Parse(from), DateOnly.Parse(to)).Should().Be(expected);
    }

    [Fact]
    public void Sheet_uses_times_new_roman_11()
    {
        var (b, ws) = NewBuilder(Context());
        b.WriteHeaderBlock(null);

        ws.Cell(1, 1).Style.Font.FontName.Should().Be("Times New Roman");
        ws.Cell(1, 1).Style.Font.FontSize.Should().Be(11);
        ws.Cell(20, 5).Style.Font.FontName.Should().Be("Times New Roman");
    }

    [Fact]
    public void Table_header_merges_groups_and_writes_the_symbol_row()
    {
        var (b, ws) = NewBuilder(Context());
        b.WriteHeaderBlock(null);
        b.WriteTitle("SỔ", "Tháng 10 năm 2026");

        b.WriteTableHeader();

        var r = b.TableHeaderFirstRow;
        b.TableHeaderLastRow.Should().Be(r + 2);
        b.Row.Should().Be(r + 3);

        ws.Cell(r, 1).GetString().Should().Be("Ngày");
        ws.Cell(r, 1).MergedRange().RangeAddress.FirstAddress.RowNumber.Should().Be(r);
        ws.Cell(r, 1).MergedRange().RangeAddress.LastAddress.RowNumber.Should().Be(r + 1);
        ws.Cell(r, 2).GetString().Should().Be("Diễn giải");
        ws.Cell(r, 3).GetString().Should().Be("Số tiền");
        ws.Cell(r, 3).MergedRange().RangeAddress.FirstAddress.ColumnNumber.Should().Be(3);
        ws.Cell(r, 3).MergedRange().RangeAddress.LastAddress.ColumnNumber.Should().Be(4);
        ws.Cell(r + 1, 3).GetString().Should().Be("Thu");
        ws.Cell(r + 1, 4).GetString().Should().Be("Chi");
        ws.Cell(r + 2, 1).GetString().Should().Be("A");
        ws.Cell(r + 2, 2).GetString().Should().Be("B");
        ws.Cell(r + 2, 3).GetString().Should().Be("1");
        ws.Cell(r + 2, 4).GetString().Should().Be("2");
        for (var c = 1; c <= 4; c++)
            ws.Cell(r + 2, c).Style.Font.Italic.Should().BeTrue();

        foreach (var cell in new[] { ws.Cell(r, 1), ws.Cell(r, 3), ws.Cell(r + 1, 3), ws.Cell(r + 1, 4) })
        {
            cell.Style.Font.Bold.Should().BeTrue();
            cell.Style.Alignment.Horizontal.Should().Be(XLAlignmentHorizontalValues.Center);
            cell.Style.Alignment.WrapText.Should().BeTrue();
            cell.Style.Border.TopBorder.Should().Be(XLBorderStyleValues.Thin);
            cell.Style.Border.BottomBorder.Should().Be(XLBorderStyleValues.Thin);
            cell.Style.Border.LeftBorder.Should().Be(XLBorderStyleValues.Thin);
            cell.Style.Border.RightBorder.Should().Be(XLBorderStyleValues.Thin);
        }
        ws.Cell(r + 2, 1).Style.Font.Bold.Should().BeFalse();
    }

    [Fact]
    public void Rows_write_typed_values()
    {
        var (b, ws) = NewBuilder(Context());
        b.WriteTableHeader();
        var row = b.Row;

        b.WriteRow(new DateOnly(2026, 10, 3), "Thu tiền", 1_000m, null);

        b.Row.Should().Be(row + 1);
        var date = ws.Cell(row, 1);
        date.DataType.Should().Be(XLDataType.DateTime);
        date.GetDateTime().Should().Be(new DateTime(2026, 10, 3));
        date.Style.DateFormat.Format.Should().Be("dd/MM/yyyy");
        date.Style.Alignment.Horizontal.Should().Be(XLAlignmentHorizontalValues.Center);

        ws.Cell(row, 2).GetString().Should().Be("Thu tiền");
        ws.Cell(row, 2).Style.Alignment.WrapText.Should().BeTrue();

        var money = ws.Cell(row, 3);
        money.GetValue<decimal>().Should().Be(1000m);
        money.Style.NumberFormat.Format.Should().Be("#,##0;(#,##0)");
        money.Style.Alignment.Horizontal.Should().Be(XLAlignmentHorizontalValues.Right);

        ws.Cell(row, 4).IsEmpty().Should().BeTrue();
        for (var c = 1; c <= 4; c++)
        {
            var border = ws.Cell(row, c).Style.Border;
            border.TopBorder.Should().Be(XLBorderStyleValues.Thin);
            border.BottomBorder.Should().Be(XLBorderStyleValues.Thin);
            border.LeftBorder.Should().Be(XLBorderStyleValues.Thin);
            border.RightBorder.Should().Be(XLBorderStyleValues.Thin);
        }
    }

    [Fact]
    public void Row_converts_an_instant_to_the_vn_date()
    {
        var (b, ws) = NewBuilder(Context());
        b.WriteTableHeader();
        var row = b.Row;

        b.WriteRow(new DateTimeOffset(2026, 10, 3, 18, 0, 0, TimeSpan.Zero), "x", null, null);

        ws.Cell(row, 1).GetDateTime().Should().Be(new DateTime(2026, 10, 4));
    }

    [Fact]
    public void Quantity_cells_use_the_quantity_format()
    {
        var sheet = new XLWorkbook().Worksheets.Add("S1");
        var b = new Tt133FormBuilder(sheet, Context(), [new Tt133Column("1", "Số lượng", 12, Tt133CellKind.Quantity)], false);
        var row = b.Row;

        b.WriteRow(1.5m);

        sheet.Cell(row, 1).GetValue<decimal>().Should().Be(1.5m);
        sheet.Cell(row, 1).Style.NumberFormat.Format.Should().Be("#,##0.###;(#,##0.###)");
        sheet.Cell(row, 1).Style.Alignment.Horizontal.Should().Be(XLAlignmentHorizontalValues.Right);
    }

    [Fact]
    public void Summary_row_is_bold_and_uses_the_label_column()
    {
        var (b, ws) = NewBuilder(Context());
        var row = b.Row;

        b.WriteSummaryRow("Cộng số phát sinh trong kỳ", (3, 10_000m), (4, 700m));

        b.Row.Should().Be(row + 1);
        ws.Cell(row, 2).GetString().Should().Be("Cộng số phát sinh trong kỳ");
        ws.Cell(row, 2).Style.Font.Bold.Should().BeTrue();
        ws.Cell(row, 3).GetValue<decimal>().Should().Be(10_000m);
        ws.Cell(row, 3).Style.Font.Bold.Should().BeTrue();
        ws.Cell(row, 3).Style.NumberFormat.Format.Should().Be("#,##0;(#,##0)");
        ws.Cell(row, 4).GetValue<decimal>().Should().Be(700m);
        ws.Cell(row, 4).Style.Font.Bold.Should().BeTrue();
        ws.Cell(row, 1).Style.Border.LeftBorder.Should().Be(XLBorderStyleValues.Thin);
    }

    [Fact]
    public void Summary_label_skips_a_grouped_text_column_and_honours_label_column()
    {
        var columns = new[]
        {
            new Tt133Column("A", "Số hiệu", 12, Tt133CellKind.Text, "Chứng từ"),
            new Tt133Column("B", "Diễn giải", 30, Tt133CellKind.Text),
            new Tt133Column("1", "Thu", 14, Tt133CellKind.Money),
        };
        var sheet = new XLWorkbook().Worksheets.Add("S1");
        var b = new Tt133FormBuilder(sheet, Context(), columns, false);
        var row = b.Row;
        b.WriteSummaryRow("Tổng", (3, 5m));
        sheet.Cell(row, 2).GetString().Should().Be("Tổng");
        sheet.Cell(row, 1).IsEmpty().Should().BeTrue();

        var sheet2 = new XLWorkbook().Worksheets.Add("S2");
        var b2 = new Tt133FormBuilder(sheet2, Context(), columns, false, labelColumn: 1);
        b2.WriteSummaryRow("Tổng", (3, 5m));
        sheet2.Cell(1, 1).GetString().Should().Be("Tổng");
        sheet2.Cell(1, 2).IsEmpty().Should().BeTrue();
    }

    [Fact]
    public void Book_footer_prints_pages_opening_date_print_date_and_signers()
    {
        var (b, ws) = NewBuilder(Context());

        b.WriteBookFooter();

        var texts = Texts(ws);
        texts.Should().Contain("- Sổ này có … trang, đánh số từ trang 01 đến trang …");
        texts.Should().Contain("- Ngày mở sổ: 01/10/2026");
        texts.Should().Contain("Ngày 13 tháng 10 năm 2026");
        texts.Should().Contain("(Ký, họ tên, đóng dấu)");

        foreach (var (title, name) in new[]
                 {
                     ("Người ghi sổ", "Lê C"),
                     ("Kế toán trưởng", "Trần B"),
                     ("Người đại diện theo pháp luật", "Nguyễn A"),
                 })
        {
            var titleCell = CellWith(ws, title);
            var nameCell = CellWith(ws, name);
            nameCell.Address.ColumnNumber.Should().Be(titleCell.Address.ColumnNumber);
            nameCell.Address.RowNumber.Should().BeGreaterThan(titleCell.Address.RowNumber);
        }
    }

    [Fact]
    public void Book_footer_without_an_opening_date_prints_dots()
    {
        var context = Context();
        context.OpeningDate = null;
        var (b, ws) = NewBuilder(context);

        b.WriteBookFooter();

        Texts(ws).Should().Contain("- Ngày mở sổ: …");
    }

    [Fact]
    public void Book_footer_adds_the_accounting_service_note()
    {
        var context = Context();
        context.AccountingServiceNote = "Dịch vụ kế toán: Công ty XYZ";
        var (b, ws) = NewBuilder(context);
        b.WriteBookFooter();
        Texts(ws).Should().Contain("Dịch vụ kế toán: Công ty XYZ");

        var (b2, ws2) = NewBuilder(Context());
        b2.WriteBookFooter();
        Texts(ws2).Should().NotContain(t => t.Contains("Dịch vụ kế toán"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Finish_sets_the_page_setup(bool landscape)
    {
        var (b, ws) = NewBuilder(Context(), landscape);
        b.WriteTableHeader();

        b.Finish();

        ws.PageSetup.PaperSize.Should().Be(XLPaperSize.A4Paper);
        ws.PageSetup.PageOrientation.Should().Be(landscape ? XLPageOrientation.Landscape : XLPageOrientation.Portrait);
        ws.PageSetup.PagesWide.Should().Be(1);
        ws.PageSetup.PagesTall.Should().Be(0);
        ws.PageSetup.FirstRowToRepeatAtTop.Should().Be(b.TableHeaderFirstRow);
        ws.PageSetup.LastRowToRepeatAtTop.Should().Be(b.TableHeaderLastRow);
        ws.PageSetup.Margins.Left.Should().BeApproximately(0.59, 0.01);
        ws.Column(1).Width.Should().BeApproximately(12, 0.01);
        ws.Column(2).Width.Should().BeApproximately(30, 0.01);
    }

    [Fact]
    public void Finish_without_repeat_leaves_no_rows_to_repeat()
    {
        var (b, ws) = NewBuilder(Context());
        b.WriteTableHeader();

        b.Finish(repeatTableHeader: false);

        ws.PageSetup.FirstRowToRepeatAtTop.Should().Be(0);
    }

    [Theory]
    [InlineData("QTM", "QTM")]
    [InlineData("KH/01:A", "KH_01_A")]
    [InlineData("0123456789012345678901234567890123456789", "0123456789012345678901234567890")]
    [InlineData("", "Sheet")]
    public void Sheet_name(string code, string expected)
    {
        Tt133FormBuilder.SheetName(code, new HashSet<string>(StringComparer.OrdinalIgnoreCase)).Should().Be(expected);
    }

    [Fact]
    public void Sheet_name_is_made_unique_inside_31_characters()
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Tt133FormBuilder.SheetName("QTM", used).Should().Be("QTM");
        Tt133FormBuilder.SheetName("QTM", used).Should().Be("QTM (2)");
        Tt133FormBuilder.SheetName("qtm", used).Should().Be("qtm (3)");

        var used2 = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var code = new string('X', 31);
        Tt133FormBuilder.SheetName(code, used2).Should().Be(code);
        var second = Tt133FormBuilder.SheetName(code, used2);
        second.Should().EndWith(" (2)");
        second.Length.Should().Be(31);
    }

    [Fact]
    public void Sheet_name_ignores_case_when_checking_for_duplicates()
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Tt133FormBuilder.SheetName("QTM", used);
        Tt133FormBuilder.SheetName("qtm", used).Should().Be("qtm (2)");
    }

    [Fact]
    public void A_row_whose_text_wraps_gets_a_height_for_all_its_lines()
    {
        // LibreOffice keeps the default height of a row without one, so wrapped text would print over its neighbours.
        var (b, ws) = NewBuilder(Context());
        b.WriteTableHeader();

        var shortRow = b.Row;
        b.WriteRow(new DateOnly(2026, 10, 5), "Thu tiền", 4_000m, null);
        var longRow = b.Row;
        b.WriteRow(new DateOnly(2026, 10, 6),
            "Thu tiền hàng đợt 1 theo hợp đồng số 25/2026/HĐMB – Công ty Cổ phần Đầu tư Xây dựng Đông Á", 4_000m, null);

        ws.Row(shortRow).Height.Should().Be(ws.RowHeight);
        ws.Row(longRow).Height.Should().Be(3 * ws.RowHeight); // 90 characters in a column of 30
    }

    [Fact]
    public void A_text_line_longer_than_the_page_wraps()
    {
        var (b, ws) = NewBuilder(Context());

        var shortRow = b.Row;
        b.WriteText("Hai bên cùng đối chiếu công nợ như sau:");
        var longRow = b.Row;
        var text = "Tính đến ngày 31/10/2026, Bên B còn nợ Bên A số tiền: 368.775.000 đồng "
            + "(Bằng chữ: Ba trăm sáu mươi tám triệu bảy trăm bảy mươi lăm nghìn đồng).";
        b.WriteText(text);

        ws.Cell(shortRow, 1).Style.Alignment.WrapText.Should().BeFalse();
        ws.Row(shortRow).Height.Should().Be(ws.RowHeight);
        ws.Cell(longRow, 1).Style.Alignment.WrapText.Should().BeTrue();
        ws.Row(longRow).Height.Should().Be(2 * ws.RowHeight); // 143 characters over 70 column units
    }

    [Fact]
    public void A_text_line_with_a_last_column_merges_only_up_to_it()
    {
        var (b, ws) = NewBuilder(Context());
        var row = b.Row;

        b.WriteText("x", lastColumn: 2);
        b.WriteText("y");

        ws.Cell(row, 1).MergedRange().RangeAddress.LastAddress.ColumnNumber.Should().Be(2);
        ws.Cell(row + 1, 1).MergedRange().RangeAddress.LastAddress.ColumnNumber.Should().Be(4);
    }

    [Fact]
    public void A_text_line_wraps_by_the_width_of_its_last_column()
    {
        var (b, ws) = NewBuilder(Context());
        var text = "Tính đến ngày 31/10/2026, Bên B còn nợ Bên A số tiền: 368.775.000 đồng "
            + "(Bằng chữ: Ba trăm sáu mươi tám triệu bảy trăm bảy mươi lăm nghìn đồng).";

        var allRow = b.Row;
        b.WriteText(text);
        var narrowRow = b.Row;
        b.WriteText(text, lastColumn: 2);

        ws.Row(allRow).Height.Should().Be(2 * ws.RowHeight); // 70 column units
        ws.Row(narrowRow).Height.Should().Be(3 * ws.RowHeight); // 42 column units
    }

    [Fact]
    public void A_long_unit_line_of_the_header_block_wraps()
    {
        var context = Context();
        context.CompanyName = "Công ty TNHH Thương mại và Sản xuất Vật liệu Xây dựng Hoàng Phương";
        var (b, ws) = NewBuilder(context);

        b.WriteHeaderBlock("S04a-DNN");

        ws.Cell(1, 1).Style.Alignment.WrapText.Should().BeTrue();
        ws.Row(1).Height.Should().Be(2 * ws.RowHeight); // 89 characters over the 42 units of columns A and B
        ws.Row(2).Height.Should().Be(ws.RowHeight);
    }
}
